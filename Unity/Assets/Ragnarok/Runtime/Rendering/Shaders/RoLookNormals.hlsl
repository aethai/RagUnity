#ifndef RO_LOOK_NORMALS_INCLUDED
#define RO_LOOK_NORMALS_INCLUDED

// Normal orientation for RO meshes. GND normals carry only the mesh winding (RoGroundMeshBuilder.BuildShadingNormals)
// and RSM normals are often flipped or the face is two-sided, so the lighting and the depth-normals pass both turn a
// normal the way the surface is seen. Requires Core.hlsl.

// ground: a top surface faces up; a wall faces the viewer
half3 RoLookOrientGroundNormal(half3 normalWS, float3 positionWS) {
    if (abs(normalWS.y) > 0.2h) {
        return normalWS.y < 0.0h ? -normalWS : normalWS;
    }

    half3 toViewer = (half3)normalize(GetCameraPositionWS() - positionWS);
    return dot(normalWS, toViewer) < 0.0h ? -normalWS : normalWS;
}

// model: the face the viewer sees (two-sided leaves and banners, inverted RSM nodes)
half3 RoLookOrientModelNormal(half3 normalWS, float3 positionWS) {
    half3 toViewer = (half3)normalize(GetCameraPositionWS() - positionWS);
    return dot(normalWS, toViewer) < 0.0h ? -normalWS : normalWS;
}

// Relief from the texture itself (RO has no normal maps): the albedo's luminance read as height, its slope across
// the texture turned into world space through a cotangent frame built from the screen derivatives - stone, roots and
// cracks catch a torch or a low sun from the side. strength 0 = the geometric normal. Requires Core.hlsl.
half3 RoLookReliefNormal(Texture2D albedo, SamplerState albedoSampler, float2 uv, half3 normalWS, float3 positionWS, half strength) {
    if (strength <= 0.0h) {
        return normalWS;
    }

    uint width, height;
    albedo.GetDimensions(width, height);
    float2 texel = 1.0 / float2(max(width, 1u), max(height, 1u));
    const float3 lumaWeights = float3(0.3, 0.55, 0.15);
    float hL = dot(albedo.Sample(albedoSampler, uv - float2(texel.x, 0.0)).rgb, lumaWeights);
    float hR = dot(albedo.Sample(albedoSampler, uv + float2(texel.x, 0.0)).rgb, lumaWeights);
    float hD = dot(albedo.Sample(albedoSampler, uv - float2(0.0, texel.y)).rgb, lumaWeights);
    float hU = dot(albedo.Sample(albedoSampler, uv + float2(0.0, texel.y)).rgb, lumaWeights);
    float2 slope = float2(hR - hL, hU - hD) * 0.5;

    float3 dp1 = ddx(positionWS), dp2 = ddy(positionWS);
    float2 duv1 = ddx(uv), duv2 = ddy(uv);
    float3 dp2perp = cross(dp2, normalWS);
    float3 dp1perp = cross(normalWS, dp1);
    float3 tangent = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 bitangent = dp2perp * duv1.y + dp1perp * duv2.y;
    float invScale = rsqrt(max(max(dot(tangent, tangent), dot(bitangent, bitangent)), 1e-12));
    tangent *= invScale;
    bitangent *= invScale;
    return (half3)normalize(normalWS - (slope.x * tangent + slope.y * bitangent) * strength);
}

#endif
