#ifndef RO_GROUND_DEPTH_PASSES_INCLUDED
#define RO_GROUND_DEPTH_PASSES_INCLUDED

// DepthOnly / DepthNormals of RO Ground Enhanced (the depth prepass and the normals SSAO reads). The material
// cbuffer must match the forward pass exactly (SRP batcher).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoLookNormals.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

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

struct RoGroundDepthAttributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
};

struct RoGroundDepthVaryings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
};

RoGroundDepthVaryings RoGroundDepthVertex(RoGroundDepthAttributes input) {
    RoGroundDepthVaryings output;
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half RoGroundDepthFragment(RoGroundDepthVaryings input) : SV_Target {
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
    return input.positionCS.z;
}

half4 RoGroundDepthNormalsFragment(RoGroundDepthVaryings input) : SV_Target {
    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
    half3 normalWS = RoLookOrientGroundNormal(normalize((half3)input.normalWS), input.positionWS);
    return half4(NormalizeNormalPerPixel(normalWS), 0.0h);
}

#endif
