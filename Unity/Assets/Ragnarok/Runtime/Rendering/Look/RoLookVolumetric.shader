Shader "Hidden/RO Look Volumetric" {
    // The HD-2D air: pass 0 marches every half-res pixel's view ray through height fog, gathering the light of the
    // nearby lamps / fires (RoLookVolumetrics fills the list) and the ambient; rgb = scattered light, a = transmittance.
    // Pass 1 composites it over the scene (bilinear upsample).
    SubShader {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #define RO_VOL_MAX_LIGHTS 16
        float4 _RoVolLightPos[RO_VOL_MAX_LIGHTS];   // xyz position, w = range
        float4 _RoVolLightColor[RO_VOL_MAX_LIGHTS]; // rgb colour x intensity
        int _RoVolLightCount;
        // x = density, y = height falloff (per unit above the base), z = fog base height, w = max march distance
        float4 _RoVolFog;
        // rgb = ambient in-scatter (the fog's own colour, linear), a = light scattering scale
        float4 _RoVolAmbient;
        // x = steps, y = anisotropy (forward scattering toward the camera), z = sun shafts scale
        float4 _RoVolParams;
        float4 _RoVolSunColor;
        float4 _RoVolSunDirection;
        TEXTURE2D(_RoVolumeTex);
        SAMPLER(sampler_RoVolumeTex);
        ENDHLSL

        Pass {
            Name "March"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float InterleavedNoise(float2 pixel) {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float4 Frag(Varyings input) : SV_Target {
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
#if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
#endif
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraWS = GetCameraPositionWS();
                float3 ray = positionWS - cameraWS;
                float rayLength = min(length(ray), _RoVolFog.w);
                float3 direction = normalize(ray);
                int steps = (int) _RoVolParams.x;
                float stepLength = rayLength / steps;
                float jitter = InterleavedNoise(input.positionCS.xy);

                float3 scattered = 0;
                float transmittance = 1;
                [loop]
                for (int i = 0; i < steps; i++) {
                    float t = (i + jitter) * stepLength;
                    float3 p = cameraWS + direction * t;
                    float density = _RoVolFog.x * exp(-max(p.y - _RoVolFog.z, 0.0) * _RoVolFog.y);
                    float3 light = _RoVolAmbient.rgb;
                    [loop]
                    for (int l = 0; l < _RoVolLightCount; l++) {
                        float3 toLight = _RoVolLightPos[l].xyz - p;
                        float distance2 = dot(toLight, toLight);
                        float range = _RoVolLightPos[l].w;
                        float fade = saturate(1.0 - distance2 / (range * range));
                        // a soft core around the flame and a long tail (inverse square, clamped near the source)
                        float falloff = fade * fade / (1.0 + distance2 * 0.35);
                        // forward scattering: brighter looking toward a light through the haze
                        float cosTheta = dot(direction, toLight * rsqrt(max(distance2, 1e-4)));
                        float g = _RoVolParams.y;
                        float phase = (1.0 - g * g) / pow(max(1.0 + g * g - 2.0 * g * cosTheta, 1e-3), 1.5) * 0.25;
                        light += _RoVolLightColor[l].rgb * falloff * phase * _RoVolAmbient.a;
                    }

                    float stepTransmittance = exp(-density * stepLength);
                    scattered += transmittance * (1.0 - stepTransmittance) * light;
                    transmittance *= stepTransmittance;
                }

                return float4(scattered, transmittance);
            }
            ENDHLSL
        }

        Pass {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target {
                float4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                // a 3x3 tent over the half-res volume: the march's per-pixel jitter averages out instead of drawing lines
                float2 texel = 1.0 / max(_ScreenParams.xy * 0.5, 1.0);
                float4 volume = 0;
                [unroll] for (int y = -1; y <= 1; y++) {
                    [unroll] for (int x = -1; x <= 1; x++) {
                        float w = (2.0 - abs(x)) * (2.0 - abs(y));
                        volume += SAMPLE_TEXTURE2D(_RoVolumeTex, sampler_RoVolumeTex, input.texcoord + float2(x, y) * texel * 1.5) * w;
                    }
                }
                volume /= 16.0;
                return float4(scene.rgb * volume.a + volume.rgb, scene.a);
            }
            ENDHLSL
        }
    }
}
