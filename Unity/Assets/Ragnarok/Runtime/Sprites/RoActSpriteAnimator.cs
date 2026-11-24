using System;
using System.Collections.Generic;
using Ragnarok.Diagnostics;
using ROIO.Models.FileTypes;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Sprites {
    [ExecuteAlways]
    public sealed class RoActSpriteAnimator : MonoBehaviour, IRoActorUpdateTick, IRoActorTickBudget, IRoActorTickVisibility {
        public const CompareFunction SpriteDepthTest = CompareFunction.LessEqual;
        private const float OffscreenAnimationTickIntervalSeconds = 0.25f;
        private const float DistantAnimationTickIntervalSeconds = 1f / 30f;
        private const float CrowdNearAnimationTickIntervalSeconds = 1f / 20f;
        private const float CrowdMidAnimationTickIntervalSeconds = 1f / 15f;
        private const float CrowdFarAnimationTickIntervalSeconds = 0.125f;
        private const int CrowdAnimatorThreshold = 32;
        private const int HeavyCrowdAnimatorThreshold = 72;
        private const float CrowdFullRateDistance = 8f;
        private const float DistantAnimationDistance = 34f;
        private const int MaxGeometryPrewarmFramesPerAnimator = 16;
        // the walk frame of game.exe (CGameActor slot 36, 0x00bd4a20): (int)(walkDist * 0.37 * 4 / fd) % frames, walkDist in
        // RO world units (a cell is 5) and fd the walk action's delay in 24 ms units, at least one
        public const float RagnarokWalkUnitsPerCell = 5f;
        public const float RagnarokWalkDistancePhaseScale = 0.37f * 4f;
        /// <summary>One ACT delay unit (ms): the client's motion clock divides by 24.</summary>
        public const float RagnarokActDelayUnitMilliseconds = 24f;
        /// <summary>World distance (cells) the sprite depth is pulled toward the camera (RO Sprite shaders).</summary>
        public const float SpriteDepthBias = 0.4f;

        [SerializeField] private ACT act;
        /// <summary>The ACT this animator plays (action delays, frame events): read by the combat rules.</summary>
        public ACT Act => act;
        [SerializeField] private int actionIndex;
        [SerializeField] private int frameIndex;
        [SerializeField] private bool loop = true;
        [SerializeField] private RoActFramePlaybackMode framePlaybackMode = RoActFramePlaybackMode.Animated;
        [SerializeField] private int fixedFrameIndex;
        [SerializeField] private float frameDelayScale = 1f;
        [SerializeField] private int sortingOrder;
        [SerializeField] private bool useFrameComposition;
        [SerializeField] private RoShadowRenderMode shadowRenderMode = RoShadowRenderMode.Original;
        [SerializeField, Range(0f, 1f)] private float realtimeShadowStrength = RoEnhancedShadowSettings.DefaultActorRealtimeShadowStrength;
        private bool actorReceivesRealtimeShadows;
        private bool actorUsesAdditionalLights;
        private bool castsRealtimeShadow = true;
        [SerializeField, Min(0f)] private float realtimeShadowCasterPullDistance = RoEnhancedShadowSettings.DefaultActorShadowCasterPullDistance;
        [SerializeField, Min(0f)] private float realtimeShadowCasterGroundClearance = RoEnhancedShadowSettings.DefaultActorShadowCasterGroundClearance;
        [SerializeField, Range(0f, 2f)] private float realtimeShadowCasterLiftPullScale = RoEnhancedShadowSettings.DefaultActorShadowCasterLiftPullScale;
        [SerializeField, Range(0f, 1f)] private float directionalLightInfluence = RoEnhancedShadowSettings.DefaultActorDirectionalLightInfluence;
        [SerializeField, Range(0f, 2f)] private float emissionLightInfluence = RoEnhancedShadowSettings.DefaultMapEmissionLightInfluence;
        [SerializeField, Min(0f)] private float realtimeShadowCasterYOffset;
        [SerializeField, Min(0f)] private float realtimeShadowCasterBottomLift;
        private float spriteDepthCutoff = SpriteDepthWriteCutoff;
        private Vector3 spriteFeetOS;
        [SerializeField] private bool useSharedActorShadowCard;
        [SerializeField] private Vector3 sharedActorShadowCardCenterOS;
        [SerializeField, Min(0f)] private float sharedActorShadowCardBottomLift;

        private readonly Dictionary<int, CompositedFrame> compositedFrameCache = new Dictionary<int, CompositedFrame>();
        private readonly Dictionary<int, CompositedFrameLayout> compositedActionLayoutCache = new Dictionary<int, CompositedFrameLayout>();
        /// <summary>The action and frame the renderer shows (-1 = nothing applied or an apply pending).</summary>
        private int appliedActionIndex = -1, appliedFrameIndex = -1;
        private readonly Dictionary<RoSpriteFrameGeometryKey, Mesh> ownedUncachedFrameMeshes = new Dictionary<RoSpriteFrameGeometryKey, Mesh>();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Material material;
        private RoSpriteMaterialLease materialLease;
        /// <summary>
        /// The renderer's own copy of <see cref="material"/> holding this actor's values (tint, map light, shadow card ...).
        /// A MaterialPropertyBlock takes a renderer off URP's SRP Batcher: every actor layer then set up its own draw state
        /// (~2.6 SetPass calls per actor); per-renderer materials of the one sprite shader are batched.
        /// </summary>
        private Material rendererMaterial;
        /// <summary>The base material <see cref="rendererMaterial"/> was copied from.</summary>
        private Material rendererMaterialSource;
        private RoSpriteAtlas atlas;
        private SPR sourceSprite;
        private Texture lastAppliedTexture;
        /// <summary>The material <see cref="lastAppliedTexture"/> went into: the same pair skips the per-property checks.</summary>
        private Material lastAppliedTextureMaterial;
        private Mesh lastAppliedMesh;
        private Mesh mutableRuntimeMesh;
        private Mesh lastAppliedMutableSourceMesh;
        private int geometryCacheId;
        private int renderRevision;
        private float actionStartTime;
        private float nextBudgetedTickTime;
        private float verticalClipOffset;
        private float opacity = 1f;
        private Color tintColor = Color.white;
        /// <summary>The classic engine's actor colour (+0x98, SetARGB by effects: Energy Coat, Overthrust, Quicken ...).</summary>
        private Color effectTint = Color.white;
        private Color mapLightMultiply = Color.white;
        private Color mapLightAdd = Color.clear;
        private bool ownsMaterial;
        private bool ownsAtlas;
        private bool frameApplyPending;
        private bool budgetRuntimeMeshBuilds;
        private bool budgetVisibleAnimationTicks;
        private bool useMutableRuntimeMesh;
        private bool playFrameSounds;
        private int lastSoundAction = -1;
        private int lastSoundFrame;
        private bool hasLastRendererEnabled;
        private bool lastRendererEnabled;
        private bool hasAppliedSortingOrder;
        private int appliedSortingOrder;
        private bool hasAppliedRendererShadowState;
        private RoShadowRenderMode appliedRendererShadowRenderMode;
        private int textureAssignmentCount;
        private int meshAssignmentCount;
        private static readonly List<Vector3> mutableVertices = new List<Vector3>(32);
        private static readonly List<Vector3> mutableNormals = new List<Vector3>(32);
        private static readonly List<Vector2> mutableUvs = new List<Vector2>(32);
        private static readonly List<Vector2> mutableClips = new List<Vector2>(32);
        private static readonly List<Color> mutableColors = new List<Color>(32);
        private static readonly List<int> mutableTriangles = new List<int>(48);

        private static readonly int VPosId = Shader.PropertyToID("_VPos");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MapLightMultiplyId = Shader.PropertyToID("_RoMapLightMultiply");
        private static readonly int MapLightAddId = Shader.PropertyToID("_RoMapLightAdd");
        private static readonly int DirectionalLightInfluenceId = Shader.PropertyToID("_RoDirectionalLightInfluence");
        private static readonly int EmissionLightInfluenceId = Shader.PropertyToID("_RoEmissionLightInfluence");
        private static readonly int ReceiveRealtimeShadowsId = Shader.PropertyToID("_RoReceiveRealtimeShadows");
        private static readonly int RealtimeShadowStrengthId = Shader.PropertyToID("_RoRealtimeShadowStrength");
        private static readonly int UseAdditionalLightsId = Shader.PropertyToID("_RoUseAdditionalLights");
        private static readonly int ShadowCasterYOffsetId = Shader.PropertyToID("_RoShadowCasterYOffset");
        private static readonly int ShadowCasterPullDistanceId = Shader.PropertyToID("_RoShadowCasterPullDistance");
        private static readonly int ShadowCasterGroundClearanceId = Shader.PropertyToID("_RoShadowCasterGroundClearance");
        private static readonly int ShadowCasterLiftPullScaleId = Shader.PropertyToID("_RoShadowCasterLiftPullScale");
        private static readonly int ShadowCasterBottomLiftId = Shader.PropertyToID("_RoShadowCasterBottomLift");
        private static readonly int SpriteDepthCutoffId = Shader.PropertyToID("_RoSpriteDepthCutoff");
        private static readonly int SpriteFeetId = Shader.PropertyToID("_RoSpriteFeetOS");
        private static readonly int SpriteZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SpriteRankId = Shader.PropertyToID("_RoSpriteRank");
        private int spriteLayerRank;
        private static readonly int SpriteStencilPassId = Shader.PropertyToID("_RoSpriteStencilPass");
        private static readonly int SpriteSinglePassId = Shader.PropertyToID("_RoSpriteSinglePass");
        private static readonly int SpriteOffsetUnitsId = Shader.PropertyToID("_RoSpriteOffsetUnits");
        const int DepthModeUnset = -1, DepthModeNone = 0, DepthModeTwoPass = 1, DepthModeSinglePass = 2;
        /// <summary>Off = every depth-writing layer keeps the two passes (A/B: !toggle singlepass).</summary>
        public static bool SinglePassDepth = true;
        private int appliedDepthMode = DepthModeUnset;
        private bool frameClipsOpaque;
        private bool frameComposited;
        private float currentBaseAlpha = 1f;

        /// <summary>
        /// The depth prepass cut of a layer the client draws in its opaque sprite list 0x180 (pass 1: alpha test 0x9f,
        /// depth write): texel alpha × vertex alpha. Layers the client puts in the translucent list (headgear, robe slot 8,
        /// a translucent actor) write no depth (reference packages/sprite-effect-depth).
        /// </summary>
        public const float SpriteDepthWriteCutoff = 0x9f / 255f;
        const float NoSpriteDepthCutoff = 2f;
        private static readonly int UseSharedActorShadowCardId = Shader.PropertyToID("_RoUseSharedActorShadowCard");
        private static readonly int ActorShadowCardCenterOSId = Shader.PropertyToID("_RoActorShadowCardCenterOS");
        private const float SharedShadowCardCenterEpsilonSqr = 1e-6f;
        private static readonly int ActorShadowCardBottomLiftId = Shader.PropertyToID("_RoActorShadowCardBottomLift");
        private static readonly ProfilerMarker ApplyFrameMarker = new ProfilerMarker("SpriteAnimator.ApplyFrame");
        private static readonly ProfilerMarker MeshAssignMarker = new ProfilerMarker("SpriteAnimator.MeshAssign");
        private static readonly ProfilerMarker TextureAssignMarker = new ProfilerMarker("SpriteAnimator.TextureAssign");
        private static readonly ProfilerMarker MutableMeshUpdateMarker = new ProfilerMarker("SpriteAnimator.MutableMeshUpdate");
        private static readonly ProfilerMarker PrebuildAllMeshesMarker = new ProfilerMarker("SpriteAnimator.PrebuildAllMeshes");
        private static UnityEngine.Camera cachedBudgetCamera;
        private static float nextBudgetCameraResolveTime;

        /// <summary>
        /// Raised when a body sprite advances to a new frame that carries an ACT sound (attack swing,
        /// footstep, ...). Args: (wav file or path, world position, volume). The Sprites assembly cannot
        /// reference the audio layer, so a Runtime-side listener forwards this to the audio system.
        /// </summary>
        public static event System.Action<string, Vector3, float> FrameSoundRequested;
        private readonly Dictionary<int, Bounds> stableActionLocalBoundsCache = new Dictionary<int, Bounds>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedFrameMeshCache() {
            RoSpriteFrameGeometryCache.Clear();
            cachedBudgetCamera = null;
            nextBudgetCameraResolveTime = 0f;
        }

        public int ActionIndex => actionIndex;
        public int FrameIndex => frameIndex;

        /// <summary>Diagnostics: whether this layer draws anything now (renderer on, mesh with vertices, atlas loaded).</summary>
        public string DescribeDrawState() {
            var filter = meshRenderer != null ? meshRenderer.GetComponent<MeshFilter>() : null;
            int vertices = filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : -1;
            return "renderer=" + (meshRenderer == null ? "none" : meshRenderer.enabled + "/" + meshRenderer.gameObject.activeInHierarchy) +
                " verts=" + vertices + " atlas=" + (atlas != null) + " act=" + (act != null) + " action=" + actionIndex + " frame=" + frameIndex +
                " mode=" + framePlaybackMode + " pending=" + frameApplyPending + " hint=" + RendererEnabledHint +
                " order=" + (meshRenderer != null ? meshRenderer.sortingOrder.ToString() : "-") + " ext=" + SortingOrderSetExternally +
                (meshRenderer != null && meshRenderer.sharedMaterial != null && meshRenderer.sharedMaterial.HasProperty("_RoSpriteRank")
                    ? " rank=" + meshRenderer.sharedMaterial.GetFloat("_RoSpriteRank").ToString("F0") +
                      " queue=" + meshRenderer.sharedMaterial.renderQueue +
                      " depthWrite=" + (meshRenderer.sharedMaterial.HasProperty("_RoSpriteDepthWrite") ? meshRenderer.sharedMaterial.GetFloat("_RoSpriteDepthWrite").ToString("F0") : "?")
                    : "");
        }
        public int RenderRevision => renderRevision;
        public int ActionCount => act?.actions?.Length ?? 0;
        public RoActFramePlaybackMode FramePlaybackMode => framePlaybackMode;
        public int FixedFrameIndex => fixedFrameIndex;
        public float FrameDelayScale => frameDelayScale;
        public float VerticalClipOffset => verticalClipOffset;
        public float Opacity => opacity;

        /// <summary>The layer's renderer (null before the first frame is built).</summary>
        public MeshRenderer LayerRenderer => meshRenderer;

        /// <summary>
        /// Whether the layer renderer is on, from the state this animator last gave it (SetRendererEnabled is the only
        /// writer of an actor layer renderer): no native read for the client draw order, which asks every layer every frame.
        /// </summary>
        public bool RendererEnabledHint => (object) meshRenderer != null && (hasLastRendererEnabled ? lastRendererEnabled : meshRenderer.enabled);

        /// <summary>The vertex alpha the layer is drawn with: the status / effect colour's alpha times the opacity.</summary>
        public float DrawAlpha => (tintColor != Color.white ? tintColor.a : effectTint.a) * opacity;
        public Color TintColor => tintColor;
        public Color MapLightMultiply => mapLightMultiply;
        public Color MapLightAdd => mapLightAdd;
        public int SortingOrder => sortingOrder;
        public bool UseFrameComposition => useFrameComposition;
        public RoShadowRenderMode ShadowRenderMode => shadowRenderMode;
        public float RealtimeShadowCasterYOffset => realtimeShadowCasterYOffset;
        public float RealtimeShadowCasterPullDistance => realtimeShadowCasterPullDistance;
        public float RealtimeShadowCasterGroundClearance => realtimeShadowCasterGroundClearance;
        public float RealtimeShadowCasterLiftPullScale => realtimeShadowCasterLiftPullScale;
        public float RealtimeShadowCasterBottomLift => realtimeShadowCasterBottomLift;
        public bool UseSharedActorShadowCard => useSharedActorShadowCard;
        public Vector3 SharedActorShadowCardCenterOS => sharedActorShadowCardCenterOS;
        public float SharedActorShadowCardBottomLift => sharedActorShadowCardBottomLift;

        public void Initialize(SPR spr, ACT act, int initialActionIndex = 0, Material spriteMaterial = null, byte[] paletteOverride = null) {
            if (spr == null) {
                throw new ArgumentNullException(nameof(spr));
            }

            if (act == null) {
                throw new ArgumentNullException(nameof(act));
            }

            if (act.actions == null || act.actions.Length == 0) {
                throw new ArgumentException("ACT must contain at least one action.", nameof(act));
            }

            InitializeWithAtlas(
                spr,
                act,
                RoSpriteAtlasBuilder.Build(spr, paletteOverride),
                true,
                initialActionIndex,
                spriteMaterial);
        }

        public void Initialize(
            SPR spr,
            ACT act,
            RoSpriteAtlas sharedAtlas,
            int initialActionIndex = 0,
            Material spriteMaterial = null
        ) {
            if (sharedAtlas == null) {
                throw new ArgumentNullException(nameof(sharedAtlas));
            }

            InitializeWithAtlas(spr, act, sharedAtlas, false, initialActionIndex, spriteMaterial);
        }

        private void InitializeWithAtlas(
            SPR spr,
            ACT act,
            RoSpriteAtlas resolvedAtlas,
            bool ownsResolvedAtlas,
            int initialActionIndex,
            Material spriteMaterial
        ) {
            if (spr == null) {
                throw new ArgumentNullException(nameof(spr));
            }

            if (act == null) {
                throw new ArgumentNullException(nameof(act));
            }

            if (act.actions == null || act.actions.Length == 0) {
                throw new ArgumentException("ACT must contain at least one action.", nameof(act));
            }

            ClearRuntimeObjects();
            this.act = act;
            actionIndex = Mathf.Clamp(initialActionIndex, 0, act.actions.Length - 1);
            frameIndex = GetInitialFrameIndex();
            atlas = resolvedAtlas;
            ownsAtlas = ownsResolvedAtlas;
            sourceSprite = spr;
            geometryCacheId = CalculateGeometryCacheId(spr, act);
            if (spriteMaterial != null) {
                material = spriteMaterial;
                ownsMaterial = false;
            } else {
                AcquireSharedSpriteMaterialForCurrentShadowMode((int) RenderQueue.Transparent);
            }
            ResetAnimatedFrameClock();

            EnsureRendererComponents();
            AssignRendererMaterial();
            if (material != null && atlas?.Texture != null) {
                SetMaterialTexture(atlas.Texture);
            }

            ApplyRendererShadowState();
            ApplyMaterialShadowStateIfInstanceMaterial();
            ApplyActorMaterialProperties();
            QueueGeometryPrewarm();
            ApplyFrame();
        }

        public void SetRuntimeMeshBuildBudgeted(bool enabled) {
            if (budgetRuntimeMeshBuilds == enabled) {
                return;
            }

            budgetRuntimeMeshBuilds = enabled;
            if (!budgetRuntimeMeshBuilds && frameApplyPending) {
                ApplyFrame();
            }
        }

        public void SetVisibleAnimationTickBudgeted(bool enabled) {
            if (budgetVisibleAnimationTicks == enabled) {
                return;
            }

            budgetVisibleAnimationTicks = enabled;
            nextBudgetedTickTime = 0f;
        }

        public void SetUseMutableRuntimeMesh(bool enabled) {
            if (useMutableRuntimeMesh == enabled) {
                return;
            }

            useMutableRuntimeMesh = enabled;
            lastAppliedMutableSourceMesh = null;
            lastAppliedMesh = null;
            if (act != null && atlas != null) {
                ApplyFrame();
            }
        }

        public int PrebuildAllFrameMeshes() {
            if (act?.actions == null || atlas == null || useFrameComposition) {
                return 0;
            }

            using (PrebuildAllMeshesMarker.Auto()) {
                int built = 0;
                for (int action = 0; action < act.actions.Length; action++) {
                    ACT.Action actAction = act.actions[action];
                    if (actAction?.frames == null) {
                        continue;
                    }

                    for (int frame = 0; frame < actAction.frames.Length; frame++) {
                        var key = new RoSpriteFrameGeometryKey(geometryCacheId, action, frame);
                        if (RoSpriteFrameGeometryCache.TryGetRenderMesh(key, out _)) {
                            continue;
                        }

                        Mesh mesh = RoSpriteMeshBuilder.Build(actAction.frames[frame], atlas);
                        if (RoSpriteFrameGeometryCache.StoreRenderMesh(key, mesh)) {
                            RoRuntimePerfCounters.IncrementSpriteFrameMeshBuild();
                            built++;
                        } else {
                            DestroyRuntimeObject(mesh);
                        }
                    }
                }

                if (frameApplyPending) {
                    ApplyFrame();
                }

                return built;
            }
        }

        public void SetShadowRenderMode(RoShadowRenderMode mode, RoEnhancedShadowSettings enhancedShadows = null) {
            RoEnhancedShadowSettings resolvedSettings = enhancedShadows ?? new RoEnhancedShadowSettings();
            float strength = Mathf.Clamp01(resolvedSettings.actorRealtimeShadowStrength);
            float pullDistance = Mathf.Max(0f, resolvedSettings.actorShadowCasterPullDistance);
            float groundClearance = Mathf.Max(0f, resolvedSettings.actorShadowCasterGroundClearance);
            float liftPullScale = Mathf.Clamp(resolvedSettings.actorShadowCasterLiftPullScale, 0f, 2f);
            float influence = Mathf.Clamp01(resolvedSettings.actorDirectionalLightInfluence);
            float emissionInfluence = Mathf.Max(0f, resolvedSettings.mapEmissionLightInfluence);
            if (shadowRenderMode == mode &&
                Mathf.Approximately(realtimeShadowStrength, strength) &&
                Mathf.Approximately(realtimeShadowCasterPullDistance, pullDistance) &&
                Mathf.Approximately(realtimeShadowCasterGroundClearance, groundClearance) &&
                Mathf.Approximately(realtimeShadowCasterLiftPullScale, liftPullScale) &&
                Mathf.Approximately(directionalLightInfluence, influence) &&
                Mathf.Approximately(emissionLightInfluence, emissionInfluence)) {
                return;
            }

            shadowRenderMode = mode;
            RoActorLightingPolicy lightingPolicy = RoActorLightingPolicy.Resolve(mode, resolvedSettings);
            actorReceivesRealtimeShadows = lightingPolicy.ReceiveRealtimeShadows;
            actorUsesAdditionalLights = lightingPolicy.UseAdditionalLights;
            realtimeShadowStrength = strength;
            realtimeShadowCasterPullDistance = pullDistance;
            realtimeShadowCasterGroundClearance = groundClearance;
            realtimeShadowCasterLiftPullScale = liftPullScale;
            directionalLightInfluence = influence;
            emissionLightInfluence = emissionInfluence;
            if (material != null &&
                (ownsMaterial || materialLease != null) &&
                !MaterialMatchesShadowMode(material, shadowRenderMode)) {
                int renderQueue = material.renderQueue;
                Texture currentTexture = material.mainTexture;
                if (useFrameComposition) {
                    UseOwnedSpriteMaterialForCurrentShadowMode(renderQueue, currentTexture);
                } else {
                    AcquireSharedSpriteMaterialForCurrentShadowMode(renderQueue);
                }

                EnsureRendererComponents();
                AssignRendererMaterial();
            }

            ApplyRendererShadowState();
            ApplyMaterialShadowStateIfInstanceMaterial();
            ApplyActorMaterialProperties();
        }

        public void SetFrameCompositionEnabled(bool enabled) {
            if (useFrameComposition == enabled) {
                return;
            }

            useFrameComposition = enabled;
            if (useFrameComposition && materialLease != null) {
                Texture currentTexture = material != null ? material.mainTexture : atlas?.Texture;
                UseOwnedSpriteMaterialForCurrentShadowMode(material.renderQueue, currentTexture);
                EnsureRendererComponents();
                AssignRendererMaterial();
            } else if (!useFrameComposition && ownsMaterial && atlas?.Texture != null) {
                int renderQueue = material != null ? material.renderQueue : (int) RenderQueue.Transparent;
                AcquireSharedSpriteMaterialForCurrentShadowMode(renderQueue);
                EnsureRendererComponents();
                AssignRendererMaterial();
                SetMaterialTexture(atlas.Texture);
            }

            ApplyActorMaterialProperties();   // the swap copied the new source's defaults over the layer's own values
            ClearFrameCaches();
            ApplyFrame();
        }

        public void SetAction(int newActionIndex, bool restart = true) {
            if (act == null || act.actions == null || act.actions.Length == 0) {
                return;
            }

            actionIndex = Mathf.Clamp(newActionIndex, 0, act.actions.Length - 1);
            if (framePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                frameIndex = GetClampedFixedFrameIndex(GetCurrentAction());
                ResetAnimatedFrameClock();
            } else if (framePlaybackMode == RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                if (restart) {
                    ResetAnimatedFrameClock();
                }

                frameIndex = GetHeadDirectionSegmentFrameIndex(GetCurrentAction());
            } else if (restart) {
                frameIndex = 0;
                ResetAnimatedFrameClock();
            }

            // the layers that follow the body ask for their action every late tick: the same action and frame on screen
            // already need no new apply (it was ~1100 of ~1340 applies a frame with 500 players)
            if (!restart && !frameApplyPending && actionIndex == appliedActionIndex && frameIndex == appliedFrameIndex) {
                return;
            }

            ApplyFrame();
        }

        /// <summary>
        /// The layer's current draw, for the classic engine's effect-look copies (RoClassicActorLookRenderer): mesh,
        /// transform, texture, vertical clip offset and render queue. False while the layer draws nothing.
        /// </summary>
        public bool TryGetLookDrawState(out Mesh mesh, out Matrix4x4 localToWorld, out Texture texture,
                                        out float verticalOffset, out int renderQueue) {
            mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            localToWorld = transform.localToWorldMatrix;
            Material material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            texture = material != null ? material.mainTexture : null;
            verticalOffset = verticalClipOffset;
            renderQueue = material != null ? material.renderQueue : 3000;
            return mesh != null && material != null && meshRenderer.enabled && gameObject.activeInHierarchy && opacity > 0f;
        }

        public void SetVerticalClipOffset(float offset) {
            if (Mathf.Approximately(verticalClipOffset, offset)) {
                return;
            }

            verticalClipOffset = offset;
            ApplyActorMaterialProperties();
        }

        /// <summary>The classic engine's actor colour for this layer (white = none).</summary>
        public void SetEffectTint(Color color) {
            if (effectTint == color) {
                return;
            }

            effectTint = color;
            ApplyActorMaterialProperties();
        }

        public void SetOpacity(float alpha) {
            float normalized = Mathf.Clamp01(float.IsNaN(alpha) || float.IsInfinity(alpha) ? 1f : alpha);
            if (Mathf.Approximately(opacity, normalized)) {
                return;
            }

            opacity = normalized;
            ApplyActorMaterialProperties();
        }

        public void SetTintColor(Color color) {
            Color normalized = ClampColor(color, Color.white, 1f);
            if (Approximately(tintColor, normalized)) {
                return;
            }

            tintColor = normalized;
            ApplyActorMaterialProperties();
        }

        public void SetMapLight(Color multiply, Color add) {
            multiply = ClampColor(multiply, Color.white, 1.35f);
            add = ClampColor(add, Color.clear, 1f);
            if (Approximately(mapLightMultiply, multiply) && Approximately(mapLightAdd, add)) {
                return;
            }

            mapLightMultiply = multiply;
            mapLightAdd = add;
            if (RendererMaterialCurrent) {
                rendererMaterial.SetColor(MapLightMultiplyId, mapLightMultiply);
                rendererMaterial.SetColor(MapLightAddId, mapLightAdd);
                return;
            }

            ApplyActorMaterialProperties();
        }

        /// <summary>
        /// The renderer already draws its copy of the current base material: a value that changed alone can go onto that
        /// copy. The full ApplyActorMaterialProperties (~25 writes, the texture sync and the depth mode again) is for a new
        /// copy; a walking crowd changed the shared shadow card, the shadow bottom and the map light at most walk frames and
        /// paid it for each layer.
        /// </summary>
        private bool RendererMaterialCurrent =>
            (object) rendererMaterial != null && (object) material != null && ReferenceEquals(rendererMaterialSource, material);

        public void SetRealtimeShadowCasterYOffset(float offset) {
            float normalized = NormalizeNonNegative(offset);
            if (Mathf.Approximately(realtimeShadowCasterYOffset, normalized)) {
                return;
            }

            realtimeShadowCasterYOffset = normalized;
            if (meshRenderer != null) {
                ApplyActorMaterialProperties();
            }
        }

        public void SetSharedActorShadowCard(bool enabled, Vector3 centerOS, float bottomLift) {
            centerOS = enabled ? NormalizeFinite(centerOS) : Vector3.zero;
            bottomLift = enabled ? NormalizeNonNegative(bottomLift) : 0f;
            // the centre comes from the layer's own rotated frame, so a camera turn adds float noise to an unchanged value
            if (useSharedActorShadowCard == enabled &&
                (sharedActorShadowCardCenterOS - centerOS).sqrMagnitude <= SharedShadowCardCenterEpsilonSqr &&
                Mathf.Approximately(sharedActorShadowCardBottomLift, bottomLift)) {
                return;
            }

            useSharedActorShadowCard = enabled;
            sharedActorShadowCardCenterOS = centerOS;
            sharedActorShadowCardBottomLift = bottomLift;
            if (RendererMaterialCurrent) {
                rendererMaterial.SetFloat(UseSharedActorShadowCardId, useSharedActorShadowCard ? 1f : 0f);
                rendererMaterial.SetVector(ActorShadowCardCenterOSId, new Vector4(centerOS.x, centerOS.y, centerOS.z, 0f));
                rendererMaterial.SetFloat(ActorShadowCardBottomLiftId, sharedActorShadowCardBottomLift);
                return;
            }

            if (meshRenderer != null) {
                ApplyActorMaterialProperties();
            }
        }

        public bool SetWalkFrameIndexFromDistance(float accumulatedDistanceCells) {
            return SetWalkFrameIndexFromDistance(
                accumulatedDistanceCells,
                RagnarokWalkUnitsPerCell,
                RagnarokWalkDistancePhaseScale);
        }

        public bool SetWalkFrameIndexFromDistance(
            float accumulatedDistanceCells,
            float walkUnitsPerCell,
            float walkPhaseScale
        ) {
            ACT.Action action = GetCurrentAction();
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            framePlaybackMode = RoActFramePlaybackMode.DistanceSynced;
            int distanceFrame = CalculateDistanceSyncedWalkFrameIndex(
                accumulatedDistanceCells,
                action.delay,
                action.frames.Length,
                walkUnitsPerCell,
                walkPhaseScale);
            if (frameIndex == distanceFrame && !frameApplyPending) {
                return true;
            }

            frameIndex = distanceFrame;
            ApplyFrame();
            return true;
        }

        public void SetFramePlayback(RoActFramePlaybackMode mode, int frame = 0) {
            frame = Mathf.Max(0, frame);
            if (framePlaybackMode == mode && fixedFrameIndex == frame && !frameApplyPending) {
                return;
            }

            RoActFramePlaybackMode previousMode = framePlaybackMode;
            framePlaybackMode = mode;
            fixedFrameIndex = frame;
            if (framePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                frameIndex = GetClampedFixedFrameIndex(GetCurrentAction());
            } else if (framePlaybackMode == RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                if (previousMode != RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                    ResetAnimatedFrameClock();
                }

                frameIndex = GetHeadDirectionSegmentFrameIndex(GetCurrentAction());
            } else if (previousMode != RoActFramePlaybackMode.Animated) {
                ResetAnimatedFrameClock();
            }

            ApplyFrame();
        }

        public void SetFrameDelayScale(float scale) {
            float normalizedScale = NormalizeFrameDelayScale(scale);
            if (Mathf.Approximately(frameDelayScale, normalizedScale)) {
                return;
            }

            frameDelayScale = normalizedScale;
        }

        /// <summary>
        /// Drives the current action at an absolute per-frame delay (seconds) regardless of the ACT's
        /// authored delay. Used to match the original client's attack timing (per-frame = attackMT /
        /// weapon motion units) so the swing always completes instead of being force-fit to amotion.
        /// </summary>
        public void SetAbsoluteFrameSeconds(float perFrameSeconds) {
            if (perFrameSeconds <= 0f) {
                return;
            }

            ACT.Action action = GetCurrentAction();
            float baseDelayMs = action != null ? Mathf.Max(1f, action.delay) : 1f;
            SetFrameDelayScale(perFrameSeconds * 1000f / baseDelayMs);
        }

        public void SetLoop(bool enabled) {
            loop = enabled;
        }

        public void SetPlayFrameSounds(bool enabled) {
            playFrameSounds = enabled;
            // Re-baseline so toggling on does not immediately replay the current frame's sound.
            lastSoundAction = actionIndex;
            lastSoundFrame = frameIndex;
        }

        public void SetMaterialRenderQueue(int renderQueue) {
            if (material == null) {
                return;
            }

            if (material.renderQueue == renderQueue) {
                return;
            }

            if (materialLease != null) {
                AcquireSharedSpriteMaterialForCurrentShadowMode(renderQueue);
                EnsureRendererComponents();
                AssignRendererMaterial();
                ApplyActorMaterialProperties();
                return;
            }

            material.renderQueue = renderQueue;
        }

        /// <summary>The layer's rank n in the client's layer map: its depth is n steps nearer than the body's.</summary>
        public void SetSpriteLayerRank(int rank) {
            if (spriteLayerRank == rank) {
                return;
            }

            spriteLayerRank = rank;
            if (rendererMaterial != null) {
                rendererMaterial.SetFloat(SpriteRankId, rank);
            }
        }

        /// <summary>Whether this layer writes the sprite depth that effects test against (<see cref="SpriteDepthWriteCutoff"/>).</summary>
        public void SetSpriteDepthWrite(bool writes) {
            float cutoff = writes ? SpriteDepthWriteCutoff : NoSpriteDepthCutoff;
            if (spriteDepthCutoff == cutoff) {
                return;
            }

            spriteDepthCutoff = cutoff;
            if (material != null) {
                ApplyActorMaterialProperties();
            }
        }

        /// <summary>The actor's ground point in this layer's object space: the origin of the client's leaning sprite depth.</summary>
        public void SetSpriteFeet(Vector3 feetOS) {
            if ((spriteFeetOS - feetOS).sqrMagnitude < 1e-8f) {
                return;
            }

            spriteFeetOS = feetOS;
            if (rendererMaterial != null) {
                rendererMaterial.SetVector(SpriteFeetId, feetOS);
            }
        }

        public void SetRendererSortingOrder(int order) {
            sortingOrder = order;
            if (!SortingOrderSetExternally) {
                EnsureRendererComponents();
            }
        }

        /// <summary>
        /// The renderer's sortingOrder is assigned elsewhere every frame (a world actor's layer in the client's single draw
        /// order, RoClientDrawOrder): the layer keeps its own order for when that stops, but does not write it - each frame
        /// one undid the other, two native writes per layer. UI previews and anything outside that order write as before.
        /// </summary>
        public bool SortingOrderSetExternally { get; set; }

        public bool TryGetCurrentAnchor(out Vector2Int anchor) {
            return TryGetAnchor(actionIndex, frameIndex, out anchor);
        }

        public bool TryGetAnchor(int requestedActionIndex, int requestedFrameIndex, out Vector2Int anchor) {
            if (!TryGetFrame(requestedActionIndex, requestedFrameIndex, out ACT.Frame frame)) {
                anchor = default;
                return false;
            }

            if (frame?.pos == null || frame.pos.Length == 0) {
                anchor = default;
                return false;
            }

            anchor = frame.pos[0];
            return true;
        }

        public bool TryGetCurrentLayerPosition(out Vector2Int position) {
            return TryGetLayerPosition(actionIndex, frameIndex, out position);
        }

        public bool TryGetActionDurationSeconds(int requestedActionIndex, out float durationSeconds) {
            durationSeconds = 0f;
            if (act?.actions == null || act.actions.Length == 0) {
                return false;
            }

            int resolvedActionIndex = PositiveModulo(requestedActionIndex, act.actions.Length);
            ACT.Action action = act.actions[resolvedActionIndex];
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            durationSeconds = GetFrameSeconds(action) * action.frames.Length;
            return true;
        }

        public bool TryGetActionBaseDurationSeconds(int requestedActionIndex, out float durationSeconds) {
            durationSeconds = 0f;
            if (act?.actions == null || act.actions.Length == 0) {
                return false;
            }

            int resolvedActionIndex = PositiveModulo(requestedActionIndex, act.actions.Length);
            ACT.Action action = act.actions[resolvedActionIndex];
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            durationSeconds = CalculateFrameDelaySeconds(action.delay, 1f) * action.frames.Length;
            return true;
        }

        /// <summary>
        /// Returns true when a non-looping action has displayed every frame and is frozen on the
        /// last one. Looping actions are never "finished" — they cycle forever. Used by the
        /// presenter to defer the Attack→Standby hand-off until the animation actually completes.
        /// </summary>
        public bool HasFinishedCurrentAction() {
            if (loop) {
                return true;
            }

            ACT.Action action = GetCurrentAction();
            if (action?.frames == null || action.frames.Length == 0) {
                return true;
            }

            return frameIndex >= action.frames.Length - 1;
        }

        public bool TryGetCurrentActionStableLocalBounds(out Bounds bounds, out int signature) {
            bounds = default;
            signature = 0;
            if (act?.actions == null || act.actions.Length == 0 || atlas?.Sprites == null) {
                return false;
            }

            int resolvedActionIndex = Mathf.Clamp(actionIndex, 0, act.actions.Length - 1);
            unchecked {
                signature = (geometryCacheId * 397) ^ resolvedActionIndex;
            }

            if (stableActionLocalBoundsCache.TryGetValue(resolvedActionIndex, out bounds)) {
                return true;
            }

            ACT.Action action = act.actions[resolvedActionIndex];
            if (!TryCalculateActionStableLocalBounds(action, out bounds)) {
                return false;
            }

            stableActionLocalBoundsCache[resolvedActionIndex] = bounds;
            return true;
        }

        public bool TryGetLayerPosition(int requestedActionIndex, int requestedFrameIndex, out Vector2Int position) {
            if (!TryGetFrame(requestedActionIndex, requestedFrameIndex, out ACT.Frame frame) ||
                frame?.layers == null ||
                frame.layers.Length == 0) {
                position = default;
                return false;
            }

            position = frame.layers[0].pos;
            return true;
        }

        public bool SetFrameIndexFromPrimary(int primaryFrameIndex) {
            if (framePlaybackMode == RoActFramePlaybackMode.FixedFrame ||
                framePlaybackMode == RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                return false;
            }

            ACT.Action action = GetCurrentAction();
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            int syncedFrameIndex = PositiveModulo(primaryFrameIndex, action.frames.Length);
            if (frameIndex == syncedFrameIndex && !frameApplyPending) {
                return true;
            }

            frameIndex = syncedFrameIndex;
            ApplyFrame();
            return true;
        }

        /// <summary>Ticked by its owner (a layered actor), so it stays out of the tick service's loop.</summary>
        private bool tickedByOwner;

        private void OnEnable() {
            if (tickedByOwner) {
                RoActorTickService.CountOwnerTickedAnimator(1);
            } else {
                RoActorTickService.Register((IRoActorUpdateTick) this);
            }
        }

        private void OnDisable() {
            if (tickedByOwner) {
                RoActorTickService.CountOwnerTickedAnimator(-1);
            } else {
                RoActorTickService.Unregister((IRoActorUpdateTick) this);
            }
        }

        /// <summary>The owner calls <see cref="ShouldRunActorTick"/> / <see cref="ActorUpdateTick"/> itself.</summary>
        public void SetTickedByOwner(bool byOwner) {
            if (tickedByOwner == byOwner) {
                return;
            }

            tickedByOwner = byOwner;
            if (!isActiveAndEnabled) {
                return;
            }

            if (byOwner) {
                RoActorTickService.Unregister((IRoActorUpdateTick) this);
                RoActorTickService.CountOwnerTickedAnimator(1);
            } else {
                RoActorTickService.CountOwnerTickedAnimator(-1);
                RoActorTickService.Register((IRoActorUpdateTick) this);
            }
        }

        /// <summary>
        /// The managed part of <see cref="ShouldRunActorTick"/>'s update-tick answer, without a native call: false only
        /// when it would say no anyway (a renderer, no pending frame, the frames set from outside - fixed, distance- or
        /// primary-synced: a walking body, every layer that follows the body). The owner asks this before
        /// isActiveAndEnabled and the rest for each of ~2000 layers a frame in a crowd.
        /// </summary>
        public bool MayNeedUpdateTick =>
            (object) meshRenderer == null ||
            frameApplyPending ||
            framePlaybackMode == RoActFramePlaybackMode.Animated ||
            framePlaybackMode == RoActFramePlaybackMode.HeadDirectionSegmentAnimated;

        public bool ShouldRunActorTick(float now, bool lateTick) {
            if (lateTick || !Application.isPlaying) {
                return true;
            }

            if (meshRenderer == null) {
                return true;
            }

            if (frameApplyPending) {
                return true;
            }

            // fixed, distance- or primary-synced frames are set from outside: the tick would do nothing
            if (framePlaybackMode != RoActFramePlaybackMode.Animated &&
                framePlaybackMode != RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                return false;
            }

            // One-shot / finite actions (attack, hit, skill, cast, pickup, ...) are non-looping and brief,
            // and they carry the visually important frames (full swing, weapon-trail slash). They MUST be
            // sampled every render frame — matching roBrowser, which never throttles. Throttling them (the
            // distance/crowd/off-screen budget below) under-samples short high-ASPD attacks and randomly
            // skips frames and the weapon trail. Held finite actions (e.g. the last attack frame, dead,
            // casting on a fixed frame) cost nothing extra here because ApplyFrame no-ops when the frame
            // index does not change. Only looping motions (idle/walk/sit/standby) keep the perf budget.
            if (!loop) {
                return true;
            }

            if (!meshRenderer.isVisible) {
                return TryConsumeBudgetedTick(now, OffscreenAnimationTickIntervalSeconds);
            }

            UnityEngine.Camera camera = ResolveBudgetCamera(now);
            if (camera == null) {
                return true;
            }

            if (budgetVisibleAnimationTicks) {
                return ShouldRunBudgetedVisibleAnimationTick(now);
            }

            float sqrDistance = (transform.position - camera.transform.position).sqrMagnitude;
            int animatorCount = RoActorTickService.RegisteredAnimatorUpdateTickCount;
            if (animatorCount >= CrowdAnimatorThreshold) {
                float closeDistance = CrowdFullRateDistance * CrowdFullRateDistance;
                float distantDistance = DistantAnimationDistance * DistantAnimationDistance;
                if (sqrDistance <= closeDistance) {
                    return animatorCount >= HeavyCrowdAnimatorThreshold
                        ? TryConsumeBudgetedTick(now, CrowdNearAnimationTickIntervalSeconds)
                        : true;
                }

                return TryConsumeBudgetedTick(
                    now,
                    sqrDistance <= distantDistance
                        ? CrowdMidAnimationTickIntervalSeconds
                        : CrowdFarAnimationTickIntervalSeconds);
            }

            if (sqrDistance <= DistantAnimationDistance * DistantAnimationDistance) {
                return true;
            }

            return TryConsumeBudgetedTick(now, DistantAnimationTickIntervalSeconds);
        }

        public RoActorTickVisibilityState ResolveActorTickVisibility(bool lateTick) {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) {
                return RoActorTickVisibilityState.Inactive;
            }

            if (meshRenderer == null || !meshRenderer.enabled || !meshRenderer.gameObject.activeInHierarchy) {
                return RoActorTickVisibilityState.Unknown;
            }

            return meshRenderer.isVisible
                ? RoActorTickVisibilityState.Visible
                : RoActorTickVisibilityState.Offscreen;
        }

        public void ActorUpdateTick(float now) {
            if (!Application.isPlaying || act == null || atlas == null) {
                return;
            }

            using (RoProfilerMarkers.SpriteAnimatorTick.Auto()) {
                if (frameApplyPending &&
                    framePlaybackMode != RoActFramePlaybackMode.Animated &&
                    framePlaybackMode != RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                    ApplyFrame();
                    return;
                }

                ACT.Action action = GetCurrentAction();
                if (action == null || action.frames == null || action.frames.Length == 0) {
                    return;
                }

                if (framePlaybackMode != RoActFramePlaybackMode.Animated &&
                    framePlaybackMode != RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                    return;
                }

                int animatedFrame = framePlaybackMode == RoActFramePlaybackMode.HeadDirectionSegmentAnimated
                    ? CalculateHeadDirectionSegmentFrameIndex(now - actionStartTime, GetFrameSeconds(action), action.frames.Length, fixedFrameIndex, loop)
                    : CalculateAnimatedFrameIndex(now - actionStartTime, GetFrameSeconds(action), action.frames.Length, loop);
                if (frameIndex != animatedFrame || frameApplyPending) {
                    frameIndex = animatedFrame;
                    ApplyFrame();
                }
            }
        }

        private bool ShouldRunBudgetedVisibleAnimationTick(float now) {
            if (framePlaybackMode != RoActFramePlaybackMode.Animated &&
                framePlaybackMode != RoActFramePlaybackMode.HeadDirectionSegmentAnimated) {
                return false;
            }

            ACT.Action action = GetCurrentAction();
            if (action == null || action.frames == null || action.frames.Length == 0) {
                return false;
            }

            float interval = Mathf.Clamp(GetFrameSeconds(action) * 0.5f, 1f / 30f, 0.1f);
            return TryConsumeBudgetedTick(now, interval);
        }

        private bool TryConsumeBudgetedTick(float now, float intervalSeconds) {
            if (now < nextBudgetedTickTime) {
                return false;
            }

            nextBudgetedTickTime = now + Mathf.Max(0f, intervalSeconds);
            return true;
        }

        private static UnityEngine.Camera ResolveBudgetCamera(float now) {
            if (cachedBudgetCamera != null && now < nextBudgetCameraResolveTime) {
                return cachedBudgetCamera;
            }

            cachedBudgetCamera = UnityEngine.Camera.main;
            nextBudgetCameraResolveTime = now + 0.5f;
            return cachedBudgetCamera;
        }

        private void OnDestroy() {
            ClearRuntimeObjects();
        }

        private ACT.Action GetCurrentAction() {
            if (act?.actions == null || act.actions.Length == 0) {
                return null;
            }

            actionIndex = Mathf.Clamp(actionIndex, 0, act.actions.Length - 1);
            return act.actions[actionIndex];
        }

        private bool TryGetFrame(int requestedActionIndex, int requestedFrameIndex, out ACT.Frame frame) {
            frame = null;
            if (act?.actions == null || act.actions.Length == 0) {
                return false;
            }

            int resolvedActionIndex = PositiveModulo(requestedActionIndex, act.actions.Length);
            ACT.Action action = act.actions[resolvedActionIndex];
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            int resolvedFrameIndex = PositiveModulo(requestedFrameIndex, action.frames.Length);
            frame = action.frames[resolvedFrameIndex];
            return frame != null;
        }

        private int GetInitialFrameIndex() {
            if (framePlaybackMode == RoActFramePlaybackMode.FixedFrame) {
                return Mathf.Max(0, fixedFrameIndex);
            }

            return 0;
        }

        private int GetClampedFixedFrameIndex(ACT.Action action) {
            if (action?.frames == null || action.frames.Length == 0) {
                return 0;
            }

            return Mathf.Clamp(fixedFrameIndex, 0, action.frames.Length - 1);
        }

        private int GetHeadDirectionSegmentFrameIndex(ACT.Action action) {
            if (action?.frames == null || action.frames.Length == 0) {
                return 0;
            }

            float elapsedSeconds = Application.isPlaying ? Mathf.Max(0f, Time.realtimeSinceStartup - actionStartTime) : 0f;
            return CalculateHeadDirectionSegmentFrameIndex(elapsedSeconds, GetFrameSeconds(action), action.frames.Length, fixedFrameIndex, loop);
        }

        public static int CalculateAnimatedFrameIndex(float elapsedSeconds, float frameSeconds, int frameCount, bool loop) {
            if (frameCount <= 0) {
                return 0;
            }

            float safeFrameSeconds = Mathf.Max(0.001f, frameSeconds);
            int elapsedFrames = Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds) / safeFrameSeconds + 0.0001f);
            if (loop) {
                return PositiveModulo(elapsedFrames, frameCount);
            }

            return Mathf.Min(elapsedFrames, frameCount - 1);
        }

        public static int CalculateHeadDirectionSegmentFrameIndex(
            float elapsedSeconds,
            float frameSeconds,
            int frameCount,
            int headDirectionFrame,
            bool loop
        ) {
            if (frameCount <= 0) {
                return 0;
            }

            int segmentFrameCount = frameCount / 3;
            if (segmentFrameCount <= 0) {
                return Mathf.Clamp(headDirectionFrame, 0, frameCount - 1);
            }

            int segmentIndex = Mathf.Clamp(headDirectionFrame, 0, 2);
            int segmentStart = Mathf.Min(segmentIndex * segmentFrameCount, frameCount - 1);
            int segmentFrame = CalculateAnimatedFrameIndex(elapsedSeconds, frameSeconds, segmentFrameCount, loop);
            return Mathf.Min(segmentStart + segmentFrame, frameCount - 1);
        }

        public static float CalculateFrameDelaySeconds(float actionDelayMilliseconds, float delayScale = 1f) {
            return Mathf.Max(1f, actionDelayMilliseconds) * NormalizeFrameDelayScale(delayScale) / 1000f;
        }

        public static float CalculateDistanceSyncedWalkFramePhase(
            float distanceCells,
            float actionDelayMilliseconds,
            float walkUnitsPerCell = RagnarokWalkUnitsPerCell,
            float walkPhaseScale = RagnarokWalkDistancePhaseScale
        ) {
            return Mathf.Max(0f, distanceCells) *
                Mathf.Max(0.001f, walkUnitsPerCell) *
                Mathf.Max(0.001f, walkPhaseScale) *
                RagnarokActDelayUnitMilliseconds /
                Mathf.Max(RagnarokActDelayUnitMilliseconds, actionDelayMilliseconds);
        }

        public static int CalculateDistanceSyncedWalkFrameIndex(
            float distanceCells,
            float actionDelayMilliseconds,
            int motionCount,
            float walkUnitsPerCell = RagnarokWalkUnitsPerCell,
            float walkPhaseScale = RagnarokWalkDistancePhaseScale
        ) {
            if (motionCount <= 0) {
                return 0;
            }

            float phase = CalculateDistanceSyncedWalkFramePhase(
                distanceCells,
                actionDelayMilliseconds,
                walkUnitsPerCell,
                walkPhaseScale);
            return PositiveModulo(Mathf.FloorToInt(phase), motionCount);
        }

        /// <summary>Returns how many seconds have elapsed since the current action started.</summary>
        public float GetCurrentElapsedSeconds() {
            return Application.isPlaying ? Mathf.Max(0f, Time.realtimeSinceStartup - actionStartTime) : 0f;
        }

        /// <summary>
        /// Sets the action, optionally resuming from a previously saved animation phase rather than
        /// restarting from frame 0. Used to preserve walk-cycle phase when the player stops and
        /// immediately starts walking again (matches roBrowser / Doddler behaviour).
        /// </summary>
        public void SetActionResuming(int newActionIndex, float resumeElapsedSeconds) {
            if (act == null || act.actions == null || act.actions.Length == 0) {
                return;
            }

            actionIndex = Mathf.Clamp(newActionIndex, 0, act.actions.Length - 1);
            if (framePlaybackMode != RoActFramePlaybackMode.FixedFrame) {
                // Rewind the clock so the animation appears to already be resumeElapsedSeconds in
                float now = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
                actionStartTime = now - Mathf.Max(0f, resumeElapsedSeconds);
                ACT.Action action = GetCurrentAction();
                if (action != null && action.frames != null && action.frames.Length > 0) {
                    float frameSeconds = GetFrameSeconds(action);
                    frameIndex = CalculateAnimatedFrameIndex(resumeElapsedSeconds, frameSeconds, action.frames.Length, loop);
                }
            }

            ApplyFrame();
        }

        private void ResetAnimatedFrameClock() {
            actionStartTime = Application.isPlaying ? Time.realtimeSinceStartup : 0f;
        }

        private void ApplyFrame() {
            long applyFrameStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (ApplyFrameMarker.Auto()) {
                try {
                    EnsureRendererComponents();
                    ACT.Action action = GetCurrentAction();
                    if (action == null || action.frames == null || action.frames.Length == 0 || atlas == null) {
                        frameApplyPending = false;
                        appliedActionIndex = -1;
                        AssignMesh(null);
                        SetRendererEnabled(false);
                        return;
                    }

                    frameIndex = Mathf.Clamp(frameIndex, 0, action.frames.Length - 1);
                    MaybeEmitFrameSound(action);
                    int cacheKey = actionIndex * 10000 + frameIndex;
                    if (useFrameComposition &&
                        TryGetOrBuildCompositedFrame(action, action.frames[frameIndex], cacheKey, out CompositedFrame composited)) {
                        UpdateFrameDepthState(action.frames[frameIndex], true);
                        SetMaterialTexture(composited.Texture);
                        ApplyFrameMesh(composited.Mesh);
                        SetRendererEnabled(true);
                        frameApplyPending = false;
                        appliedActionIndex = actionIndex;
                        appliedFrameIndex = frameIndex;
                        return;
                    }

                    UpdateFrameDepthState(action.frames[frameIndex], false);
                    SetMaterialTexture(atlas.Texture);
                    var sharedMeshKey = new RoSpriteFrameGeometryKey(geometryCacheId, actionIndex, frameIndex);
                    if (!RoSpriteFrameGeometryCache.TryGetRenderMesh(sharedMeshKey, out Mesh mesh) &&
                        !ownedUncachedFrameMeshes.TryGetValue(sharedMeshKey, out mesh)) {
                        if (budgetRuntimeMeshBuilds && !RoSpriteFrameGeometryCache.TryReserveRuntimeMeshBuild()) {
                            RoSpriteFrameGeometryCache.EnqueueRenderMeshPrewarm(sharedMeshKey, action.frames[frameIndex], atlas);
                            frameApplyPending = true;
                            appliedActionIndex = -1;
                            if (meshFilter.sharedMesh == null) {
                                AssignMesh(null);
                                SetRendererEnabled(false);
                            }

                            return;
                        }

                        long buildStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        mesh = RoSpriteMeshBuilder.Build(action.frames[frameIndex], atlas);
                        RoSpriteFrameGeometryCache.AddRuntimeMeshBuildTicks(System.Diagnostics.Stopwatch.GetTimestamp() - buildStart);
                        if (!RoSpriteFrameGeometryCache.StoreRenderMesh(sharedMeshKey, mesh)) {
                            // the shared cache is full: this animator keeps the frame for its next loop instead of building
                            // a new mesh at every frame change (a walking crowd built ~200 a frame and kept them all)
                            ownedUncachedFrameMeshes[sharedMeshKey] = mesh;
                        }

                        RoRuntimePerfCounters.IncrementSpriteFrameMeshBuild();
                    }

                    ApplyFrameMesh(mesh);
                    SetRendererEnabled(true);
                    frameApplyPending = false;
                    appliedActionIndex = actionIndex;
                    appliedFrameIndex = frameIndex;
                } finally {
                    RoRuntimePerfCounters.AddSpriteAnimatorApplyFrameTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(applyFrameStartTicks));
                }
            }
        }

        /// <summary>
        /// The client's ACT event sounds (FUN_00bd4a60, reference packages/sound §4.2): every frame from the last processed
        /// one to the current one plays its event, skipped frames and a loop's wrap included; a new action starts from frame
        /// 0 without playing frame 0's event; only events naming a .wav sound. No visibility check: PlaySound3D's distance
        /// rule decides.
        /// </summary>
        private void MaybeEmitFrameSound(ACT.Action action) {
            if (!playFrameSounds || !Application.isPlaying) {
                return;
            }

            if (actionIndex != lastSoundAction) {
                lastSoundAction = actionIndex;
                lastSoundFrame = 0;
            }

            int current = frameIndex;
            if (current == lastSoundFrame) {
                return;
            }

            int last = lastSoundFrame;
            lastSoundFrame = current;
            System.Action<string, Vector3, float> handler = FrameSoundRequested;
            if (handler == null || act == null || action.frames == null) {
                return;
            }

            int n = action.frames.Length;
            if (current > last) {
                EmitFrameSounds(action, last + 1, current, handler);
            } else {
                EmitFrameSounds(action, last + 1, n - 1, handler);
                EmitFrameSounds(action, 0, current, handler);
            }
        }

        private void EmitFrameSounds(ACT.Action action, int from, int to, System.Action<string, Vector3, float> handler) {
            for (int f = Mathf.Max(0, from); f <= to && f < action.frames.Length; f++) {
                ACT.Frame frame = action.frames[f];
                if (frame != null && frame.soundId >= 0 && act.TryGetSoundName(frame.soundId, out string soundName) &&
                    soundName.IndexOf(".wav", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    handler.Invoke(soundName, transform.position, 1f);
                }
            }
        }

        private void ApplyFrameMesh(Mesh mesh) {
            if (useMutableRuntimeMesh && mesh != null) {
                ApplyMutableFrameMesh(mesh);
                return;
            }

            AssignMesh(mesh);
        }

        private void ApplyMutableFrameMesh(Mesh sourceMesh) {
            Mesh targetMesh = EnsureMutableRuntimeMesh();
            bool updated = false;
            if (lastAppliedMutableSourceMesh != sourceMesh || meshFilter.sharedMesh != targetMesh) {
                long mutableMeshStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
                using (MutableMeshUpdateMarker.Auto()) {
                    CopyMeshData(sourceMesh, targetMesh);
                }

                RoRuntimePerfCounters.AddSpriteAnimatorMutableMeshUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(mutableMeshStartTicks));
                lastAppliedMutableSourceMesh = sourceMesh;
                updated = true;
            }

            bool assigned = AssignMesh(targetMesh);
            if (updated && !assigned) {
                UpdateRealtimeShadowCasterBottomLift(targetMesh);
                AdvanceRenderRevision();
            }
        }

        private bool AssignMesh(Mesh mesh) {
            if (meshFilter == null) {
                return false;
            }

            if (lastAppliedMesh == mesh && meshFilter.sharedMesh == mesh) {
                return false;
            }

            long meshAssignStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (MeshAssignMarker.Auto()) {
                meshFilter.sharedMesh = mesh;
            }

            RoRuntimePerfCounters.AddSpriteAnimatorMeshAssignTicks(
                RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(meshAssignStartTicks));

            lastAppliedMesh = mesh;
            meshAssignmentCount++;
            UpdateRealtimeShadowCasterBottomLift(mesh);
            AdvanceRenderRevision();
            return true;
        }

        private void AdvanceRenderRevision() {
            unchecked {
                renderRevision++;
                if (renderRevision == 0) {
                    renderRevision = 1;
                }
            }
        }

        private bool TryCalculateActionStableLocalBounds(ACT.Action action, out Bounds bounds) {
            bounds = default;
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            bool hasBounds = false;
            for (int frameIndex = 0; frameIndex < action.frames.Length; frameIndex++) {
                ACT.Frame frame = action.frames[frameIndex];
                if (frame?.layers == null) {
                    continue;
                }

                for (int layerIndex = 0; layerIndex < frame.layers.Length; layerIndex++) {
                    if (!TryCalculateLayerLocalBounds(frame.layers[layerIndex], out Bounds layerBounds)) {
                        continue;
                    }

                    if (!hasBounds) {
                        bounds = layerBounds;
                        hasBounds = true;
                    } else {
                        bounds.Encapsulate(layerBounds);
                    }
                }
            }

            return hasBounds;
        }

        private bool TryCalculateLayerLocalBounds(ACT.Layer layer, out Bounds bounds) {
            bounds = default;
            if (layer == null || atlas?.Sprites == null) {
                return false;
            }

            int spriteIndex = ResolveSpriteFrameIndex(layer);
            if (spriteIndex < 0 || spriteIndex >= atlas.Sprites.Length || atlas.Sprites[spriteIndex] == null) {
                return false;
            }

            Sprite sprite = atlas.Sprites[spriteIndex];
            float width = sprite.rect.width;
            float height = sprite.rect.height;
            if (width <= 0f || height <= 0f) {
                return false;
            }

            float halfWidth = width * 0.5f / SPR.PIXELS_PER_UNIT;
            float halfHeight = height * 0.5f / SPR.PIXELS_PER_UNIT;
            Quaternion rotation = Quaternion.Euler(0f, 0f, -layer.angle);
            var scale = new Vector3(layer.scale.x * (layer.isMirror ? -1f : 1f), layer.scale.y, 1f);
            float offsetX = Mathf.RoundToInt(width) % 2 == 1 ? 0.5f : 0f;
            float offsetY = Mathf.RoundToInt(height) % 2 == 1 ? 0.5f : 0f;
            Vector3 layerOffset = new Vector3(layer.pos.x - offsetX, -layer.pos.y + offsetY, 0f) / SPR.PIXELS_PER_UNIT;

            bool initialized = false;
            for (int x = -1; x <= 1; x += 2) {
                for (int y = -1; y <= 1; y += 2) {
                    Vector3 corner = new Vector3(x * halfWidth, y * halfHeight, 0f);
                    Vector3 vertex = rotation * Vector3.Scale(corner, scale) + layerOffset;
                    if (!initialized) {
                        bounds = new Bounds(vertex, Vector3.zero);
                        initialized = true;
                    } else {
                        bounds.Encapsulate(vertex);
                    }
                }
            }

            return initialized;
        }

        private void SetRendererEnabled(bool enabled) {
            if (meshRenderer == null) {
                return;
            }

            if (hasLastRendererEnabled &&
                lastRendererEnabled == enabled &&
                meshRenderer.enabled == enabled) {
                return;
            }

            meshRenderer.enabled = enabled;
            hasLastRendererEnabled = true;
            lastRendererEnabled = enabled;
        }

        private Mesh EnsureMutableRuntimeMesh() {
            if (mutableRuntimeMesh != null) {
                return mutableRuntimeMesh;
            }

            mutableRuntimeMesh = new Mesh {
                name = "RO ACT Runtime Frame"
            };
            mutableRuntimeMesh.MarkDynamic();
            return mutableRuntimeMesh;
        }

        private void CopyMeshData(Mesh source, Mesh target) {
            if (source == null || target == null) {
                return;
            }

            mutableVertices.Clear();
            mutableNormals.Clear();
            mutableUvs.Clear();
            mutableClips.Clear();
            mutableColors.Clear();
            mutableTriangles.Clear();

            source.GetVertices(mutableVertices);
            source.GetNormals(mutableNormals);
            source.GetUVs(0, mutableUvs);
            source.GetUVs(1, mutableClips);
            source.GetColors(mutableColors);
            source.GetTriangles(mutableTriangles, 0);

            target.Clear(keepVertexLayout: false);
            target.SetVertices(mutableVertices);
            if (mutableNormals.Count == mutableVertices.Count) {
                target.SetNormals(mutableNormals);
            }

            if (mutableUvs.Count == mutableVertices.Count) {
                target.SetUVs(0, mutableUvs);
            }

            if (mutableClips.Count == mutableVertices.Count) {
                target.SetUVs(1, mutableClips);
            }

            if (mutableColors.Count == mutableVertices.Count) {
                target.SetColors(mutableColors);
            }

            target.SetTriangles(mutableTriangles, 0);
            target.bounds = source.bounds;
        }

        private bool TryGetOrBuildCompositedFrame(ACT.Action action, ACT.Frame frame, int cacheKey, out CompositedFrame composited) {
            if (compositedFrameCache.TryGetValue(cacheKey, out composited) && composited != null) {
                return true;
            }

            if (!TryBuildCompositedFrame(action, frame, out composited)) {
                composited = null;
                return false;
            }

            compositedFrameCache[cacheKey] = composited;
            return true;
        }

        private bool TryBuildCompositedFrame(ACT.Action action, ACT.Frame frame, out CompositedFrame composited) {
            composited = null;
            if (frame?.layers == null || frame.layers.Length == 0 || sourceSprite?.frames == null) {
                return false;
            }

            var layers = new List<CompositedLayer>(frame.layers.Length);
            for (int i = 0; i < frame.layers.Length; i++) {
                if (!TryCreateCompositedLayer(frame.layers[i], out CompositedLayer layer)) {
                    continue;
                }

                layers.Add(layer);
            }

            if (layers.Count == 0) {
                return false;
            }

            if (!TryGetOrBuildCompositedActionLayout(action, out CompositedFrameLayout layout)) {
                layout = BuildCompositedLayoutFromLayers(layers);
            }

            var pixels = new byte[checked(layout.Width * layout.Height * 4)];
            for (int i = 0; i < layers.Count; i++) {
                CompositeLayer(pixels, layout.Width, layout.Height, layers[i], layout.CanvasOffsetX, layout.CanvasOffsetY);
            }

            var texture = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false, linear: false) {
                name = $"{sourceSprite.filename ?? "sprite"}:act-frame:{actionIndex}:{frameIndex}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            texture.LoadRawTextureData(pixels);
            texture.Apply(false, false);

            composited = new CompositedFrame(texture, BuildCompositedFrameMesh(layout));
            RoRuntimePerfCounters.IncrementSpriteCompositedFrameBuild();
            return true;
        }

        private bool TryGetOrBuildCompositedActionLayout(ACT.Action action, out CompositedFrameLayout layout) {
            if (action == null) {
                layout = default;
                return false;
            }

            if (compositedActionLayoutCache.TryGetValue(actionIndex, out layout)) {
                return true;
            }

            if (!TryBuildCompositedActionLayout(action, out layout)) {
                return false;
            }

            compositedActionLayoutCache[actionIndex] = layout;
            return true;
        }

        private bool TryBuildCompositedActionLayout(ACT.Action action, out CompositedFrameLayout layout) {
            layout = default;
            if (action?.frames == null || action.frames.Length == 0) {
                return false;
            }

            bool found = false;
            int minLeft = int.MaxValue;
            int minTop = int.MaxValue;
            int maxRight = int.MinValue;
            int maxBottom = int.MinValue;

            for (int frame = 0; frame < action.frames.Length; frame++) {
                ACT.Frame motion = action.frames[frame];
                if (motion?.layers == null) {
                    continue;
                }

                for (int layerIndex = 0; layerIndex < motion.layers.Length; layerIndex++) {
                    if (!TryCreateCompositedLayer(motion.layers[layerIndex], out CompositedLayer layer)) {
                        continue;
                    }

                    found = true;
                    minLeft = Math.Min(minLeft, layer.Left);
                    minTop = Math.Min(minTop, layer.Top);
                    maxRight = Math.Max(maxRight, layer.Right);
                    maxBottom = Math.Max(maxBottom, layer.Bottom);
                }
            }

            if (!found) {
                return false;
            }

            layout = BuildCompositedLayoutFromBounds(minLeft, minTop, maxRight, maxBottom);
            return true;
        }

        private bool TryCreateCompositedLayer(ACT.Layer layer, out CompositedLayer compositedLayer) {
            compositedLayer = default;
            int spriteIndex = ResolveSpriteFrameIndex(layer);
            if (spriteIndex < 0 || spriteIndex >= sourceSprite.frames.Length) {
                return false;
            }

            SPR.Frame sourceFrame = sourceSprite.frames[spriteIndex];
            if (sourceFrame == null || sourceFrame.type != SPR.TYPE_RGBA || sourceFrame.data == null) {
                return false;
            }

            int sourceWidth = sourceFrame.originalWidth > 0 ? sourceFrame.originalWidth : sourceFrame.width;
            int sourceHeight = sourceFrame.originalHeight > 0 ? sourceFrame.originalHeight : sourceFrame.height;
            if (sourceWidth <= 0 || sourceHeight <= 0) {
                return false;
            }

            float scaleX = ResolveLayerScale(layer.scale.x);
            float scaleY = ResolveLayerScale(layer.scale.y);
            int scaledWidth = Math.Max(1, Mathf.RoundToInt(sourceWidth * scaleX));
            int scaledHeight = Math.Max(1, Mathf.RoundToInt(sourceHeight * scaleY));
            int left = layer.pos.x - (scaledWidth - 1) / 2;
            int top = layer.pos.y - (scaledHeight - 1) / 2;
            bool mirrored = layer.isMirror ^ (layer.scale.x < 0f);
            compositedLayer = new CompositedLayer(
                sourceFrame,
                sourceWidth,
                sourceHeight,
                scaledWidth,
                scaledHeight,
                left,
                top,
                scaleX,
                scaleY,
                mirrored,
                ResolveLayerTint(layer));
            return true;
        }

        private static float ResolveLayerScale(float scale) {
            float absolute = Mathf.Abs(scale);
            return absolute <= 0.0001f ? 1f : Mathf.Max(0.01f, absolute);
        }

        private int ResolveSpriteFrameIndex(ACT.Layer layer) {
            if (layer == null || layer.index < 0) {
                return -1;
            }

            return layer.sprType == SPR.TYPE_RGBA
                ? layer.index + (atlas?.RgbaFrameOffset ?? 0)
                : layer.index;
        }

        private static Color ResolveLayerTint(ACT.Layer layer) {
            Color color = layer != null ? layer.color : Color.clear;
            if (Mathf.Approximately(color.r, 0f) &&
                Mathf.Approximately(color.g, 0f) &&
                Mathf.Approximately(color.b, 0f) &&
                Mathf.Approximately(color.a, 0f)) {
                return Color.white;
            }

            return color;
        }

        private static void CompositeLayer(byte[] canvas, int canvasWidth, int canvasHeight, CompositedLayer layer, int originX, int originY) {
            for (int y = 0; y < layer.ScaledHeight; y++) {
                int targetY = y + layer.Top + originY;
                if (targetY < 0 || targetY >= canvasHeight) {
                    continue;
                }

                int sourceY = Mathf.Clamp((int) (y / layer.ScaleY), 0, layer.SourceHeight - 1);
                for (int x = 0; x < layer.ScaledWidth; x++) {
                    int targetX = x + layer.Left + originX;
                    if (targetX < 0 || targetX >= canvasWidth) {
                        continue;
                    }

                    int sourceX = Mathf.Clamp((int) (x / layer.ScaleX), 0, layer.SourceWidth - 1);
                    if (layer.Mirrored) {
                        sourceX = layer.SourceWidth - 1 - sourceX;
                    }

                    int sourceOffset = GetCompiledFramePixelOffset(layer.Frame, layer.SourceWidth, layer.SourceHeight, sourceX, sourceY);
                    byte sourceAlphaByte = layer.Frame.data[sourceOffset + 3];
                    if (sourceAlphaByte == 0) {
                        continue;
                    }

                    float sourceR = (layer.Frame.data[sourceOffset] / 255f) * layer.Tint.r;
                    float sourceG = (layer.Frame.data[sourceOffset + 1] / 255f) * layer.Tint.g;
                    float sourceB = (layer.Frame.data[sourceOffset + 2] / 255f) * layer.Tint.b;
                    float sourceA = (sourceAlphaByte / 255f) * layer.Tint.a;
                    if (sourceA <= 0f) {
                        continue;
                    }

                    int canvasY = canvasHeight - 1 - targetY;
                    int targetOffset = ((canvasY * canvasWidth) + targetX) * 4;
                    float destinationR = canvas[targetOffset] / 255f;
                    float destinationG = canvas[targetOffset + 1] / 255f;
                    float destinationB = canvas[targetOffset + 2] / 255f;
                    float destinationA = canvas[targetOffset + 3] / 255f;

                    float outA = sourceA + destinationA * (1f - sourceA);
                    if (outA <= 0.0001f) {
                        continue;
                    }

                    float outR = (sourceR * sourceA + destinationR * destinationA * (1f - sourceA)) / outA;
                    float outG = (sourceG * sourceA + destinationG * destinationA * (1f - sourceA)) / outA;
                    float outB = (sourceB * sourceA + destinationB * destinationA * (1f - sourceA)) / outA;

                    canvas[targetOffset] = (byte) Mathf.Clamp(Mathf.RoundToInt(outR * 255f), 0, 255);
                    canvas[targetOffset + 1] = (byte) Mathf.Clamp(Mathf.RoundToInt(outG * 255f), 0, 255);
                    canvas[targetOffset + 2] = (byte) Mathf.Clamp(Mathf.RoundToInt(outB * 255f), 0, 255);
                    canvas[targetOffset + 3] = (byte) Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255);
                }
            }
        }

        private static int GetCompiledFramePixelOffset(SPR.Frame frame, int sourceWidth, int sourceHeight, int x, int yFromTop) {
            int startX = Mathf.FloorToInt((frame.width - sourceWidth) * 0.5f);
            int startY = Mathf.FloorToInt((frame.height - sourceHeight) * 0.5f);
            int rawY = startY + sourceHeight - 1 - yFromTop;
            int rawX = startX + x;
            return ((rawY * frame.width) + rawX) * 4;
        }

        private static int CalculateGeometryCacheId(SPR sprite, ACT action) {
            unchecked {
                int hash = 17;
                hash = (hash * 397) ^ HashPath(sprite?.filename);
                hash = (hash * 397) ^ HashPath(action?.filename);
                if (hash != 17) {
                    return hash;
                }

                hash = (hash * 397) ^ (sprite != null ? sprite.GetHashCode() : 0);
                hash = (hash * 397) ^ (action != null ? action.GetHashCode() : 0);
                return hash;
            }
        }

        private static int HashPath(string path) {
            return string.IsNullOrWhiteSpace(path)
                ? 0
                : StringComparer.OrdinalIgnoreCase.GetHashCode(path);
        }

        private static CompositedFrameLayout BuildCompositedLayoutFromLayers(List<CompositedLayer> layers) {
            int minLeft = int.MaxValue;
            int minTop = int.MaxValue;
            int maxRight = int.MinValue;
            int maxBottom = int.MinValue;

            for (int i = 0; i < layers.Count; i++) {
                CompositedLayer layer = layers[i];
                minLeft = Math.Min(minLeft, layer.Left);
                minTop = Math.Min(minTop, layer.Top);
                maxRight = Math.Max(maxRight, layer.Right);
                maxBottom = Math.Max(maxBottom, layer.Bottom);
            }

            return BuildCompositedLayoutFromBounds(minLeft, minTop, maxRight, maxBottom);
        }

        private static CompositedFrameLayout BuildCompositedLayoutFromBounds(int minLeft, int minTop, int maxRight, int maxBottom) {
            int halfWidth = Math.Max(Math.Abs(minLeft), Math.Abs(maxRight));
            int width = Math.Max(1, halfWidth * 2 + 1);
            int contentHeight = Math.Max(1, maxBottom - minTop + 1);
            int topPadding = contentHeight % 2 == 0 ? 1 : 0;
            int height = Math.Max(1, contentHeight + topPadding);
            return new CompositedFrameLayout(width, height, halfWidth, topPadding - minTop);
        }

        private static Mesh BuildCompositedFrameMesh(CompositedFrameLayout layout) {
            float left = -layout.CanvasOffsetX / (float) SPR.PIXELS_PER_UNIT;
            float right = (layout.Width - layout.CanvasOffsetX) / (float) SPR.PIXELS_PER_UNIT;
            float top = layout.CanvasOffsetY / (float) SPR.PIXELS_PER_UNIT;
            float bottom = (layout.CanvasOffsetY - layout.Height) / (float) SPR.PIXELS_PER_UNIT;

            var mesh = new Mesh {
                name = "RO Composited ACT Frame"
            };
            mesh.SetVertices(new[] {
                new Vector3(left, bottom, 0f),
                new Vector3(right, bottom, 0f),
                new Vector3(left, top, 0f),
                new Vector3(right, top, 0f)
            });
            mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetUVs(0, new[] {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 1, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void EnsureRendererComponents() {
            if (meshFilter == null) {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null) {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null) {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }

                hasLastRendererEnabled = false;
                hasAppliedSortingOrder = false;
                hasAppliedRendererShadowState = false;
            }

            ApplyRendererSortingOrder();
            ApplyRendererShadowState();
        }

        private void ApplyRendererSortingOrder() {
            if (meshRenderer == null) {
                return;
            }

            if (SortingOrderSetExternally) {
                hasAppliedSortingOrder = false;   // written once the order is this layer's again
                return;
            }

            if (hasAppliedSortingOrder &&
                appliedSortingOrder == sortingOrder &&
                meshRenderer.sortingOrder == sortingOrder) {
                return;
            }

            meshRenderer.sortingOrder = sortingOrder;
            appliedSortingOrder = sortingOrder;
            hasAppliedSortingOrder = true;
        }

        /// <summary>Puts the renderer's copy of the base material on the renderer (copying the base when it changed).</summary>
        private void AssignRendererMaterial() {
            EnsureRendererComponents();
            if (material == null) {
                meshRenderer.sharedMaterial = null;
                return;
            }

            if (rendererMaterial == null) {
                rendererMaterial = new Material(material) { name = "RO Sprite Material (actor)", hideFlags = HideFlags.DontSave };
            } else if (rendererMaterialSource != material) {
                if (rendererMaterial.shader != material.shader) {
                    rendererMaterial.shader = material.shader;
                }

                rendererMaterial.CopyPropertiesFromMaterial(material);
                rendererMaterial.renderQueue = material.renderQueue;
            }

            rendererMaterialSource = material;
            if (meshRenderer.HasPropertyBlock()) {
                meshRenderer.SetPropertyBlock(null);
            }

            meshRenderer.sharedMaterial = rendererMaterial;
        }

        /// <summary>The base material's texture and render queue, which change on the base itself, onto the renderer's copy.</summary>
        private void SyncRendererMaterialFromSource() {
            if (rendererMaterial == null || material == null) {
                return;
            }

            Texture texture = material.mainTexture;
            if (rendererMaterial.mainTexture != texture) {
                rendererMaterial.mainTexture = texture;
                SetTextureIfPropertyExists(rendererMaterial, "_BaseMap", texture);
                SetTextureIfPropertyExists(rendererMaterial, "_MainTex", texture);
            }

            if (rendererMaterial.renderQueue != material.renderQueue) {
                rendererMaterial.renderQueue = material.renderQueue;
            }
        }

        private void ApplyActorMaterialProperties() {
            EnsureRendererComponents();
            if (material == null) {
                return;
            }

            if (rendererMaterialSource != material || meshRenderer.sharedMaterial != rendererMaterial) {
                AssignRendererMaterial();
            }

            SyncRendererMaterialFromSource();
            Material target = rendererMaterial;
            target.SetFloat(VPosId, verticalClipOffset);
            // the client keeps one actor colour: a status colour (stone, freeze, poison ...) replaces whatever effects
            // wrote, otherwise the effects' colour multiplies the layer
            Color tint = tintColor != Color.white ? tintColor : effectTint;
            Color baseColor = new Color(tint.r, tint.g, tint.b, tint.a * opacity);
            target.SetColor(BaseColorId, baseColor);
            target.SetColor(ColorId, baseColor);
            target.SetColor(MapLightMultiplyId, mapLightMultiply);
            target.SetColor(MapLightAddId, mapLightAdd);
            target.SetFloat(DirectionalLightInfluenceId, directionalLightInfluence);
            target.SetFloat(EmissionLightInfluenceId, emissionLightInfluence);
            target.SetFloat(ReceiveRealtimeShadowsId, actorReceivesRealtimeShadows ? 1f : 0f);
            target.SetFloat(RealtimeShadowStrengthId, realtimeShadowStrength);
            target.SetFloat(UseAdditionalLightsId, actorUsesAdditionalLights ? 1f : 0f);
            target.SetFloat(ShadowCasterYOffsetId, realtimeShadowCasterYOffset);
            target.SetFloat(ShadowCasterPullDistanceId, realtimeShadowCasterPullDistance);
            target.SetFloat(ShadowCasterGroundClearanceId, realtimeShadowCasterGroundClearance);
            target.SetFloat(ShadowCasterLiftPullScaleId, realtimeShadowCasterLiftPullScale);
            target.SetFloat(ShadowCasterBottomLiftId, realtimeShadowCasterBottomLift);
            target.SetFloat(SpriteDepthCutoffId, spriteDepthCutoff);
            target.SetVector(SpriteFeetId, spriteFeetOS);
            // the client writes a sprite's depth while drawing it (list 0x180 pass 1), before any effect list: the forward
            // pass writes depth and stencil for the depth-writing layers of an opaque actor; the separate depth prepass's
            // depth never reached the effects (the effect batches tested against a buffer without it)
            // the depth comes from the sprite's own depth pass (SRPDefaultUnlit, per clip: vertex alpha 255 and the
            // 0x9f cut), drawn with the sprite, or from the colour pass itself for a fully opaque frame (ApplyDepthMode)
            currentBaseAlpha = baseColor.a;
            appliedDepthMode = DepthModeUnset;
            ApplyDepthMode(target);
            // the layers of an actor share the body's leaning plane; the client's z takes rhw + n·1e-6, so a head drawn
            // after the body is not lost against the body's depth (z-fighting at the neck)
            target.SetFloat(SpriteRankId, spriteLayerRank);
            target.SetFloat(UseSharedActorShadowCardId, useSharedActorShadowCard ? 1f : 0f);
            target.SetVector(ActorShadowCardCenterOSId, new Vector4(
                sharedActorShadowCardCenterOS.x,
                sharedActorShadowCardCenterOS.y,
                sharedActorShadowCardCenterOS.z,
                0f));
            target.SetFloat(ActorShadowCardBottomLiftId, sharedActorShadowCardBottomLift);
        }

        /// <summary>
        /// How the layer gets its depth, as cheap as gives the same picture: none (a layer the client does not write
        /// depth for, or a translucent actor: the depth pass would clip every pixel, so it is not drawn at all); the
        /// depth pass then the colour pass (a frame with a translucent clip, which must not write depth, or texels with
        /// partial alpha); or one pass, the colour pass writing the depth pass's depth itself (every clip at vertex alpha
        /// 255 and texels 0 or 255: the same pixels pass the 0x9f cut in both). Two passes per layer drawn in turn cost a
        /// shader switch per draw (300 players: ~2600 switches, ~16 ms a frame); one pass batches.
        /// </summary>
        private void ApplyDepthMode(Material target) {
            if (target == null) {
                return;
            }

            int mode;
            if (spriteDepthCutoff > 1f || currentBaseAlpha < 0.999f) {
                mode = DepthModeNone;
            } else if (SinglePassDepth && frameClipsOpaque && atlas != null && !atlas.HasPartialAlpha && !frameComposited) {
                mode = DepthModeSinglePass;
            } else {
                mode = DepthModeTwoPass;
            }

            if (mode == appliedDepthMode) {
                return;
            }

            appliedDepthMode = mode;
            bool single = mode == DepthModeSinglePass;
            target.SetShaderPassEnabled("SRPDefaultUnlit", mode == DepthModeTwoPass);
            target.SetFloat(SpriteZWriteId, single ? 1f : 0f);
            // the stencil mark of the depth-writing pixels: the depth pass's (Replace) or, in one pass, the colour pass's
            target.SetFloat(SpriteStencilPassId, single ? 2f : 0f);
            target.SetFloat(SpriteSinglePassId, single ? 1f : 0f);
            target.SetFloat(SpriteOffsetUnitsId, single ? -1f : -2f);
        }

        /// <summary>The current frame's clips all at vertex alpha 255 (the depth pass's per-clip condition)?</summary>
        private void UpdateFrameDepthState(ACT.Frame frame, bool composited) {
            bool opaque = !composited && frame != null && frame.layers != null;
            if (opaque) {
                for (int i = 0; i < frame.layers.Length; i++) {
                    ACT.Layer layer = frame.layers[i];
                    if (layer != null && layer.color.a < 0.999f) {
                        opaque = false;
                        break;
                    }
                }
            }

            frameClipsOpaque = opaque;
            frameComposited = composited;
            ApplyDepthMode(rendererMaterial);
        }

        /// <summary>Diagnostics (!toggle singlepass, !abdepth single): every animator re-picks its depth mode.</summary>
        public static int ReapplyDepthModeForAll() {
            int count = 0;
            foreach (RoActSpriteAnimator animator in FindObjectsByType<RoActSpriteAnimator>(FindObjectsSortMode.None)) {
                animator.appliedDepthMode = DepthModeUnset;
                animator.ApplyDepthMode(animator.rendererMaterial);
                count++;
            }

            return count;
        }

        private void UpdateRealtimeShadowCasterBottomLift(Mesh mesh) {
            float lift = 0f;
            if (mesh != null) {
                Bounds bounds = mesh.bounds;
                lift = NormalizeNonNegative(-bounds.min.y);
            }

            if (Mathf.Approximately(realtimeShadowCasterBottomLift, lift)) {
                return;
            }

            realtimeShadowCasterBottomLift = lift;
            if (RendererMaterialCurrent) {
                rendererMaterial.SetFloat(ShadowCasterBottomLiftId, realtimeShadowCasterBottomLift);
                return;
            }

            if (meshRenderer != null) {
                ApplyActorMaterialProperties();
            }
        }

        private static Color ClampColor(Color color, Color fallback, float maxRgb) {
            if (float.IsNaN(color.r) || float.IsNaN(color.g) || float.IsNaN(color.b) || float.IsNaN(color.a) ||
                float.IsInfinity(color.r) || float.IsInfinity(color.g) || float.IsInfinity(color.b) || float.IsInfinity(color.a)) {
                return fallback;
            }

            return new Color(
                Mathf.Clamp(color.r, 0f, Mathf.Max(0f, maxRgb)),
                Mathf.Clamp(color.g, 0f, Mathf.Max(0f, maxRgb)),
                Mathf.Clamp(color.b, 0f, Mathf.Max(0f, maxRgb)),
                Mathf.Clamp01(color.a)
            );
        }

        private static bool Approximately(Color left, Color right) {
            return Mathf.Approximately(left.r, right.r) &&
                Mathf.Approximately(left.g, right.g) &&
                Mathf.Approximately(left.b, right.b) &&
                Mathf.Approximately(left.a, right.a);
        }

        private static bool Approximately(Vector3 left, Vector3 right) {
            return Mathf.Approximately(left.x, right.x) &&
                Mathf.Approximately(left.y, right.y) &&
                Mathf.Approximately(left.z, right.z);
        }

        private static Vector3 NormalizeFinite(Vector3 value) {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z)) {
                return Vector3.zero;
            }

            return value;
        }

        private static Material CreateSpriteMaterial() {
            Shader shader = Shader.Find("Ragnarok/RO Sprite Unlit");
            return CreateSpriteMaterial(shader, RoShadowRenderMode.Original);
        }

        private Material CreateSpriteMaterialForCurrentShadowMode(int renderQueue = (int) RenderQueue.Transparent) {
            Shader shader = Shader.Find(shadowRenderMode == RoShadowRenderMode.Enhanced
                ? "Ragnarok/RO Sprite Enhanced"
                : "Ragnarok/RO Sprite Unlit");
            return CreateSpriteMaterial(shader, shadowRenderMode, renderQueue);
        }

        private void AcquireSharedSpriteMaterialForCurrentShadowMode(int renderQueue) {
            ReleaseCurrentMaterial();
            Texture texture = atlas?.Texture;
            if (texture == null) {
                UseOwnedSpriteMaterialForCurrentShadowMode(renderQueue, null);
                return;
            }

            materialLease = RoSpriteMaterialCache.Acquire(texture, shadowRenderMode, renderQueue);
            material = materialLease.Material;
            ownsMaterial = false;
        }

        private void UseOwnedSpriteMaterialForCurrentShadowMode(int renderQueue, Texture texture) {
            ReleaseCurrentMaterial();
            material = CreateSpriteMaterialForCurrentShadowMode(renderQueue);
            ownsMaterial = true;
            SetMaterialTexture(texture);
            ApplyMaterialShadowState(material);
        }

        private void ReleaseCurrentMaterial() {
            materialLease?.Dispose();
            materialLease = null;
            if (ownsMaterial) {
                DestroyRuntimeObject(material);
            }

            material = null;
            ownsMaterial = false;
            lastAppliedTexture = null;
        }

        private static Material CreateSpriteMaterial(Shader shader, RoShadowRenderMode mode, int renderQueue = (int) RenderQueue.Transparent) {
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Sprite Material",
                renderQueue = renderQueue
            };
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_ZTest", (float) SpriteDepthTest);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            SetFloatIfPropertyExists(material, "_DepthCorrection", 1f);
            SetFloatIfPropertyExists(material, "_RoSpriteDepthBias", SpriteDepthBias);
            SetFloatIfPropertyExists(material, "_RoDirectionalLightInfluence", RoEnhancedShadowSettings.DefaultActorDirectionalLightInfluence);
            SetFloatIfPropertyExists(material, "_RoEmissionLightInfluence", RoEnhancedShadowSettings.DefaultMapEmissionLightInfluence);
            RoActorLightingPolicy materialPolicy = RoActorLightingPolicy.Resolve(mode, null);
            SetFloatIfPropertyExists(material, "_RoReceiveRealtimeShadows", materialPolicy.ReceiveRealtimeShadows ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoRealtimeShadowStrength", materialPolicy.RealtimeShadowStrength);
            SetFloatIfPropertyExists(material, "_RoUseAdditionalLights", materialPolicy.UseAdditionalLights ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoShadowCasterYOffset", 0f);
            SetFloatIfPropertyExists(material, "_RoShadowCasterPullDistance", RoEnhancedShadowSettings.DefaultActorShadowCasterPullDistance);
            SetFloatIfPropertyExists(material, "_RoShadowCasterGroundClearance", RoEnhancedShadowSettings.DefaultActorShadowCasterGroundClearance);
            SetFloatIfPropertyExists(material, "_RoShadowCasterLiftPullScale", RoEnhancedShadowSettings.DefaultActorShadowCasterLiftPullScale);
            SetFloatIfPropertyExists(material, "_RoShadowCasterBottomLift", 0f);
            SetFloatIfPropertyExists(material, "_RoUseSharedActorShadowCard", 0f);
            SetVectorIfPropertyExists(material, "_RoActorShadowCardCenterOS", Vector4.zero);
            SetFloatIfPropertyExists(material, "_RoActorShadowCardBottomLift", 0f);
            return material;
        }

        private static bool MaterialMatchesShadowMode(Material candidate, RoShadowRenderMode mode) {
            if (candidate == null || candidate.shader == null) {
                return false;
            }

            string expectedShader = mode == RoShadowRenderMode.Enhanced
                ? "Ragnarok/RO Sprite Enhanced"
                : "Ragnarok/RO Sprite Unlit";
            return string.Equals(candidate.shader.name, expectedShader, StringComparison.Ordinal);
        }

        public void SetCastsRealtimeShadow(bool value) {
            if (castsRealtimeShadow == value) {
                return;
            }

            castsRealtimeShadow = value;
            hasAppliedRendererShadowState = false;
            ApplyRendererShadowState();
        }

        private void ApplyRendererShadowState() {
            if (meshRenderer == null) {
                return;
            }

            if (hasAppliedRendererShadowState && appliedRendererShadowRenderMode == shadowRenderMode) {
                return;
            }

            if (shadowRenderMode == RoShadowRenderMode.Enhanced) {
                meshRenderer.shadowCastingMode = castsRealtimeShadow ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
                meshRenderer.receiveShadows = true;
                meshRenderer.renderingLayerMask |= RoEnhancedShadowSettings.DefaultModelLampRenderingLayerMask;
                appliedRendererShadowRenderMode = shadowRenderMode;
                hasAppliedRendererShadowState = true;
                return;
            }

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.renderingLayerMask &= ~RoEnhancedShadowSettings.DefaultModelLampRenderingLayerMask;
            appliedRendererShadowRenderMode = shadowRenderMode;
            hasAppliedRendererShadowState = true;
        }

        private void ApplyMaterialShadowStateIfInstanceMaterial() {
            if (materialLease != null) {
                return;
            }

            ApplyMaterialShadowState(material);
        }

        private void ApplyMaterialShadowState(Material target) {
            if (target == null) {
                return;
            }

            SetFloatIfPropertyExists(target, "_RoReceiveRealtimeShadows", actorReceivesRealtimeShadows ? 1f : 0f);
            SetFloatIfPropertyExists(target, "_RoRealtimeShadowStrength", realtimeShadowStrength);
            SetFloatIfPropertyExists(target, "_RoUseAdditionalLights", actorUsesAdditionalLights ? 1f : 0f);
            SetFloatIfPropertyExists(target, "_RoShadowCasterPullDistance", realtimeShadowCasterPullDistance);
            SetFloatIfPropertyExists(target, "_RoShadowCasterGroundClearance", realtimeShadowCasterGroundClearance);
            SetFloatIfPropertyExists(target, "_RoShadowCasterLiftPullScale", realtimeShadowCasterLiftPullScale);
            SetFloatIfPropertyExists(target, "_RoShadowCasterBottomLift", realtimeShadowCasterBottomLift);
            SetFloatIfPropertyExists(target, "_RoUseSharedActorShadowCard", useSharedActorShadowCard ? 1f : 0f);
            SetVectorIfPropertyExists(target, "_RoActorShadowCardCenterOS", new Vector4(
                sharedActorShadowCardCenterOS.x,
                sharedActorShadowCardCenterOS.y,
                sharedActorShadowCardCenterOS.z,
                0f));
            SetFloatIfPropertyExists(target, "_RoActorShadowCardBottomLift", sharedActorShadowCardBottomLift);
            SetFloatIfPropertyExists(target, "_RoDirectionalLightInfluence", directionalLightInfluence);
            SetFloatIfPropertyExists(target, "_RoEmissionLightInfluence", emissionLightInfluence);
        }

        private void SetMaterialTexture(Texture texture) {
            if (material == null || texture == null) {
                return;
            }

            // this texture already went into this very material (every frame of a layer shares the atlas)
            if (ReferenceEquals(lastAppliedTexture, texture) && ReferenceEquals(lastAppliedTextureMaterial, material)) {
                return;
            }

            if (lastAppliedTexture == texture &&
                material.mainTexture == texture &&
                TexturePropertyMatches(material, "_BaseMap", texture) &&
                TexturePropertyMatches(material, "_MainTex", texture)) {
                lastAppliedTextureMaterial = material;
                return;
            }

            long textureAssignStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (TextureAssignMarker.Auto()) {
                try {
                    if (materialLease != null && material.mainTexture != texture) {
                        int renderQueue = material.renderQueue;
                        bool assignedByOwnedMaterial = false;
                        if (useFrameComposition) {
                            UseOwnedSpriteMaterialForCurrentShadowMode(renderQueue, texture);
                            assignedByOwnedMaterial = true;
                        } else {
                            AcquireSharedSpriteMaterialForCurrentShadowMode(renderQueue);
                        }

                        EnsureRendererComponents();
                        AssignRendererMaterial();
                        // the renderer copy took every property of the new source: the layer's own (rank, feet, colour, depth cut)
                        // go back on, or a head drawn after a texture swap lost its rank against the body (the neck through it)
                        ApplyActorMaterialProperties();
                        lastAppliedTexture = texture;
                        lastAppliedTextureMaterial = material;
                        if (!assignedByOwnedMaterial) {
                            textureAssignmentCount++;
                        }
                        return;
                    }

                    if (material.mainTexture != texture) {
                        material.mainTexture = texture;
                    }

                    SetTextureIfPropertyExists(material, "_BaseMap", texture);
                    SetTextureIfPropertyExists(material, "_MainTex", texture);
                    SyncRendererMaterialFromSource();
                    lastAppliedTexture = texture;
                    lastAppliedTextureMaterial = material;
                    textureAssignmentCount++;
                } finally {
                    RoRuntimePerfCounters.AddSpriteAnimatorTextureAssignTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(textureAssignStartTicks));
                }
            }
        }

        private float GetFrameSeconds(ACT.Action action) {
            return CalculateFrameDelaySeconds(action.delay, frameDelayScale);
        }

        private static float NormalizeFrameDelayScale(float scale) {
            if (float.IsNaN(scale) || float.IsInfinity(scale)) {
                return 1f;
            }

            return Mathf.Max(0.0001f, scale);
        }

        private static float NormalizeNonNegative(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value)) {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private void ClearRuntimeObjects() {
            ClearFrameCaches();
            ReleaseCurrentMaterial();
            if (rendererMaterial != null) {
                if (meshRenderer != null && meshRenderer.sharedMaterial == rendererMaterial) {
                    meshRenderer.sharedMaterial = null;
                }

                DestroyRuntimeObject(rendererMaterial);
                rendererMaterial = null;
                rendererMaterialSource = null;
            }
            if (ownsAtlas) {
                atlas?.Dispose();
            }

            if (mutableRuntimeMesh != null) {
                if (meshFilter != null && meshFilter.sharedMesh == mutableRuntimeMesh) {
                    meshFilter.sharedMesh = null;
                }

                DestroyRuntimeObject(mutableRuntimeMesh);
            }

            atlas = null;
            ownsAtlas = false;
            sourceSprite = null;
            lastAppliedTexture = null;
            lastAppliedMesh = null;
            mutableRuntimeMesh = null;
            lastAppliedMutableSourceMesh = null;
            hasLastRendererEnabled = false;
            hasAppliedSortingOrder = false;
            hasAppliedRendererShadowState = false;
            geometryCacheId = 0;
            frameApplyPending = false;
            appliedActionIndex = -1;
            stableActionLocalBoundsCache.Clear();
        }

        private void ClearFrameCaches() {
            foreach (Mesh owned in ownedUncachedFrameMeshes.Values) {
                DestroyRuntimeObject(owned);
            }

            ownedUncachedFrameMeshes.Clear();

            foreach (CompositedFrame frame in compositedFrameCache.Values) {
                frame?.Dispose();
            }

            compositedFrameCache.Clear();
            compositedActionLayoutCache.Clear();
        }

        private void QueueGeometryPrewarm() {
            if (!budgetRuntimeMeshBuilds ||
                !Application.isPlaying ||
                useFrameComposition ||
                act?.actions == null ||
                atlas == null) {
                return;
            }

            int queued = 0;
            QueueGeometryPrewarmAction(actionIndex, ref queued);
            if (queued >= MaxGeometryPrewarmFramesPerAnimator) {
                return;
            }

            for (int action = 0; action < act.actions.Length; action++) {
                if (action == actionIndex) {
                    continue;
                }

                QueueGeometryPrewarmAction(action, ref queued);
                if (queued >= MaxGeometryPrewarmFramesPerAnimator) {
                    return;
                }
            }
        }

        private void QueueGeometryPrewarmAction(int requestedActionIndex, ref int queued) {
            if (act?.actions == null ||
                requestedActionIndex < 0 ||
                requestedActionIndex >= act.actions.Length ||
                queued >= MaxGeometryPrewarmFramesPerAnimator) {
                return;
            }

            ACT.Action actAction = act.actions[requestedActionIndex];
            if (actAction?.frames == null) {
                return;
            }

            for (int frame = 0; frame < actAction.frames.Length; frame++) {
                RoSpriteFrameGeometryCache.EnqueueRenderMeshPrewarm(
                    new RoSpriteFrameGeometryKey(geometryCacheId, requestedActionIndex, frame),
                    actAction.frames[frame],
                    atlas);
                queued++;
                if (queued >= MaxGeometryPrewarmFramesPerAnimator) {
                    return;
                }
            }
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target != null && target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void SetVectorIfPropertyExists(Material target, string property, Vector4 value) {
            if (target != null && target.HasProperty(property)) {
                target.SetVector(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture texture) {
            if (target != null && target.HasProperty(property)) {
                target.SetTexture(property, texture);
            }
        }

        private static bool TexturePropertyMatches(Material target, string property, Texture texture) {
            return target == null ||
                !target.HasProperty(property) ||
                target.GetTexture(property) == texture;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }

        private sealed class CompositedFrame : IDisposable {
            public CompositedFrame(Texture2D texture, Mesh mesh) {
                Texture = texture;
                Mesh = mesh;
            }

            public Texture2D Texture { get; }
            public Mesh Mesh { get; }

            public void Dispose() {
                DestroyRuntimeObject(Texture);
                DestroyRuntimeObject(Mesh);
            }
        }

        private readonly struct CompositedLayer {
            public CompositedLayer(
                SPR.Frame frame,
                int sourceWidth,
                int sourceHeight,
                int scaledWidth,
                int scaledHeight,
                int left,
                int top,
                float scaleX,
                float scaleY,
                bool mirrored,
                Color tint
            ) {
                Frame = frame;
                SourceWidth = sourceWidth;
                SourceHeight = sourceHeight;
                ScaledWidth = scaledWidth;
                ScaledHeight = scaledHeight;
                Left = left;
                Top = top;
                ScaleX = scaleX;
                ScaleY = scaleY;
                Mirrored = mirrored;
                Tint = tint;
            }

            public SPR.Frame Frame { get; }
            public int SourceWidth { get; }
            public int SourceHeight { get; }
            public int ScaledWidth { get; }
            public int ScaledHeight { get; }
            public int Left { get; }
            public int Top { get; }
            public int Right => Left + ScaledWidth - 1;
            public int Bottom => Top + ScaledHeight - 1;
            public float ScaleX { get; }
            public float ScaleY { get; }
            public bool Mirrored { get; }
            public Color Tint { get; }
        }

        private readonly struct CompositedFrameLayout {
            public CompositedFrameLayout(int width, int height, int canvasOffsetX, int canvasOffsetY) {
                Width = width;
                Height = height;
                CanvasOffsetX = canvasOffsetX;
                CanvasOffsetY = canvasOffsetY;
            }

            public int Width { get; }
            public int Height { get; }
            public int CanvasOffsetX { get; }
            public int CanvasOffsetY { get; }
        }
    }

    public enum RoActFramePlaybackMode {
        Animated,
        FixedFrame,
        DistanceSynced,
        PrimarySynced,
        HeadDirectionSegmentAnimated
    }

    public enum RoActorDirection : byte {
        South = 0,
        SouthWest = 1,
        West = 2,
        NorthWest = 3,
        North = 4,
        NorthEast = 5,
        East = 6,
        SouthEast = 7,
        None = 8
    }

    public enum RoSpriteMotionProfile {
        Player,
        Monster,
        Monster2,
        Npc,
        ActionNpc,
        Pet
    }

    public enum RoSpriteMotion {
        Idle,
        Walk,
        Sit,
        PickUp,
        Standby,
        Attack1,
        Hit,
        Freeze1,
        Dead,
        Freeze2,
        Attack2,
        Attack3,
        Casting,
        Special,
        Performance1,
        Performance2,
        Performance3,
        Throwing
    }

    public static class RoSpriteActionMap {
        private static readonly RoSpriteMotionAction[] PlayerMotionTable = {
            new RoSpriteMotionAction(RoSpriteMotion.Walk, 1 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Sit, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.PickUp, 3 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Standby, 4 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Throwing, 5 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Hit, 6 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Freeze1, 7 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Dead, 8 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Freeze2, 9 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack2, 10 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack1, 11 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack3, 11 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Casting, 12 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Special, 12 * 8)
        };

        private static readonly RoSpriteMotionAction[] MonsterMotionTable = {
            new RoSpriteMotionAction(RoSpriteMotion.Walk, 1 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack1, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack2, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack3, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Throwing, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Hit, 3 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Dead, 4 * 8)
        };

        private static readonly RoSpriteMotionAction[] PetMotionTable = {
            new RoSpriteMotionAction(RoSpriteMotion.Special, 5 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Performance1, 6 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Performance2, 7 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Performance3, 8 * 8)
        };

        private static readonly RoSpriteMotionAction[] ActionNpcMotionTable = {
            new RoSpriteMotionAction(RoSpriteMotion.Walk, 1 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Hit, 2 * 8),
            new RoSpriteMotionAction(RoSpriteMotion.Attack1, 3 * 8)
        };

        public static int GetPcActionIndex(
            RoSpriteMotion motion,
            RoActorDirection actorDirection,
            RoActorDirection cameraDirection,
            int actionCount
        ) {
            return GetActionIndex(RoSpriteMotionProfile.Player, motion, actorDirection, cameraDirection, actionCount);
        }

        public static int GetActionIndex(
            RoSpriteMotionProfile profile,
            RoSpriteMotion motion,
            RoActorDirection actorDirection,
            RoActorDirection cameraDirection,
            int actionCount
        ) {
            if (actionCount <= 0) {
                return 0;
            }

            int baseAction = GetMotionBaseAction(profile, motion);
            int actor = actorDirection == RoActorDirection.None ? 0 : (int) actorDirection;
            int camera = cameraDirection == RoActorDirection.None ? 0 : (int) cameraDirection;
            int relativeDirection = PositiveModulo(actor + camera, 8);
            return PositiveModulo(baseAction + relativeDirection, actionCount);
        }

        public static int GetPcMotionBaseAction(RoSpriteMotion motion) {
            return GetMotionBaseAction(RoSpriteMotionProfile.Player, motion);
        }

        public static int GetMotionBaseAction(RoSpriteMotionProfile profile, RoSpriteMotion motion) {
            return TryGetMotionBaseAction(profile, motion, out int baseAction) ? baseAction : 0;
        }

        public static bool TryGetMotionBaseAction(RoSpriteMotionProfile profile, RoSpriteMotion motion, out int baseAction) {
            if (motion == RoSpriteMotion.Idle) {
                baseAction = 0;
                return true;
            }

            if (profile == RoSpriteMotionProfile.ActionNpc) {
                return TryFindMotion(ActionNpcMotionTable, motion, out baseAction);
            }

            if (profile == RoSpriteMotionProfile.Monster2 && motion == RoSpriteMotion.Attack2) {
                baseAction = 5 * 8;
                return true;
            }

            if (profile == RoSpriteMotionProfile.Monster ||
                profile == RoSpriteMotionProfile.Monster2 ||
                profile == RoSpriteMotionProfile.Pet) {
                if (TryFindMotion(MonsterMotionTable, motion, out baseAction)) {
                    return true;
                }
            }

            if (profile == RoSpriteMotionProfile.Pet &&
                TryFindMotion(PetMotionTable, motion, out baseAction)) {
                return true;
            }

            if (profile == RoSpriteMotionProfile.Player) {
                return TryFindMotion(PlayerMotionTable, motion, out baseAction);
            }

            baseAction = 0;
            return false;
        }

        public static bool IsEightDirection(RoSpriteMotionProfile profile, RoSpriteMotion motion) {
            if (motion == RoSpriteMotion.Hit) {
                return profile == RoSpriteMotionProfile.Player ||
                    profile == RoSpriteMotionProfile.Monster ||
                    profile == RoSpriteMotionProfile.Monster2 ||
                    profile == RoSpriteMotionProfile.Pet ||
                    profile == RoSpriteMotionProfile.ActionNpc;
            }

            if (motion != RoSpriteMotion.Idle &&
                motion != RoSpriteMotion.Sit &&
                motion != RoSpriteMotion.Walk) {
                return false;
            }

            return profile == RoSpriteMotionProfile.Player ||
                profile == RoSpriteMotionProfile.Npc;
        }

        public static RoActorDirection GetDirectionFromWorldDelta(Vector3 delta, RoActorDirection fallback = RoActorDirection.South) {
            return GetDirectionFromWorldDelta(delta, fallback, 0f);
        }

        /// <summary>
        /// Converts a world-space heading into one of the 8 facing directions. With
        /// <paramref name="hysteresisDegrees"/> &gt; 0 the current <paramref name="fallback"/> direction is
        /// kept until the heading leaves its sector by that extra margin, so a heading hovering on a sector
        /// boundary (e.g. a slightly off-axis walk wobbling between South and SouthWest) does not flip-flop
        /// the rendered facing every frame/re-route. Used for walk facing; precise actions pass 0.
        /// </summary>
        public static RoActorDirection GetDirectionFromWorldDelta(Vector3 delta, RoActorDirection fallback, float hysteresisDegrees) {
            delta.y = 0f;
            if (delta.sqrMagnitude <= 0.0001f) {
                return fallback;
            }

            float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            if (angle < 0f) {
                angle += 360f;
            }

            if (hysteresisDegrees > 0f && TryGetDirectionCenterDegrees(fallback, out float fallbackCenterDegrees)) {
                // Keep the current facing while the heading stays within the fallback sector widened by the
                // hysteresis margin (sector half-width is 22.5deg). This adds a deadband around each boundary.
                if (Mathf.Abs(Mathf.DeltaAngle(angle, fallbackCenterDegrees)) <= 22.5f + hysteresisDegrees) {
                    return fallback;
                }
            }

            int octant = Mathf.RoundToInt(angle / 45f) % 8;
            return octant switch {
                0 => RoActorDirection.North,
                1 => RoActorDirection.NorthEast,
                2 => RoActorDirection.East,
                3 => RoActorDirection.SouthEast,
                4 => RoActorDirection.South,
                5 => RoActorDirection.SouthWest,
                6 => RoActorDirection.West,
                7 => RoActorDirection.NorthWest,
                _ => fallback
            };
        }

        private static bool TryGetDirectionCenterDegrees(RoActorDirection direction, out float degrees) {
            switch (direction) {
                case RoActorDirection.North: degrees = 0f; return true;
                case RoActorDirection.NorthEast: degrees = 45f; return true;
                case RoActorDirection.East: degrees = 90f; return true;
                case RoActorDirection.SouthEast: degrees = 135f; return true;
                case RoActorDirection.South: degrees = 180f; return true;
                case RoActorDirection.SouthWest: degrees = 225f; return true;
                case RoActorDirection.West: degrees = 270f; return true;
                case RoActorDirection.NorthWest: degrees = 315f; return true;
                default: degrees = 0f; return false;
            }
        }

        public static RoActorDirection GetCameraDirectionFromYaw(float yawDegrees) {
            Vector3 cameraForward = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
            float horizontalAngle = Mathf.Atan2(cameraForward.z, cameraForward.x) * Mathf.Rad2Deg;
            float normalized = (horizontalAngle + 22.5f) / 360f;
            normalized -= Mathf.Floor(normalized);

            int direction = PositiveModulo(Mathf.FloorToInt((normalized - 0.25f) * 8f), 8);
            return (RoActorDirection) direction;
        }

        private static int PositiveModulo(int value, int divisor) {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static bool TryFindMotion(RoSpriteMotionAction[] table, RoSpriteMotion motion, out int baseAction) {
            for (int i = 0; i < table.Length; i++) {
                if (table[i].Motion == motion) {
                    baseAction = table[i].BaseAction;
                    return true;
                }
            }

            baseAction = 0;
            return false;
        }
    }

    public readonly struct RoSpriteMotionAction {
        public RoSpriteMotionAction(RoSpriteMotion motion, int baseAction) {
            Motion = motion;
            BaseAction = baseAction;
        }

        public RoSpriteMotion Motion { get; }
        public int BaseAction { get; }
    }
}
