float4 _RoWindSwayGlobalParams;
float4 _RoWeatherWindDirection;
float _RoWeatherWindStrength;
float _RoWindSwayEnvironmentScale;

float3 RoApplyWindSway(float3 positionWS, half4 vertexColor, half mode, half amplitude, half frequency) {
    if (mode < 0.5h || amplitude <= 0.00001h) {
        return positionWS;
    }

    float weight = saturate((float)vertexColor.r);
    if (weight <= 0.00001) {
        return positionWS;
    }

    bool globalsInitialized = dot(abs(_RoWindSwayGlobalParams), float4(1.0, 1.0, 1.0, 1.0)) > 0.000001;
    float amplitudeMultiplier = globalsInitialized ? max(_RoWindSwayGlobalParams.x, 0.0) : 1.0;
    float frequencyMultiplier = globalsInitialized ? max(_RoWindSwayGlobalParams.y, 0.0) : 1.0;
    float resolvedAmplitude = max((float)amplitude, 0.0) * amplitudeMultiplier;
    float resolvedFrequency = max((float)frequency, 0.0) * frequencyMultiplier;
    if (resolvedAmplitude <= 0.00001 || resolvedFrequency <= 0.00001) {
        return positionWS;
    }

    float2 windDirWS = _RoWeatherWindDirection.xz;
    float windLenSq = dot(windDirWS, windDirWS);
    windDirWS = windLenSq > 0.000001 ? windDirWS * rsqrt(windLenSq) : float2(1.0, 0.0);
    float2 perpWS = float2(-windDirWS.y, windDirWS.x);
    float baseBreeze = globalsInitialized ? saturate(_RoWindSwayGlobalParams.z) : 0.35;
    float strengthFactor = saturate(baseBreeze + max(_RoWeatherWindStrength, 0.0) * (1.0 - baseBreeze));
    float gustStrength = globalsInitialized ? saturate(_RoWindSwayGlobalParams.w) : 0.5;
    float gustPhase = dot(positionWS.xz, windDirWS) * 0.05;
    float gust = 0.7 * sin(_Time.y * 0.31 + gustPhase) + 0.3 * sin(_Time.y * 0.53 + gustPhase * 1.7);
    float gustFactor = max(1.0 + gustStrength * 0.5 * gust, 0.0);
    strengthFactor *= gustFactor;
    float environmentScale = _RoWindSwayEnvironmentScale > 0.000001 ? saturate(_RoWindSwayEnvironmentScale) : 1.0;
    strengthFactor *= environmentScale;
    float phase = (float)vertexColor.g * 6.2831 + dot(positionWS.xz, float2(0.7, 1.3));
    float time = _Time.y;
    float2 offset;

    if (mode < 1.5h) {
        float primary = sin(time * resolvedFrequency + phase + positionWS.y * 2.1);
        float secondary = 0.35 * sin(time * resolvedFrequency * 2.33 + phase * 1.7);
        float flutter = 0.3 * sin(time * resolvedFrequency * 1.51 + phase);
        offset = (windDirWS * (primary + secondary) + perpWS * flutter) * resolvedAmplitude * weight * strengthFactor;
    } else {
        float s1 = sin(time * resolvedFrequency + phase);
        float s2 = sin(time * resolvedFrequency * 1.37 + phase * 1.41);
        offset = (windDirWS * s1 + perpWS * 0.6 * s2) * resolvedAmplitude * weight * strengthFactor;
    }

    return positionWS + float3(offset.x, 0.0, offset.y);
}
