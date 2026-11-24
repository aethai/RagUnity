Shader "Ragnarok/RO Ground Enhanced" {
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
        _RoReceiveRealtimeShadows ("Receive Realtime Shadows", Float) = 1
        _RoRealtimeShadowStrength ("Realtime Shadow Strength", Range(0, 1)) = 0.85
        [HideInInspector] _RoDebugRealtimeShadows ("Debug Realtime Shadows", Float) = 0
        _RoEmissionLightInfluence ("Map Emission Light Influence", Range(0, 2)) = 0.65
        _RoRainWetness ("Rain Wetness", Range(0, 1)) = 0
        _RoRainDarkening ("Rain Darkening", Range(0, 1)) = 1
        _RoRainHighlight ("Rain Highlight", Range(0, 1)) = 0.1
        _RoRainPuddleStrength ("Rain Puddle Strength", Range(0, 1)) = 0.55
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

            ZWrite On
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
                float _Cull;
                float _RoUseLighting;
                float4 _RoLightDirection;
                float4 _RoLightAmbient;
                float4 _RoLightDiffuse;
                float _RoLightOpacity;
                float _RoLightingStrength;
                float _RoUseLightmapAlpha;
                float _RoGroundMinLight;
                float _RoReceiveRealtimeShadows;
                float _RoRealtimeShadowStrength;
                float _RoDebugRealtimeShadows;
                float _RoEmissionLightInfluence;
                float _RoRainWetness;
                float _RoRainDarkening;
                float _RoRainHighlight;
                float _RoRainPuddleStrength;
            CBUFFER_END

            float RoRainHash21(float2 p) {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float2 RoRainRotate(float2 p) {
                return float2(
                    p.x * 0.842217f - p.y * 0.539139f,
                    p.x * 0.539139f + p.y * 0.842217f);
            }

            float RoRainValueNoise(float2 p) {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0f - 2.0f * local);

                float a = RoRainHash21(cell);
                float b = RoRainHash21(cell + float2(1.0f, 0.0f));
                float c = RoRainHash21(cell + float2(0.0f, 1.0f));
                float d = RoRainHash21(cell + float2(1.0f, 1.0f));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            half RoRainSparseRippleLayer(float2 worldPos, float cellSize, float density, float speed, float seed) {
                float2 p = RoRainRotate(worldPos + float2(seed * 13.17f, -seed * 8.41f)) / cellSize;
                float2 baseCell = floor(p);
                float2 local = frac(p);
                half ripple = 0.0h;

                [unroll]
                for (int y = -1; y <= 1; y++) {
                    [unroll]
                    for (int x = -1; x <= 1; x++) {
                        float2 offset = float2(x, y);
                        float2 cell = baseCell + offset;
                        float2 seededCell = cell + float2(seed, seed * 1.37f);
                        float baseHash = RoRainHash21(seededCell);
                        float phase = _Time.y * speed + baseHash * 1.731f + seed * 0.173f;
                        float cycle = floor(phase);
                        float age = frac(phase);
                        float2 cycleSeed = seededCell + float2(cycle * 37.11f, cycle * 19.73f);
                        float activeHash = RoRainHash21(cycleSeed + float2(11.19f, 91.73f));
                        half active = step(1.0h - density, activeHash);
                        float2 center = offset + float2(
                            RoRainHash21(cycleSeed + float2(17.13f, 3.71f)),
                            RoRainHash21(cycleSeed + float2(43.71f, 29.19f)));

                        float radius = lerp(0.022f, 0.16f, age);
                        float dist = length(local - center);
                        half ring = 1.0h - smoothstep(0.006h, 0.024h, abs(dist - radius));
                        half fade = smoothstep(0.02h, 0.15h, age) * (1.0h - smoothstep(0.52h, 1.0h, age));
                        ripple = max(ripple, active * ring * fade);
                    }
                }

                return ripple;
            }

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
                float fogDepth : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 normalWS : TEXCOORD6;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
#endif
            };

            Varyings vert(Attributes input) {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.lightUv = input.lightUv;
                output.tintUv = input.tintUv;
                output.fogDepth = RoFogDepth(output.positionHCS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(positionInputs);
#endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = baseSample.a * _BaseColor.a;
                clip(alpha - _Cutoff);

                bool hasTileColor = dot(input.tintUv, input.tintUv) > 0.000001;
                half3 color = baseSample.rgb * _BaseColor.rgb;

                if (_UseTint > 0.5 && hasTileColor) {
                    half3 tint = SAMPLE_TEXTURE2D(_Tintmap, sampler_Tintmap, input.tintUv).bgr;
#if !defined(UNITY_COLORSPACE_GAMMA)
                    // the tintmap is created linear (raw gamma bytes); a multiply wants it decoded like the albedo
                    tint = SRGBToLinear(tint);
#endif
                    color *= tint;
                }

                half3 baseColor = color;
                half3 normalWS = RoLookOrientGroundNormal(normalize((half3)input.normalWS), input.positionWS);
                normalWS = RoLookReliefNormal(_BaseMap, sampler_BaseMap, input.uv, normalWS, input.positionWS, (half)_RoLookRelief.x);
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

                // full shadow: the ambient fills it (the light's shadowStrength is the look rig's)
                half realtimeShadow = _RoReceiveRealtimeShadows > 0.5 ? mainLight.shadowAttenuation : 1.0h;
                half cloudShadow = RoCloudShadowAttenuation(input.positionWS.xz, mainLight.shadowAttenuation, (half)_RoCloudShadowParams.z);

                RoLookSurface surface;
                surface.albedo = baseColor;
                surface.normalWS = normalWS;
                surface.positionWS = input.positionWS;
                surface.screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                AmbientOcclusionFactor occlusion = RoLookOcclusion(surface.screenUV);

                // the GND lightmap: rgb = the map's baked light (lamps, coloured pools) lighting the albedo,
                // alpha = the baked occlusion, kept on the ambient only (the sun has realtime shadows)
                half3 bakedLight = 0;
                half bakedOcclusion = 1.0h;
                if (_UseLightmap > 0.5) {
                    half4 light = SAMPLE_TEXTURE2D(_Lightmap, sampler_Lightmap, input.lightUv);
                    half3 lightRgb = light.rgb;
#if !defined(UNITY_COLORSPACE_GAMMA)
                    lightRgb = SRGBToLinear(lightRgb);
#endif
                    // the coloured part of the bake (lamps, magic, Comodo's lights) stays; its grey base (a baked sun
                    // that burnt the white stone of Prontera) mostly goes - the realtime sun replaces it
                    half lightGrey = min(min(lightRgb.r, lightRgb.g), lightRgb.b);
                    bakedLight = (lightRgb - lightGrey * 0.8h * (1.0h - saturate((half)_RoLookIndoor.x))) * (half)_RoLookParams.x;
                    // with no sky (dungeon, interior) the bake is the map's lighting, as in the original: its alpha shades
                    // everything; outdoors only the ambient (the sun has realtime shadows)
                    bakedOcclusion = lerp(1.0h, light.a, lerp(saturate((half)_RoLookParams.y), 1.0h, saturate((half)_RoLookIndoor.x)));
                }

                half3 ambient = RoLookAmbient(normalWS) * occlusion.indirectAmbientOcclusion * bakedOcclusion;
                half3 sun = RoLookSunColor(mainLight) * RoLookSunTerm(normalWS, mainLight.direction) *
                    realtimeShadow * cloudShadow * occlusion.directAmbientOcclusion *
                    lerp(1.0h, bakedOcclusion, saturate((half)_RoLookIndoor.x));
                half3 lamps = RoLookLamps(surface, 0.35h) * saturate(_RoEmissionLightInfluence) * 1.5h;
                // outdoors the bake's colour lights the albedo; with no sky it is added on top as in the original
                // (C3dGround: colour * light + lightmap) - Payon Cave's violet haze and the warm halo around its torches
                half indoor = saturate((half)_RoLookIndoor.x);
                half albedoLight = lerp(1.0h, (half)_RoLookIndoor.w, indoor);
                color = baseColor * ((ambient + sun + bakedLight * (1.0h - indoor)) * albedoLight + lamps) + bakedLight * indoor * (half)_RoLookIndoor.z;

                half rainWetness = saturate(_RoRainWetness);
                if (rainWetness > 0.0001h) {
                    float2 rainPos = input.positionWS.xz;
                    float2 rotatedRainPos = RoRainRotate(rainPos);
                    float broadNoise = RoRainValueNoise(rotatedRainPos * 0.055f);
                    float detailNoise = RoRainValueNoise((rotatedRainPos + float2(37.0f, -19.0f)) * 0.115f);
                    half puddle = smoothstep(0.48h, 0.86h, broadNoise * 0.72h + detailNoise * 0.28h);
                    half ripple = max(
                        RoRainSparseRippleLayer(rainPos, 3.25f, 0.34f, 0.78f, 2.0f),
                        max(
                            RoRainSparseRippleLayer(rainPos + float2(5.1f, -2.7f), 4.7f, 0.25f, 0.58f, 7.0f),
                            RoRainSparseRippleLayer(rainPos + float2(11.7f, -6.3f), 7.2f, 0.18f, 0.41f, 13.0f)));
                    ripple *= saturate(0.38h + puddle * 0.5h + saturate(_RoRainPuddleStrength) * 0.42h);

                    half wetMask = rainWetness * saturate(0.45h + puddle * saturate(_RoRainPuddleStrength));
                    color = lerp(color, color * (1.0h - saturate(_RoRainDarkening)), wetMask);
                    color += baseColor * saturate(_RoRainHighlight) * rainWetness * (puddle * 0.28h + ripple * 0.62h);
                }

                color = RoApplyFog(color, input.fogDepth);
                return half4(color, alpha);
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
            #pragma vertex RoGroundDepthVertex
            #pragma fragment RoGroundDepthFragment
            #define RO_GROUND_DEPTH_ONLY
            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoGroundDepthPasses.hlsl"
            ENDHLSL
        }

        Pass {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex RoGroundDepthVertex
            #pragma fragment RoGroundDepthNormalsFragment
            #include "Assets/Ragnarok/Runtime/Maps/Shaders/RoGroundDepthPasses.hlsl"
            ENDHLSL
        }
    }
}
