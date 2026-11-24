Shader "Ragnarok/RO Water Unlit" {
    Properties {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 0.78)
        _WaterOffset ("Water Offset", Float) = 0
        _WaveHeight ("Wave Height", Float) = 0
        _WavePitch ("Wave Pitch", Float) = 0
        _WaterOpacity ("Water Opacity", Range(0, 1)) = 0.6
        _Cull ("Cull", Float) = 0
        _ZWrite ("ZWrite", Float) = 0
        _SrcBlend ("Src Blend", Float) = 5
        _DstBlend ("Dst Blend", Float) = 10
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
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoCloudShadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _WaterOffset;
                float _WaveHeight;
                float _WavePitch;
                float _WaterOpacity;
            CBUFFER_END

            // the enhanced look's water (RoLookRig): x = on, y = depth absorption scale, z = refraction strength, w = foam
            float4 _RoLookWater;
            // rgb = deep water colour (linear), a = how much of the RO water texture tints the surface
            float4 _RoLookWaterDeep;
            float4 _RoLookAmbient;
            float4 _RoLookSky;
            float4 _RoLookSun;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogDepth : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            float PositiveMod(float value, float divisor) {
                return value - divisor * floor(value / divisor);
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float x = PositiveMod(positionOS.x, 2.0);
                float z = PositiveMod(positionOS.z, 2.0);
                float diff = x < 1.0 ? (z < 1.0 ? 1.0 : -1.0) : 0.0;
                float phase = radians(_WaterOffset + 0.5 * _WavePitch * (positionOS.x + positionOS.z + diff));
                positionOS.y += sin(phase) * _WaveHeight;

                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogDepth = RoFogDepth(output.positionHCS);
                return output;
            }

            // small travelling waves for the surface normal (world xz), two directions and scales
            float3 RoWaterNormal(float2 p, float t) {
                float2 d1 = float2(0.8, 0.6), d2 = float2(-0.45, 0.9), d3 = float2(0.2, -1.0);
                float f1 = 1.7, f2 = 2.9, f3 = 5.3;
                float a1 = 0.08, a2 = 0.05, a3 = 0.025;
                float2 g = d1 * (a1 * f1 * cos(dot(p, d1) * f1 + t * 1.3)) +
                           d2 * (a2 * f2 * cos(dot(p, d2) * f2 + t * 1.9)) +
                           d3 * (a3 * f3 * cos(dot(p, d3) * f3 + t * 2.7));
                return normalize(float3(-g.x, 1.0, -g.y));
            }

            float RoWaterHash(float2 p) {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float RoWaterNoise(float2 p) {
                float2 c = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(RoWaterHash(c), RoWaterHash(c + float2(1, 0)), f.x),
                            lerp(RoWaterHash(c + float2(0, 1)), RoWaterHash(c + float2(1, 1)), f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half cloudShadow = RoCloudShadowAttenuation(input.positionWS.xz, 1.0h, (half)_RoCloudShadowParams.z);

                if (_RoLookWater.x < 0.5) {
                    // the classic water: the RO texture at the map's opacity
                    half3 classic = tex.rgb * _BaseColor.rgb * cloudShadow;
                    classic = RoApplyFog(classic, input.fogDepth);
                    return half4(classic, saturate(_WaterOpacity));
                }

                float t = _Time.y;
                // the surface normal: the RO water texture's own ripple pattern (its luminance slope, animated by the
                // frame flipbook) on top of slow broad swells - RO's waves, not a regular sine "zebra"
                float lum = dot(tex.rgb, float3(0.3, 0.5, 0.2));
                float2 ripple = float2(ddx(lum), ddy(lum));
                float3 swell = RoWaterNormal(input.positionWS.xz * 0.35, t * 0.6);
                float3 n = normalize(float3(swell.x * 0.6 - ripple.x * 1.6, 1.0, swell.z * 0.6 - ripple.y * 1.6));
                float3 toCamera = normalize(GetCameraPositionWS() - input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);

                // water thickness along the view ray (the seabed behind the surface)
                float surfaceEye = LinearEyeDepth(input.positionHCS.z, _ZBufferParams);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(sceneEye - surfaceEye, 0.0);

                // refraction: the seabed seen through the moving surface, only from what lies behind the water
                float2 refractUV = screenUV + n.xz * _RoLookWater.z * saturate(thickness * 0.5);
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                if (refractEye < surfaceEye) {
                    refractUV = screenUV;
                    refractEye = sceneEye;
                }

                float refractThickness = max(refractEye - surfaceEye, 0.0);
                half3 seabed = SampleSceneColor(refractUV);

                // light that reaches the water: the look rig's sun and sky
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half sunShadow = mainLight.shadowAttenuation * cloudShadow;
                half3 sunColor = mainLight.color * _RoLookSun.rgb * _RoLookSun.a;
                half3 sky = _RoLookAmbient.rgb * _RoLookSky.rgb * _RoLookSky.a;

                // absorption: red goes first, then green - shallow water turquoise, deep water the deep colour
                half3 absorb = exp(-refractThickness * _RoLookWater.y * half3(0.9, 0.32, 0.18));
                half3 deep = _RoLookWaterDeep.rgb * (sky * 1.5 + sunColor * 0.35 * sunShadow);
                half3 roSurface = tex.rgb * _BaseColor.rgb;
                half3 body = lerp(deep, roSurface * (sky + sunColor * 0.5 * sunShadow), _RoLookWaterDeep.a);
                half3 transmitted = seabed * absorb + body * (1.0 - absorb);

                // reflection: the sky over the surface (brighter at grazing angles) and the sun's glint
                half fresnel = 0.02h + 0.98h * pow(1.0h - saturate(dot(n, toCamera)), 5.0h);
                // the sky reflected: a clear blue scaled by the scene's sky light (so it darkens at night)
                half skyLum = dot(sky, half3(0.3h, 0.5h, 0.2h));
                half3 reflected = half3(0.32h, 0.52h, 0.85h) * skyLum * 3.0h + sunColor * 0.08h;
                float3 halfVector = normalize(mainLight.direction + toCamera);
                half glint = pow(saturate(dot(n, halfVector)), 600.0h) * 2.5h * sunShadow;
                half3 color = lerp(transmitted, reflected, fresnel) + sunColor * glint;

                // foam where the water is thin against the shore, broken up by drifting noise
                float foamNoise = RoWaterNoise(input.positionWS.xz * 1.7 + float2(t * 0.25, t * 0.18)) * 0.6 +
                                  RoWaterNoise(input.positionWS.xz * 4.1 - float2(t * 0.4, 0.0)) * 0.4;
                half foam = saturate(1.0 - thickness / max(_RoLookWater.w, 0.001)) * smoothstep(0.35, 0.75, foamNoise + 0.25);
                color = lerp(color, sky * 2.0h + sunColor * 0.8h * sunShadow, foam * 0.85h);

                color = RoApplyFog(color, input.fogDepth);
                // the refraction already holds what lies below: the surface is opaque
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
