TEXTURE2D(_RoCloudShadowNoise);
SAMPLER(sampler_RoCloudShadowNoise);

float4 _RoCloudShadowOffsetScaleA;
float4 _RoCloudShadowOffsetScaleB;
float4 _RoCloudShadowParams;
float _RoCloudShadowLightmapInfluence;
float _RoCloudShadowActorStrength;

// the look's cloud shape (RoLookRig "clouds"; all zero = the defaults below): x = size (uv scale, < 1 = bigger clouds),
// y = domain warp (billowy, non-repeating outlines), z = macro coverage swing (cloudy spells and clear spells),
// w = edge detail (ragged rims, smooth cores)
float4 _RoCloudShadowShape;
// x = strength scale of the shadow (0 = 1)
float4 _RoCloudShadowLook;

#define RO_CLOUD_NOISE(uv) SAMPLE_TEXTURE2D(_RoCloudShadowNoise, sampler_RoCloudShadowNoise, (uv)).r

half RoCloudShadowMask(float2 worldXZ) {
    float4 shape = any(_RoCloudShadowShape != 0.0) ? _RoCloudShadowShape : float4(0.3, 0.35, 0.22, 0.16);
    // the drift offsets are in uv units of the base scale: scaling the whole uv keeps the clouds' speed in the world
    float2 uvA = (worldXZ * _RoCloudShadowOffsetScaleA.z + _RoCloudShadowOffsetScaleA.xy) * shape.x;
    float2 uvB = (worldXZ * _RoCloudShadowOffsetScaleB.z + _RoCloudShadowOffsetScaleB.xy) * shape.x;
    // domain warp: a slow low-frequency field bends the lookup, the tiled noise loses its repeating blobs
    float2 warp = float2(RO_CLOUD_NOISE(uvB * 0.41 + float2(0.13, 0.71)), RO_CLOUD_NOISE(uvB * 0.41 + float2(0.57, 0.29))) - 0.5;
    half noiseA = RO_CLOUD_NOISE(uvA + warp * shape.y);
    half noiseB = RO_CLOUD_NOISE(uvB - warp * (shape.y * 0.6));
    half noise = noiseA * 0.7h + noiseB * 0.3h;
    // macro coverage: a far larger field travelling with the clouds moves the threshold (a bank of clouds, then sun)
    half macro = RO_CLOUD_NOISE(uvA * 0.13 + float2(0.37, 0.11));
    half threshold = (half)_RoCloudShadowParams.x - (macro - 0.5h) * (half)shape.z;
    half softness = max((half)_RoCloudShadowParams.y, 0.0001h);
    // ragged rims: fine noise only matters inside the soft band, the cores stay smooth
    noise += (RO_CLOUD_NOISE(uvA * 3.7 + warp) - 0.5h) * (half)shape.w;
    return smoothstep(threshold - softness, threshold + softness, noise);
}

half RoCloudShadowAttenuation(float2 worldXZ, half sunVisibility, half strength) {
    if (strength <= 0.0001h) {
        return 1.0h;
    }

    half sunlitMaskBlend = saturate((half)_RoCloudShadowParams.w);
    half lookStrength = _RoCloudShadowLook.x > 0.0 ? (half)_RoCloudShadowLook.x : 0.8h;
    half effectiveStrength = saturate(strength * lookStrength) * lerp(1.0h, saturate(sunVisibility), sunlitMaskBlend);
    return 1.0h - RoCloudShadowMask(worldXZ) * effectiveStrength;
}
