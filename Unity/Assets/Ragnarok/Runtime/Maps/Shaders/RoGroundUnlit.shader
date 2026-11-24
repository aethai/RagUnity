Shader "Ragnarok/RO Ground Unlit" {
    Properties {
        [MainTexture] _BaseMap ("Texture Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Lightmap ("RO Lightmap", 2D) = "black" {}
        _Tintmap ("RO Tintmap", 2D) = "white" {}
        _UseLightmap ("Use Lightmap", Float) = 1
        _UseTint ("Use Tint", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _Cull ("Cull", Float) = 0
        _RoUseLighting ("Use RSW Lighting", Float) = 0
        _RoLightDirection ("RSW Light Direction", Vector) = (0.5, 0.7071068, 0.5, 0)
        _RoLightAmbient ("RSW Light Ambient", Color) = (0.3, 0.3, 0.3, 1)
        _RoLightDiffuse ("RSW Light Diffuse", Color) = (1, 1, 1, 1)
        _RoLightOpacity ("RSW Light Opacity", Float) = 1
        _RoLightingStrength ("RSW Lighting Strength", Range(0, 1)) = 1
        _RoUseLightmapAlpha ("Use Lightmap Alpha", Float) = 0
        _RoGroundMinLight ("Ground Min Light", Float) = 0.1
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Lightmap);
            SAMPLER(sampler_Lightmap);
            TEXTURE2D(_Tintmap);
            SAMPLER(sampler_Tintmap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _UseLightmap;
                float _UseTint;
                float _Cutoff;
                float _RoUseLighting;
                float4 _RoLightDirection;
                float4 _RoLightAmbient;
                float4 _RoLightDiffuse;
                float _RoLightOpacity;
                float _RoLightingStrength;
                float _RoUseLightmapAlpha;
                float _RoGroundMinLight;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightUv : TEXCOORD1;
                float2 tintUv : TEXCOORD2;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 lightUv : TEXCOORD1;
                float2 tintUv : TEXCOORD2;
                float lightWeight : TEXCOORD3;
                float fogDepth : TEXCOORD4;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.lightUv = input.lightUv;
                output.tintUv = input.tintUv;
                output.fogDepth = RoFogDepth(output.positionHCS);
                float3 lightDirection = normalize(_RoLightDirection.xyz);
                // GND already carries baked per-tile lightmaps. Using per-vertex normals for
                // global RSW light exposes the mesh diagonal as a visible triangle seam.
                output.lightWeight = max(dot(float3(0.0, 1.0, 0.0), lightDirection), _RoGroundMinLight);
                return output;
            }


            half4 frag(Varyings input) : SV_Target {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                bool hasTileColor = dot(input.tintUv, input.tintUv) > 0.000001;
                half3 color = baseSample.rgb * _BaseColor.rgb;

                if (_UseTint > 0.5 && hasTileColor) {
                    half4 tint = SAMPLE_TEXTURE2D(_Tintmap, sampler_Tintmap, input.tintUv);
                    color *= tint.bgr;
                }

                half3 lighting = half3(1.0, 1.0, 1.0);
                if (_RoUseLighting > 0.5) {
                    half lightWeight = hasTileColor ? input.lightWeight : 1.0;
                    half3 rswLighting = _RoLightAmbient.rgb * _RoLightOpacity + _RoLightDiffuse.rgb * lightWeight;
                    lighting = lerp(half3(1.0, 1.0, 1.0), rswLighting, saturate(_RoLightingStrength));
                }

                if (_UseLightmap > 0.5) {
                    half4 light = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.lightUv);
                    if (_RoUseLighting > 0.5 && _RoUseLightmapAlpha > 0.5) {
                        color = color * saturate(lighting * light.a) + light.rgb;
                    } else {
                        color *= saturate(lighting);
                        color += light.rgb;
                    }
                } else {
                    color *= saturate(lighting);
                }

                color = RoApplyFog(color, input.fogDepth);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
