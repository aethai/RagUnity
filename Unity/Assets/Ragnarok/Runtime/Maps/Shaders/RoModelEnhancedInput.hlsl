#ifndef RO_MODEL_ENHANCED_INPUT_INCLUDED
#define RO_MODEL_ENHANCED_INPUT_INCLUDED

// The material inputs of RO Model Enhanced, shared by every pass (the SRP batcher needs one UnityPerMaterial layout).

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _Cutoff;
    float _UseAlphaClip;
    float _Cull;
    float _ZWrite;
    float _SrcBlend;
    float _DstBlend;
    float _RoUseLighting;
    float4 _RoLightDirection;
    float4 _RoLightAmbient;
    float4 _RoLightDiffuse;
    float _RoLightOpacity;
    float _RoLightingStrength;
    float _RoModelMinLight;
    float _RoReceiveRealtimeShadows;
    float _RoRealtimeShadowStrength;
    float _RoDebugRealtimeShadows;
    float _RoModelTopLightStrength;
    float _RoEmissionLightInfluence;
    float _RoRainWetness;
    float _RoRainDarkening;
    float _RoRainHighlight;
    float _RoRainPuddleStrength;
    float _RoRainStreakStrength;
    float _RoWindSwayMode;
    float _RoWindSwayAmplitude;
    float _RoWindSwayFrequency;
    float _RoModelWaterFlowMode;
    float _RoModelWaterFlowStrength;
    float _RoModelWaterFlowSpeed;
    float _RoShadowKeepsAlphaClip;
CBUFFER_END

#endif
