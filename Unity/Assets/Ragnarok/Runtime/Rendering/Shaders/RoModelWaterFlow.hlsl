#ifndef RO_MODEL_WATER_FLOW_INCLUDED
#define RO_MODEL_WATER_FLOW_INCLUDED

float2 RoRotateWaterFlowUv(float2 p, float angle) {
    float s = sin(angle);
    float c = cos(angle);
    return float2((p.x * c) - (p.y * s), (p.x * s) + (p.y * c));
}

half RoModelWaterFlowMask(float2 uv, half mode) {
    if (mode < 1.5h) {
        return 1.0h;
    }

    float2 p = abs(frac(uv) - 0.5);
    float octagonDistance = max(p.x, p.y) + min(p.x, p.y) * 0.42;
    return (half)(1.0 - smoothstep(0.34, 0.43, octagonDistance));
}

float2 RoApplyModelWaterFlowUv(float2 uv, half mode, half strength, half speed) {
    if (mode <= 0.5h || strength <= 0.0001h || speed <= 0.0001h) {
        return uv;
    }

    half flowStrength = saturate(strength) * RoModelWaterFlowMask(uv, mode);
    if (flowStrength <= 0.0001h) {
        return uv;
    }

    float t = _Time.y * speed;
    float2 center = floor(uv) + 0.5;
    float2 centered = uv - center;

    float radius = length(centered);
    float angle = t * 0.42 * flowStrength;
    float2 rotated = RoRotateWaterFlowUv(centered, angle) + center;

    float2 ripple = float2(
        sin((uv.y * 18.0) + (t * 1.7) + radius * 8.0),
        cos((uv.x * 15.0) - (t * 1.35) + radius * 6.0)
    ) * (0.012 * flowStrength);

    return lerp(uv, rotated + ripple, flowStrength);
}

#endif
