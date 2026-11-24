float _RoFogUse;
float _RoFogNear;
float _RoFogFar;
float4 _RoFogColor;
float _RoFogFactor;

float RoFogDepth(float4 positionHCS) {
    return abs(positionHCS.w);
}

half3 RoApplyFog(half3 color, float fogDepth) {
    if (_RoFogUse <= 0.5) {
        return color;
    }

    // roBrowser stores fog.factor but its world shaders blend only from near/far depth.
    float factor = smoothstep(_RoFogNear, _RoFogFar, fogDepth);
    return lerp(color, _RoFogColor.rgb, factor);
}
