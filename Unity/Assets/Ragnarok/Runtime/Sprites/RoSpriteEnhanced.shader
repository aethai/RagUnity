Shader "Ragnarok/RO Sprite Enhanced" {
    Properties {
        [MainTexture] _BaseMap ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _Cull ("Cull", Float) = 0
        _ZWrite ("ZWrite", Float) = 0
        // the forward pass's stencil op on drawn pixels: Keep (0) while the depth prepass marks them, Replace (2) when the
        // forward pass writes depth and stencil itself (prepass off)
        _RoSpriteStencilPass ("Sprite Stencil Pass Op", Float) = 0
        _ZTest ("ZTest", Float) = 4
        _SrcBlend ("Src Blend", Float) = 5
        _DstBlend ("Dst Blend", Float) = 10
        _VPos ("Vertical Sprite Offset", Float) = 0
        _RoShadowCasterYOffset ("Shadow Caster Y Offset", Float) = 0
        _RoShadowCasterPullDistance ("Shadow Caster Pull Distance", Float) = 0.45
        _RoShadowCasterGroundClearance ("Shadow Caster Ground Clearance", Float) = 0.04
        _RoShadowCasterLiftPullScale ("Shadow Caster Lift Pull Scale", Range(0, 2)) = 1
        _RoShadowCasterBottomLift ("Shadow Caster Bottom Lift", Float) = 0
        _DepthCorrection ("Depth Correction", Float) = 1
        _RoSpriteDepthBias ("RO Sprite Depth Bias (cells toward the camera)", Float) = 0.4
        _RoMapLightMultiply ("RO Map Light Multiply", Color) = (1, 1, 1, 1)
        _RoMapLightAdd ("RO Map Light Add", Color) = (0, 0, 0, 0)
        _RoDirectionalLightInfluence ("Directional Light Influence", Range(0, 1)) = 0.35
        _RoEmissionLightInfluence ("Map Emission Light Influence", Range(0, 2)) = 0.65
        _RoUseAdditionalLights ("Use Additional Lights", Float) = 0
        _RoReceiveRealtimeShadows ("Receive Realtime Shadows", Float) = 1
        _RoRealtimeShadowStrength ("Realtime Shadow Strength", Range(0, 1)) = 0.65
        _RoShadowReceiveLightOffset ("Realtime Shadow Receive Self-Shadow Bias", Float) = 0.5
        _RoShadowReceiveHeight ("Realtime Shadow Receive Height Ref (unused)", Float) = 2.0
        _RoUseSharedActorShadowCard ("Use Shared Actor Shadow Card", Float) = 0
        _RoActorShadowCardCenterOS ("Actor Shadow Card Center OS", Vector) = (0, 0, 0, 0)
        _RoActorShadowCardBottomLift ("Actor Shadow Card Bottom Lift", Float) = 0
        // the depth prepass cut: 0x9f / 255 for the layers the client draws in list 0x180 (body, head, weapon, shield,
        // robe), above 1 (no depth) for headgear and the other translucent layers (reference packages/sprite-effect-depth)
        _RoSpriteDepthCutoff ("Sprite Depth Cutoff", Float) = 0.01
        // the actor's ground point in the layer's object space (the lean depth's origin)
        _RoSpriteFeetOS ("Sprite Feet OS", Vector) = (0, 0, 0, 0)
        // the layer's rank n in the client's layer map (its depth uses rhw + n·1e-6)
        _RoSpriteRank ("Sprite Layer Rank", Float) = 0
        // 1 = one pass: the colour pass writes the depth itself, at the depth pass's depth (fully opaque layers only;
        // RoActSpriteAnimator decides), and the depth pass is off
        _RoSpriteSinglePass ("Sprite Single Pass", Float) = 0
        // the colour pass's Offset units: -2 (one depth unit nearer than the depth pass it tests against), -1 single pass
        _RoSpriteOffsetUnits ("Sprite Offset Units", Float) = -2
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass {
            Name "SpriteDepthPrepass"
            // the sprite's depth, drawn with the sprite itself (SRPDefaultUnlit runs with UniversalForward in the
            // transparent pass): the client's list 0x180 pass 1 writes depth only for clips with vertex alpha 255 and
            // texel x vertex alpha >= 0x9f; translucent clips (list 0x18c) never write it (reference npc-ground-circle)
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ColorMask 0
            ZWrite On
            ZTest LEqual
            Offset -1, -1
            Cull [_Cull]
            // no AlphaToMask: the pass returns alpha 0, which with MSAA meant zero coverage and no depth at all

            Stencil {
                Ref 32
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Cull;
                float _ZWrite;
                float _ZTest;
                float _SrcBlend;
                float _DstBlend;
                float _VPos;
                float _RoShadowCasterYOffset;
                float _RoShadowCasterPullDistance;
                float _RoShadowCasterGroundClearance;
                float _RoShadowCasterLiftPullScale;
                float _RoShadowCasterBottomLift;
                float _DepthCorrection;
                float _RoSpriteDepthBias;
                float4 _RoMapLightMultiply;
                float4 _RoMapLightAdd;
                float _RoDirectionalLightInfluence;
                float _RoEmissionLightInfluence;
                float _RoUseAdditionalLights;
                float _RoReceiveRealtimeShadows;
                float _RoRealtimeShadowStrength;
                float _RoShadowReceiveLightOffset;
                float _RoShadowReceiveHeight;
                float _RoUseSharedActorShadowCard;
                float4 _RoActorShadowCardCenterOS;
                float _RoActorShadowCardBottomLift;
                float _RoSpriteDepthCutoff;
                float4 _RoSpriteFeetOS;
                float _RoSpriteRank;
                float _RoSpriteSinglePass;
                float _RoSpriteOffsetUnits;
            CBUFFER_END

            float _RoSpriteLegacyDepth;
            float _RoSpriteCurvedDepth;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                // x: the clip's index in its ACT frame (RoSpriteMeshBuilder); 0 for meshes without it
                float2 clip : TEXCOORD1;
            };

            // the clips of one ACT frame share a plane, so their pixels tied in depth and the last bit of each quad's
            // interpolation picked the winner as the camera moved (a cart's wheel flickering through its box); the client
            // keys clip i nearer by (3 + i)·9e-6 rhw (draw-order §3), here i·RoSpriteClipStep view units, under one rank step
            #define RoSpriteClipStep 0.002

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float4 ResolveSpritePosition(float3 positionOS) {
                float4 positionHCS = TransformObjectToHClip(positionOS);
                if (_DepthCorrection <= 0.5) {
                    return positionHCS;
                }

                float3 worldVertex = TransformObjectToWorld(positionOS);
                float3 worldCenter = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 rayDirection = normalize(worldVertex - _WorldSpaceCameraPos.xyz);
                float3 planeNormal = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0));
                float3 flatPlaneNormal = float3(planeNormal.x, 0.0, planeNormal.z);
                planeNormal = dot(flatPlaneNormal, flatPlaneNormal) < 0.000001
                    ? normalize(planeNormal)
                    : normalize(flatPlaneNormal);

                float denominator = dot(planeNormal, rayDirection);
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = dot(worldCenter - _WorldSpaceCameraPos.xyz, planeNormal) / denominator;
                float3 correctedWorld = _WorldSpaceCameraPos.xyz + rayDirection * distance;
                float4 correctedHCS = TransformWorldToHClip(correctedWorld);
                float correctedZ = correctedHCS.z * (positionHCS.w / max(correctedHCS.w, 0.000001));

#if UNITY_REVERSED_Z
                positionHCS.z = max(positionHCS.z, correctedZ);
#else
                positionHCS.z = min(positionHCS.z, correctedZ);
#endif
                return positionHCS;
            }

            // pulls the depth a fixed world distance (_RoSpriteDepthBias, in cells) toward the camera: an NDC offset grew
            // with the square of the camera distance and let actors behind walls draw over them once the camera moved away
            float4 ApplyRoSpriteDepthBias(float4 positionHCS) {
                float bias = max(_RoSpriteDepthBias, 0.0);
                if (bias <= 0.0 || UNITY_MATRIX_P[3][3] != 0.0 || positionHCS.w <= 0.0) {
                    return positionHCS;
                }

                // perspective: z_ndc = P23 / d - P22 at view distance d (clip w), whatever the depth direction
                float p22 = UNITY_MATRIX_P[2][2];
                float p23 = UNITY_MATRIX_P[2][3];
                float zNdc = positionHCS.z / positionHCS.w;
                float denominator = zNdc + p22;
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = p23 / denominator;
                float biasedDistance = max(distance - bias, _ProjectionParams.y);
                positionHCS.z = (p23 / biasedDistance - p22) * positionHCS.w;
                return positionHCS;
            }

            // the client's sprite depth (vf09 0xbce6f0): rhw = 1 / (vz + 0.16 * dy), vz the actor's view depth and dy the
            // pixel offset from the actor's screen point (y down), so the quad is a plane leaning towards the camera, each
            // pixel higher 0.16 RO units (0.032 Unity) nearer; the effects' TL vertices are in the same space
            // (reference packages/sprite-effect-depth). _RoSpriteLegacyDepth (global, !toggle lean off) = the old plane
            float4 ResolveClientLeanPosition(float3 positionOS, float clipIndex) {
                // precise: the depth pass and the colour pass run this in two programs and must get the same bits (the colour
                // pass tests LEqual against the depth pass's pixel; reordered or fused maths lost rows of a sprite, striped)
                precise float4 positionHCS = TransformObjectToHClip(positionOS);
                precise float4 feetHCS = TransformObjectToHClip(_RoSpriteFeetOS.xyz);
                if (positionHCS.w <= 0.0 || feetHCS.w <= 0.0) {
                    return positionHCS;
                }

                // the client's sprite plane (vf09 0xbce6f0): depth falls 0.16 tan(-lat) / (H / 816) RO units per pixel above
                // the ground point; with its camera (XFactor (H / 2) / tan 7.5°, distance 300) that is 1.65 tan(-lat) view
                // units per world unit of height, which is what is kept here so the plane does not depend on our zoom.
                // Only the depth changes: x, y and w stay the vertex's, so the texture interpolation is not warped
                precise float upPixels = (positionHCS.y / positionHCS.w - feetHCS.y / feetHCS.w) * 0.5 * _ScreenParams.y;
                precise float3 forwardWS = -UNITY_MATRIX_V[2].xyz;
                precise float pitch = clamp(asin(saturate(-forwardWS.y)), 0.05, 1.4);
                precise float focalPixels = UNITY_MATRIX_P[1][1] * _ScreenParams.y * 0.5;
                precise float upWorld = upPixels * feetHCS.w / focalPixels;
                precise float depth = feetHCS.w - 1.65 * tan(pitch) * upWorld;
                // the client keys every layer of a player from one line through the body rect: rhw linear in screen y
                // (draw-order §1.5: rhwTop = top·a + b, rhwBottom = bottom·a + b, z linear in rhw), so all layers of an actor
                // get the same depth at the same pixel and only the rank n orders them. A depth recomputed per vertex in view
                // units and interpolated by the GPU differed per quad size (a tall body quad vs small wings: tenths of a unit,
                // far over the 0.01 rank step) and the below-feet ground clamp pulled a whole quad forward when its bottom
                // corners were below the feet; the robe drew behind a cloak the client puts it on. The plane here is the
                // tangent at the feet: z_ndc linear in screen y, exact under interpolation. _RoSpriteCurvedDepth = the old one
                if (_RoSpriteCurvedDepth < 0.5) {
                    precise float feetDepth = max(feetHCS.w - 1.6 - _RoSpriteRank * 0.01 - clipIndex * RoSpriteClipStep, _ProjectionParams.y);
                    precise float zFeet = (UNITY_MATRIX_P[2][2] * -feetDepth + UNITY_MATRIX_P[2][3]) / feetDepth;
                    precise float zSlope = -UNITY_MATRIX_P[2][3] / (feetDepth * feetDepth);
                    precise float zLinear = zFeet + zSlope * (-1.65 * tan(pitch) * upWorld);
                    return float4(positionHCS.xy, zLinear * positionHCS.w, positionHCS.w);
                }

                // rows down to 96 px below the ground point stay just in front of the ground at their pixel (feet, an
                // NPC's ground circle; a quad corner decides its whole edge); lower rows lean on and the ground clips them
                if (upPixels < 0.0 && upPixels > -96.0) {
                    precise float3 vertexWS = TransformObjectToWorld(positionOS);
                    precise float3 feetWS = TransformObjectToWorld(_RoSpriteFeetOS.xyz);
                    precise float3 rayWS = vertexWS - _WorldSpaceCameraPos.xyz;
                    if (rayWS.y < -0.0001) {
                        precise float3 groundWS = _WorldSpaceCameraPos.xyz + rayWS * ((feetWS.y - _WorldSpaceCameraPos.y) / rayWS.y);
                        precise float groundDepth = -mul(UNITY_MATRIX_V, float4(groundWS, 1.0)).z;
                        depth = min(depth, groundDepth - 0.05);
                    }
                }

                // the layer rank n (the client's n·1e-6) as a fixed view distance, so zooming in does not merge the layers
                // the client's rhw terms ((3 + clip) K9, the K36 scale term) put a sprite about 8 RO units (1.6 view units)
                // in front of the ground under it at its default camera (npc-ground-circle §3): the feet stand on an aura disc
                // 1 unit above the ground instead of under it
                depth = max(depth - 1.6 - _RoSpriteRank * 0.01 - clipIndex * RoSpriteClipStep, _ProjectionParams.y);
                precise float zNdc = (UNITY_MATRIX_P[2][2] * -depth + UNITY_MATRIX_P[2][3]) / depth;
                return float4(positionHCS.xy, zNdc * positionHCS.w, positionHCS.w);
            }

            float4 ResolveSpriteDepthPosition(float3 positionOS, float clipIndex) {
                if (_RoSpriteLegacyDepth < 0.5 && UNITY_MATRIX_P[3][3] == 0.0) {
                    return ResolveClientLeanPosition(positionOS, clipIndex);
                }

                return ApplyRoSpriteDepthBias(ResolveSpritePosition(positionOS));
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += _VPos;
                output.positionHCS = ResolveSpriteDepthPosition(positionOS, input.clip.x);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = tex.a * input.color.a;
                clip(alpha - _RoSpriteDepthCutoff);
                clip(input.color.a - 0.999);
                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "ForwardEnhanced"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            // one depth unit nearer than the depth pass (Offset -1, -1): this pass recomputes that pass's depth in another
            // program and tests LEqual against the pixel it wrote, which a last-bit difference lost from frame to frame
            // while the camera moved (actors flickering see-through) - exactly one LSB at every distance
            Offset -1, [_RoSpriteOffsetUnits]
            Cull [_Cull]

            Stencil {
                Ref 32
                Comp Always
                Pass [_RoSpriteStencilPass]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoFog.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoCloudShadows.hlsl"
            #include "Assets/Ragnarok/Runtime/Rendering/Shaders/RoLookLighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Cull;
                float _ZWrite;
                float _ZTest;
                float _SrcBlend;
                float _DstBlend;
                float _VPos;
                float _RoShadowCasterYOffset;
                float _RoShadowCasterPullDistance;
                float _RoShadowCasterGroundClearance;
                float _RoShadowCasterLiftPullScale;
                float _RoShadowCasterBottomLift;
                float _DepthCorrection;
                float _RoSpriteDepthBias;
                float4 _RoMapLightMultiply;
                float4 _RoMapLightAdd;
                float _RoDirectionalLightInfluence;
                float _RoEmissionLightInfluence;
                float _RoUseAdditionalLights;
                float _RoReceiveRealtimeShadows;
                float _RoRealtimeShadowStrength;
                float _RoShadowReceiveLightOffset;
                float _RoShadowReceiveHeight;
                float _RoUseSharedActorShadowCard;
                float4 _RoActorShadowCardCenterOS;
                float _RoActorShadowCardBottomLift;
                float _RoSpriteDepthCutoff;
                float4 _RoSpriteFeetOS;
                float _RoSpriteRank;
                float _RoSpriteSinglePass;
                float _RoSpriteOffsetUnits;
            CBUFFER_END

            float _RoSpriteLegacyDepth;
            float _RoSpriteCurvedDepth;
            // the colour pass's pull toward the camera over its own depth pass, in view units (global, !toggle pull): the two
            // passes compute the lean depth in separate programs and a 32-bit depth step (~4e-6) is far below their difference.
            // Rule: the colour pass never relies on recomputing the same depth as the depth pass - it always keeps an explicit
            // margin smaller than one layer rank step (0.003 < the 0.01 of _RoSpriteRank); whoever changes the rank step or
            // the margin changes both (RoActorLayerRankTests guards it)
            float _RoSpriteColorDepthPull;

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                // x: the clip's index in its ACT frame (RoSpriteMeshBuilder); 0 for meshes without it
                float2 clip : TEXCOORD1;
            };

            // the clips of one ACT frame share a plane, so their pixels tied in depth and the last bit of each quad's
            // interpolation picked the winner as the camera moved (a cart's wheel flickering through its box); the client
            // keys clip i nearer by (3 + i)·9e-6 rhw (draw-order §3), here i·RoSpriteClipStep view units, under one rank step
            #define RoSpriteClipStep 0.002

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float fogDepth : TEXCOORD1;
                float3 shadowSampleWS : TEXCOORD2;
                // world units right of the actor's feet axis, seen from the camera (the sprite's body cylinder)
                float sideOffset : TEXCOORD3;
                // where the sprite pixel really is (the lamps light it there, not at the shadow card)
                float3 positionWS : TEXCOORD4;
            };

            float3 ResolveForwardShadowPullDirectionWS() {
                float3 lightHorizontal = float3(_MainLightPosition.x, 0.0, _MainLightPosition.z);
                if (dot(lightHorizontal, lightHorizontal) <= 0.000001) {
                    return float3(0.0, 0.0, 0.0);
                }

                return normalize(lightHorizontal);
            }

            float ResolveForwardShadowLiftPullDistance(float verticalLift) {
                if (verticalLift <= 0.0) {
                    return 0.0;
                }

                float horizontal = length(float2(_MainLightPosition.x, _MainLightPosition.z));
                float vertical = max(abs(_MainLightPosition.y), 0.2);
                return verticalLift * (horizontal / vertical) * saturate(_RoShadowCasterLiftPullScale);
            }

            float3 ResolveForwardShadowRightWS(float3 pullDirectionWS) {
                if (dot(pullDirectionWS, pullDirectionWS) <= 0.000001) {
                    return float3(1.0, 0.0, 0.0);
                }

                return normalize(cross(float3(0.0, 1.0, 0.0), pullDirectionWS));
            }

            float3 ResolveActorShadowCardPositionWS(float3 positionOS) {
                bool useSharedCard = _RoUseSharedActorShadowCard > 0.5;
                float3 cardCenterOS = useSharedCard ? _RoActorShadowCardCenterOS.xyz : float3(0.0, 0.0, 0.0);
                float3 cardDeltaOS = useSharedCard ? positionOS - cardCenterOS : positionOS;
                float3 objectCenterWS = TransformObjectToWorld(cardCenterOS);
                float scaleX = max(length(TransformObjectToWorld(cardCenterOS + float3(1.0, 0.0, 0.0)) - objectCenterWS), 0.0001);
                float scaleY = max(length(TransformObjectToWorld(cardCenterOS + float3(0.0, 1.0, 0.0)) - objectCenterWS), 0.0001);
                float3 centerWS = objectCenterWS;
                centerWS.y -= max(_RoShadowCasterYOffset, 0.0);
                float bottomLift = useSharedCard ? _RoActorShadowCardBottomLift : _RoShadowCasterBottomLift;
                float verticalLift = max(_RoShadowCasterGroundClearance, 0.0) + max(bottomLift, 0.0) * scaleY;
                centerWS.y += verticalLift;
                float pullDistance = max(
                    max(_RoShadowCasterPullDistance, 0.0),
                    ResolveForwardShadowLiftPullDistance(verticalLift));
                float3 pullDirectionWS = ResolveForwardShadowPullDirectionWS();
                centerWS += pullDirectionWS * pullDistance;
                return
                    centerWS +
                    ResolveForwardShadowRightWS(pullDirectionWS) * (cardDeltaOS.x * scaleX) +
                    float3(0.0, 1.0, 0.0) * (cardDeltaOS.y * scaleY);
            }

            float4 ResolveSpritePosition(float3 positionOS) {
                float4 positionHCS = TransformObjectToHClip(positionOS);
                if (_DepthCorrection <= 0.5) {
                    return positionHCS;
                }

                float3 worldVertex = TransformObjectToWorld(positionOS);
                float3 worldCenter = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 rayDirection = normalize(worldVertex - _WorldSpaceCameraPos.xyz);
                float3 planeNormal = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0));
                float3 flatPlaneNormal = float3(planeNormal.x, 0.0, planeNormal.z);
                planeNormal = dot(flatPlaneNormal, flatPlaneNormal) < 0.000001
                    ? normalize(planeNormal)
                    : normalize(flatPlaneNormal);

                float denominator = dot(planeNormal, rayDirection);
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = dot(worldCenter - _WorldSpaceCameraPos.xyz, planeNormal) / denominator;
                float3 correctedWorld = _WorldSpaceCameraPos.xyz + rayDirection * distance;
                float4 correctedHCS = TransformWorldToHClip(correctedWorld);
                float correctedZ = correctedHCS.z * (positionHCS.w / max(correctedHCS.w, 0.000001));

