Shader "Ragnarok/RO Look Mote" {
    // A speck of dust in the air (RoLookMotes): a soft round dot, additive, HDR colour from the particle; lit by the
    // map's lamps / fires near it so the specks around a torch glow.
    Properties {
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+60"
            "IgnoreProjector" = "True"
        }

        Pass {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _RoLookAmbient;

            struct Attributes {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float2 d = input.uv * 2.0 - 1.0;
                half dot2 = saturate(1.0h - dot(d, d));
                half shape = dot2 * dot2;
                // faint by themselves, bright near a lamp or a fire
                half3 light = _RoLookAmbient.rgb * 0.15h;
#if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                uint additionalLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light lamp = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
                    light += lamp.color * lamp.distanceAttenuation;
                LIGHT_LOOP_END
#endif
                half3 color = input.color.rgb * light * shape * input.color.a;
                return half4(color, 0.0h);
            }
            ENDHLSL
        }
    }
}
