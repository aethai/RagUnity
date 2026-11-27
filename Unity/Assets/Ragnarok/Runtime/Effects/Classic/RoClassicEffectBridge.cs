using System;
using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Audio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityCamera = UnityEngine.Camera;

namespace Ragnarok.Effects.Classic {
    /// <summary>
    /// Runs the ported original-client effect engine (Core/, oracle-verified against game.exe)
    /// inside Unity. The engine keeps projecting with the client's own math; this bridge feeds it
    /// a view matrix/projection derived from the Unity camera, so its screen-space output lands on
    /// the same pixels, then unprojects every TL vertex back to world space for drawing.
    /// </summary>
    // after the presenter (10020) and the actors' late ticks: the frame feeds the actors' final layers into the render lists
    [DefaultExecutionOrder(10100)]
    public sealed partial class RoClassicEffectBridge : MonoBehaviour, IFxHost, IFxBlackKeyTextureHost, IFxResourceProvider {
        /// <summary>Global switch: when false, effects fall back to the legacy Unity implementations.</summary>
        public static bool Enabled = true;

        /// <summary>Logs spawns and per-frame draw counts (diagnostics).</summary>
        public static bool Verbose;
        /// <summary>Diagnostics: draw with Sprites/Default instead of the STR shader.</summary>
        public static bool DebugPlainShader;
        int verboseFrames;

        /// <summary>RO world units per Unity unit (1 GAT cell = 5 RO units = 1 Unity unit).</summary>
        public const float RoUnitsPerUnity = 5f;

        /// <summary>Sign of RO world Z relative to Unity Z (to be confirmed against captures of the original).</summary>
        public static float RoZSign = 1f;

        // the frame's phases (cpuprobe)
        static readonly Unity.Profiling.ProfilerMarker FollowMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Follow");
        static readonly Unity.Profiling.ProfilerMarker AdvanceMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Advance");
        static readonly Unity.Profiling.ProfilerMarker RenderMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Render");
        static readonly Unity.Profiling.ProfilerMarker LooksMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Looks");
        static readonly Unity.Profiling.ProfilerMarker DrawMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw");
        static readonly Unity.Profiling.ProfilerMarker DrawFlushMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Flush");
        static readonly Unity.Profiling.ProfilerMarker FlushCollectMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Flush.Collect");
        static readonly Unity.Profiling.ProfilerMarker FlushListsMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Flush.Lists");
        static readonly Unity.Profiling.ProfilerMarker FlushAssignMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Flush.Assign");
        static readonly Unity.Profiling.ProfilerMarker DrawBuildMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Build");
        static readonly Unity.Profiling.ProfilerMarker DrawUploadMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Upload");
        static readonly Unity.Profiling.ProfilerMarker DrawCopyMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Build.Copy");
        static readonly Unity.Profiling.ProfilerMarker DrawMaterialMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Build.Material");
        static readonly Unity.Profiling.ProfilerMarker DrawMaterialSetMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Draw.Build.MaterialSet");
        static readonly Unity.Profiling.ProfilerMarker LaunchMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Launch");
        static readonly Unity.Profiling.ProfilerMarker ResourceMarker = new Unity.Profiling.ProfilerMarker("RO.ClassicFx.Resource");

        /// <summary>Render queue of the frame's first draw; each further draw (sub-mesh) is one higher.</summary>
        const int FaceQueue = 3100;
        const string ShaderName = "Ragnarok/RO Classic Effect";
        const uint HeapBytes = 48 * 1024 * 1024;
        /// <summary>The engine's resource caches (STR / sprite / texture objects) are never freed, like the client's
        /// CResMgr, so the heap grows with the number of distinct effects a session shows.</summary>
        const uint MaxHeapBytes = 512u * 1024 * 1024;

        struct Tracked {
            /// <summary>The host's handle: a serial, not the effect's address (the engine heap reuses addresses, so a
            /// stale address could name a newer, unrelated effect).</summary>
            public uint Handle;
            public RagEffect Effect;
            public uint Owner;
            public bool OwnsOwner;   // a throw-away object (freed with the effect) rather than an actor shim
            public Transform Follow;
            public Vector3 FollowOffset;
            public Vector3 LastPosition;
            /// <summary>Diagnostics (!fxdump): the effect id, the owner actor and when it was launched.</summary>
            public int EffectId;
            public uint OwnerAid;
            public float LaunchedAt;
        }

        /// <summary>A run of draws with the same texture and state: one sub-mesh, one material.</summary>
        struct Batch {
            public Texture Texture;
            public DrawState State;
            public int First, Count;   // draw ranges (drawRanges), in draw order
            public int VertexCount;
            /// <summary>The run of effect draws between two actor layers (one renderer, one sorting order).</summary>
            public int Segment;
        }

        /// <summary>The device state of a draw in CRenderer::Flush that a material encodes.</summary>
        struct DrawState : IEquatable<DrawState> {
            /// <summary>D3DBLEND source / destination; ONE / ZERO when the list draws without blending.</summary>
            public int Src, Dst;
            public bool ZTest, ZWrite;
            /// <summary>ALPHAREF (0..255) of the alpha test; 0 when the test is off.</summary>
            public int AlphaRef;
            /// <summary>ALPHAFUNC LESS (the second pass of list 0x180), else GREATEREQUAL.</summary>
            public bool AlphaLess;
            /// <summary>Texture stage 0 COLOROP / ALPHAOP (D3DTOP; 4 = MODULATE) the own-blend lists set per RP (mtpreset).</summary>
            public int ColorOp, AlphaOp;

            public bool Equals(DrawState o) =>
                Src == o.Src && Dst == o.Dst && ZTest == o.ZTest && ZWrite == o.ZWrite && AlphaRef == o.AlphaRef && AlphaLess == o.AlphaLess &&
                ColorOp == o.ColorOp && AlphaOp == o.AlphaOp;

            public override bool Equals(object obj) => obj is DrawState o && Equals(o);

            public override int GetHashCode() => (((((Src * 31 + Dst) * 31 + AlphaRef) * 31 + ColorOp) * 31 + AlphaOp) * 8) | (ZTest ? 4 : 0) | (ZWrite ? 2 : 0) | (AlphaLess ? 1 : 0);

            public static DrawState From(in FxRenderDrawState d) {
                bool blend = d.AlphaBlend != 0, test = d.AlphaTest != 0;
                return new DrawState {
                    Src = blend ? (int) d.SrcBlend : 2,
                    Dst = blend ? (int) d.DestBlend : 1,
                    ZTest = d.ZEnable != 0,
                    ZWrite = d.ZEnable != 0 && d.ZWrite != 0,
                    AlphaRef = test ? (int) (d.AlphaRef & 0xff) : 0,
                    AlphaLess = test && d.AlphaFunc == 2,
                    ColorOp = ExactDeviceState ? (int) d.ColorOp0 : 4,
                    AlphaOp = ExactDeviceState ? (int) d.AlphaOp0 : 4,
                };
            }
        }

        /// <summary>
        /// The draws use the device state Flush recorded as it is: the RP's D3DBLEND pair (DESTALPHA reads 1 and
        /// INVDESTALPHA 0, the client's back buffer has no alpha channel) and the texture stage 0 ops of its mtpreset.
        /// Off (!toggle fxexact off): the earlier mapping (ONE / ZERO drawn as SRCALPHA / INVSRCALPHA, SRCALPHA /
        /// DESTALPHA as ONE / ONE scaled by the diffuse alpha alone, MODULATE whatever the preset).
        /// </summary>
        public static bool ExactDeviceState = true;

        /// <summary>
        /// The client's process pass on a rendered frame without a whole 16 ms tick advances the hit effects (fire /
        /// wind / … hit, EF 0x31..0x35) and a few prim types: at the client's frame rate about once a tick, at this
        /// player's several times (the fire hit's flash lasted one 30 Hz capture frame against three in the client).
        /// Off: effects advance on ticks only. (!toggle fxtickless on: the client's pass as it is.)
        /// </summary>
        public static bool ProcessOnTicklessFrames;

        /// <summary>
        /// !toggle fxqueue on: every batch of a segment gets its own render queue in Flush order. Off (default, as before
        /// 10-04): the one shared queue, a renderer's sub-meshes in their order; with <see cref="KeyedMaterials"/> a frame
        /// then needs one material per (texture, state) (per-position queues made a material per position: 10 ms).
        /// </summary>
        public static bool OrderedQueues;

        /// <summary>One RP of the frame: its texture and its vertices (a triangle list) in the frame's vertex buffer.</summary>
        struct PendingRp {
            public Texture Texture;
            public int First, Count;
            /// <summary>1 + the index of the actor layer in <see cref="clientEntries"/> this RP stands for, 0 for an effect RP.</summary>
            public int Actor;
        }

        RoAssetDatabase assets;
        UnityCamera preferredCamera;
        FxWorld world;
        FxRuntime runtime;
        readonly List<Tracked> tracked = new List<Tracked>();
        /// <summary>New-system CEffects launched through <see cref="TrySpawn(int, Vector3, in RoEffectSpawnOptions, out uint)"/>: (handle, CEffect).</summary>
        readonly List<KeyValuePair<uint, uint>> trackedNew = new List<KeyValuePair<uint, uint>>();
        uint nextHandle;
        readonly RoClassicActorLookRenderer lookRenderer = new RoClassicActorLookRenderer();
        /// <summary>Actor option bits (+0x2c0: 0x4000 Chase Walk, 0x8000 flying, 0x40 GM hide) mirrored into the shims.</summary>
        readonly Dictionary<uint, int> actorOptions = new Dictionary<uint, int>();
        /// <summary>Last frame's anchor position and the walk state written to +0x70, per actor shim.</summary>
        readonly Dictionary<uint, (Vector3 position, bool walking)> actorMotion = new Dictionary<uint, (Vector3, bool)>();
        readonly Dictionary<uint, Transform> actorAnchors = new Dictionary<uint, Transform>();
        readonly List<uint> staleAnchors = new List<uint>();

        /// <summary>An actor's level aura: its pair (main, sub), whether hide / cloak / invisibility blocks it, and the ids
        /// launched for it (the client's stamp +0x2b8 keeps a repeated dispatch from relaunching).</summary>
        struct LevelAura {
            public int Main, Sub;
            public bool Blocked;
            public int ShownMain, ShownSub;
        }

        readonly Dictionary<uint, LevelAura> levelAuras = new Dictionary<uint, LevelAura>();

        /// <summary>The map's RSW effect records waiting for the engine (loaded on the first frame with assets).</summary>
        List<RswEffectRecord> pendingMapEffects;
        string pendingMapName;
        /// <summary>Diagnostics (!fxdump): what the last map load created.</summary>
        string lastMapEffects = "";
        /// <summary>The client's Lua state for the EffectTool scripts lives for the whole session.</summary>
        readonly EffectToolLuaState effectToolState = new EffectToolLuaState();
        readonly List<uint> levelAuraActors = new List<uint>();
        int levelAuraSettingsApplied;
        bool levelAurasRelaunch, levelAurasPending;
        static bool showLevelAura = true, simpleLevelAura;
        static int levelAuraSettingsVersion;

        /// <summary>/aura2 ([0x1552d3c]): the level aura at all. A change re-dispatches every actor's aura.</summary>
        public static bool ShowLevelAura {
            get => showLevelAura;
            set {
                if (showLevelAura != value) {
                    showLevelAura = value;
                    levelAuraSettingsVersion++;
                }
            }
        }

        /// <summary>/aura, the graphics option SIMPLE_AURA ([0x1552d38] != 1): only the aura's sub id (the lights).</summary>
        public static bool SimpleLevelAura {
            get => simpleLevelAura;
            set {
                if (simpleLevelAura != value) {
                    simpleLevelAura = value;
                    levelAuraSettingsVersion++;
                }
            }
        }
        readonly Dictionary<uint, Texture2D> texturesByHandle = new Dictionary<uint, Texture2D>();
        readonly Dictionary<string, uint> handlesByName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<FxSprImage, Texture2D> spriteTextures = new Dictionary<FxSprImage, Texture2D>();
        readonly List<Batch> batches = new List<Batch>();
        /// <summary>The frame's RPs in submission order (the index is the RP's address for the depth sorts' tie-break).</summary>
        readonly List<PendingRp> pendingRps = new List<PendingRp>();
        /// <summary>AddRP (0x537b00) / CRenderer::Flush (0x539f90): the client's 16 render lists (reference render-lists).</summary>
        readonly FxRenderLists renderLists = new FxRenderLists();
        /// <summary>The device Flush draws on: its state persists across frames like the client's D3D device.</summary>
        readonly FxD3D7State device = FxD3D7State.DeviceDefaults();
        /// <summary>Texture identities for the texture-sorted lists and the SetTexture cache (0 = none).</summary>
        readonly Dictionary<Texture, uint> textureKeys = new Dictionary<Texture, uint>(ReferenceComparer<Texture>.Instance);
        /// <summary>
        /// One effect vertex as the client hands it to D3D7 (a TL vertex): screen x / y in pixels and rhw, the diagonal
        /// colour, texture coordinates. The vertex shader projects it (RoClassicEffect.shader), so the CPU no longer turns
        /// every vertex back into a world position, and a frame's vertices go up in one interleaved stream.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct FxVertex {
            public float X, Y, Rhw;
            public Color32 Color;
            public Vector2 Uv;
        }

        static readonly VertexAttributeDescriptor[] FxVertexLayout = {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };

        /// <summary>
        /// The frame's effect vertices, in submission order, in a plain array: ~15 000 faces a frame with 300 effects made
        /// the per-vertex List.Add calls and the per-index copy into the segment lists a measurable part of the draw.
        /// </summary>
        FxVertex[] frameVertices = new FxVertex[4096];
        int vertexCount;
        /// <summary>All of a frame's geometry in one dynamic mesh: one sub-mesh (and one pooled material) per batch.</summary>
        Mesh frameMesh;
        MeshRenderer frameRenderer;
        readonly List<SubMeshDescriptor> subMeshes = new List<SubMeshDescriptor>();
        readonly List<Material> frameMaterials = new List<Material>();
        readonly List<Material> materialPool = new List<Material>();
        /// <summary>Decode buffer reused by every SPR image converted to a texture.</summary>
        Color32[] spritePixels = new Color32[0];
        /// <summary>The vertex range of every effect draw in Flush order (each RP's vertices are contiguous).</summary>
        readonly List<VertexRange> drawRanges = new List<VertexRange>(4096);

        readonly struct VertexRange {
            public VertexRange(int start, int count) {
                Start = start;
                Count = count;
            }

            public readonly int Start;
            public readonly int Count;
        }
        /// <summary>The frame's actor layers in the client's render lists (RoClientDrawOrder) and whether Flush reached them.</summary>
        readonly List<Ragnarok.Entities.RoClientDrawEntry> clientEntries = new List<Ragnarok.Entities.RoClientDrawEntry>(1024);
        readonly List<bool> clientEntrySeen = new List<bool>(1024);
        /// <summary>The sorting order of each run of effect draws (segment), in Flush order.</summary>
        readonly List<int> segmentOrders = new List<int>(64);
        readonly List<Mesh> segmentMeshes = new List<Mesh>();
        readonly List<MeshRenderer> segmentRenderers = new List<MeshRenderer>();
        FxVertex[] segmentVertices = new FxVertex[4096];
        /// <summary>The vertex / index capacity each segment mesh was set up with (the identity index buffer only goes up when it grows).</summary>
        readonly List<int> segmentMeshCapacity = new List<int>();
        /// <summary>0, 1, 2 ... : a segment's vertices are laid out in draw order, so its index buffer is the identity.</summary>
        static int[] identityIndices = new int[0];
        static readonly int FxScreenId = Shader.PropertyToID("_RoFxScreen");
        static readonly int FxCameraToWorldId = Shader.PropertyToID("_RoFxCameraToWorld");
        UnityCamera frameCamera;
        /// <summary>Per-frame camera data: read once in RunFrame instead of once per vertex (native calls).</summary>
        Matrix4x4 frameCameraToWorld;
        /// <summary>The projection of the vertices about to be drawn (the frame the core rendered them in).</summary>
        Vector4 drawScreen;
        Matrix4x4 drawCameraToWorld;
        /// <summary>Time.time of the frame, for code the render job may reach (Time is main-thread only).</summary>
        float frameTime;
        /// <summary>Ground-cell quad textures to switch to CLAMP before the draw (texture state is main-thread only).</summary>
        readonly List<Texture> pendingClamp = new List<Texture>();
        /// <summary>Texture2D.whiteTexture, read once on the main thread.</summary>
        Texture2D whiteTexture;
        int frameCenterX, frameCenterY;
        float frameXFactor, frameYFactor;
        /// <summary>The last texture object resolved this frame (consecutive faces mostly share one).</summary>
        uint lastTextureObject;
        Texture lastResolvedTexture;
        Texture lastKeyedTexture;
        uint lastTextureKey;
        float frameNearClip;
        /// <summary>D3DPT_TRIANGLESTRIP (0,1,2) (1,3,2) expanded to a list.</summary>
        static readonly int[] QuadOrder = { 0, 1, 2, 1, 3, 2 };
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int BlendSrcId = Shader.PropertyToID("_RoBlendSrc");
        static readonly int BlendDstId = Shader.PropertyToID("_RoBlendDst");
        static readonly int LookFxGammaBlendId = Shader.PropertyToID("_RoLookFxGammaBlend");
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        static readonly int FogEnabledId = Shader.PropertyToID("_RoFogEnabled");
        static readonly int AlphaRefId = Shader.PropertyToID("_RoAlphaRef");
        static readonly int AlphaLessId = Shader.PropertyToID("_RoAlphaLess");
        readonly HashSet<uint> aliveEffects = new HashSet<uint>();
        readonly HashSet<Texture> clampedTextures = new HashSet<Texture>(ReferenceComparer<Texture>.Instance);
        /// <summary>The state each pooled material was last given, so unchanged batches skip the Set* calls.</summary>
        readonly List<(Texture texture, DrawState state, int queue)> materialState = new List<(Texture, DrawState, int)>();
        Shader shader;

