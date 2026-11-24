#ifndef RO_LOOK_LIGHTING_INCLUDED
#define RO_LOOK_LIGHTING_INCLUDED

// The enhanced world lighting in linear HDR (the "look" rig, RoLookRig.cs sets the globals): albedo times the light
// that reaches the surface - the sun (the URP main light, already linear and with its intensity), a sky / ground
// hemisphere ambient taken from the map's RSW ambient, screen-space ambient occlusion on the ambient only, and the
// map lamps. The tonemapper turns the HDR sum into the picture. Requires Lighting.hlsl.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

// the scene ambient (RenderSettings.ambientLight: the map's RSW ambient, then day / night), linear
float4 _RoLookAmbient;
// rgb = sky ambient tint, a = ambient scale
float4 _RoLookSky;
// rgb = ground bounce tint, a = how much the hemisphere leans to the ground colour facing down
float4 _RoLookGround;
// rgb = sun tint, a = sun intensity scale
float4 _RoLookSun;
// x = lightmap light (GND lightmap rgb as light on the albedo), y = lightmap occlusion (alpha on the ambient),
// z = sun wrap, w = map lamp scale
float4 _RoLookParams;
// x = sprite sun share (a camera-facing card has no normal), y = sprite ambient scale, z = sprite lamp scale,
// w = model ambient floor (RSM normals are often wrong; a floor keeps a broken face from going black)
float4 _RoLookSprite;
// x = body half-width (world units) of the cylinder a sprite card is shaded as, y = outline bevel radius (texels),
// z = outline bevel strength, w = cylinder strength
float4 _RoLookSpriteShape;
// x = 1 on a map with no sky (dungeon, interior): no sun, the baked GND light is the map's lighting;
// y = sprite ambient scale there (a sprite has no lightmap)
float4 _RoLookIndoor;
// relief strength from the texture luminance (RoLookReliefNormal): x = ground, y = models
float4 _RoLookRelief;

struct RoLookSurface {
    half3 albedo;
    half3 normalWS;
    float3 positionWS;
    float2 screenUV;
};

half3 RoLookAmbient(half3 normalWS) {
    half up = saturate(normalWS.y * 0.5h + 0.5h);
    half3 hemisphere = lerp((half3)_RoLookGround.rgb, (half3)_RoLookSky.rgb, lerp(1.0h, up, (half)_RoLookGround.a));
    return (half3)_RoLookAmbient.rgb * hemisphere * (half)_RoLookSky.a;
}

// models (RoLookRig "rim"; y = 0 = the defaults): x = sky rim strength, y = its falloff power, z = how far the sun
// wraps round cut-out foliage (0 = the default)
float4 _RoLookRim;

// The sky seen at grazing angles: a cool lift along the silhouettes of buildings and trees (more on upward faces), so
// a roof edge or a wall corner reads against the street instead of melting into one dark mass.
half3 RoLookRim(half3 normalWS, half3 viewWS) {
    half strength = _RoLookRim.y > 0.0 ? (half)_RoLookRim.x : 0.6h;
    half power = _RoLookRim.y > 0.0 ? (half)_RoLookRim.y : 2.5h;
    half fresnel = pow(1.0h - saturate(dot(normalWS, viewWS)), power);
    return RoLookAmbient(half3(0.0h, 1.0h, 0.0h)) * (fresnel * strength * (0.5h + 0.5h * saturate(normalWS.y + 0.5h)));
}

half RoLookSunTerm(half3 normalWS, half3 lightDirection) {
    half wrap = (half)_RoLookParams.z;
    return saturate((dot(normalWS, lightDirection) + wrap) / (1.0h + wrap));
}

half3 RoLookSunColor(Light mainLight) {
    return mainLight.color * (half3)_RoLookSun.rgb * (half)_RoLookSun.a;
}

AmbientOcclusionFactor RoLookOcclusion(float2 screenUV) {
    return GetScreenSpaceAmbientOcclusion(screenUV);
}

// the map lamps (URP additional lights): wrapped so a lamp also lights the far side of a curved surface a little
half3 RoLookLamps(RoLookSurface s, half wrapAmount) {
    half3 light = 0;
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = s.positionWS;
    inputData.normalizedScreenSpaceUV = s.screenUV;
    uint additionalLightCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();
    LIGHT_LOOP_BEGIN(additionalLightCount)
        Light lamp = GetAdditionalLight(lightIndex, inputData.positionWS, half4(1.0h, 1.0h, 1.0h, 1.0h));
        bool includeLight = true;
#if defined(_LIGHT_LAYERS)
        includeLight = IsMatchingLightLayer(lamp.layerMask, meshRenderingLayers);
#endif
        if (includeLight) {
            half facing = saturate((dot(s.normalWS, lamp.direction) + wrapAmount) / (1.0h + wrapAmount));
            light += lamp.color * (lamp.distanceAttenuation * lamp.shadowAttenuation * facing);
        }
    LIGHT_LOOP_END
#endif
    return light * (half)_RoLookParams.w;
}

// A sprite card has no normal. It is shaded as a body: a vertical cylinder around the actor's feet axis (sideOffset =
// world units right of the axis, seen from the camera) plus a bevel from the sprite's own outline (the alpha gradient,
// turned into screen space through the uv derivatives, so mirrored frames bevel the right way). A torch on the left
// lights the left side; a low sun behind the actor rims its outline.
half3 RoLookSpriteNormal(Texture2D spriteTexture, SamplerState spriteSampler, float2 uv, float sideOffset) {
    uint width, height;
    spriteTexture.GetDimensions(width, height);
    float2 texel = float2(1.0 / max(width, 1u), 1.0 / max(height, 1u)) * _RoLookSpriteShape.y;
    half aL = spriteTexture.Sample(spriteSampler, uv - float2(texel.x, 0.0)).a;
    half aR = spriteTexture.Sample(spriteSampler, uv + float2(texel.x, 0.0)).a;
    half aD = spriteTexture.Sample(spriteSampler, uv - float2(0.0, texel.y)).a;
    half aU = spriteTexture.Sample(spriteSampler, uv + float2(0.0, texel.y)).a;
    float2 gradUV = float2(aR - aL, aU - aD);
    float2 dAdUV = gradUV / (2.0 * texel);
    // screen-space alpha gradient (x right, y down); the outline normal points down it, from opaque to empty
    float2 gradScreen = float2(dot(dAdUV, ddx(uv)), dot(dAdUV, ddy(uv)));
    float edge = saturate(length(gradUV));
    float2 outward = length(gradScreen) > 1e-5 ? -normalize(gradScreen) * edge : float2(0.0, 0.0);

    float3 right = UNITY_MATRIX_V[0].xyz;
    float3 up = UNITY_MATRIX_V[1].xyz;
    float3 toCamera = UNITY_MATRIX_V[2].xyz;
    float side = clamp(sideOffset / max(_RoLookSpriteShape.x, 0.01), -1.0, 1.0);
    float3 lateral = right * (side * _RoLookSpriteShape.w + outward.x * _RoLookSpriteShape.z) - up * (outward.y * _RoLookSpriteShape.z);
    return (half3)normalize(lateral + toCamera * sqrt(saturate(1.0 - dot(lateral, lateral) * 0.5)) + toCamera * 0.15);
}

#endif