#if UNITY_REVERSED_Z
                positionHCS.z = max(positionHCS.z, correctedZ);
#else
                positionHCS.z = min(positionHCS.z, correctedZ);
#endif
                return positionHCS;
            }

            // pulls the depth a fixed world distance (_RoSpriteDepthBias, in cells) toward the camera: an NDC offset grew
            // with the square of the camera distance and let actors behind walls draw over them once the camera moved away
            float4 ApplyRoSpriteDepthBias(float4 positionHCS) {
                float bias = max(_RoSpriteDepthBias, 0.0);
                if (bias <= 0.0 || UNITY_MATRIX_P[3][3] != 0.0 || positionHCS.w <= 0.0) {
                    return positionHCS;
                }

                // perspective: z_ndc = P23 / d - P22 at view distance d (clip w), whatever the depth direction
                float p22 = UNITY_MATRIX_P[2][2];
                float p23 = UNITY_MATRIX_P[2][3];
                float zNdc = positionHCS.z / positionHCS.w;
                float denominator = zNdc + p22;
                if (abs(denominator) <= 0.000001) {
                    return positionHCS;
                }

                float distance = p23 / denominator;
                float biasedDistance = max(distance - bias, _ProjectionParams.y);
                positionHCS.z = (p23 / biasedDistance - p22) * positionHCS.w;
                return positionHCS;
            }

            // the client's sprite depth (vf09 0xbce6f0): rhw = 1 / (vz + 0.16 * dy), vz the actor's view depth and dy the
            // pixel offset from the actor's screen point (y down), so the quad is a plane leaning towards the camera, each
            // pixel higher 0.16 RO units (0.032 Unity) nearer; the effects' TL vertices are in the same space
            // (reference packages/sprite-effect-depth). _RoSpriteLegacyDepth (global, !toggle lean off) = the old plane
            float4 ResolveClientLeanPosition(float3 positionOS, float clipIndex) {
                // precise: the depth pass and the colour pass run this in two programs and must get the same bits (the colour
                // pass tests LEqual against the depth pass's pixel; reordered or fused maths lost rows of a sprite, striped)
                precise float4 positionHCS = TransformObjectToHClip(positionOS);
                precise float4 feetHCS = TransformObjectToHClip(_RoSpriteFeetOS.xyz);
                if (positionHCS.w <= 0.0 || feetHCS.w <= 0.0) {
                    return positionHCS;
                }

                // the client's sprite plane (vf09 0xbce6f0): depth falls 0.16 tan(-lat) / (H / 816) RO units per pixel above
                // the ground point; with its camera (XFactor (H / 2) / tan 7.5°, distance 300) that is 1.65 tan(-lat) view
                // units per world unit of height, which is what is kept here so the plane does not depend on our zoom.
                // Only the depth changes: x, y and w stay the vertex's, so the texture interpolation is not warped
                precise float upPixels = (positionHCS.y / positionHCS.w - feetHCS.y / feetHCS.w) * 0.5 * _ScreenParams.y;
                precise float3 forwardWS = -UNITY_MATRIX_V[2].xyz;
                precise float pitch = clamp(asin(saturate(-forwardWS.y)), 0.05, 1.4);
                precise float focalPixels = UNITY_MATRIX_P[1][1] * _ScreenParams.y * 0.5;
                precise float upWorld = upPixels * feetHCS.w / focalPixels;
                precise float depth = feetHCS.w - 1.65 * tan(pitch) * upWorld;
                // the client keys every layer of a player from one line through the body rect: rhw linear in screen y
                // (draw-order §1.5: rhwTop = top·a + b, rhwBottom = bottom·a + b, z linear in rhw), so all layers of an actor
                // get the same depth at the same pixel and only the rank n orders them. A depth recomputed per vertex in view
                // units and interpolated by the GPU differed per quad size (a tall body quad vs small wings: tenths of a unit,
                // far over the 0.01 rank step) and the below-feet ground clamp pulled a whole quad forward when its bottom
                // corners were below the feet; the robe drew behind a cloak the client puts it on. The plane here is the
                // tangent at the feet: z_ndc linear in screen y, exact under interpolation. _RoSpriteCurvedDepth = the old one
                if (_RoSpriteCurvedDepth < 0.5) {
                    precise float feetDepth = max(feetHCS.w - 1.6 - _RoSpriteRank * 0.01 - clipIndex * RoSpriteClipStep - _RoSpriteColorDepthPull * (1.0 - _RoSpriteSinglePass), _ProjectionParams.y);
                    precise float zFeet = (UNITY_MATRIX_P[2][2] * -feetDepth + UNITY_MATRIX_P[2][3]) / feetDepth;
                    precise float zSlope = -UNITY_MATRIX_P[2][3] / (feetDepth * feetDepth);
                    precise float zLinear = zFeet + zSlope * (-1.65 * tan(pitch) * upWorld);
                    return float4(positionHCS.xy, zLinear * positionHCS.w, positionHCS.w);
                }

                // rows down to 96 px below the ground point stay just in front of the ground at their pixel (feet, an
                // NPC's ground circle; a quad corner decides its whole edge); lower rows lean on and the ground clips them
                if (upPixels < 0.0 && upPixels > -96.0) {
                    precise float3 vertexWS = TransformObjectToWorld(positionOS);
                    precise float3 feetWS = TransformObjectToWorld(_RoSpriteFeetOS.xyz);
                    precise float3 rayWS = vertexWS - _WorldSpaceCameraPos.xyz;
                    if (rayWS.y < -0.0001) {
                        precise float3 groundWS = _WorldSpaceCameraPos.xyz + rayWS * ((feetWS.y - _WorldSpaceCameraPos.y) / rayWS.y);
                        precise float groundDepth = -mul(UNITY_MATRIX_V, float4(groundWS, 1.0)).z;
                        depth = min(depth, groundDepth - 0.05);
                    }
                }

                // the layer rank n (the client's n·1e-6) as a fixed view distance, so zooming in does not merge the layers
                // the client's rhw terms ((3 + clip) K9, the K36 scale term) put a sprite about 8 RO units (1.6 view units)
                // in front of the ground under it at its default camera (npc-ground-circle §3): the feet stand on an aura disc
                // 1 unit above the ground instead of under it
                depth = max(depth - 1.6 - _RoSpriteRank * 0.01 - clipIndex * RoSpriteClipStep, _ProjectionParams.y);
                // one pass: no pull, the depth written is exactly the depth pass's
                depth = max(depth - _RoSpriteColorDepthPull * (1.0 - _RoSpriteSinglePass), _ProjectionParams.y);
                precise float zNdc = (UNITY_MATRIX_P[2][2] * -depth + UNITY_MATRIX_P[2][3]) / depth;
                return float4(positionHCS.xy, zNdc * positionHCS.w, positionHCS.w);
            }

            float4 ResolveSpriteDepthPosition(float3 positionOS, float clipIndex) {
                if (_RoSpriteLegacyDepth < 0.5 && UNITY_MATRIX_P[3][3] == 0.0) {
                    return ResolveClientLeanPosition(positionOS, clipIndex);
                }

                return ApplyRoSpriteDepthBias(ResolveSpritePosition(positionOS));
            }

            Varyings vert(Attributes input) {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += _VPos;
                output.positionHCS = ResolveSpriteDepthPosition(positionOS, input.clip.x);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                // ACT layer colours are gamma bytes in the vertex stream (never converted), _BaseColor is already linear
                half4 vertexColor = input.color;
#if !defined(UNITY_COLORSPACE_GAMMA)
                vertexColor.rgb = SRGBToLinear(vertexColor.rgb);
#endif
                output.color = vertexColor * _BaseColor;
                float3 feetWS = TransformObjectToWorld(_RoSpriteFeetOS.xyz);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.sideOffset = dot(output.positionWS - feetWS, UNITY_MATRIX_V[0].xyz);
                output.fogDepth = RoFogDepth(output.positionHCS);
                // CARD-ALIGNED RECEIVE: sample the realtime shadow at the same stable card the ShadowCaster
                // pass casts from. Layered actors can opt into a shared actor card so body/head/equipment all
                // compare against one coplanar caster instead of shadowing each other.
                output.shadowSampleWS = ResolveActorShadowCardPositionWS(positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 color = tex * input.color;
                // one pass writes depth: the depth pass's 0x9f cut (the layer's texels are 0 or 255, so no colour is lost)
                clip(color.a - (_RoSpriteSinglePass > 0.5 ? _RoSpriteDepthCutoff : _Cutoff));
                half3 albedo = color.rgb;
                half3 normalWS = RoLookSpriteNormal(_BaseMap, sampler_BaseMap, input.uv, input.sideOffset);

                // Sample at the cast-card position computed in the vertex stage (so the standard URP shadow
                // bias already rejects most of the actor's own coincident caster), plus a SMALL uniform nudge
                // toward the light to clear any residual self-shadow at the top of the card (head).
                float3 cardShadowBiasWS = normalize(_MainLightPosition.xyz) * _RoShadowReceiveLightOffset;
                float4 shadowCoord = TransformWorldToShadowCoord(input.shadowSampleWS + cardShadowBiasWS);
                Light mainLight = GetMainLight(shadowCoord);
                half cloudShadow = RoCloudShadowAttenuation(input.shadowSampleWS.xz, mainLight.shadowAttenuation, (half)_RoCloudShadowActorStrength);
                half realtimeShadow = _RoReceiveRealtimeShadows > 0.5 ? mainLight.shadowAttenuation : 1.0h;

                // a card is never fully away from the sun: part of the sun lights it flat (_RoLookSprite.x), the rest
                // through the body normal (the lit side and the rim)
                half sunTerm = lerp(RoLookSunTerm(normalWS, mainLight.direction), 1.0h, saturate((half)_RoLookSprite.x));
                half3 sun = RoLookSunColor(mainLight) * sunTerm * realtimeShadow * cloudShadow;
                half3 ambient = RoLookAmbient(normalWS) * (half)_RoLookSprite.y * lerp(1.0h, (half)_RoLookIndoor.y, saturate((half)_RoLookIndoor.x));

                RoLookSurface surface;
                surface.albedo = albedo;
                surface.normalWS = normalWS;
                surface.positionWS = input.positionWS;
                surface.screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                // a wide wrap: a flat card facing the camera still takes a lamp beside it, brightest on the side facing it
                half3 lamps = RoLookLamps(surface, 0.75h) * (half)_RoLookSprite.z * 1.6h;
                color.rgb = albedo * (ambient + sun + lamps);

                color.rgb = RoApplyFog(color.rgb, input.fogDepth);
                return color;
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _Cull;
                float _ZWrite;
                float _ZTest;
                float _SrcBlend;
                float _DstBlend;
                float _VPos;
                float _RoShadowCasterYOffset;
                float _RoShadowCasterPullDistance;
                float _RoShadowCasterGroundClearance;
                float _RoShadowCasterLiftPullScale;
                float _RoShadowCasterBottomLift;
                float _DepthCorrection;
                float _RoSpriteDepthBias;
                float4 _RoMapLightMultiply;
                float4 _RoMapLightAdd;
                float _RoDirectionalLightInfluence;
                float _RoEmissionLightInfluence;
                float _RoUseAdditionalLights;
                float _RoReceiveRealtimeShadows;
                float _RoRealtimeShadowStrength;
                float _RoShadowReceiveLightOffset;
                float _RoShadowReceiveHeight;
                float _RoUseSharedActorShadowCard;
                float4 _RoActorShadowCardCenterOS;
                float _RoActorShadowCardBottomLift;
                float _RoSpriteDepthCutoff;
                float4 _RoSpriteFeetOS;
                float _RoSpriteRank;
                float _RoSpriteSinglePass;
                float _RoSpriteOffsetUnits;
            CBUFFER_END

            float _RoSpriteLegacyDepth;
            float _RoSpriteCurvedDepth;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float3 ResolveStableShadowRightWS(float3 centerWS) {
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                // a lamp / torch sees the card face on: the card turns toward it (a fixed world-x card was seen edge-on
                // from a torch beside the actor, and the shadow was a thin line)
                float3 toLight = float3(_LightPosition.x - centerWS.x, 0.0, _LightPosition.z - centerWS.z);
                if (dot(toLight, toLight) <= 0.000001) {
                    return float3(1.0, 0.0, 0.0);
                }

                return normalize(cross(float3(0.0, 1.0, 0.0), normalize(toLight)));
#else
                float3 lightHorizontal = float3(_LightDirection.x, 0.0, _LightDirection.z);
                if (dot(lightHorizontal, lightHorizontal) <= 0.000001) {
                    return float3(1.0, 0.0, 0.0);
                }

                return normalize(cross(float3(0.0, 1.0, 0.0), normalize(lightHorizontal)));
#endif
            }

            float3 ResolveStableShadowNormalWS(float3 positionWS) {
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightHorizontal = float3(_LightPosition.x - positionWS.x, 0.0, _LightPosition.z - positionWS.z);
#else
                float3 lightHorizontal = float3(_LightDirection.x, 0.0, _LightDirection.z);
#endif
                if (dot(lightHorizontal, lightHorizontal) <= 0.000001) {
                    return float3(0.0, 0.0, -1.0);
                }

                return normalize(lightHorizontal);
            }

            float3 ResolveShadowPullDirectionWS(float3 centerWS) {
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightHorizontal = float3(_LightPosition.x - centerWS.x, 0.0, _LightPosition.z - centerWS.z);
#else
                float3 lightHorizontal = float3(_LightDirection.x, 0.0, _LightDirection.z);
#endif
                if (dot(lightHorizontal, lightHorizontal) <= 0.000001) {
                    return float3(0.0, 0.0, 0.0);
                }

                return normalize(lightHorizontal);
            }

            float ResolveShadowLiftPullDistance(float3 centerWS, float verticalLift) {
                if (verticalLift <= 0.0) {
                    return 0.0;
                }

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightVector = _LightPosition - centerWS;
#else
                float3 lightVector = _LightDirection;
#endif
                float horizontal = length(float2(lightVector.x, lightVector.z));
                float vertical = max(abs(lightVector.y), 0.2);
                return verticalLift * (horizontal / vertical) * saturate(_RoShadowCasterLiftPullScale);
            }

            float4 GetShadowPositionHClip(Attributes input) {
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += _VPos;

                // The visible actor sprite billboards to the camera. Rebuild only the shadow
                // caster on a stable world-space card so the cast shadow does not rotate with view yaw.
                bool useSharedCard = _RoUseSharedActorShadowCard > 0.5;
                float3 cardCenterOS = useSharedCard ? _RoActorShadowCardCenterOS.xyz : float3(0.0, 0.0, 0.0);
                float3 cardDeltaOS = useSharedCard ? positionOS - cardCenterOS : positionOS;
                float3 objectCenterWS = TransformObjectToWorld(cardCenterOS);
                float scaleX = max(length(TransformObjectToWorld(cardCenterOS + float3(1.0, 0.0, 0.0)) - objectCenterWS), 0.0001);
                float scaleY = max(length(TransformObjectToWorld(cardCenterOS + float3(0.0, 1.0, 0.0)) - objectCenterWS), 0.0001);
                float3 centerWS = objectCenterWS;
                centerWS.y -= max(_RoShadowCasterYOffset, 0.0);
                float bottomLift = useSharedCard ? _RoActorShadowCardBottomLift : _RoShadowCasterBottomLift;
                float verticalLift = max(_RoShadowCasterGroundClearance, 0.0) + max(bottomLift, 0.0) * scaleY;
                centerWS.y += verticalLift;
                float pullDistance = max(
                    max(_RoShadowCasterPullDistance, 0.0),
                    ResolveShadowLiftPullDistance(centerWS, verticalLift));
                centerWS += ResolveShadowPullDirectionWS(centerWS) *
                    pullDistance;
                float3 positionWS =
                    centerWS +
                    ResolveStableShadowRightWS(objectCenterWS) * (cardDeltaOS.x * scaleX) +
                    float3(0.0, 1.0, 0.0) * (cardDeltaOS.y * scaleY);
                float3 normalWS = ResolveStableShadowNormalWS(positionWS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                return ApplyShadowClamping(positionCS);
            }

            Varyings ShadowPassVertex(Attributes input) {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * input.color.a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
