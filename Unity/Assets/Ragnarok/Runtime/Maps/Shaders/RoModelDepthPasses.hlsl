#ifndef RO_MODEL_DEPTH_PASSES_INCLUDED
#define RO_MODEL_DEPTH_PASSES_INCLUDED

// DepthOnly / DepthNormals of RO Model Enhanced: the same wind sway and cut-out as the colour pass. A model that does
// not write depth in its colour pass (faded out of the player's way, blended) stays out of both.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/Ragnarok/Runtime/Maps/Shaders/RoModelEnhancedInput.hlsl"
#include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoWindSway.hlsl"
#include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoModelWaterFlow.hlsl"
#include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoLookNormals.hlsl"

struct RoModelDepthAttributes {
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct RoModelDepthVaryings {
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
};

RoModelDepthVaryings RoModelDepthVertex(RoModelDepthAttributes input) {
    RoModelDepthVaryings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    positionWS = RoApplyWindSway(positionWS, (half4)input.color, (half)_RoWindSwayMode, (half)_RoWindSwayAmplitude, (half)_RoWindSwayFrequency);
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

void RoModelDepthClip(float2 uv) {
    clip(_ZWrite - 0.5);
    float2 sampleUv = RoApplyModelWaterFlowUv(uv, (half)_RoModelWaterFlowMode, (half)_RoModelWaterFlowStrength, (half)_RoModelWaterFlowSpeed);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUv).a * _BaseColor.a;
    if (_UseAlphaClip > 0.5) {
        clip(alpha - _Cutoff);
    }
}

half RoModelDepthFragment(RoModelDepthVaryings input) : SV_Target {
    RoModelDepthClip(input.uv);
    return input.positionCS.z;
}

half4 RoModelDepthNormalsFragment(RoModelDepthVaryings input) : SV_Target {
    RoModelDepthClip(input.uv);
    half3 normalWS = RoLookOrientModelNormal(normalize((half3)input.normalWS), input.positionWS);
    return half4(NormalizeNormalPerPixel(normalWS), 0.0h);
}

#endif