        public static RoClassicEffectBridge Instance { get; private set; }
        public int ActiveEffects => world?.EffectCount ?? 0;
        public int ActivePrims => world?.PrimCount ?? 0;

        /// <summary>
        /// The one engine instance. It lives at the scene root, independent of any map: a new map installs its frame
        /// and ground on it (<see cref="SetMapFrame"/>, <see cref="SetGround"/>) while the previous map may still be
        /// alive for a frame, so parenting it to a map's effect root would lose the new ground when the old map goes.
        /// </summary>
        public static RoClassicEffectBridge GetOrCreate(Transform parent) {
            if (Instance != null) {
                return Instance;
            }

            var go = new GameObject("RO Classic Effect Engine");
            if (Application.isPlaying) {
                DontDestroyOnLoad(go);
            } else if (parent != null) {
                go.transform.SetParent(parent, false);   // edit-mode tools / tests clean up through their root
            }

            Instance = go.AddComponent<RoClassicEffectBridge>();
            return Instance;
        }

        public void Initialize(RoAssetDatabase assetDatabase, UnityCamera camera) {
            // callers initialise on every launch: the same database and camera again change nothing the render job reads
            if (orphansSwept && ReferenceEquals(assets, assetDatabase) && ReferenceEquals(preferredCamera, camera)) {
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (assetDatabase != null && assets != assetDatabase) {
                DamageSkins = RoDamageSkinCatalog.Load(assetDatabase);
                appliedDamageSkin = -1;
            }

            assets = assetDatabase;
            preferredCamera = camera;
            // callers initialise on every use (actor options, launches ...); the orphan sweep scans every MeshRenderer
            // in memory (~3 ms), so it runs once per bridge
            if (!orphansSwept) {
                orphansSwept = true;
                DestroyOrphanedRenderers();
            }

            RoClassicEngineWarmUp.Start(LoadEffectFile);
        }

        bool orphansSwept;

        void EnsureWorld() {
            if (world != null) {
                return;
            }

            var memory = new FxMemory(HeapBytes, FxMemory.DefaultBase, MaxHeapBytes);
            runtime = new FxRuntime(memory, new MsvcRand((uint) Environment.TickCount)) { Host = this };
            runtime.Renderer.InitQuadPool(64);
            onFace = OnFace;
            onQuad = OnQuad;
            onSprite = OnSprite;
            onFaceTimed = (face, flags) => {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                OnFace(face, flags);
                Ragnarok.Diagnostics.RoSkillSweepProbe.AddSubmitTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            };
            onQuadTimed = (quad, flags) => {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                OnQuad(quad, flags);
                Ragnarok.Diagnostics.RoSkillSweepProbe.AddSubmitTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            };
            onSpriteTimed = draw => {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                OnSprite(draw);
                Ragnarok.Diagnostics.RoSkillSweepProbe.AddSubmitTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            };
            SetSubmitTiming(false);
            // sprite/act resources (CResMgr shim): client paths arrive as engine byte strings
            runtime.LoadDataFile = path => {
                if (!OnMainThread) {
                    // the asset database is read on the main thread (the render job waits, once per file)
                    byte[] read = null;
                    RunOnMain(() => read = runtime.LoadDataFile(path));
                    return read;
                }

                using var resource = ResourceMarker.Auto();
                long start = Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                byte[] bytes = assets?.ReadBytes(FromEngine(path));
                if (start != 0) Ragnarok.Diagnostics.RoSkillSweepProbe.AddResourceTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
                return bytes;
            };
            runtime.ResourceClock = (uint) Environment.TickCount;
            runtime.TimeGetTimeImpl = () => (uint) Environment.TickCount;
            // effects launched by the engine itself (prim types 0x6d / 0x78 on actors)
            runtime.LaunchEffectOnImpl = (actor, efId) => world.Launch(efId, actor)?.A ?? 0u;
            // CBlurPC (Snap, Mirror Image): a frozen tinted copy of the source actor at the object's position;
            // msg 0 carries the colour (r, g, b) as the three 64-bit parameters
            runtime.BlurPcSpawnedImpl = (obj, source, msg, r, g, b) => {
                if (msg != 0) {
                    return;
                }

                // the engine's state is read here, the after-image is made on the main thread (queued from the render job)
                Transform actor = null;
                foreach (KeyValuePair<uint, Transform> kv in actorAnchors) {
                    if (world.TryGetActor(kv.Key, out uint shim) && shim == source) {
                        actor = kv.Value;
                        break;
                    }
                }

                FxMemory m = runtime.M;
                Vector3 position = ToUnity(new Vector3(m.F32(obj + 0x10), m.F32(obj + 0x14), m.F32(obj + 0x18)));
                var tint = new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
                PostToMain(() => {
                    if (Application.isPlaying) RoClassicAfterImage.Spawn(actor, position, tint);
                });
            };
            // the renderer's sprite-image texture cache (CSpriteParticle, EF 44 chimney smoke): a host handle per image
            runtime.SprTextureHandleImpl = SpriteImageTextureHandle;
            // CGameMode::SendMsg: 0x7a = screen quake (Meteor Storm, Hammerfall, Stave Crasher, …)
            runtime.GameModeSendMsgImpl = (msg, a1, a2, a3, a4) => PostToMain(() => {
                if (msg == 0x7a && Application.isPlaying) {
                    Ragnarok.Camera.RoCameraQuake.Start(a1);
                }

                // 0x21: the local player's emotion balloon ended (or ZC_EMOTION found no record): mode +0x284 = 0
                if (msg == 0x21) Ragnarok.UI.RoEmotionSender.EmotionDone();
            });
            // FUN_00c028a0: a ground effect at a point for a launcher lifetime (footprints of Chase Walk / TK_RUN): the
            // walk direction goes to f_180 before the first tick
            runtime.GroundEffectImpl = (x, y, z, ef, lifetime, angle) => LaunchGround(x, y, z, ef, angle);
            // an effect sending an actor message to its owner: 0x6d removes the oldest instance of an id (EF 62 → 22)
            runtime.OwnerSendMsgImpl = (owner, args) => {
                if (args != null && args.Length > 3 && args[1] == 0x6d) {
                    world.RemoveEffectById(owner, (int) args[3]);
                }
            };
            world = new FxWorld(runtime, this);
        }

        /// <summary>
        /// FUN_00c028a0: an effect at a point on a CDummyEffectLauncher, its heading <paramref name="angle"/> (also written
        /// to f_180 before the first tick). The client's holder lives a set number of 24 ms ticks (footprints, vanish 300 /
        /// 600), always longer than the effects it carries, so here the holder goes with the effect.
        /// </summary>
        bool LaunchGround(float x, float y, float z, int ef, float angle) => LaunchGround(x, y, z, ef, angle, 0, out _);

        /// <summary>The next effect handle (never 0).</summary>
        uint NextHandle() {
            if (++nextHandle == 0) nextHandle = 1;
            return nextHandle;
        }

        bool LaunchGround(float x, float y, float z, int ef, float angle, uint reservedHandle, out uint handle) {
            handle = 0;
            uint owner = world.CreateOwner(x, y, z);
            world.SetOwnerHeading(owner, angle);
            RagEffect? launched = world.Launch(ef, owner);
            if (!launched.HasValue) {
                runtime.M.Free(owner);
                return false;
            }

            RagEffect effect = launched.Value;
            effect.f_180 = angle;
            handle = reservedHandle != 0 ? reservedHandle : NextHandle();
            tracked.Add(new Tracked {
                Handle = handle,
                Effect = effect,
                Owner = owner,
                OwnsOwner = true,
                LastPosition = ToUnity(new Vector3(x, y, z)),
                EffectId = ef,
                LaunchedAt = frameTime,   // the render job may launch ground effects (footprints): Time is main-thread only
            });
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"classic {ef} ground angle={angle}");
            return true;
        }

        /// <summary>
        /// A ground effect where an actor was (FUN_00c028a0(position, id, life, 0)): ZC_NOTIFY_VANISH's EF 314 / 304 and the
        /// local player's EF_DEVIL. It stays at the point; the actor may be gone already.
        /// </summary>
        public bool LaunchGroundEffect(int effectId, Vector3 worldPosition) => LaunchGroundEffect(effectId, worldPosition, out _);

        /// <summary><see cref="LaunchGroundEffect(int, Vector3)"/> returning the effect's handle for <see cref="EndEffect"/>.</summary>
        public bool LaunchGroundEffect(int effectId, Vector3 worldPosition, out uint handle) {
            handle = 0;
            if (!CanHandle(effectId) || assets == null) {
                return false;
            }

            if (ShouldDefer) {
                uint reserved = NextHandle();
                handle = reserved;
                deferredHandles.Add(reserved);
                DeferLaunchGroundEffect(effectId, worldPosition, reserved);
                return true;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            EnsureWorld();
            Vector3 ro = ToRo(worldPosition);
            return LaunchGround(ro.x, ro.y, ro.z, effectId, 0f, 0, out handle);
        }

        /// <summary>
        /// The actor's level aura (combat-timing §7.2): <paramref name="mainId"/> / <paramref name="subId"/> from
        /// RoOriginalCombatRules.LevelAuraIds (0 / 0 = none), off while <paramref name="blocked"/> (hide, cloak,
        /// invisibility) or with /aura2, the sub id alone with /aura. A change takes the shown ids off (RemoveAll
        /// FUN_00bcfa70 → msg 0x6d → FUN_00a4d760) and launches the new ones like the actor message handlers 0xbdb9b6 /
        /// 0xbdbc73: FUN_00a4d090(AID, id, position, target 0) on the actor's shim, which they follow until switched off or
        /// until the actor goes. The same state again does nothing, so a running aura keeps its particles.
        /// </summary>
        public void SetLevelAura(uint aid, Transform anchor, int mainId, int subId, bool blocked) {
            // "no aura" for an actor without one changes nothing (the presenter sent it on every sync of an aura-less
            // actor: ~100 calls a frame in !woe 500); the render job never touches levelAuras, so no wait for that
            if (aid == 0 || (mainId == 0 && subId == 0 && !levelAuras.ContainsKey(aid))) {
                return;
            }

            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferSetLevelAura(aid, anchor, mainId, subId, blocked);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (aid == 0) {
                return;
            }

            bool known = levelAuras.TryGetValue(aid, out LevelAura aura);
            if (!known && mainId == 0 && subId == 0) {
                return;
            }

            if (anchor != null) {
                actorAnchors[aid] = anchor;
            }

            aura.Main = mainId;
            aura.Sub = subId;
            aura.Blocked = blocked;
            ApplyLevelAura(aid, ref aura);
            if (mainId == 0 && subId == 0) {
                levelAuras.Remove(aid);
            } else {
                levelAuras[aid] = aura;
            }
        }

        void ApplyLevelAura(uint aid, ref LevelAura aura) {
            bool on = !aura.Blocked && showLevelAura;
            int main = on && !simpleLevelAura ? aura.Main : 0;
            int sub = on ? aura.Sub : 0;
            if (main == aura.ShownMain && sub == aura.ShownSub) {
                return;
            }

            if (world != null && world.TryGetActor(aid, out uint actor)) {
                if (aura.ShownMain != 0) world.RemoveNewEffect(actor, aura.ShownMain);
                if (aura.ShownSub != 0) world.RemoveNewEffect(actor, aura.ShownSub);
            }

            aura.ShownMain = 0;
            aura.ShownSub = 0;
            if (main == 0 && sub == 0) {
                return;
            }

            if (assets == null || !actorAnchors.TryGetValue(aid, out Transform anchor) || anchor == null) {
                levelAurasPending = true;   // tried again each frame until the bridge has its assets and the actor's anchor
                return;
            }

            EnsureWorld();
            Vector3 ro = ToRo(anchor.position);
            uint shim = world.EnsureActor(aid, ro.x, ro.y, ro.z);
            // the shown ids are kept even when an id has no class here (1226, 2275 ...): it is not retried every call
            if (main != 0) world.LaunchNew(main, shim, 0);
            if (sub != 0) world.LaunchNew(sub, shim, 0);
            aura.ShownMain = main;
            aura.ShownSub = sub;
        }

        /// <summary>Every actor's aura again: after an /aura or /aura2 change, or a map change (ClearNewEffects ended them).</summary>
        void RefreshLevelAuras() {
            levelAurasPending = false;
            levelAuraActors.Clear();
            levelAuraActors.AddRange(levelAuras.Keys);
            foreach (uint aid in levelAuraActors) {
                LevelAura aura = levelAuras[aid];
                if (levelAurasRelaunch) {
                    aura.ShownMain = 0;
                    aura.ShownSub = 0;
                }

                ApplyLevelAura(aid, ref aura);
                levelAuras[aid] = aura;
            }

            levelAurasRelaunch = false;
            levelAuraSettingsApplied = levelAuraSettingsVersion;
        }

        /// <summary>
        /// FUN_00cf1210's part after a skin change (startup / Apply): the skin's BaseNumber / BaseMsg / BaseBlue become the
        /// CMsgEffect sprite names, DSMillionUnit the digit layout mode; the blue burst uses its own .act on any skin but
        /// the default. Glyphs made afterwards use it.
        /// </summary>
        void ApplyDamageSkin() {
            appliedDamageSkin = DamageSkin;
            RoDamageSkin skin = DamageSkins.Get(DamageSkin);
            runtime.DamageNumberSprite = ToEngine(skin.BaseNumber);
            runtime.DamageMessageSprite = ToEngine(skin.BaseMessage);
            runtime.DamageBlueMessageSprite = ToEngine(skin.BaseBlue);
            runtime.DamageBlueMessageOwnAct = skin.Id != 0;
            runtime.DamageNumberMode = skin.MillionUnit;
        }

        /// <summary>Unicode → engine byte string (one char per CP949 byte), the inverse of <see cref="FromEngine"/>.</summary>
        public static string ToEngine(string text) {
            if (string.IsNullOrEmpty(text)) {
                return text ?? string.Empty;
            }

            byte[] bytes = RoTextEncoding.GetKoreanEncoding().GetBytes(text);
            var chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) {
                chars[i] = (char) bytes[i];
            }

            return new string(chars);
        }

        /// <summary>
        /// Engine byte string (one char per CP949 byte, the client's own string convention) → Unicode.
        /// Names the engine hands the host (textures, wavs, sprite paths) are in that form.
        /// </summary>
        public static string FromEngine(string byteString) {
            if (string.IsNullOrEmpty(byteString)) {
                return byteString;
            }

            bool ascii = true;
            for (int i = 0; i < byteString.Length; i++) {
                if (byteString[i] > 0x7f) {
                    ascii = false;
                    break;
                }
            }

            if (ascii) {
                return byteString;
            }

            var bytes = new byte[byteString.Length];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = (byte) byteString[i];
            }

