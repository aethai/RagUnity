Shader "Ragnarok/RO Model Enhanced" {
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
        _RoReceiveRealtimeShadows ("Receive Realtime Shadows", Float) = 1
        _RoRealtimeShadowStrength ("Realtime Shadow Strength", Range(0, 1)) = 0.75
        [HideInInspector] _RoDebugRealtimeShadows ("Debug Realtime Shadows", Float) = 0
        _RoModelTopLightStrength ("Model Top Light Strength", Range(0, 1)) = 0.08
        _RoEmissionLightInfluence ("Map Emission Light Influence", Range(0, 2)) = 0.65
        _RoRainWetness ("Rain Wetness", Range(0, 1)) = 0
        _RoRainDarkening ("Rain Darkening", Range(0, 1)) = 1
        _RoRainHighlight ("Rain Highlight", Range(0, 1)) = 0.1
        _RoRainPuddleStrength ("Rain Puddle Strength", Range(0, 1)) = 0.55
        [HideInInspector] _RoRainStreakStrength ("Rain Streak Strength", Range(0, 1)) = 0
        [HideInInspector] _RoWindSwayMode ("Wind Sway Mode", Float) = 0
        [HideInInspector] _RoWindSwayAmplitude ("Wind Sway Amplitude", Float) = 0
        [HideInInspector] _RoWindSwayFrequency ("Wind Sway Frequency", Float) = 1
        [HideInInspector] _RoModelWaterFlowMode ("Model Water Flow Mode", Float) = 0
        [HideInInspector] _RoModelWaterFlowStrength ("Model Water Flow Strength", Float) = 0
        [HideInInspector] _RoModelWaterFlowSpeed ("Model Water Flow Speed", Float) = 1
        [HideInInspector] _RoShadowKeepsAlphaClip ("Shadow Keeps Alpha Clip", Float) = 0
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass {
            Name "ForwardEnhanced"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoCloudShadows.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoLookNormals.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoLookLighting.hlsl"

            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoModelEnhancedInput.hlsl"

            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoWindSway.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoModelWaterFlow.hlsl"

            float RoRainHash21(float2 p) {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogDepth : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD4;
#endif
            };

            VertexPositionInputs ResolveModelVertexPositionInputs(float3 positionWS) {
                VertexPositionInputs output;
                output.positionWS = positionWS;
                output.positionVS = TransformWorldToView(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionNDC = ComputeScreenPos(output.positionCS);
                return output;
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = RoApplyWindSway(positionWS, (half4)input.color, (half)_RoWindSwayMode, (half)_RoWindSwayAmplitude, (half)_RoWindSwayFrequency);
                VertexPositionInputs positionInputs = ResolveModelVertexPositionInputs(positionWS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogDepth = RoFogDepth(output.positionHCS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(positionInputs);
#endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float2 sampleUv = RoApplyModelWaterFlowUv(input.uv, (half)_RoModelWaterFlowMode, (half)_RoModelWaterFlowStrength, (half)_RoModelWaterFlowSpeed);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUv);
                half alpha = tex.a * _BaseColor.a;
                if (_UseAlphaClip > 0.5) {
                    clip(alpha - _Cutoff);
                }

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
                float4 shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
#endif
                Light mainLight = GetMainLight(shadowCoord);
                if (_RoDebugRealtimeShadows > 0.5) {
                    return half4(mainLight.shadowAttenuation.xxx, alpha);
                }

                half realtimeShadow = 1.0h;
                if (_RoReceiveRealtimeShadows > 0.5) {
                    realtimeShadow = mainLight.shadowAttenuation;
                }
                half cloudShadow = RoCloudShadowAttenuation(input.positionWS.xz, mainLight.shadowAttenuation, (half)_RoCloudShadowParams.z);

                half3 normalWS = RoLookOrientModelNormal(normalize((half3)input.normalWS), input.positionWS);
                normalWS = RoLookReliefNormal(_BaseMap, sampler_BaseMap, sampleUv, normalWS, input.positionWS, (half)_RoLookRelief.y);
                half3 baseColor = tex.rgb * _BaseColor.rgb;

                RoLookSurface surface;
                surface.albedo = baseColor;
                surface.normalWS = normalWS;
                surface.positionWS = input.positionWS;
                surface.screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                AmbientOcclusionFactor occlusion = RoLookOcclusion(surface.screenUV);

                half3 ambient = RoLookAmbient(normalWS) * occlusion.indirectAmbientOcclusion;
                ambient = max(ambient, RoLookAmbient(half3(0.0h, 1.0h, 0.0h)) * (half)_RoLookSprite.w);
                // foliage (cut-out leaves): a crown is a cloud of cards, not a solid - the sun wraps far round it and shines
                // through the leaves a little, so its far side is green, not black
                half foliage = _UseAlphaClip > 0.5 ? 1.0h : 0.0h;
                half leafWrap = _RoLookRim.z > 0.0 ? (half)_RoLookRim.z : 0.8h;
                half sunTerm = lerp(RoLookSunTerm(normalWS, mainLight.direction),
                    saturate((dot(normalWS, mainLight.direction) + leafWrap) / (1.0h + leafWrap)) +
                    saturate(-dot(normalWS, mainLight.direction)) * leafWrap * 0.3h,
                    foliage);
                half3 sun = RoLookSunColor(mainLight) * sunTerm *
                    realtimeShadow * cloudShadow * occlusion.directAmbientOcclusion;
                half3 lamps = RoLookLamps(surface, 0.35h) * saturate(_RoEmissionLightInfluence) * 1.5h;
                half3 rim = RoLookRim(normalWS, (half3)GetWorldSpaceNormalizeViewDir(input.positionWS)) * occlusion.indirectAmbientOcclusion;
                half3 color = baseColor * (ambient + sun + lamps + rim);

                half rainWetness = saturate(_RoRainWetness);
                if (rainWetness > 0.0001h) {
                    half topFacing = saturate((normalWS.y * 0.75h) + 0.25h);
                    half wallFacing = saturate(1.0h - abs(normalWS.y) * 1.35h);
                    float2 streakColumn = floor(float2((input.positionWS.x + input.positionWS.z * 0.37) * 0.55, input.positionWS.y * 0.08));
                    float columnNoise = RoRainHash21(streakColumn);
                    half columnMask = smoothstep(0.78h, 0.96h, columnNoise);
                    float movingDrop = frac(input.positionWS.y * 0.85 - _Time.y * 1.25 + columnNoise);
                    half dropHead = 1.0h - smoothstep(0.0h, 0.2h, movingDrop);
                    half dropTail = 1.0h - smoothstep(0.0h, 0.62h, movingDrop);
                    half streakStrength = saturate(_RoRainStreakStrength) * 0.18h;
                    half streak = wallFacing * columnMask * (dropHead + dropTail * 0.25h) * streakStrength;
                    half wetMask = rainWetness * saturate(topFacing * 0.68h + wallFacing * 0.28h);

                    half3 wetDarkColor = color * (1.0h - saturate(_RoRainDarkening));
                    half puddleSheen = wallFacing * saturate(_RoRainPuddleStrength) * 0.16h;
                    half3 wetHighlight = half3(_RoRainHighlight, _RoRainHighlight, _RoRainHighlight) * (topFacing * 0.36h + puddleSheen + streak);
                    color = lerp(color, wetDarkColor, wetMask);
                    color += wetHighlight * rainWetness;
                }

                color = RoApplyFog(max(color, half3(0.0, 0.0, 0.0)), input.fogDepth);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoModelEnhancedInput.hlsl"

            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoWindSway.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoModelWaterFlow.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 GetShadowPositionHClip(Attributes input) {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = RoApplyWindSway(positionWS, (half4)input.color, (half)_RoWindSwayMode, (half)_RoWindSwayAmplitude, (half)_RoWindSwayFrequency);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            Varyings ShadowPassVertex(Attributes input) {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target {
                float2 sampleUv = RoApplyModelWaterFlowUv(input.uv, (half)_RoModelWaterFlowMode, (half)_RoModelWaterFlowStrength, (half)_RoModelWaterFlowSpeed);
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUv).a;
                // a model faded out of the way of the local player (blended, no clip in its colour pass) keeps the
                // shadow of its cut-out self: texture alpha against the cutoff, the fade alpha left out
                if (_RoShadowKeepsAlphaClip > 0.5) {
                    clip(alpha - _Cutoff);
                } else if (_UseAlphaClip > 0.5) {
                    clip(alpha * _BaseColor.a - _Cutoff);
                }

                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex RoModelDepthVertex
            #pragma fragment RoModelDepthFragment
            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoModelDepthPasses.hlsl"
            ENDHLSL
        }

        Pass {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex RoModelDepthVertex
            #pragma fragment RoModelDepthNormalsFragment
            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoModelDepthPasses.hlsl"
            ENDHLSL
        }
    }
}
