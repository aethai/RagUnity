// The classic effect engine's draws (RoClassicEffectBridge): one draw state of the original's CRenderer::Flush
// (reference packages/render-lists) per material — blend, depth test / write, and the alpha test every list runs
// (ALPHATESTENABLE with ALPHAREF / ALPHAFUNC: GREATEREQUAL, or LESS for the second pass of list 0x180). Colour =
// texture × vertex diffuse (texture stage 0 MODULATE / MODULATE, the state Flush sets). No black-texel discard: the
// client has none, black only vanishes under additive blends.
Shader "Ragnarok/RO Classic Effect" {
    Properties {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cull ("Cull", Float) = 0
        _ZWrite ("ZWrite", Float) = 0
        _ZTest ("ZTest", Float) = 4
        _SrcBlend ("Src Blend", Float) = 5
        _DstBlend ("Dst Blend", Float) = 10
        _RoAlphaRef ("Alpha Ref (0..255)", Float) = 1
        _RoAlphaLess ("Alpha Func LESS", Float) = 0
        _RoFogEnabled ("RO Fog Enabled", Float) = 0
        _RoColorOp ("Stage 0 COLOROP (D3DTOP)", Float) = 4
        _RoAlphaOp ("Stage 0 ALPHAOP (D3DTOP)", Float) = 4
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _RO_MULTIPLY_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _RoAlphaRef;
                float _RoAlphaLess;
                float _RoFogEnabled;
                float _RoColorOp;
                float _RoAlphaOp;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogDepth : TEXCOORD1;
                float4 color : COLOR;
            };

            // the bridge's frame data (RoClassicEffectBridge.RunFrame): the core's screen centre and 1 / (factor × RO units
            // per Unity unit), and the camera-to-world of the camera the core projected with
            float4 _RoFxScreen;
            float4x4 _RoFxCameraToWorld;
            // the enhanced look's effect intensity (RoLookRig): above 1 an effect's bright parts reach the bloom
            float _RoLookFxBoost;
            // the original blends in gamma: a translucent layer over a dark scene adds a * gamma(colour), i.e. a^2.2 of
            // the colour in linear; blended in linear the same layer came out far brighter and denser (the torch glow of
            // Payon Cave as a solid orange slab). alpha^g restores the gamma look over dark backgrounds (1 = off)
            float _RoLookFxAlphaGamma;

            Varyings vert(Attributes input) {
                Varyings output;
                // the vertex is the client's TL vertex: screen x / y in pixels and rhw (positionOS.xyz); back to the camera
                // space of the core's projection (RO units → Unity, y and z flipped), then world and this camera's clip space
                // a negative rhw marks a screen-space vertex (damage numbers, STR layers, screen prims): it goes back
                // through this camera, so it stays on the screen; the others through the camera the core projected with
                // (the same one unless the render pass ran on the worker a frame earlier)
                float rhw = input.positionOS.z;
                float zc = 1.0 / abs(rhw);
                float3 cameraSpace = float3((input.positionOS.x - _RoFxScreen.x) * zc * _RoFxScreen.z,
                    -(input.positionOS.y - _RoFxScreen.y) * zc * _RoFxScreen.w, -zc * 0.2);
                float4x4 cameraToWorld = rhw < 0.0 ? UNITY_MATRIX_I_V : _RoFxCameraToWorld;
                float3 world = mul(cameraToWorld, float4(cameraSpace, 1.0)).xyz;
                output.positionHCS = TransformWorldToHClip(world);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogDepth = RoFogDepth(output.positionHCS);
                // the TL vertex colours are D3D gamma bytes; in a linear project they are decoded like the textures
                output.color = input.color;
#if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb = SRGBToLinear(output.color.rgb);
#endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                // texture stage 0 of the draw (arg1 = TEXTURE, arg2 = DIFFUSE; presets of 0x53ce20): COLOROP MODULATE (4),
                // SELECTARG1 (2), ADD (7) or BLENDTEXTUREALPHA (13); ALPHAOP MODULATE (4), SELECTARG1 (2: texture alpha
                // alone) or SELECTARG2 (3: diffuse alpha alone)
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 diffuse = _BaseColor * input.color;
                half4 color = tex * diffuse;
                if (_RoColorOp > 12.5) {
                    color.rgb = lerp(diffuse.rgb, tex.rgb, tex.a);
                } else if (_RoColorOp > 6.5) {
                    color.rgb = saturate(tex.rgb + diffuse.rgb);
                } else if (_RoColorOp < 2.5) {
                    color.rgb = tex.rgb;
                }

                if (_RoAlphaOp < 2.5) {
                    color.a = tex.a;
                } else if (_RoAlphaOp < 3.5) {
                    color.a = diffuse.a;
                }

                // the D3D alpha test compares the 8-bit alpha with ALPHAREF
                float a8 = floor(color.a * 255.0 + 0.5);
                if (_RoAlphaLess > 0.5 ? a8 >= _RoAlphaRef : a8 < _RoAlphaRef) {
                    discard;
                }

                float alphaGamma = max(_RoLookFxAlphaGamma, 1.0);
                color.a = pow(saturate(color.a), alphaGamma);

                #if defined(_RO_MULTIPLY_ALPHA)
                    color *= pow(saturate(input.color.a * _BaseColor.a), alphaGamma);
                #endif

                color.rgb *= max(_RoLookFxBoost, 1.0);

                if (_RoFogEnabled > 0.5h) {
                    color.rgb = RoApplyFog(color.rgb, input.fogDepth);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
