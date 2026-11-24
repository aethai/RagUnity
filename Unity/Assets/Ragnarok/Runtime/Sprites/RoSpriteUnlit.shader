Shader "Ragnarok/RO Sprite Unlit" {
    Properties {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _Cull ("Cull", Float) = 0
        _ZWrite ("ZWrite", Float) = 0
        _ZTest ("ZTest", Float) = 4
        _SrcBlend ("Src Blend", Float) = 5
        _DstBlend ("Dst Blend", Float) = 10
        _VPos ("Vertical Sprite Offset", Float) = 0
        _DepthCorrection ("Depth Correction", Float) = 1
        _RoSpriteDepthBias ("RO Sprite Depth Bias (cells toward the camera)", Float) = 0.4
        _RoMapLightMultiply ("RO Map Light Multiply", Color) = (1, 1, 1, 1)
        _RoMapLightAdd ("RO Map Light Add", Color) = (0, 0, 0, 0)
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass {
            Name "SpriteDepthPrepass"

            ColorMask 0
            ZWrite On
            ZTest LEqual
            Offset -1, -1
            Cull [_Cull]
            AlphaToMask On

            Stencil {
                Ref 32
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _VPos;
                float _DepthCorrection;
                float _RoSpriteDepthBias;
                float4 _RoMapLightMultiply;
                float4 _RoMapLightAdd;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            // pulls the depth a fixed world distance (_RoSpriteDepthBias, in cells) toward the camera: an NDC offset grew
            // with the square of the camera distance and let actors behind walls draw over them once the camera moved away
            float4 ApplyRoSpriteDepthBias(float4 positionHCS) {
                float bias = max(_RoSpriteDepthBias, 0.0);
                if (bias <= 0.0 || UNITY_MATRIX_P[3][3] != 0.0 || positionHCS.w <= 0.0) {
                    return positionHCS;
                }

                // perspective: z_ndc = P23 / d - P22 at view distance d (clip w), whatever the depth direction
                float p22 = UNITY_MATRIX_P[2][2];
                float p23 = UNITY_MATRIX_P[2][3];
                float zNdc = positionHCS.z / positionHCS.w;
                float denominator = zNdc + p22;
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = p23 / denominator;
                float biasedDistance = max(distance - bias, _ProjectionParams.y);
                positionHCS.z = (p23 / biasedDistance - p22) * positionHCS.w;
                return positionHCS;
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += _VPos;
                output.positionHCS = TransformObjectToHClip(positionOS);

                if (_DepthCorrection > 0.5) {
                    float3 worldVertex = TransformObjectToWorld(positionOS);
                    float3 worldCenter = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                    float3 rayDirection = normalize(worldVertex - _WorldSpaceCameraPos.xyz);
                    float3 planeNormal = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0));
                    float3 flatPlaneNormal = float3(planeNormal.x, 0.0, planeNormal.z);
                    if (dot(flatPlaneNormal, flatPlaneNormal) < 0.000001) {
                        planeNormal = normalize(planeNormal);
                    } else {
                        planeNormal = normalize(flatPlaneNormal);
                    }

                    float denominator = dot(planeNormal, rayDirection);
                    if (abs(denominator) > 0.000001) {
                        float distance = dot(worldCenter - _WorldSpaceCameraPos.xyz, planeNormal) / denominator;
                        float3 correctedWorld = _WorldSpaceCameraPos.xyz + rayDirection * distance;
                        float4 correctedHCS = TransformWorldToHClip(correctedWorld);
                        float correctedZ = correctedHCS.z * (output.positionHCS.w / max(correctedHCS.w, 0.000001));
#if UNITY_REVERSED_Z
                        output.positionHCS.z = max(output.positionHCS.z, correctedZ);
#else
                        output.positionHCS.z = min(output.positionHCS.z, correctedZ);
#endif
                    }
                }

                output.positionHCS = ApplyRoSpriteDepthBias(output.positionHCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = tex.a * input.color.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Offset -1, -1
            Cull [_Cull]

            Stencil {
                Ref 32
                Comp Always
                Pass Keep
            }

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
                float _VPos;
                float _DepthCorrection;
                float _RoSpriteDepthBias;
                float4 _RoMapLightMultiply;
                float4 _RoMapLightAdd;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float fogDepth : TEXCOORD1;
            };

            // pulls the depth a fixed world distance (_RoSpriteDepthBias, in cells) toward the camera: an NDC offset grew
            // with the square of the camera distance and let actors behind walls draw over them once the camera moved away
            float4 ApplyRoSpriteDepthBias(float4 positionHCS) {
                float bias = max(_RoSpriteDepthBias, 0.0);
                if (bias <= 0.0 || UNITY_MATRIX_P[3][3] != 0.0 || positionHCS.w <= 0.0) {
                    return positionHCS;
                }

                // perspective: z_ndc = P23 / d - P22 at view distance d (clip w), whatever the depth direction
                float p22 = UNITY_MATRIX_P[2][2];
                float p23 = UNITY_MATRIX_P[2][3];
                float zNdc = positionHCS.z / positionHCS.w;
                float denominator = zNdc + p22;
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = p23 / denominator;
                float biasedDistance = max(distance - bias, _ProjectionParams.y);
                positionHCS.z = (p23 / biasedDistance - p22) * positionHCS.w;
                return positionHCS;
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += _VPos;
                output.positionHCS = TransformObjectToHClip(positionOS);

                if (_DepthCorrection > 0.5) {
                    float3 worldVertex = TransformObjectToWorld(positionOS);
                    float3 worldCenter = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                    float3 rayDirection = normalize(worldVertex - _WorldSpaceCameraPos.xyz);
                    float3 planeNormal = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0));
                    float3 flatPlaneNormal = float3(planeNormal.x, 0.0, planeNormal.z);
                    if (dot(flatPlaneNormal, flatPlaneNormal) < 0.000001) {
                        planeNormal = normalize(planeNormal);
                    } else {
                        planeNormal = normalize(flatPlaneNormal);
                    }

                    float denominator = dot(planeNormal, rayDirection);
                    if (abs(denominator) > 0.000001) {
                        float distance = dot(worldCenter - _WorldSpaceCameraPos.xyz, planeNormal) / denominator;
                        float3 correctedWorld = _WorldSpaceCameraPos.xyz + rayDirection * distance;
                        float4 correctedHCS = TransformWorldToHClip(correctedWorld);
                        float correctedZ = correctedHCS.z * (output.positionHCS.w / max(correctedHCS.w, 0.000001));
#if UNITY_REVERSED_Z
                        output.positionHCS.z = max(output.positionHCS.z, correctedZ);
#else
                        output.positionHCS.z = min(output.positionHCS.z, correctedZ);
#endif
                    }
                }

                output.positionHCS = ApplyRoSpriteDepthBias(output.positionHCS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                output.fogDepth = RoFogDepth(output.positionHCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 color = tex * input.color;
                clip(color.a - _Cutoff);
                color.rgb = saturate(color.rgb * _RoMapLightMultiply.rgb + _RoMapLightAdd.rgb);
                color.rgb = RoApplyFog(color.rgb, input.fogDepth);
                return color;
            }
            ENDHLSL
        }
    }
}
