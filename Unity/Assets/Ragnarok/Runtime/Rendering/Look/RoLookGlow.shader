Shader "Ragnarok/RO Look Glow" {
    // The glow in the air around a lamp: a camera-facing quad (uv -1..1) around the object's origin, additive HDR,
    // a bright core and a wide soft falloff, so the bloom spreads it further. Pulled toward the camera so the lantern's
    // own glass does not cut it.
    Properties {
        [HDR] _Color ("Color", Color) = (1, 0.7, 0.4, 1)
        _Radius ("Radius", Float) = 1.5
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
        }

        Pass {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogDepth : TEXCOORD1;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 toCamera = normalize(GetCameraPositionWS() - centerWS);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = centerWS + toCamera * (_Radius * 0.6) + (right * input.uv.x + up * input.uv.y) * _Radius;
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.fogDepth = RoFogDepth(output.positionHCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float d2 = dot(input.uv, input.uv);
                half core = exp(-d2 * 18.0);
                half halo = exp(-d2 * 3.5) * (1.0h - saturate(d2));
                half3 color = _Color.rgb * (core * 1.6h + halo * 0.45h);
                // fog thins the glow with the scene (RoApplyFog on black keeps only the glow's share)
                color = RoApplyFog(color, input.fogDepth) - RoApplyFog(half3(0.0h, 0.0h, 0.0h), input.fogDepth);
                return half4(max(color, half3(0.0h, 0.0h, 0.0h)), 0.0h);
            }
            ENDHLSL
        }
    }
}