            RoTextEncoding.EnsureCodePagesRegistered();
            return System.Text.Encoding.GetEncoding(RoTextEncoding.KoreanCodePage).GetString(bytes);
        }

        public static bool CanHandle(int effectId) => Enabled && FxWorld.IsPorted(effectId);

        /// <summary>The local player's account id (the client's DAT_0154f2b4): homing prims compare their target with it.</summary>
        public uint LocalPlayerId {
            get => runtime?.PlayerAid ?? 0;
            set {
                // set on every effect-manager lookup (several times a frame): the same value leaves the engine alone, so
                // it does not wait for a running render job (the job never writes this field)
                if (runtime != null && runtime.PlayerAid == value) return;
                WaitForRenderJob();
                EnsureWorld();
                runtime.PlayerAid = value;
            }
        }

        /// <summary>Starts an effect id at a Unity world position, optionally following a transform.</summary>
        public bool TrySpawn(int effectId, Vector3 worldPosition, Transform follow = null, Vector3 followOffset = default)
            => TrySpawn(effectId, worldPosition, new RoEffectSpawnOptions { FollowTarget = follow, FollowLocalOffset = followOffset });

        /// <summary>
        /// Starts an effect id the way the client's CAbleToMakeEffect::LaunchEffect + SendMsg sequence does: the
        /// effect is launched on an owner object at <paramref name="worldPosition"/> (the caster's or target's actor
        /// shim when an id is known, so homing prims and after-images find it), then receives the skill level
        /// (+0x174), the aim position (+0x180.., the other end of the cast) and the owner's heading (+0x14c).
        /// </summary>
        public bool TrySpawn(int effectId, Vector3 worldPosition, in RoEffectSpawnOptions options) =>
            TrySpawn(effectId, worldPosition, options, out _);

        /// <summary>
        /// <see cref="TrySpawn(int, Vector3, in RoEffectSpawnOptions)"/> returning the launched effect's handle (its
        /// CRagEffect address) for <see cref="EndEffect"/> / <see cref="IsEffectAlive"/>.
        /// </summary>
        public bool TrySpawn(int effectId, Vector3 worldPosition, in RoEffectSpawnOptions options, out uint handle) {
            if (ShouldDefer && CanHandle(effectId) && assets != null) {
                // the render job is running: launched at the bridge's LateUpdate (or the next call that needs the engine),
                // with the handle reserved now
                uint reserved = NextHandle();
                handle = reserved;
                RoEffectSpawnOptions copy = options;
                deferredHandles.Add(reserved);
                DeferTrySpawn(effectId, worldPosition, copy, reserved);
                return true;
            }

            return SpawnNow(effectId, worldPosition, options, 0, out handle);
        }

        bool SpawnNow(int effectId, Vector3 worldPosition, in RoEffectSpawnOptions options, uint reservedHandle, out uint handle) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            using var launch = LaunchMarker.Auto();
            if (!Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                return Spawn(effectId, worldPosition, options, reservedHandle, out handle);
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            bool spawned = Spawn(effectId, worldPosition, options, reservedHandle, out handle);
            Ragnarok.Diagnostics.RoSkillSweepProbe.RecordLaunch(effectId, System.Diagnostics.Stopwatch.GetTimestamp() - start);
            return spawned;
        }

        bool Spawn(int effectId, Vector3 worldPosition, in RoEffectSpawnOptions options, uint reservedHandle, out uint handle) {
            handle = 0;
            if (!CanHandle(effectId) || assets == null) {
                return false;
            }

            EnsureWorld();
            Transform follow = options.FollowTarget;
            Vector3 aim = options.HasTargetWorldPosition ? options.TargetWorldPosition
                : options.HasSourceWorldPosition ? options.SourceWorldPosition
                : options.SourceTarget != null ? options.SourceTarget.position
                : worldPosition;
            Vector3 ro = ToRo(worldPosition), roAim = ToRo(aim);

            // the owner: the actor the effect follows (its shim by account id) or a throw-away object at the spawn point
            // (a ground cell spawn follows nothing and gets a throw-away owner at the cell)
            uint ownerAid = 0;
            if (follow != null) {
                bool followsSource = options.SourceTarget != null && follow == options.SourceTarget;
                ownerAid = followsSource ? options.SourceActorId : options.TargetActorId;
            } else if (options.OnActorId != 0 && options.ClassicOwner == 0) {
                // launched on an actor that is not presented yet: still the actor's (its shim at the position), checked
                // by ForgetGoneActors, so the effect ends when the actor leaves even if it never got a transform
                ownerAid = options.OnActorId;
                unanchoredActors.Add(ownerAid);
            }

            if (options.SourceActorId != 0 && options.SourceTarget != null) {
                actorAnchors[options.SourceActorId] = options.SourceTarget;
            }

            if (ownerAid != 0 && follow != null) {
                actorAnchors[ownerAid] = follow;
            }

            uint owner;
            bool ownsOwner;
            if (options.ClassicOwner != 0) {
                owner = options.ClassicOwner;
                ownsOwner = false;
                ownerAid = 0;
            } else if (ownerAid != 0) {
                owner = world.EnsureActor(ownerAid, ro.x, ro.y, ro.z);
                ownsOwner = false;
            } else {
                owner = world.CreateOwner(ro.x, ro.y, ro.z);
                ownsOwner = true;
            }

            // the other actor of the cast, so FindActorByAid resolves it
            uint otherAid = ownerAid == options.SourceActorId ? options.TargetActorId : options.SourceActorId;
            if (otherAid != 0 && otherAid != ownerAid) {
                world.EnsureActor(otherAid, roAim.x, roAim.y, roAim.z);
            }

            // heading: the client turns the caster towards the target; FaceTowards semantics (0 = -z, 90 = +x)
            float dx = roAim.x - ro.x, dz = roAim.z - ro.z;
            if (dx != 0f || dz != 0f) {
                float heading = Mathf.Atan2(dx, -dz) * Mathf.Rad2Deg;
                world.SetOwnerHeading(owner, heading < 0f ? heading + 360f : heading);
            }

            if (FxWorld.IsNewSystem(effectId)) {
                // the client asks CEffectMgr first (FUN_00a4d090) and uses LaunchEffect only when it declines, so for
                // these ids (Heal 312/313/325, thrown items, Storm Kick, Pneuma ...) the new system's version is shown
                uint targetAid = otherAid != 0 && otherAid != ownerAid ? otherAid : FxWorld.NoTarget;
                uint created = world.LaunchNew(effectId, owner, targetAid);
                if (ownsOwner) {
                    runtime.M.Free(owner);   // the new system refers to actors by account id only
                }

                if (created == 0) {
                    return false;
                }

                if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"classic {effectId} aid={ownerAid} level={options.SkillLevel} new");
                handle = reservedHandle != 0 ? reservedHandle : NextHandle();
                trackedNew.Add(new KeyValuePair<uint, uint>(handle, created));
                return true;
            }

            RagEffect? launched = world.Launch(effectId, owner);
            if (!launched.HasValue) {
                if (ownsOwner) {
                    runtime.M.Free(owner);
                }

                return false;
            }

            RagEffect effect = launched.Value;
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"classic {effectId} aid={ownerAid} level={options.SkillLevel}");
            if (Verbose) {
                Debug.Log($"[ClassicFx] spawn EF {effectId} owner=0x{owner:x8} aid={ownerAid} at {worldPosition} ro={ro} aim={roAim} level={options.SkillLevel} cam={(preferredCamera != null ? preferredCamera.name : "null")}");
            }
            if (options.SkillLevel > 0) {
                effect.f_174 = options.SkillLevel;
            }

            effect.f_180 = roAim.x;
            effect.f_184 = roAim.y;
            effect.f_188 = roAim.z;

            if (options.ClassicDurationTicks > 0) {
                effect.f_17c = options.ClassicDurationTicks;   // vf18 SetDuration
            }

            handle = reservedHandle != 0 ? reservedHandle : NextHandle();
            tracked.Add(new Tracked {
                Handle = handle,
                Effect = effect,
                Owner = owner,
                OwnsOwner = ownsOwner,
                Follow = follow,
                FollowOffset = options.FollowLocalOffset,
                LastPosition = worldPosition,
                EffectId = effectId,
                OwnerAid = ownerAid,
                LaunchedAt = Time.time,
            });
            return true;
        }

        /// <summary>
        /// The RO frame: RO units = Unity units × 5, y down, and the origin at the map's centre (the client centres
        /// the GAT on the origin; FUN_006c32a0 GetHeight depends on it). Set from the map controller on map load.
        /// </summary>
        public void SetMapFrame(Vector3 mapCentreWorld, RoZSignKind zSign = RoZSignKind.Same) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            mapCentre = mapCentreWorld;
            RoZSign = zSign == RoZSignKind.Same ? 1f : -1f;
        }

        public enum RoZSignKind { Same, Flipped }

        static Vector3 mapCentre;

        public static Vector3 ToRo(Vector3 u) => new Vector3((u.x - mapCentre.x) * RoUnitsPerUnity, -(u.y - mapCentre.y) * RoUnitsPerUnity, (u.z - mapCentre.z) * RoUnitsPerUnity * RoZSign);
        public static Vector3 ToUnity(Vector3 r) => new Vector3(r.x / RoUnitsPerUnity + mapCentre.x, -r.y / RoUnitsPerUnity + mapCentre.y, r.z / RoUnitsPerUnity * RoZSign + mapCentre.z);

        /// <summary>
        /// Installs the loaded map's GAT into the engine world (GetHeight for bouncing / ground-hugging prims).
        /// <paramref name="cornerHeights"/>: four corners per cell in RO units, y down (the GAT file's raw values),
        /// row-major from the map's first row.
        /// </summary>
        public void SetGround(int width, int height, float[] cornerHeights, uint[] cellTypes) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            EnsureWorld();
            world.ClearNewEffects();
            trackedNew.Clear();
            lookRenderer.Clear();
            levelAurasRelaunch = levelAuras.Count > 0;
            world.SetGround(width, height, cornerHeights, cellTypes);
        }

        /// <summary>
        /// The RSW effect records of the map just installed (object type 4, file order — the order the client's world
        /// builder FUN_009f9770 rolls rand() in), with <paramref name="mapName"/> as the server names the map ("prontera").
        /// The previous map's makers go now; the new ones are created on the next frame with assets: one CEffectMaker per
        /// classic id, one ownerless new-system effect per 974 / 44 record, then the map's EffectTool script configures the
        /// 974 emitters (reference packages/mapfx §7).
        /// </summary>
        public void SetMapEffects(string mapName, IReadOnlyList<RswEffectRecord> records) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            EnsureWorld();
            world.ClearMapEffects();
            pendingMapName = mapName;
            pendingMapEffects = records != null ? new List<RswEffectRecord>(records) : null;
        }

        void LoadPendingMapEffects() {
            List<RswEffectRecord> records = pendingMapEffects;
            pendingMapEffects = null;
            MapEffectsLoadResult load = world.LoadMapEffects(records);
            string rsw = pendingMapName.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase) ? pendingMapName : pendingMapName + ".rsw";
            string key = EffectToolScript.MapKey(rsw);
            string folder = "data\\luafiles514\\lua files\\effecttool\\";
            byte[] script = assets.ReadBytes(folder + key + ".lub") ?? assets.ReadBytes(folder + key + ".lua");
            EffectToolResult tool = world.ApplyEffectTool(rsw, script, effectToolState);
            lastMapEffects = $" mapfx {pendingMapName}: {records.Count} rec, {load.Makers} makers, {load.NewSystemLaunched} new, tool {tool.Outcome}" +
                (records.Count > 0 ? $", rec0 id {records[0].Id} ({records[0].X:F1},{records[0].Y:F1},{records[0].Z:F1}) emit {records[0].EmitSpeed:F2}" : "");
            if (Verbose || Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                string line = $"map effects {pendingMapName}: {records.Count} records, {load.Makers} makers, {load.NewSystemLaunched} new-system" +
                    $" (unported: makers [{string.Join(",", load.UnportedMakerIds)}], new [{string.Join(",", load.NewSystemSkippedIds)}]), effecttool {tool.Outcome}";
                if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record(line);
                if (Verbose) Debug.Log("[ClassicFx] " + line);
            }
        }

        /// <summary>Diagnostics: the first ownerless EF 44 (its RO position, out-of-range flag, drawn particles) and the player object.</summary>
        string DescribeFirstMapSmoke() {
            FxMemory m = runtime.M;
            // the ownerless EF 44 nearest the player (owner 0's list in the effect manager's owner tree)
            uint smoke = 0;
            float best = float.MaxValue;
            uint node = runtime.EffectMgr != 0 ? runtime.TreeFind(runtime.EffectMgr + 0x10, 0) : 0;
            if (node != 0) {
                uint list = m.U32(node + 0x14);
                for (uint n = m.U32(list); n != list; n = m.U32(n)) {
                    uint e = m.U32(n + 8);
                    if (m.I32(e + 4) != 44) continue;
                    float dx = m.F32(e + 8) - m.F32(runtime.Player + 0x10), dz = m.F32(e + 0x10) - m.F32(runtime.Player + 0x18);
                    if (dx * dx + dz * dz < best) {
                        best = dx * dx + dz * dz;
                        smoke = e;
                    }
                }
            }
            string player = $" player=({m.F32(runtime.Player + 0x10):F1},{m.F32(runtime.Player + 0x14):F1},{m.F32(runtime.Player + 0x18):F1})";
            uint actor0 = runtime.FindActorByAid(0), skill0 = runtime.FindSkillObjectById_9f8c80(0);
            player += $" aid0=0x{actor0:x}/skill0=0x{skill0:x}" + (actor0 != 0 ? $" ({m.F32(actor0 + 0x10):F1},{m.F32(actor0 + 0x14):F1},{m.F32(actor0 + 0x18):F1})" : "");
            return smoke == 0 ? " smoke44=none" + player
                : $" smoke44=({m.F32(smoke + 8):F1},{m.F32(smoke + 0xc):F1},{m.F32(smoke + 0x10):F1}) out={m.U8(smoke + 0x34)} drawn={m.I32(smoke + 0xcc)}" + player;
        }

        /// <summary>One-line state for diagnostics: effects, prims, ground scopes, ground object.</summary>
        public string DescribeState() {
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (world == null) return "no world";
            uint ground = runtime.M.U32(runtime.World + 0x28);
            uint attr = runtime.M.U32(runtime.World + 0x30);
            return $"effects={world.EffectCount} prims={world.PrimCount} scopes={world.GroundScopes.Count} ground=0x{ground:x} attr=0x{attr:x} " +
                (ground != 0 ? $"gnd={runtime.M.I32(ground + 8)}x{runtime.M.I32(ground + 0xc)} cell={runtime.M.F32(ground + 0x10)}" : "") +
                $" now={runtime.TimeGetTimeImpl()}" +
                (world.GroundScopes.Count > 0 ? $" scope0 end={runtime.M.U32(world.GroundScopes[0].A + 0x164)} n={runtime.M.I32(world.GroundScopes[0].A + 0x168)} cell={runtime.M.I32(world.GroundScopes[0].A + 0x15c)},{runtime.M.I32(world.GroundScopes[0].A + 0x160)}" : "");
        }

        /// <summary>
        /// The client's damage display options the CMsgEffect reads, from the game options: the drift mode [0x1555804]
        /// (0 behind the actor, 1..3 fixed directions), MISS shown [0x1552d9c], and values past 999 999 as K / M
        /// [0x15556c0] (the "Damage Display: Expand" radio, 0x98ca21). Applied to the runtime every frame.
        /// </summary>
        public static int DamageDriftMode;
        public static bool ShowMissText = true;
        public static bool DamageNumberAbbreviate = true;

        /// <summary>
        /// The damage skin [0x15557fc] (DSList id, the equipment window's Damage Indicator tab): a client-local option, so
        /// every number this client draws uses it. Resolved through <see cref="DamageSkins"/> into the CMsgEffect sprite
        /// names when it changes; an id out of range is 0.
        /// </summary>
        public static int DamageSkin;

        /// <summary>The damage skin table (damageskininfo.lub), loaded with the bridge's assets.</summary>
        public static RoDamageSkinCatalog DamageSkins { get; private set; } = RoDamageSkinCatalog.Default;

        int appliedDamageSkin = -1;

        /// <summary>Live CMsgEffect glyphs (damage number digits and messages).</summary>
        public int MsgEffectCount { get { WaitForRenderJob(); return world != null ? world.MsgEffectCount : 0; } }

        /// <summary>The last screen rect each render object submitted (probe only, for !msgdump).</summary>
        readonly Dictionary<uint, Rect> probeSpriteRects = new Dictionary<uint, Rect>();

        /// <summary>!msgdump: every live CMsgEffect glyph (type, RO position, zoom, x offset, last screen rect) and the
        /// screen position of its actor's anchor.</summary>
        /// <summary>
        /// Diagnostics (!fxdepth x y): around the pixel (y from the top), the effect batches of the last frame with their
        /// segment order, depth state and vertex view depths, and the actor layers covering it with their order, flags,
        /// key, depth cut and the sprite depth the shader writes there (ground point depth − 0.4 − 0.032 per pixel above).
        /// </summary>
        public string DescribeDepthAt(float x, float yFromTop) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            UnityCamera cam = frameCamera;
            if (cam == null) return "no camera";
            var text = new System.Text.StringBuilder();
            var p = new Vector2(x, cam.pixelHeight - yFromTop);
            for (int bi = 0; bi < batches.Count; bi++) {
                Batch b = batches[bi];
                float near = float.MaxValue, far = float.MinValue;
                int hits = 0;
                for (int k = 0; k < b.VertexCount; k++) {
                    Vector3 s = cam.WorldToScreenPoint(BatchVertex(b, k));
                    if ((new Vector2(s.x, s.y) - p).sqrMagnitude < 60f * 60f) {
                        hits++;
                        near = Mathf.Min(near, s.z);
                        far = Mathf.Max(far, s.z);
                    }
                }

                if (hits > 0) {
                    text.Append("\n  fx batch ").Append(bi).Append(" seg ").Append(b.Segment).Append(" order ")
                        .Append(b.Segment < segmentOrders.Count ? segmentOrders[b.Segment] : -1).Append(' ')
                        .Append(b.Texture != null ? b.Texture.name : "-").Append(" ztest=").Append(b.State.ZTest)
                        .Append(" zwrite=").Append(b.State.ZWrite).Append(" depth ").Append(near.ToString("F2")).Append("..")
                        .Append(far.ToString("F2")).Append(" (").Append(hits).Append(" verts)");
                }
            }

            var entries = new List<Ragnarok.Entities.RoClientDrawEntry>();
            Ragnarok.Entities.RoClientDrawOrder.Collect(cam, entries);
            foreach (Ragnarok.Entities.RoClientDrawEntry e in entries) {
                Bounds bb = e.Renderer.bounds;
                Vector3 lo = cam.WorldToScreenPoint(bb.min), hi = cam.WorldToScreenPoint(bb.max);
                if (p.x < Mathf.Min(lo.x, hi.x) || p.x > Mathf.Max(lo.x, hi.x) || p.y < Mathf.Min(lo.y, hi.y) || p.y > Mathf.Max(lo.y, hi.y)) continue;
                float up = Mathf.Max(0f, p.y - e.FeetScreenY);
                Material m = e.Renderer.sharedMaterial;
                text.Append("\n  actor ").Append(e.Renderer.name).Append(" order ").Append(e.Renderer.sortingOrder).Append(" flags 0x")
                    .Append(e.Flags.ToString("x")).Append(" key ").Append(e.Key.ToString("F5")).Append(" cut ")
                    .Append(m != null && m.HasProperty("_RoSpriteDepthCutoff") ? m.GetFloat("_RoSpriteDepthCutoff").ToString("F3") : "-")
                    .Append(" feetY ").Append(e.FeetScreenY.ToString("F0")).Append(" spriteBottomY ").Append(Mathf.Min(lo.y, hi.y).ToString("F0")).Append(" feet ").Append(e.FeetDepth.ToString("F2")).Append(" depth here ")
                    .Append((e.FeetDepth - 0.4f - up * 0.032f).ToString("F2"));
            }

            return "fxdepth " + x + "," + yFromTop + " (" + batches.Count + " batches, " + segmentOrders.Count + " segments)" + text;
        }

        public string DescribeMsgEffects() {
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (world == null) return "no world";
            FxMemory m = runtime.M;
            var text = new System.Text.StringBuilder();
            text.Append("glyphs=").Append(world.MsgEffectCount);
            foreach (uint g in world.MsgEffects) {
                uint aid = m.U32(g + 0x15c) != 0 ? m.U32(m.U32(g + 0x15c) + 0x110) : 0;
                text.Append("\n  glyph ").Append(g.ToString("x")).Append(" type 0x").Append(m.I32(g + 0x1a8).ToString("x"))
                    .Append(" pos ").Append(m.F32(g + 0x10).ToString("0.0")).Append(',').Append(m.F32(g + 0x14).ToString("0.0"))
                    .Append(',').Append(m.F32(g + 0x18).ToString("0.0")).Append(" y0 ").Append(m.F32(g + 0x190).ToString("0.0"))
                    .Append(" zoom ").Append(m.F32(g + 0x50).ToString("0.00")).Append(" xoff ").Append(m.I32(g + 0x78))
                    .Append(" aid ").Append(aid);
                if (probeSpriteRects.TryGetValue(g, out Rect r)) text.Append(" rect ").Append(r.ToString("F0"));
                if (aid != 0 && actorAnchors.TryGetValue(aid, out Transform anchor) && anchor != null && frameCamera != null) {
                    Vector3 ro = ToRo(anchor.position);
                    Vector3 screen = frameCamera.WorldToScreenPoint(anchor.position);
                    text.Append(" anchorRo ").Append(ro.ToString("F1")).Append(" anchorScreen ")
                        .Append(screen.x.ToString("F0")).Append(',').Append((frameCamera.pixelHeight - screen.y).ToString("F0"));
                }
            }

            return text.ToString();
        }

        /// <summary>!fxdump: every running classic effect (id, owner, age, position) and the engine's counts.</summary>
        public string DescribeEffects() {
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (world == null) return "no world";
            var text = new System.Text.StringBuilder();
            text.Append("effects=").Append(world.EffectCount).Append(" prims=").Append(world.PrimCount)
                .Append(" scopes=").Append(world.GroundScopes.Count).Append(" newsys=").Append(trackedNew.Count).Append('/').Append(world.NewEffectCount).Append(" makers=").Append(world.EffectMakerCount).Append(" sprtex=").Append(sprHandleCalls).Append("/nospr ").Append(sprHandleNoSprite).Append("/noimg ").Append(sprHandleNoImage)
                .Append(DescribeFirstMapSmoke()).Append(lastMapEffects);
            for (int i = 0; i < tracked.Count; i++) {
                Tracked t = tracked[i];
                text.Append("\n  ef=").Append(t.EffectId).Append(" aid=").Append(t.OwnerAid)
                    .Append(" age=").Append((Time.time - t.LaunchedAt).ToString("F0")).Append("s pos=").Append(t.LastPosition.ToString("F1"))
                    .Append(" follow=").Append(t.Follow != null ? t.Follow.name : "-").Append(" prims=").Append(t.Effect.PrimListCount);
            }

            // actor looks (+0xe8 flags / tint) the look renderer draws on actors
            foreach (KeyValuePair<uint, Transform> kv in actorAnchors) {
                FxActorLook look = world.GetActorLook(kv.Key);
                if (look.Flags == 0 && (look.Tint == 0xffffffff || look.Tint == 0)) continue;
                text.Append("\n  look aid=").Append(kv.Key).Append(" flags=0x").Append(look.Flags.ToString("x"))
                    .Append(" tint=0x").Append(look.Tint.ToString("x")).Append(" scale=").Append(look.Scale.ToString("F2"))
                    .Append(" anchor=").Append(kv.Value != null ? kv.Value.name : "-");
            }

            return text.ToString();
        }

        /// <summary>Whether the effect behind <paramref name="handle"/> is still running.</summary>
        public bool IsEffectAlive(uint handle) {
            if (deferredHandles.Contains(handle)) return true;   // launched at the next flush
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (handle == 0) return false;
            for (int i = 0; i < tracked.Count; i++) {
                if (tracked[i].Handle == handle) return true;
            }

            for (int i = 0; i < trackedNew.Count; i++) {
                if (trackedNew[i].Key == handle) return world.IsNewEffectAlive(trackedNew[i].Value);
            }

            return false;
        }

        /// <summary>
        /// Ends a running effect at its next process pass (its duration is cut to the ticks it already ran), e.g. a
        /// persistent unit effect whose skill unit disappeared or a cast circle whose cast ended early.
        /// </summary>
        public bool EndEffect(uint handle) {
            if (deferredHandles.Remove(handle)) return true;   // never launched
            if (ShouldDefer && handle != 0) {
                // queued while the render job runs (the callers ignore the result; a later IsEffectAlive waits and sees it)
                DeferEndEffect(handle);
                return true;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (handle == 0) return false;
            for (int i = 0; i < tracked.Count; i++) {
                if (tracked[i].Handle == handle) {
                    // the client's effect msg 0x18 (CRagEffect::SendMsg case 1): the owner is cleared and the next
                    // OnProcess returns false
                    runtime.M.U32(tracked[i].Effect.A + 0x138, 0);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The colour floats an effect msg 0x2c(0xffff, sel) sets right after the launch (+0x10 / +0x14 / +0x18, 0..255),
        /// e.g. EF 166's after-image tint for Quicken / Overthrust / Energy Coat.
        /// </summary>
        public bool SetEffectColor(uint handle, float r, float g, float b) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (handle == 0) return false;
            for (int i = 0; i < tracked.Count; i++) {
                if (tracked[i].Handle == handle) {
                    uint a = tracked[i].Effect.A;
                    runtime.M.F32(a + 0x10, r);
                    runtime.M.F32(a + 0x14, g);
                    runtime.M.F32(a + 0x18, b);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Actor message 0x8f: every classic effect of type <paramref name="effectId"/> running on the actor's shim ends
        /// (effect msg 0x18) — whoever launched it (a skill packet, a status, a unit). Status "off" lists and the client's
        /// recast rule remove effects this way (while HasEffect(id) → msg 0x6d(id)).
        /// </summary>
        public int RemoveAllEffectsById(uint aid, int effectId) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferRemoveAllEffectsById(aid, effectId);
                return 0;   // the callers ignore the count
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (world == null || aid == 0 || !world.TryGetActor(aid, out uint owner)) return 0;
            return world.RemoveAllEffectsById(owner, effectId);
        }

        /// <summary>Actor message 0x6d: the oldest classic effect of type <paramref name="effectId"/> on the actor ends.</summary>
        public bool RemoveEffectById(uint aid, int effectId) {
            if (ShouldDefer) {
                DeferRemoveEffectById(aid, effectId);
                return true;   // the callers ignore the result
            }

            WaitForRenderJob();
            return world != null && aid != 0 && world.TryGetActor(aid, out uint owner) && world.RemoveEffectById(owner, effectId);
        }

        /// <summary>Actor message 0x78: is a classic effect of type <paramref name="effectId"/> alive on the actor?</summary>
        public bool HasEffectOnActor(uint aid, int effectId) {
            WaitForRenderJob();
            return world != null && aid != 0 && world.TryGetActor(aid, out uint owner) && world.HasEffect(owner, effectId);
        }

        /// <summary>
        /// The actor's option bits as the server sent them (ZC_STATE_CHANGE, entry packets): mirrored into its shim's
        /// +0x2c0 each frame (Chase Walk footprints, flying hover, GM hide checks of the actor renderer).
        /// </summary>
        public void SetActorOption(uint aid, int option, Transform anchor) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferSetActorOption(aid, option, anchor);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (aid == 0) return;
            if (option == 0) {
                actorOptions.Remove(aid);
            } else {
                actorOptions[aid] = option;
                if (anchor != null) actorAnchors[aid] = anchor;
            }

            if (world != null && world.TryGetActor(aid, out uint shim)) {
                runtime.M.I32(shim + 0x2c0, option);
            }
        }

        /// <summary>
        /// ZC_EMOTION (0x00c0) → the actor's msg 0xa0 (0xbd8c08): a balloon (CMsgEffect type 1, emotion\\basic.spr) that
        /// follows the actor for one pass of its ACT action; a new emotion never replaces a running one. False when the
        /// emotion has no record (no balloon, like the client).
        /// </summary>
        public bool ShowEmotion(uint aid, int type, Transform anchor) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            if (aid == 0 || anchor == null) return false;
            EnsureWorld();
            actorAnchors[aid] = anchor;
            Vector3 ro = ToRo(anchor.position);
            world.EnsureActor(aid, ro.x, ro.y, ro.z);
            return world.ShowEmotion(aid, type) != 0;
        }

        /// <summary>
        /// CRenderObject::SetARGB (0xbe3d40) on the actor's shim (+0x98..+0x9b, −1 keeps a channel): the tints the game's
        /// own status handlers write (Blade Stop's grey) go into the same bytes the effects read, modify and restore.
        /// </summary>
        public void SetActorArgb(uint aid, int a, int r, int g, int b, Transform anchor) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferSetActorArgb(aid, a, r, g, b, anchor);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (aid == 0) return;
            EnsureWorld();
            if (anchor != null) actorAnchors[aid] = anchor;
            Vector3 ro = ToRo(anchor != null ? anchor.position : Vector3.zero);
            uint shim = world.EnsureActor(aid, ro.x, ro.y, ro.z);
            runtime.SetArgb(shim, a, r, g, b);
        }

        /// <summary>
        /// CPc::Render's prelude (0xcc89b0 → FUN_00b88070): every drawn player with Chase Walk (option 0x4000) or TK_RUN
        /// (flag 0x8000) leaves a footprint every 12 / 7 frames on frames with logic ticks.
        /// </summary>
        void RenderFootprints() {
            foreach (KeyValuePair<uint, int> kv in actorOptions) {
                if ((kv.Value & 0x4000) == 0 || !actorAnchors.TryGetValue(kv.Key, out Transform anchor) || anchor == null) continue;
                Vector3 ro = ToRo(anchor.position);
                uint shim = world.EnsureActor(kv.Key, ro.x, ro.y, ro.z);
                runtime.M.I32(shim + 0x2c0, kv.Value);
            }

            foreach (uint aid in actorAnchors.Keys) {
                if (!world.TryGetActor(aid, out uint shim)) continue;
                bool chaseWalk = actorOptions.TryGetValue(aid, out int option) && (option & 0x4000) != 0;
                if (chaseWalk || (runtime.M.U32(shim + 0xe8) & 0x8000) != 0) {
                    runtime.PcRenderFootprints_cc89b0(shim);
                }
            }
        }

        /// <summary>An owner object several effects share for their whole life (a skill unit, like the client's CSkill).</summary>
        public uint CreateSharedOwner(Vector3 worldPosition) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            EnsureWorld();
            Vector3 ro = ToRo(worldPosition);
            return world.CreateOwner(ro.x, ro.y, ro.z);
        }

        /// <summary>
        /// The owner is destroyed (ZC_SKILL_DISAPPEAR deletes the CSkill): every effect on it gets msg 0x18 and dies on
        /// its next pass (FxWorld.ReleaseOwner); the owner object itself is freed after that pass.
        /// </summary>
        public void ReleaseSharedOwner(uint owner) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferReleaseSharedOwner(owner);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (owner == 0 || world == null) return;
            world.ReleaseOwner(owner);
            ownersToFree.Add(owner);
        }

        readonly List<uint> ownersToFree = new List<uint>();

        Action<uint, int> onFace, onQuad, onFaceTimed, onQuadTimed;
        Action<FxSpriteDraw> onSprite, onSpriteTimed;

        /// <summary>Geometry callbacks, timed while a sweep records (the bridge's share of the render phase).</summary>
        void SetSubmitTiming(bool timed) {
            runtime.Renderer.FaceSubmitted = timed ? onFaceTimed : onFace;
            runtime.Renderer.QuadSubmitted = timed ? onQuadTimed : onQuad;
            runtime.Renderer.SpriteSubmitted = timed ? onSpriteTimed : onSprite;
        }

        /// <summary>Diagnostics (!fxcover): records which effect / prim emitted each vertex range of a frame.</summary>
        public static bool TraceSources;
        Action<FxProfileKind, int, long> profileAndTrace;
        /// <summary>(end vertex, effect id or prim type, master effect id or -1, 0 effect / 1 prim / 2 other) per rendered item.</summary>
        readonly List<(int end, int id, int master, int kind)> sourceMarks = new List<(int, int, int, int)>();
        int primRenderIndex;

        void ProfileAndTrace(FxProfileKind kind, int id, long ticks) {
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                Ragnarok.Diagnostics.RoSkillSweepProbe.AddClassicStep((int) kind, id, ticks);
            }

            switch (kind) {
                case FxProfileKind.EffectRender:
                    sourceMarks.Add((vertexCount, id, -1, 0));
                    break;
                case FxProfileKind.PrimRender:
                    int master = -1;
                    if (primRenderIndex < world.Prims.Count) {
                        uint m = world.Prims[primRenderIndex].m_master_ptr;
                        if (m != 0) master = new RagEffect(runtime.M, m).m_type;
                    }

                    primRenderIndex++;
                    sourceMarks.Add((vertexCount, id, master, 1));
                    break;
                case FxProfileKind.NewSystemRender:
                    sourceMarks.Add((vertexCount, -1, -1, 2));
                    break;
            }
        }

        string DescribeSource(int vertex) {
            for (int i = 0; i < sourceMarks.Count; i++) {
                (int end, int id, int master, int kind) s = sourceMarks[i];
                if (vertex < s.end) {
                    return s.kind == 0 ? "EF " + s.id
                        : s.kind == 1 ? "prim 0x" + s.id.ToString("x") + (s.master >= 0 ? " of EF " + s.master : " (no master)")
                        : "scopes/arrows/msg/new system";
                }
            }

            return "?";
        }

        /// <summary>
        /// Diagnostics (!fxcover x y): every effect triangle of the last frame that covers the pixel (y from the top), grouped
        /// by batch, with its blend state and the effect / prim that emitted it (needs <see cref="TraceSources"/> on for a frame).
        /// </summary>
        public string DescribeCoverAt(float x, float yFromTop) {
            WaitForRenderJob();   // the engine is the render job's while it runs
            UnityCamera cam = frameCamera;
            if (cam == null) return "no camera";
            var text = new System.Text.StringBuilder();
            var p = new Vector2(x, cam.pixelHeight - yFromTop);
            var sources = new Dictionary<string, int>();
            for (int bi = 0; bi < batches.Count; bi++) {
                Batch b = batches[bi];
                sources.Clear();
                int covered = 0;
                for (int r = b.First; r < b.First + b.Count; r++) {
                    VertexRange range = drawRanges[r];
                    for (int k = 0; k + 2 < range.Count; k += 3) {
                        int v = range.Start + k;
                        Vector3 a = cam.WorldToScreenPoint(VertexWorld(v));
                        Vector3 c1 = cam.WorldToScreenPoint(VertexWorld(v + 1));
                        Vector3 c2 = cam.WorldToScreenPoint(VertexWorld(v + 2));
                        if (a.z <= 0f || c1.z <= 0f || c2.z <= 0f) continue;
                        float d1 = (p.x - c1.x) * (a.y - c1.y) - (a.x - c1.x) * (p.y - c1.y);
                        float d2 = (p.x - c2.x) * (c1.y - c2.y) - (c1.x - c2.x) * (p.y - c2.y);
                        float d3 = (p.x - a.x) * (c2.y - a.y) - (c2.x - a.x) * (p.y - a.y);
                        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                        if (neg && pos) continue;
                        covered++;
                        string src = DescribeSource(v);
                        sources[src] = sources.TryGetValue(src, out int n) ? n + 1 : 1;
                    }
                }

                if (covered == 0) continue;
                text.Append("\n  batch ").Append(bi).Append(' ').Append(b.Texture != null ? b.Texture.name : "-")
                    .Append(" blend ").Append(b.State.Src).Append('/').Append(b.State.Dst).Append(" ztest=").Append(b.State.ZTest)
                    .Append(" tris ").Append(covered).Append(" from");
                foreach (KeyValuePair<string, int> s in sources) text.Append(' ').Append(s.Key).Append(" x").Append(s.Value).Append(';');
            }

            return "fxcover " + x + "," + yFromTop + " (" + batches.Count + " batches, traced " + sourceMarks.Count + " items)" + text;
        }

        /// <summary>The world's per-step profiler while a sweep records (spike attribution).</summary>
        static readonly Action<FxProfileKind, int, long> ProfileStep =
            (kind, id, ticks) => Ragnarok.Diagnostics.RoSkillSweepProbe.AddClassicStep((int) kind, id, ticks);

        /// <summary>CGroundScope for a ground-target cast (ZC_USESKILL_ACK with no target actor): the rotating magic
        /// circle over the target area, sized per skill, for the cast time. Returns 0 when there is nothing to draw.</summary>
        public uint LaunchGroundScope(int cellX, int cellY, int skillId, int delayMs, uint casterAid) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferLaunchGroundScope(cellX, cellY, skillId, delayMs, casterAid);
                return 0;   // the caller ignores the scope id
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (assets == null) return 0;
            EnsureWorld();
            uint scope = world.LaunchGroundScope(cellX, cellY, skillId, delayMs, casterAid);
            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) Ragnarok.Diagnostics.RoSkillSweepProbe.Record($"scope skill={skillId} cell={cellX},{cellY} delay={delayMs} handle=0x{scope:x}");
            return scope;
        }

        /// <summary>Lets the engine's shim for <paramref name="aid"/> follow <paramref name="anchor"/> (homing prims
        /// then track a moving target even when the effect is owned by another actor).</summary>
        public void RegisterActorAnchor(uint aid, Transform anchor) {
            // only the logic step reads the anchors (after-images); a render-only job never does, so the map is written
            // at once (this call came ~40 times a frame in !woe 500, each a queued closure)
            if (jobRunning && jobWithLogic && OnMainThread) {
                if (ShouldDefer) {
                    DeferRegisterActorAnchor(aid, anchor);
                    return;
                }

                WaitForRenderJob();
            }

            if (aid != 0 && anchor != null) {
                actorAnchors[aid] = anchor;
            }
        }

        /// <summary>
        /// A basic-attack arrow or bullet (CArrowEffect, FUN_00bfb110): from the attacker's shim toward the target's,
        /// released after <paramref name="releaseTicks"/> (24 ms ticks); <paramref name="spriteClass"/> 0 for players,
        /// the monster's job for its bullet. The engine flies and draws it and deletes it when it lands.
        /// </summary>
        public bool LaunchArrow(uint sourceAid, Transform source, uint targetAid, Transform target, float releaseTicks, int spriteClass) {
            if (assets == null || source == null || target == null || targetAid == 0) {
                return false;
            }

            if (ShouldDefer) {
                DeferLaunchArrow(sourceAid, source, targetAid, target, releaseTicks, spriteClass);
                return true;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs

            EnsureWorld();
            RegisterActorAnchor(sourceAid, source);
            RegisterActorAnchor(targetAid, target);
            Vector3 from = ToRo(source.position), to = ToRo(target.position);
            world.EnsureActor(sourceAid, from.x, from.y, from.z);
            world.EnsureActor(targetAid, to.x, to.y, to.z);
            return world.LaunchArrow(sourceAid, targetAid, releaseTicks, spriteClass) != 0;
        }

        /// <summary>The actor's shim at its anchor (numbers and messages attach to it and follow its x / z).</summary>
        bool EnsureMessageActor(uint aid, Transform anchor) {
            if (assets == null || aid == 0 || anchor == null) {
                return false;
            }

            EnsureWorld();
            RegisterActorAnchor(aid, anchor);
            Vector3 ro = ToRo(anchor.position);
            world.EnsureActor(aid, ro.x, ro.y, ro.z);
            return true;
        }

        /// <summary>A damage / heal number over an actor (CMsgEffect, actor msg 0x58): type 0xe, 0x10, 0x15, 0x16 or 0x72.</summary>
        public void ShowNumber(uint aid, Transform anchor, int value, uint argb, int type) {
            if (ShouldDefer) {
                DeferShowNumber(aid, anchor, value, argb, type);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (EnsureMessageActor(aid, anchor)) {
                world.ShowNumber(aid, value, argb, type);
            }
        }

        /// <summary>MISS over an actor (actor msg 0x59; the client shows it on the attacker).</summary>
        public void ShowMiss(uint aid, Transform anchor, uint argb) {
            if (ShouldDefer) {
                DeferShowMiss(aid, anchor, argb);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (EnsureMessageActor(aid, anchor)) {
                world.ShowMiss(aid, argb);
            }
        }

        /// <summary>The red "Critical" burst behind a critical number (actor msg 0x5c).</summary>
        public void ShowCriticalBurst(uint aid, Transform anchor) {
            if (ShouldDefer) {
                DeferShowCriticalBurst(aid, anchor);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (EnsureMessageActor(aid, anchor)) {
                world.ShowCriticalBurst(aid);
            }
        }

        /// <summary>The Taekwon kick's blue burst behind its combo total (actor msg 0x83).</summary>
        public void ShowComboBurst(uint aid, Transform anchor) {
            if (ShouldDefer) {
                DeferShowComboBurst(aid, anchor);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (EnsureMessageActor(aid, anchor)) {
                world.ShowComboBurst(aid);
            }
        }

        /// <summary>"LUCKY!!" over an actor that dodged by luck (actor msg 0x5f).</summary>
        public void ShowLucky(uint aid, Transform anchor) {
            if (ShouldDefer) {
                DeferShowLucky(aid, anchor);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            if (EnsureMessageActor(aid, anchor)) {
                world.ShowLucky(aid);
            }
        }

        /// <summary>
        /// Drops the actor shim of an actor that left the view (~CGameActor: the effects launched on it end). With
        /// <paramref name="root"/>, the effects that follow a transform under it end too: those launched without the
        /// actor's id run on a throw-away owner that only follows its anchor, and a pooled root kept them alive.
        /// </summary>
        public void ForgetActor(uint actorId, Transform root = null) {
            if (ShouldDefer) {
                // queued while the render job runs (call order kept), see DeferLaunches
                DeferForgetActor(actorId, root);
                return;
            }

            WaitForRenderJob();   // the engine is the render job's while it runs
            actorAnchors.Remove(actorId);
            levelAuras.Remove(actorId);
            world?.RemoveActor(actorId);
            if (root == null || world == null) {
                return;
            }

            for (int i = 0; i < tracked.Count; i++) {
                Transform follow = tracked[i].Follow;
                if (follow != null && (follow == root || follow.IsChildOf(root))) {
                    // effect msg 0x18, as EndEffect: the owner cleared, the next OnProcess deletes it
                    runtime.M.U32(tracked[i].Effect.A + 0x138, 0);
                }
            }
        }

        void LateUpdate() {
            RunFrame(RoEffectCameraCache.Resolve(preferredCamera), Time.deltaTime * 1000.0);
        }

        /// <summary>
        /// One rendered frame: follow owners, sync the projection with <paramref name="cam"/>, run the
        /// whole 16 ms logic ticks covered by <paramref name="deltaMs"/>, then submit this frame's geometry.
        /// </summary>
        public void RunFrame(UnityCamera cam, double deltaMs) {
            if (world == null || cam == null) {
                if (Verbose && world != null && world.EffectCount > 0 && verboseFrames++ % 60 == 0) {
                    Debug.LogWarning("[ClassicFx] no camera: effects pending but nothing is drawn");
                }

                return;
            }

            JoinRenderJob();   // the previous frame's job (unless a call this frame already waited); nothing below may run beside it
            runtime.ProcessOnTicklessFrames = ProcessOnTicklessFrames;
            runtime.DamageDriftMode = DamageDriftMode;
            runtime.ShowMissText = ShowMissText;
            // [0x01552d7c], the effective /mineffect (the player's choice or a map that forces it)
            runtime.MinEffect = Ragnarok.UI.RoClassicCommandFlags.MinEffect;
            runtime.DamageNumberAbbreviate = DamageNumberAbbreviate;
            if (DamageSkin != appliedDamageSkin) {
                ApplyDamageSkin();
            }

            bool timed = Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled;
            long t0 = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            if (timed) Ragnarok.Diagnostics.RoSkillSweepProbe.MarkClassicFrameStart();
            world.Profiler = StepProfiler ?? (TraceSources ? profileAndTrace ??= ProfileAndTrace : timed ? ProfileStep : null);
            SetSubmitTiming(timed);
            frameCamera = cam;
            CaptureMainThread();
            frameTime = Time.time;
            if (whiteTexture == null) whiteTexture = Texture2D.whiteTexture;
            if (UseRenderThread || jobResultsReady || jobRunning) {
                // the previous frame's render job (normally joined at this frame's start): the rest of its frame in the
                // client's order (footprints and actor looks after the render pass, then the draw), before the logic
                JoinRenderJob();
                if (jobResultsReady) {
                    jobResultsReady = false;
                    using (RenderMarker.Auto()) RenderFootprints();
                    using (LooksMarker.Auto()) lookRenderer.Render(world, actorAnchors);
                    using (DrawMarker.Auto()) Draw(listsFlushed: true);
                    // the sweep's per-skill effect counts (RenderInline records them when nothing is threaded)
                    Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicFrame(world.EffectCount + world.NewEffectCount, world.PrimCount, batches.Count, vertexCount);
                }
            }

            RunDeferredCalls();   // the launches made while the job ran, in call order, before the logic step
            frameCameraToWorld = cam.cameraToWorldMatrix;
            frameNearClip = cam.nearClipPlane;
            // the client builds them with its player object in the world: an ownerless new-system effect follows
            // FUN_00a64480(0), which is the player object while the player AID is still 0
            if (pendingMapEffects != null && assets != null && runtime.PlayerAid != 0 && world.TryGetActor(runtime.PlayerAid, out _)) {
                LoadPendingMapEffects();
            }

            using (FollowMarker.Auto()) {
                FollowOwners();
                if (levelAurasRelaunch || levelAurasPending || levelAuraSettingsApplied != levelAuraSettingsVersion) {
                    RefreshLevelAuras();
                }

                ConfigureProjection(frameCamera);
            }

            long t1 = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            jobFrameSkip = Ragnarok.UI.RoClassicCommandFlags.FrameSkip;
            jobDeltaMs = deltaMs;
            if (UseRenderThread && ThreadedLogic) {
                // the logic step goes with the render pass on the worker (the client's order: process, then render)
                PrepareRenderFrame();
                CollectActorLayers();
                KickRenderJob(withLogic: true);
                return;
            }

            AdvanceWorld();
            PrepareRenderFrame();
            if (UseRenderThread) {
                // the render pass and Flush on the worker; this frame's LateUpdate already drew the previous job
                CollectActorLayers();
                KickRenderJob(withLogic: false);
                return;
            }

            RenderInline(timed, t0, t1);
        }

        bool jobFrameSkip;
        double jobDeltaMs;

        /// <summary>
        /// !toggle fxlogic: with <see cref="ThreadedRender"/>, the logic step (Advance) runs in the render job as well. The
        /// engine's callbacks during a step reach Unity only through queued work (sounds, quakes, after-images) or a
        /// main-thread request (file reads), so the step itself does not need the main thread. Off by default: in the
        /// fixed-seed bench it gave 23.2 / 17.0 ms against 19.3 / 17.4 with the render pass alone (the longer job is
        /// waited for at the next frame's start).
        /// </summary>
        public static bool ThreadedLogic;

        /// <summary>The logic step: the 16 ms ticks the frame covers (FrameSkip) or one, then the finished effects pruned.</summary>
        void AdvanceWorld() {
            using (AdvanceMarker.Auto()) {
                // /skip (0xba4560): on, every logic tick the elapsed time covers; off, one tick per rendered frame
                if (jobFrameSkip) world.Advance(jobDeltaMs);
                else world.Tick();
                PruneFinished();
            }

            if (ownersToFree.Count > 0) {
                foreach (uint owner in ownersToFree) {
                    runtime.M.Free(owner);
                }

                ownersToFree.Clear();
            }
        }

        /// <summary>The frame's render state reset and its projection taken from the core's renderer (ConfigureProjection ran).</summary>
        void PrepareRenderFrame() {
            batches.Clear();
            pendingRps.Clear();
            renderLists.Clear();
            vertexCount = 0;
            sourceMarks.Clear();
            primRenderIndex = 0;
            frameCenterX = runtime.Renderer.CenterX;
            frameCenterY = runtime.Renderer.CenterY;
            frameXFactor = runtime.Renderer.XFactor;
            frameYFactor = runtime.Renderer.YFactor;
            drawScreen = new Vector4(frameCenterX, frameCenterY, 1f / (frameXFactor * RoUnitsPerUnity), 1f / (frameYFactor * RoUnitsPerUnity));
            drawCameraToWorld = frameCameraToWorld;
            lastTextureObject = 0;
            lastResolvedTexture = null;
        }

        /// <summary>The frame's render pass, looks and draw on the main thread (no render job).</summary>
        void RenderInline(bool timed, long t0, long t1) {
            long t2 = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            using (RenderMarker.Auto()) {
                world.Render();
                RenderFootprints();
            }

            long t3 = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            // the actor renderers: effect looks (+0xe8) drawn on the actors, each marked rendered like the client's
            using (LooksMarker.Auto()) {
                lookRenderer.Render(world, actorAnchors);
            }

            long t4 = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

            using (DrawMarker.Auto()) {
                Draw();
            }
            if (timed) {
                long t5 = System.Diagnostics.Stopwatch.GetTimestamp();
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Follow, t1 - t0);
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Advance, t2 - t1);
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Render, t3 - t2);
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Looks, t4 - t3);
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Draw, t5 - t4);
                Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicPhase(Ragnarok.Diagnostics.RoSkillSweepProbe.ClassicPhase.Total, t5 - t0);
            }
            Ragnarok.Diagnostics.RoSkillSweepProbe.RecordClassicFrame(world.EffectCount + world.NewEffectCount, world.PrimCount, batches.Count, vertexCount);
            Ragnarok.Diagnostics.RoSkillSweepProbe.EndClassicFrame();
            Ragnarok.Diagnostics.RoSkillSweepProbe.RecordHeap(runtime.M.BumpBytes, runtime.M.Size);
            if (Verbose && (world.EffectCount > 0 || world.PrimCount > 0) && verboseFrames++ % 30 == 0) {
                UnityCamera cam = frameCamera;
                Debug.Log($"[ClassicFx] frame: effects={world.EffectCount} prims={world.PrimCount} batches={batches.Count} verts={vertexCount} cam={cam.name} centre={mapCentre} v0={(vertexCount > 0 ? VertexWorld(0).ToString("F2") : "-")} camPos={cam.transform.position.ToString("F2")} xf={runtime.Renderer.XFactor:F1} cx={runtime.Renderer.CenterX} px={cam.pixelWidth}x{cam.pixelHeight} r0={(frameRenderer != null && frameMaterials.Count > 0 ? $"{frameRenderer.enabled}/{frameRenderer.isVisible}/{frameMesh.vertexCount}v/{frameMesh.subMeshCount}sub/{frameMaterials[0].shader.name}/q{frameMaterials[0].renderQueue}/{frameMaterials[0].GetTexture("_BaseMap")?.name}" : "-")} cams=[{string.Join("; ", System.Array.ConvertAll(UnityCamera.allCameras, c => $"{c.name}:{(c.enabled ? "on" : "off")}:{c.transform.position.ToString("F1")}:fov{c.fieldOfView:F0}:depth{c.depth}"))}] main={(UnityCamera.main != null ? UnityCamera.main.name + UnityCamera.main.transform.position.ToString("F1") : "null")}");
            }
        }

        /// <summary>The render job (worker thread when <see cref="ThreadedRender"/>): the logic step if asked, the engine's render pass and Flush.</summary>
        void RenderJob() {
            if (jobWithLogic) {
                AdvanceWorld();
            }

            using (RenderMarker.Auto()) {
                world.Render();
            }

            FlushLists();
            AssignDraws();   // the batches and the actor layers' orders, applied by the next LateUpdate
        }

        /// <summary>The host's world state: whether an actor (server or account id) is still there.</summary>
        public static Func<uint, bool> ActorExists;

        /// <summary>Actors effects were launched on before they had a transform (no presenter release ends them).</summary>
        readonly HashSet<uint> unanchoredActors = new HashSet<uint>();
        readonly List<uint> goneActors = new List<uint>();
        int goneCheckFrame;

        /// <summary>
        /// About twice a second: an actor that effects were launched on before it was presented and that is no longer in
        /// the world state is forgotten, so its effects end (~CGameActor) although the presenter never released it.
        /// </summary>
        void ForgetGoneActors() {
            if (unanchoredActors.Count == 0 || ActorExists == null || ++goneCheckFrame < 30) {
                return;
            }

            goneCheckFrame = 0;
            goneActors.Clear();
            foreach (uint aid in unanchoredActors) {
                if (actorAnchors.ContainsKey(aid) || !ActorExists(aid)) goneActors.Add(aid);
            }

            foreach (uint aid in goneActors) {
                unanchoredActors.Remove(aid);
                // presented meanwhile: the presenter's release forgets it; gone: forget it here
                if (!actorAnchors.ContainsKey(aid) && aid != runtime.PlayerAid) ForgetActor(aid);
            }
        }

        void FollowOwners() {
            ForgetGoneActors();
            for (int i = 0; i < tracked.Count; i++) {
                Tracked t = tracked[i];
                if (t.Follow != null) {
                    t.LastPosition = t.Follow.position + t.FollowOffset;
                    tracked[i] = t;
                } else if (!ReferenceEquals(t.Follow, null)) {
                    // the transform it followed was destroyed with its actor: the effect ends like one on the actor
                    runtime.M.U32(t.Effect.A + 0x138, 0);
                    t.Follow = null;
                    tracked[i] = t;
                }

                Vector3 ro = ToRo(t.LastPosition);
                world.SetOwnerPosition(t.Owner, ro.x, ro.y, ro.z);
            }

            // actor shims that no tracked effect follows still move with their actors (homing prims, launches on them)
            staleAnchors.Clear();
            foreach (KeyValuePair<uint, Transform> kv in actorAnchors) {
                if (kv.Value == null) {
                    staleAnchors.Add(kv.Key);
                    continue;
                }

                if (world.TryGetActor(kv.Key, out uint actor)) {
                    Vector3 ro = ToRo(kv.Value.position);
                    world.SetOwnerPosition(actor, ro.x, ro.y, ro.z);
                    MirrorWalkState(kv.Key, actor, kv.Value.position);
                } else if (kv.Key == runtime.PlayerAid) {
                    // the local player always has a shim: the new system measures its 130-unit range from it
                    Vector3 ro = ToRo(kv.Value.position);
                    world.EnsureActor(kv.Key, ro.x, ro.y, ro.z);
                }
            }

            foreach (uint aid in staleAnchors) {
                actorMotion.Remove(aid);
                actorAnchors.Remove(aid);
                levelAuras.Remove(aid);
                world.RemoveActor(aid);
            }
        }

        /// <summary>
        /// The actor's action (+0x70) as far as effects need it: 1 while it walks, 0 standing. EF 166 leaves its
        /// after-images only while the actor is not standing; Chase Walk's EF 1195 spins only at 0. Written only when
        /// the state changes, so effects that set +0x70 themselves keep their value.
        /// </summary>
        void MirrorWalkState(uint aid, uint shim, Vector3 position) {
            bool walking = false;
            if (actorMotion.TryGetValue(aid, out (Vector3 position, bool walking) last)) {
                walking = (position - last.position).sqrMagnitude > 1e-6f;
                if (walking == last.walking) {
                    actorMotion[aid] = (position, walking);
                    return;
                }
            }

            actorMotion[aid] = (position, walking);
            runtime.M.I32(shim + 0x70, walking ? 1 : 0);
        }

        void PruneFinished() {
            for (int i = trackedNew.Count - 1; i >= 0; i--) {
                if (!world.IsNewEffectAlive(trackedNew[i].Value)) trackedNew.RemoveAt(i);
            }

            aliveEffects.Clear();
            IReadOnlyList<RagEffect> effects = world.Effects;
            for (int i = 0; i < effects.Count; i++) {
                aliveEffects.Add(effects[i].A);
            }

            for (int i = tracked.Count - 1; i >= 0; i--) {
                if (!aliveEffects.Contains(tracked[i].Effect.A)) {
                    if (tracked[i].OwnsOwner) {
                        runtime.M.Free(tracked[i].Owner);
                    }
                    tracked.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Writes the client's 4x3 view matrix (RO world → RO camera: x right, y down, z forward, RO units)
        /// and projection factors so ProjectVertex reproduces the Unity camera's pixel positions.
        /// </summary>
        void ConfigureProjection(UnityCamera cam) {
            Matrix4x4 roToUnity = Matrix4x4.TRS(mapCentre, Quaternion.identity, new Vector3(1f / RoUnitsPerUnity, -1f / RoUnitsPerUnity, RoZSign / RoUnitsPerUnity));
            Matrix4x4 unityCamToRoCam = Matrix4x4.Scale(new Vector3(RoUnitsPerUnity, -RoUnitsPerUnity, -RoUnitsPerUnity));
            Matrix4x4 m = unityCamToRoCam * cam.worldToCameraMatrix * roToUnity;
            uint view = runtime.View + 0x98;
            for (int r = 0; r < 4; r++) {
                for (int c = 0; c < 3; c++) {
                    runtime.M.F32(view + (uint) (r * 3 + c) * 4, m[c, r]);
                }
            }

            float halfW = cam.pixelWidth * 0.5f, halfH = cam.pixelHeight * 0.5f;
            Matrix4x4 p = cam.projectionMatrix;
            runtime.Renderer.XFactor = p.m00 * halfW;
            runtime.Renderer.YFactor = p.m11 * halfH;
            runtime.Renderer.CenterX = runtime.Renderer.CenterX2 = Mathf.RoundToInt(halfW);
            runtime.Renderer.CenterY = runtime.Renderer.CenterY2 = Mathf.RoundToInt(halfH);
            runtime.Renderer.ScreenWidth = cam.pixelWidth;
            runtime.Renderer.ScreenHeight = cam.pixelHeight;
            // CRenderer::SetSize (0x53d83a): +0x18 = width / 640, the factor of every render object's x offset (+0x78):
            // the digits of a damage number, the aura copies of a player
            runtime.Renderer.Field18 = cam.pixelWidth / 640f;
        }

        /// <summary>TL vertex (screen x/y, rhw) → Unity world position.</summary>
        Vector3 Unproject(float sx, float sy, float rhw) {
            float zc = 1f / rhw;
            // the renderer's centre and factors read once a frame (four reads of the emulated renderer per vertex before);
            // the same operations in the same order, so the same bits
            float xc = (sx - frameCenterX) * zc / frameXFactor;
            float yc = (sy - frameCenterY) * zc / frameYFactor;
            var unityCam = new Vector3(xc / RoUnitsPerUnity, -yc / RoUnitsPerUnity, -zc / RoUnitsPerUnity);
            return frameCameraToWorld.MultiplyPoint3x4(unityCam);
        }

        /// <summary>
        /// The rhw of the client's screen-space draws (type 0, 2, 3 and 4 prims such as Endure's, STR layers: z ≈ 1e-6,
        /// rhw 0.999999) and of the damage numbers / crit star (+0x124 = 0x200: rhw ≈ 0.1, z ≈ 0.002): nothing in the world
        /// is nearer, so no actor sprite ever covers them (reference packages/sprite-effect-depth). Their own rhw would
        /// land them on (or behind) the near plane, or at the actor's depth, so they keep their pixel position and go a few
        /// units in front of the camera instead.
        /// </summary>
        float OverlayRhw => 1f / ((frameNearClip * 4f + 1f) * RoUnitsPerUnity);   // depth in RO units

        /// <summary>A TL vertex the client draws in screen space (z ≈ 0, rhw ≈ 1).</summary>
        static bool IsScreenSpaceRhw(float rhw) => rhw >= 0.99f;

        /// <summary>A TL vertex drawn in front of everything: the device depth is its z (+0x8), constant near 0 for the screen-space
        /// prims (1e-6) and STR layers (3.1e-5), or its rhw ≈ 1; the aura lights and other flags-5 quads carry real depth.</summary>
        bool IsScreenSpaceVertex(uint v) => runtime.M.F32(v + 0x8) <= 1e-4f || IsScreenSpaceRhw(runtime.M.F32(v + 0xc));

        void AppendVertex(uint v, bool flipV = true, bool screenSpace = false) {
            runtime.M.ReadTlVertex(v, out float sx, out float sy, out float rhw, out uint argb, out float tu, out float tv);

            if (screenSpace) {
                rhw = -OverlayRhw;   // negative: the vertex shader keeps screen-space vertices on the screen
            }

            AddVertex(sx, sy, rhw, new Vector2(tu, flipV ? 1f - tv : tv),
                new Color32((byte) (argb >> 16), (byte) (argb >> 8), (byte) argb, (byte) (argb >> 24)));
        }

        /// <summary>Diagnostics: frame vertex <paramref name="v"/> as a world position (what the vertex shader computes).</summary>
        Vector3 VertexWorld(int v) => Unproject(frameVertices[v].X, frameVertices[v].Y, Mathf.Abs(frameVertices[v].Rhw));

        /// <summary>
        /// AddRP (0x537b00): files an RP whose vertices are already in the frame's buffers into its render list by its
        /// flags; <paramref name="rhw0"/> / <paramref name="rhw1"/> (vertices 0 and 1) are the depth-sort keys, the blend and
        /// <paramref name="mtPreset"/> only matter to the own-blend lists. Effect faces, quads and sprites are all pooled RPs
        /// (+0x14 = 1, +0x15 = 0: no depth writes in the default alpha list).
        /// </summary>
        void Submit(Texture texture, int flags, int first, int count, int src, int dst, int mtPreset, float rhw0, float rhw1, int primitiveType) {
            int rp = pendingRps.Count;
            pendingRps.Add(new PendingRp { Texture = texture, First = first, Count = count });

            uint key = TextureKey(texture);
            renderLists.Add(new FxRenderPrimitive {
                Address = (uint) rp, Vertices = 1, VertexCount = count, Texture = key, Surface = key, B14 = 1, B15 = 0,
                SrcBlend = src, DstBlend = dst, MtPreset = mtPreset, PrimitiveType = primitiveType, Rhw0 = rhw0, Rhw1 = rhw1,
            }, flags);
        }

        uint TextureKey(Texture texture) {
            if (texture == null) {
                return 0;
            }

            if (ReferenceEquals(texture, lastKeyedTexture)) {
                return lastTextureKey;
            }

            if (!textureKeys.TryGetValue(texture, out uint key)) {
                key = (uint) textureKeys.Count + 1;
                textureKeys[texture] = key;
            }

            lastKeyedTexture = texture;
            lastTextureKey = key;
            return key;
        }

        /// <summary>
        /// The frame end: CRenderer::Flush replayed on the device model (list order, sorts, states), then each draw's
        /// vertices appended to the index buffer in that order; a draw with the texture and state of the previous one
        /// extends its batch.
        /// </summary>
        void FlushRenderLists() {
            CollectActorLayers();
            FlushLists();
            AssignDraws();
            ApplyActorOrders();
        }

        /// <summary>The sorting order each actor layer got in AssignDraws (entry index, order); applied on the main thread.</summary>
        readonly List<(int entry, int order)> actorOrders = new List<(int, int)>(2048);
        bool actorOrdersClientOrder;

        /// <summary>The actor layers' places in the client's draw order, queued for the camera (Unity objects: main thread).</summary>
        void ApplyActorOrders() {
            if (!actorOrdersClientOrder) {
                return;
            }

            Ragnarok.Entities.RoClientDrawOrder.BeginAssign();
            for (int i = 0; i < actorOrders.Count; i++) {
                Ragnarok.Entities.RoClientDrawOrder.Assign(clientEntries[actorOrders[i].entry].Renderer, actorOrders[i].order);
            }
        }

        /// <summary>The actors' sprite layers of this frame (main thread: transforms, renderers).</summary>
        void CollectActorLayers() {
            using var collect = FlushCollectMarker.Auto();
            Ragnarok.Entities.RoClientDrawOrder.Collect(frameCamera, clientEntries);
        }

        /// <summary>CRenderer::Flush on the device model (no Unity calls: the render job runs it on the worker).</summary>
        void FlushLists() {
            device.Draws.Clear();
            // the actors' sprite layers join the effects in the client's lists (0x180 opaque, 0x18c translucent) as
            // placeholder RPs keyed by their sprite rhw: Flush's order is then the one draw order of the frame
            clientEntrySeen.Clear();
            for (int i = 0; i < clientEntries.Count; i++) {
                Ragnarok.Entities.RoClientDrawEntry entry = clientEntries[i];
                int placeholder = pendingRps.Count;
                pendingRps.Add(new PendingRp { Actor = i + 1 });
                renderLists.Add(new FxRenderPrimitive {
                    Address = (uint) placeholder, Vertices = 1, VertexCount = 1, B14 = 1, B15 = 0, SrcBlend = 5, DstBlend = 6,
                    PrimitiveType = 5, Rhw0 = entry.Key, Rhw1 = entry.Key,
                }, entry.Flags);
                clientEntrySeen.Add(false);
            }

            using (FlushListsMarker.Auto()) {
                renderLists.Flush(device);
                renderLists.Clear();
            }
        }

        /// <summary>Flush's draws in order: actor layers get their sorting orders, effect draws become batches and segments (main thread).</summary>
        void AssignDraws() {
            batches.Clear();
            drawRanges.Clear();
            segmentOrders.Clear();
            bool clientOrder = Ragnarok.Entities.RoClientDrawOrder.Enabled;
            using var assignMarker = FlushAssignMarker.Auto();
            // no Unity call here: the render job runs this after Flush; ApplyActorOrders hands the layers their orders
            actorOrders.Clear();
            actorOrdersClientOrder = clientOrder;
            int order = Ragnarok.Entities.RoClientDrawOrder.FirstSortingOrder;
            bool segmentOpen = false;
            List<FxRenderDraw> draws = device.Draws;
            for (int i = 0; i < draws.Count; i++) {
                FxRenderDraw d = draws[i];
                PendingRp rp = pendingRps[d.Rp];
                if (rp.Actor != 0) {
                    // the first draw of the layer places it (list 0x180 draws twice: alpha >= 0x9f, then the rest)
                    int e = rp.Actor - 1;
                    if (!clientEntrySeen[e]) {
                        clientEntrySeen[e] = true;
                        actorOrders.Add((e, order++));
                        segmentOpen = false;
                    }

                    continue;
                }

                if (!segmentOpen) {
                    segmentOrders.Add(clientOrder ? order++ : EffectSortingOrder);
                    segmentOpen = true;
                }

                int segment = segmentOrders.Count - 1;
                DrawState state = DrawState.From(d.State);
                if (batches.Count > 0) {
                    Batch last = batches[batches.Count - 1];
                    if (last.Segment == segment && ReferenceEquals(last.Texture, rp.Texture) && last.State.Equals(state) &&
                        last.First + last.Count == drawRanges.Count) {
                        // the batch goes on; a draw whose vertices follow the previous draw's (a prim's next face) extends
                        // its range, so the build copies one run instead of a range per face
                        VertexRange tail = drawRanges[drawRanges.Count - 1];
                        if (tail.Start + tail.Count == rp.First) {
                            drawRanges[drawRanges.Count - 1] = new VertexRange(tail.Start, tail.Count + rp.Count);
                        } else {
                            drawRanges.Add(new VertexRange(rp.First, rp.Count));
                            last.Count++;
                        }

                        last.VertexCount += rp.Count;
                        batches[batches.Count - 1] = last;
                        continue;
                    }
                }

                int start = drawRanges.Count;
                drawRanges.Add(new VertexRange(rp.First, rp.Count));
                batches.Add(new Batch { Texture = rp.Texture, State = state, First = start, Count = 1, VertexCount = rp.Count, Segment = segment });
            }

            pendingRps.Clear();
        }

        /// <summary>Diagnostics (!fxbridge): time spent in the face / quad / sprite callbacks, in Stopwatch ticks.</summary>
        public static bool TimeCallbacks;
        public static long CallbackTicks, CallbackCount;

        void OnFace(uint face, int flags) {
            if (!TimeCallbacks) {
                OnFaceCore(face, flags);
                return;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            OnFaceCore(face, flags);
            CallbackTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            CallbackCount++;
        }

        void OnFaceCore(uint face, int flags) {
            FxMemory m = runtime.M;
            int first = vertexCount;
            bool screenSpace = IsScreenSpaceVertex(face) && IsScreenSpaceVertex(face + FxRenderer.VertexSize) &&
                IsScreenSpaceVertex(face + 2 * FxRenderer.VertexSize);
            for (uint k = 0; k < 3; k++) {
                AppendVertex(face + k * FxRenderer.VertexSize, screenSpace: screenSpace);
            }

            // AddRP 0x537dd0: a pooled RP with the face's vertices, texture +0x60, blend +0x64 / +0x68, mtpreset 0
            Submit(ResolveTexture(m.U32(face + 0x60)), flags, first, 3, m.I32(face + 0x64), m.I32(face + 0x68), 0,
                m.F32(face + 0xc), m.F32(face + FxRenderer.VertexSize + 0xc), 4);
        }

        void OnQuad(uint quad, int flags) {
            if (!TimeCallbacks) {
                OnQuadCore(quad, flags);
                return;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            OnQuadCore(quad, flags);
            CallbackTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            CallbackCount++;
        }

        void OnQuadCore(uint quad, int flags) {
            FxMemory m = runtime.M;
            uint v = m.U32(quad);
            int first = vertexCount;
            // STR layers (flags 5) are drawn with a constant z on top of everything; ground-cell quads (flags 1: the
            // CGroundScope circle, cursor cell, /range) go through the depth-sorted alpha list at their real depth,
            // with clamped UVs
            bool overlay = (IsScreenSpaceVertex(v) && IsScreenSpaceVertex(v + FxRenderer.VertexSize) &&
                IsScreenSpaceVertex(v + 2 * FxRenderer.VertexSize) && IsScreenSpaceVertex(v + 3 * FxRenderer.VertexSize));
            if (!overlay) Ragnarok.Diagnostics.RoSkillSweepProbe.CountGroundQuad();
            // D3DPT_TRIANGLESTRIP (quad +0x24 = 5): (0,1,2) (1,3,2) expanded to a list
            for (int i = 0; i < QuadOrder.Length; i++) {
                AppendVertex(v + (uint) QuadOrder[i] * FxRenderer.VertexSize, screenSpace: overlay);
            }

            Texture texture = ResolveTexture(m.U32(quad + 8));
            if (!overlay && (object) texture != null && clampedTextures.Add(texture)) {
                pendingClamp.Add(texture);   // set to CLAMP before the draw, on the main thread
            }

            Submit(texture, flags, first, QuadOrder.Length, m.I32(quad + 0x18), m.I32(quad + 0x1c), m.I32(quad + 0x20),
                m.F32(v + 0xc), m.F32(v + FxRenderer.VertexSize + 0xc), 5);
        }

        /// <summary>
        /// CRenderObject::AddSprite (0xbd7930): a screen-space sprite quad. Four TL vertices at (Left, Top),
        /// (Right, Top), (Left, Bottom), (Right, Bottom) + 0.5 px, rotated about the rect centre by Angle
        /// (integer degrees, client trig tables); top vertices carry RhwTop, bottom ones RhwBottom (+ bias·2e-4).
        /// Mirrored clips have Left &gt; Right, which flips the quad by vertex order. Blend SRCALPHA / ONE when the
        /// flags equal 5, else SRCALPHA / INVSRCALPHA. See reference/packages/prims-sprites/NOTES.md.
        /// </summary>
        void OnSprite(FxSpriteDraw d) {
            if (!TimeCallbacks) {
                OnSpriteCore(d);
                return;
            }

            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            OnSpriteCore(d);
            CallbackTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            CallbackCount++;
        }

        void OnSpriteCore(FxSpriteDraw d) {
            Texture texture = ResolveSpriteTexture(d);
            if ((object) texture == null) {
                return;
            }

            float rhwTop = d.RhwTop + d.DepthBias * 2e-4f, rhwBottom = d.RhwBottom + d.DepthBias * 2e-4f;
            uint flags = d.Flags | runtime.M.U32(d.Object + 0x124);
            bool overlay = (flags & 0x200) != 0 || (IsScreenSpaceRhw(rhwTop) && IsScreenSpaceRhw(rhwBottom));
            if (overlay) {
                // damage numbers / MISS / crit star (+0x124 = 0x200) and screen-space sprites: never behind a sprite
                rhwTop = rhwBottom = OverlayRhw;
            }

            // the vertices of a screen-space sprite carry a negative rhw: the vertex shader keeps them on the screen
            float vertexRhwTop = overlay ? -rhwTop : rhwTop, vertexRhwBottom = overlay ? -rhwBottom : rhwBottom;

            float l = d.Left + 0.5f, r = d.Right + 0.5f, t = d.Top + 0.5f, b = d.Bottom + 0.5f;
            Vector2 p0 = new Vector2(l, t), p1 = new Vector2(r, t), p2 = new Vector2(l, b), p3 = new Vector2(r, b);
            int a = (int) d.Angle;
            if (a != 0) {
                float c = FxMath.GetCos(a), s = FxMath.GetSin(a);
                float hw = (d.Right - d.Left) * 0.5f, hh = (d.Bottom - d.Top) * 0.5f;
                float cx = d.Left + hw + d.OffsetX, cy = d.Top + hh + d.OffsetY;
                p0 = new Vector2(-hw * c + hh * s + cx, -hh * c - hw * s + cy);
                p1 = new Vector2(hw * c + hh * s + cx, -hh * c + hw * s + cy);
                p2 = new Vector2(-hw * c - hh * s + cx, hh * c - hw * s + cy);
                p3 = new Vector2(hw * c - hh * s + cx, hh * c + hw * s + cy);
            }

            if (Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled) {
                probeSpriteRects[d.Object] = Rect.MinMaxRect(d.Left, d.Top, d.Right, d.Bottom);
            }

            var color = new Color32((byte) (d.Argb >> 16), (byte) (d.Argb >> 8), (byte) d.Argb, (byte) (d.Argb >> 24));
            int first = vertexCount;
            // strip (0,1,2) (1,3,2) as a list; sprite textures are built top row last, so V is 1 at the top
            AppendSpriteVertex(p0, vertexRhwTop, new Vector2(0f, 1f), color);
            AppendSpriteVertex(p1, vertexRhwTop, new Vector2(1f, 1f), color);
            AppendSpriteVertex(p2, vertexRhwBottom, new Vector2(0f, 0f), color);
            AppendSpriteVertex(p1, vertexRhwTop, new Vector2(1f, 1f), color);
            AppendSpriteVertex(p3, vertexRhwBottom, new Vector2(1f, 0f), color);
            AppendSpriteVertex(p2, vertexRhwBottom, new Vector2(0f, 0f), color);

            // AddSprite 0xbd7930: SRCALPHA and ONE for flags 5, else INVSRCALPHA; v0 / v1 are the top edge
            Submit(texture, (int) flags, first, 6, 5, flags == 5 ? 2 : 6, 0, rhwTop, rhwTop, 5);
        }

        void AppendSpriteVertex(Vector2 screen, float rhw, Vector2 uv, Color32 color) {
            AddVertex(screen.x, screen.y, rhw, uv, color);
        }

        /// <summary>The Unity texture of the SPR image a draw refers to (palette images resolved through the sprite's palette).</summary>
        Texture ResolveSpriteTexture(in FxSpriteDraw d) {
            FxSprImage image = runtime.ResolveSpriteImage(d, out FxSprite sprite);
            return image == null ? null : SpriteTexture(image, sprite);
        }

        /// <summary>
        /// The host texture handle for an SPR image in engine memory (SprImg* <paramref name="img"/>, the sprite's palette at
        /// CSprRes + 0x110): the image is found in the CSprRes's palette (+0x510) or RGBA (+0x51c) image vector.
        /// </summary>
        uint SpriteImageTextureHandle(uint img, uint palette) {
            sprHandleCalls++;
            FxSprite sprite = runtime.GetSpriteModel(palette - 0x110);
            if (sprite == null) {
                sprHandleNoSprite++;
                return 0;
            }

            FxSprImage image = null;
            for (int type = 0; type < 2 && image == null; type++) {
                uint vector = sprite.Address + (type == 0 ? 0x510u : 0x51cu);
                uint begin = runtime.M.U32(vector), end = runtime.M.U32(vector + 4);
                for (uint at = begin, index = 0; at < end; at += 4, index++) {
                    if (runtime.M.U32(at) == img) {
                        image = sprite.Get(type, (int) index);
                        break;
                    }
                }
            }

            if (image == null) {
                sprHandleNoImage++;
                return 0;
            }

            Texture2D tex = SpriteTexture(image, sprite);
            if (!spriteTextureHandles.TryGetValue(tex, out uint handle)) {
                handle = (uint) (texturesByHandle.Count + 1);
                texturesByHandle[handle] = tex;
                spriteTextureHandles[tex] = handle;
            }

            return handle;
        }

        readonly Dictionary<Texture2D, uint> spriteTextureHandles = new Dictionary<Texture2D, uint>();
        /// <summary>Diagnostics (!classic): sprite-image texture requests, and those without a sprite / image.</summary>
        int sprHandleCalls, sprHandleNoSprite, sprHandleNoImage;

        Texture2D SpriteTexture(FxSprImage image, FxSprite sprite) {
            // reference check (the render job calls this off the main thread); a new texture is made on the main thread,
            // which the job waits for once per image
            if (spriteTextures.TryGetValue(image, out Texture2D tex) && (object) tex != null) {
                return tex;
            }

            return CreateSpriteTextureOnMain(image, sprite);
        }

        /// <summary>The slow path apart: a lambda in <see cref="SpriteTexture"/> captured its locals, so every call (also a
        /// cache hit) allocated the closure (~120 a frame with 300 effects).</summary>
        Texture2D CreateSpriteTextureOnMain(FxSprImage image, FxSprite sprite) {
            Texture2D tex = null;
            RunOnMain(() => tex = CreateSpriteTexture(image, sprite));
            return tex;
        }

        Texture2D CreateSpriteTexture(FxSprImage image, FxSprite sprite) {
            if (spriteTextures.TryGetValue(image, out Texture2D tex) && tex != null) {
                return tex;
            }

            int pixelCount = image.Width * image.Height;
            if (spritePixels.Length < pixelCount) {
                spritePixels = new Color32[Mathf.NextPowerOfTwo(pixelCount)];
            }

            Color32[] pixels = spritePixels;
            for (int y = 0; y < image.Height; y++) {
                int srcRow = y * image.Width;
                int dstRow = (image.Height - 1 - y) * image.Width;   // Unity rows run bottom-up
                for (int x = 0; x < image.Width; x++) {
                    uint argb;
                    if (image.Indices != null) {
                        byte index = image.Indices[srcRow + x];
                        argb = index == 0 ? 0u : sprite.Palette[index];
                        // near-magenta palette entries are the client's colour key (FUN_0054d5d0 → 0x421)
                        if (index != 0 && (argb & 0xfe00fe) == 0xfe00fe && ((argb >> 8) & 0xff) < 4) {
                            argb = 0;
                        }
                    } else {
                        argb = image.Argb[srcRow + x];
                    }

                    pixels[dstRow + x] = new Color32((byte) (argb >> 16), (byte) (argb >> 8), (byte) argb, (byte) (argb >> 24));
                }
            }

            tex = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false) {
                name = "RO sprite " + FromEngine(sprite.Name),
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixelData(pixels, 0);   // copies Width·Height pixels of the shared buffer
            tex.Apply(false, true);
            spriteTextures[image] = tex;
            return tex;
        }

        Texture ResolveTexture(uint textureObject) {
            // reference checks only (the render job calls this off the main thread); a destroyed texture becomes the
            // white one at the draw (GetMaterial)
            if (textureObject == 0) {
                return whiteTexture;
            }

            if (textureObject == lastTextureObject && (object) lastResolvedTexture != null) {
                return lastResolvedTexture;
            }

            uint handle = runtime.M.U32(textureObject);
            Texture resolved = texturesByHandle.TryGetValue(handle, out Texture2D tex) && (object) tex != null ? tex : whiteTexture;
            lastTextureObject = textureObject;
            lastResolvedTexture = resolved;
            return resolved;
        }

        const MeshUpdateFlags QuietUpdate = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontNotifyMeshUsers;

        /// <summary>
        /// Submits the frame: the whole vertex list goes up once, each batch is a sub-mesh drawn with its own pooled
        /// material (render queue = submission order), so a frame costs a few uploads instead of a mesh per batch.
        /// </summary>
        void AddVertex(float sx, float sy, float rhw, Vector2 uv, Color32 color) {
            if (vertexCount == frameVertices.Length) {
                Array.Resize(ref frameVertices, vertexCount * 2);
            }

            ref FxVertex v = ref frameVertices[vertexCount++];
            v.X = sx;
            v.Y = sy;
            v.Rhw = rhw;
            v.Color = color;
            v.Uv = uv;
        }

        /// <summary>Vertex <paramref name="k"/> of a batch, through its draw ranges, as a world position (diagnostics).</summary>
        Vector3 BatchVertex(Batch b, int k) {
            for (int r = b.First; r < b.First + b.Count; r++) {
                VertexRange range = drawRanges[r];
                if (k < range.Count) {
                    return VertexWorld(range.Start + k);
                }

                k -= range.Count;
            }

            return Vector3.zero;
        }

        void EnsureSegmentCapacity(int count) {
            if (count <= segmentVertices.Length) {
                return;
            }

            Array.Resize(ref segmentVertices, Math.Max(count, segmentVertices.Length * 2));
        }

        static int[] IdentityIndices(int count) {
            if (identityIndices.Length < count) {
                int size = Math.Max(count, identityIndices.Length * 2);
                identityIndices = new int[size];
                for (int i = 0; i < size; i++) {
                    identityIndices[i] = i;
                }
            }

            return identityIndices;
        }

        void Draw(bool listsFlushed = false) {
            using (DrawFlushMarker.Auto()) {
                if (listsFlushed) {
                    ApplyActorOrders();   // the render job collected, flushed and batched them
                } else {
                    FlushRenderLists();
                }
            }

            // the vertex shader's projection of the TL vertices: the inverse of the core's (centre, factors), then the
            // camera-to-world of the frame the core projected (the job's frame when threaded, this one otherwise)
            Shader.SetGlobalVector(FxScreenId, drawScreen);
            Shader.SetGlobalMatrix(FxCameraToWorldId, drawCameraToWorld);
            for (int i = 0; i < pendingClamp.Count; i++) {
                Texture t = pendingClamp[i];
                if (t != null && t.wrapMode != TextureWrapMode.Clamp) {
                    t.wrapMode = TextureWrapMode.Clamp;
                }
            }

            pendingClamp.Clear();

            if (batches.Count == 0) {
                for (int i = 0; i < segmentRenderers.Count; i++) {
                    segmentRenderers[i].enabled = false;
                }

                if (frameRenderer != null) {
                    frameRenderer.enabled = false;
                }

                return;
            }

            if (shader == null) {
                shader = (DebugPlainShader ? null : Shader.Find(ShaderName)) ?? Shader.Find("Sprites/Default");
            }

            // one renderer per run of effect draws between actor layers, sorted at that run's place in Flush's order
            int batch = 0;
            for (int s = 0; s < segmentOrders.Count; s++) {
                int segmentVertexCount = 0;
                subMeshes.Clear();
                frameMaterials.Clear();
                DrawBuildMarker.Begin();
                segmentFirstBatch = batch;
                for (; batch < batches.Count && batches[batch].Segment == s; batch++) {
                    Batch b = batches[batch];
                    int start = segmentVertexCount;
                    EnsureSegmentCapacity(segmentVertexCount + b.VertexCount);
                    DrawCopyMarker.Begin();
                    for (int r = b.First; r < b.First + b.Count; r++) {
                        VertexRange range = drawRanges[r];
                        Array.Copy(frameVertices, range.Start, segmentVertices, segmentVertexCount, range.Count);
                        segmentVertexCount += range.Count;
                    }

                    DrawCopyMarker.End();
                    subMeshes.Add(new SubMeshDescriptor(start, b.VertexCount, MeshTopology.Triangles));
                    DrawMaterialMarker.Begin();
                    frameMaterials.Add(GetMaterial(batch, b));
                    DrawMaterialMarker.End();
                }

                int vertexCount = segmentVertexCount;
                for (int k = 0; k < subMeshes.Count; k++) {
                    SubMeshDescriptor sub = subMeshes[k];
                    sub.firstVertex = 0;
                    sub.vertexCount = vertexCount;
                    subMeshes[k] = sub;
                }

                DrawBuildMarker.End();
                using var upload = DrawUploadMarker.Auto();
                MeshRenderer renderer = EnsureSegmentRenderer(s, out Mesh mesh);
                if (mesh.vertexCount < vertexCount) {
                    // the buffers only grow: the identity index buffer goes up once per growth, not every frame (~200 KB
                    // a frame at 300 effects before); vertices past this frame's count are left over and unreferenced
                    int capacity = Math.Max(Math.Max(vertexCount, mesh.vertexCount * 2), 4096);
                    mesh.Clear(false);
                    mesh.SetVertexBufferParams(capacity, FxVertexLayout);
                    mesh.SetIndexBufferParams(capacity, IndexFormat.UInt32);
                    mesh.SetIndexBufferData(IdentityIndices(capacity), 0, 0, capacity, QuietUpdate);
                }

                mesh.SetVertexBufferData(segmentVertices, 0, 0, vertexCount, 0, QuietUpdate);
                mesh.SetSubMeshes(subMeshes, QuietUpdate);
                mesh.bounds = new Bounds(frameCamera.transform.position, Vector3.one * 10000f);
                renderer.SetSharedMaterials(frameMaterials);
                renderer.sortingOrder = segmentOrders[s];
                renderer.enabled = true;
            }

            for (int i = segmentOrders.Count; i < segmentRenderers.Count; i++) {
                segmentRenderers[i].enabled = false;
            }
        }

        /// <summary>The renderer of effect segment <paramref name="index"/> (0 = the frame renderer), created on first use.</summary>
        MeshRenderer EnsureSegmentRenderer(int index, out Mesh mesh) {
            EnsureFrameRenderer();
            if (segmentRenderers.Count == 0) {
                segmentRenderers.Add(frameRenderer);
                segmentMeshes.Add(frameMesh);
            }

            while (segmentRenderers.Count <= index) {
                var segmentMesh = new Mesh { name = "RO classic effects " + segmentRenderers.Count, hideFlags = HideFlags.DontSave };
                segmentMesh.MarkDynamic();
                var go = new GameObject("RO classic effect batch " + segmentRenderers.Count) { hideFlags = HideFlags.DontSave };
                go.AddComponent<MeshFilter>().sharedMesh = segmentMesh;
                MeshRenderer segmentRenderer = go.AddComponent<MeshRenderer>();
                segmentRenderer.shadowCastingMode = ShadowCastingMode.Off;
                segmentRenderer.receiveShadows = false;
                segmentRenderer.lightProbeUsage = LightProbeUsage.Off;
                segmentRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                segmentRenderers.Add(segmentRenderer);
                segmentMeshes.Add(segmentMesh);
            }

            mesh = segmentMeshes[index];
            return segmentRenderers[index];
        }

        /// <summary>
        /// Effect renderers of bridges that are gone without their OnDestroy (a domain reload drops the references while
        /// the DontSave objects stay): they kept drawing their last frame, a white ring in every scene, in and out of
        /// Play mode. Removed when a bridge is initialised, before it builds its own.
        /// </summary>
        void DestroyOrphanedRenderers() {
            var live = new HashSet<MeshRenderer>();
            foreach (RoClassicEffectBridge bridge in Resources.FindObjectsOfTypeAll<RoClassicEffectBridge>()) {
                if (bridge != null && bridge.frameRenderer != null) live.Add(bridge.frameRenderer);
            }

            foreach (MeshRenderer r in Resources.FindObjectsOfTypeAll<MeshRenderer>()) {
                if (r == null || live.Contains(r) || (r.gameObject.hideFlags & HideFlags.DontSave) == 0) {
                    continue;
                }

                string name = r.gameObject.name;
                if (name == "RO classic effects" || name.StartsWith("RO classic effect batch", StringComparison.Ordinal)) {
                    DestroyImmediate(r.gameObject);
                }
            }
        }

        /// <summary>The scene mesh renderer of the frame mesh, like the other effect renderers of the game; drawn by every camera.</summary>
        void EnsureFrameRenderer() {
            if (frameRenderer != null) {
                return;
            }

            frameMesh = new Mesh { name = "RO classic effects", hideFlags = HideFlags.DontSave };
            frameMesh.MarkDynamic();
            // vertices are world-space: the object stays unparented at the world identity (the map root is offset
            // and possibly scaled); OnDestroy removes it with the bridge
            var go = new GameObject("RO classic effects") { hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = frameMesh;
            frameRenderer = go.AddComponent<MeshRenderer>();
            frameRenderer.shadowCastingMode = ShadowCastingMode.Off;
            frameRenderer.receiveShadows = false;
            frameRenderer.lightProbeUsage = LightProbeUsage.Off;
            frameRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // transparents sort by sorting order before the render queue: the actors' orders (screen-y buckets × stride,
            // up to ~24200) drew every actor after the effects, over numbers and Endure whatever their depth; the client
            // draws its effect lists after the sprite list 0x180, so the frame sorts above every actor (below the
            // weather layer at 25000 and the overlay text at 30000)
            frameRenderer.sortingOrder = EffectSortingOrder;
        }

        /// <summary>The frame renderer's sorting order: above every actor (RoOnlineActorPresenter.CalculateActorSortingOrder).</summary>
        public const int EffectSortingOrder = 24900;

        /// <summary>One material per draw position: render queues keep the order of the client's Flush.</summary>
        Material GetMaterial(int index, Batch b) {
            while (materialPool.Count <= index) {
                materialPool.Add(new Material(shader) { hideFlags = HideFlags.DontSave, name = "RO classic effect" });
            }

            Material material = materialPool[index];
            // one render queue with the actor layers under the client's draw order (RoClientDrawOrder): transparents sort
            // by queue as well, and a higher effect queue kept every effect after every actor whatever the sorting order
            int queue = Ragnarok.Entities.RoClientDrawOrder.Enabled
                ? Ragnarok.Entities.RoClientDrawOrder.SharedRenderQueue + (OrderedQueues ? Mathf.Min(1 + index - segmentFirstBatch, 1900) : 0)
                : FaceQueue + index;
            while (materialState.Count <= index) {
                materialState.Add((null, default, -1));
            }

            if (ExactDeviceState) queue |= 1 << 20;   // a switch of the mapping re-applies every pooled material
            if (KeyedMaterials) {
                return GetKeyedMaterial(b, queue);
            }

            var state = (b.Texture, b.State, queue);
            if (materialState[index].texture == state.Texture && materialState[index].state.Equals(state.State) &&
                materialState[index].queue == queue) {
                return material;   // same draw state as last frame: nothing to upload
            }

            using var set = DrawMaterialSetMarker.Auto();   // diagnostics: how many pooled materials change a frame
            var previous = materialState[index];
            materialState[index] = state;
            if (previous.queue == queue && previous.state.Equals(state.State) && previous.texture != null) {
                // the batch at this position changed texture only (the common case in a busy frame: ~90 % of the pooled
                // materials change every frame, nearly all of them by texture): one texture write instead of ~14 sets
                material.SetTexture(BaseMapId, b.Texture);
                return material;
            }

            ConfigureMaterial(material, b, queue);
            return material;
        }

        /// <summary>
        /// !toggle fxkeyed: one material per (texture, draw state, queue), made once and reused, instead of a material per
        /// draw position whose texture changes nearly every frame (~500 texture writes a frame at 300 effects). With the
        /// shared queue (!toggle fxqueue off) a frame needs as many materials as it has (texture, state) pairs.
        /// </summary>
        public static bool KeyedMaterials = true;

        readonly Dictionary<(Texture texture, DrawState state, int queue), Material> keyedMaterials =
            new Dictionary<(Texture, DrawState, int), Material>();

        Material GetKeyedMaterial(Batch b, int queue) {
            var key = (b.Texture, b.State, queue);
            if (keyedMaterials.TryGetValue(key, out Material material) && material != null) {
                return material;
            }

            using var set = DrawMaterialSetMarker.Auto();
            if (keyedMaterials.Count >= 8192) {
                // a long session with per-position queues: start over rather than grow without bound
                foreach (Material m in keyedMaterials.Values) {
                    if (m != null) Destroy(m);
                }

                keyedMaterials.Clear();
            }

            material = new Material(shader) { hideFlags = HideFlags.DontSave, name = "RO classic effect (keyed)" };
            ConfigureMaterial(material, b, queue);
            keyedMaterials[key] = material;
            return material;
        }

        void ConfigureMaterial(Material material, Batch b, int queue) {
            DrawState d = b.State;
            RoEffectBlendState blend = RoEffectBlendState.FromD3dBlend((ulong) d.Src, (ulong) d.Dst, preserveD3dBlend: ExactDeviceState);
            BlendMode srcBlend = blend.SourceBlend, dstBlend = blend.DestinationBlend;
            if (ExactDeviceState) {
                srcBlend = OpaqueTargetBlend(srcBlend);
                dstBlend = OpaqueTargetBlend(dstBlend);
            }

            material.renderQueue = queue & 0xfffff;
            material.SetTexture(BaseMapId, b.Texture);
            if (material.HasProperty(MainTexId)) {
                material.SetTexture(MainTexId, b.Texture);
            }
            material.SetColor(BaseColorId, Color.white);
            // the draw's own blend for the gamma-blend emulation (RoClassicEffect.shader); an additive draw blends as a
            // screen (One, OneMinusSrcColor) there, so that stacked layers saturate at white like the original's clamped
            // add instead of summing past it in the HDR target
            material.SetFloat(BlendSrcId, (float) srcBlend);
            material.SetFloat(BlendDstId, (float) dstBlend);
            if (Shader.GetGlobalFloat(LookFxGammaBlendId) > 0.5f && !blend.MultiplyAlpha && dstBlend == BlendMode.One &&
                (srcBlend == BlendMode.One || srcBlend == BlendMode.SrcAlpha)) {
                srcBlend = BlendMode.One;
                dstBlend = BlendMode.OneMinusSrcColor;
            }

            material.SetFloat(SrcBlendId, (float) srcBlend);
            material.SetFloat(DstBlendId, (float) dstBlend);
            material.SetFloat(ColorOpId, d.ColorOp);
            material.SetFloat(AlphaOpId, d.AlphaOp);
            material.SetFloat(ZWriteId, d.ZWrite ? 1f : 0f);
            material.SetFloat(CullId, (float) CullMode.Off);
            material.SetFloat(ZTestId, (float) (d.ZTest ? CompareFunction.LessEqual : CompareFunction.Always));
            material.SetFloat(AlphaRefId, d.AlphaRef);
            material.SetFloat(AlphaLessId, d.AlphaLess ? 1f : 0f);
            material.SetFloat(FogEnabledId, 0f);
            if (blend.MultiplyAlpha) {
                material.EnableKeyword("_RO_MULTIPLY_ALPHA");
            } else {
                material.DisableKeyword("_RO_MULTIPLY_ALPHA");
            }
        }

        /// <summary>Diagnostics (!fxkinds): receives every timed step of the world (kind, effect id / prim type, Stopwatch ticks).</summary>
        public static Action<FxProfileKind, int, long> StepProfiler;

        static readonly int ColorOpId = Shader.PropertyToID("_RoColorOp"), AlphaOpId = Shader.PropertyToID("_RoAlphaOp");
        /// <summary>The first batch of the segment being built (the queue of a batch counts from it).</summary>
        int segmentFirstBatch;

        /// <summary>A blend factor on a target without an alpha channel: destination alpha is 1.</summary>
        static BlendMode OpaqueTargetBlend(BlendMode mode) =>
            mode == BlendMode.DstAlpha ? BlendMode.One : mode == BlendMode.OneMinusDstAlpha ? BlendMode.Zero : mode;

        // ---- IFxHost / IFxResourceProvider ----------------------------------------------------

        public bool TryGetTexture(string name, out uint handle, out int imageWidth, out int imageHeight, out int surfaceWidth, out int surfaceHeight) =>
            TryGetTexture(name, false, out handle, out imageWidth, out imageHeight, out surfaceWidth, out surfaceHeight);

        /// <summary>
        /// The texture of <paramref name="name"/>; with <paramref name="blackKey"/> (STR layer textures, see
        /// <see cref="IFxBlackKeyTextureHost"/>) a copy in which pure black texels are transparent (alpha 0, so the alpha
        /// test drops them under ONE / ZERO and they add nothing under SRCALPHA blends).
        /// </summary>
        public bool TryGetTexture(string name, bool blackKey, out uint handle, out int imageWidth, out int imageHeight, out int surfaceWidth, out int surfaceHeight) {
            string cacheName = blackKey ? name + "\u0001" : name;
            if (!OnMainThread) {
                // the render job: a loaded texture from the caches, a new one through the main thread (once per name)
                if (handlesByName.TryGetValue(cacheName, out handle) && textureSizes.TryGetValue(handle, out Vector2Int known)) {
                    imageWidth = surfaceWidth = known.x;
                    imageHeight = surfaceHeight = known.y;
                    return true;
                }

                bool found = false;
                uint loaded = 0;
                int w = 0, h = 0;
                RunOnMain(() => found = TryGetTexture(name, blackKey, out loaded, out w, out h, out _, out _));
                handle = loaded;
                imageWidth = surfaceWidth = w;
                imageHeight = surfaceHeight = h;
                return found;
            }

            using var resource = ResourceMarker.Auto();
            handle = 0;
            imageWidth = imageHeight = surfaceWidth = surfaceHeight = 0;
            if (!handlesByName.TryGetValue(cacheName, out handle)) {
                long start = Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                Texture2D tex = assets?.LoadTexture("data\\texture\\" + FromEngine(name));
                if (tex != null && blackKey && name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)) {
                    // the key only matters to images without an alpha channel: a TGA's own alpha decides (a black texel
                    // the artist left opaque there is meant to be seen)
                    tex = BlackKeyedCopy(tex);
                }

                if (start != 0) Ragnarok.Diagnostics.RoSkillSweepProbe.AddResourceTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
                if (tex == null) {
                    return false;
                }

                handle = (uint) (texturesByHandle.Count + 1);
                texturesByHandle[handle] = tex;
                handlesByName[cacheName] = handle;
            }

            Texture2D t = texturesByHandle[handle];
            // Unity textures hold exactly the image, so surface == image and the client's UV ratio is 1.
            imageWidth = surfaceWidth = t.width;
            imageHeight = surfaceHeight = t.height;
            textureSizes[handle] = new Vector2Int(imageWidth, imageHeight);
            return true;
        }

        /// <summary>Black-keyed copies made by this bridge (the asset database owns the originals).</summary>
        readonly List<Texture2D> blackKeyedTextures = new List<Texture2D>();

        /// <summary>A copy of <paramref name="source"/> with every pure black texel transparent (the source when it has none or cannot be read).</summary>
        Texture2D BlackKeyedCopy(Texture2D source) {
            if (!source.isReadable) {
                return source;
            }

            Color32[] pixels = source.GetPixels32();
            bool keyed = false;
            for (int i = 0; i < pixels.Length; i++) {
                Color32 c = pixels[i];
                if (c.r == 0 && c.g == 0 && c.b == 0 && c.a != 0) {
                    pixels[i] = default;
                    keyed = true;
                }
            }

            if (!keyed) {
                return source;
            }

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear: false) {
                name = source.name + " (black key)",
                hideFlags = HideFlags.DontSave,
                filterMode = source.filterMode,
                wrapMode = source.wrapMode,
            };
            copy.SetPixels32(pixels);
            copy.Apply(false, true);
            blackKeyedTextures.Add(copy);
            return copy;
        }

        /// <summary>The image size of every loaded texture handle (the render job cannot ask a texture for it).</summary>
        readonly Dictionary<uint, Vector2Int> textureSizes = new Dictionary<uint, Vector2Int>();

        /// <summary>
        /// PlaySound3D (0x5e5070): the engine passes the sound's offset from its player object, which mirrors the local
        /// player (FxWorld.SyncPlayerObject), with the client's distances (usually 250 / 40 RO units). Without the local
        /// player's shim the player object stays at the RO origin and (x, z) are absolute.
        /// </summary>
        public void PlaySound3D(string wav, float x, float y, float z, int maxDist, int minDist, float factor, int loop) {
            long start = Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            // the engine's state is read here, the audio plays on the main thread (queued from the render job)
            bool relative = runtime != null && runtime.PlayerAid != 0 && world != null && world.TryGetActor(runtime.PlayerAid, out _);
            string name = FromEngine(wav);
            if (relative) {
                PostToMain(() => RoAudioEvents.PlayEffectRelative(name, x, y, z, maxDist, minDist, factor));
            } else {
                Vector3 at = ToUnity(new Vector3(x, 0f, z));
                PostToMain(() => RoAudioEvents.PlayEffect(name, at, factor));
            }

            if (start != 0) Ragnarok.Diagnostics.RoSkillSweepProbe.AddAudioTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
        }

        public byte[] LoadEffectFile(string strName) {
            if (!OnMainThread) {
                byte[] read = null;
                RunOnMain(() => read = LoadEffectFile(strName));
                return read;
            }

            using var resource = ResourceMarker.Auto();
            long start = Ragnarok.Diagnostics.RoSkillSweepProbe.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            byte[] bytes = assets?.ReadBytes("data\\texture\\effect\\" + FromEngine(strName));
            if (start != 0) Ragnarok.Diagnostics.RoSkillSweepProbe.AddResourceTicks(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            return bytes;
        }

        void OnDestroy() {
            StopRenderThread();
            if (frameMesh != null) {
                Destroy(frameMesh);
            }

            foreach (Texture2D tex in blackKeyedTextures) {
                if (tex != null) {
                    Destroy(tex);
                }
            }

            foreach (Texture2D tex in spriteTextures.Values) {
                if (tex != null) {
                    Destroy(tex);
                }
            }

            if (frameRenderer != null) {
                Destroy(frameRenderer.gameObject);
            }

            // the segment renderers past the frame renderer (index 0) and their meshes
            for (int i = 1; i < segmentRenderers.Count; i++) {
                if (segmentRenderers[i] != null) Destroy(segmentRenderers[i].gameObject);
                if (segmentMeshes[i] != null) Destroy(segmentMeshes[i]);
            }

            foreach (Material material in materialPool) {
                if (material != null) {
                    Destroy(material);
                }
            }

            foreach (Material material in keyedMaterials.Values) {
                if (material != null) {
                    Destroy(material);
                }
            }

            // the world's heap is native memory the GC does not see: release it now, not at finalization (in the
            // Editor a stopped Play session's heap otherwise stays committed until the domain goes, if ever)
            if (runtime != null) {
                runtime.M.Dispose();
                runtime = null;
                world = null;
            }

            if (Instance == this) {
                Instance = null;
            }
        }
    }

    /// <summary>Identity comparison for Unity objects used as keys (Object.Equals / GetHashCode go through the engine).</summary>
    sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
