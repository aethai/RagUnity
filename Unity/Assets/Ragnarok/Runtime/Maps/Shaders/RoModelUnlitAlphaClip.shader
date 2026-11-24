Shader "Ragnarok/RO Model Unlit AlphaClip" {
    Properties {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _UseAlphaClip ("Use Alpha Clip", Float) = 1
        _Cull ("Cull", Float) = 2
        _ZWrite ("ZWrite", Float) = 1
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _RoUseLighting ("Use RSW Lighting", Float) = 0
        _RoLightDirection ("RSW Light Direction", Vector) = (0.5, 0.7071068, 0.5, 0)
        _RoLightAmbient ("RSW Light Ambient", Color) = (0.3, 0.3, 0.3, 1)
        _RoLightDiffuse ("RSW Light Diffuse", Color) = (1, 1, 1, 1)
        _RoLightOpacity ("RSW Light Opacity", Float) = 1
        _RoLightingStrength ("RSW Lighting Strength", Range(0, 1)) = 1
        _RoModelMinLight ("Model Min Light", Float) = 0.5
        [HideInInspector] _RoWindSwayMode ("Wind Sway Mode", Float) = 0
        [HideInInspector] _RoWindSwayAmplitude ("Wind Sway Amplitude", Float) = 0
        [HideInInspector] _RoWindSwayFrequency ("Wind Sway Frequency", Float) = 1
        [HideInInspector] _RoModelWaterFlowMode ("Model Water Flow Mode", Float) = 0
        [HideInInspector] _RoModelWaterFlowStrength ("Model Water Flow Strength", Float) = 0
        [HideInInspector] _RoModelWaterFlowSpeed ("Model Water Flow Speed", Float) = 1
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

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _UseAlphaClip;
                float _RoUseLighting;
                float4 _RoLightDirection;
                float4 _RoLightAmbient;
                float4 _RoLightDiffuse;
                float _RoLightOpacity;
                float _RoLightingStrength;
                float _RoModelMinLight;
                float _RoWindSwayMode;
                float _RoWindSwayAmplitude;
                float _RoWindSwayFrequency;
                float _RoModelWaterFlowMode;
                float _RoModelWaterFlowStrength;
                float _RoModelWaterFlowSpeed;
            CBUFFER_END

            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoWindSway.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoModelWaterFlow.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float lightWeight : TEXCOORD1;
                float fogDepth : TEXCOORD2;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = RoApplyWindSway(positionWS, (half4)input.color, (half)_RoWindSwayMode, (half)_RoWindSwayAmplitude, (half)_RoWindSwayFrequency);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogDepth = RoFogDepth(output.positionHCS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                float3 lightDirection = normalize(_RoLightDirection.xyz);
                output.lightWeight = max(dot(normalWS, lightDirection), _RoModelMinLight);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float2 sampleUv = RoApplyModelWaterFlowUv(input.uv, (half)_RoModelWaterFlowMode, (half)_RoModelWaterFlowStrength, (half)_RoModelWaterFlowSpeed);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUv);
                half alpha = tex.a * _BaseColor.a;
                if (_UseAlphaClip > 0.5) {
                    clip(alpha - _Cutoff);
                }

                half3 color = tex.rgb * _BaseColor.rgb;
                if (_RoUseLighting > 0.5) {
                    half3 rswLighting = saturate(_RoLightAmbient.rgb * _RoLightOpacity + _RoLightDiffuse.rgb * input.lightWeight);
                    half3 lighting = lerp(half3(1.0, 1.0, 1.0), rswLighting, saturate(_RoLightingStrength));
                    color *= lighting;
                }

                color = RoApplyFog(color, input.fogDepth);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
