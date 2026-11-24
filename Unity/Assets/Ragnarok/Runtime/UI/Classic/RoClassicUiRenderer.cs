using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.UI.Classic {
    /// <summary>A UI layer (a window part, the chat, the world overlays...). Rewrite <see cref="Quads"/>, then MarkDirty.</summary>
    public sealed class RoClassicUiLayer {
        internal int RangeStartQuad;
        internal int RangeCapacityQuads;
        internal int UploadedQuads = -1;
        internal bool Dirty = true;
        internal bool visible = true;
        internal RoClassicUiRenderer Owner;

        public readonly RoClassicUiQuads Quads = new RoClassicUiQuads();
        public string Name;

        public bool Visible {
            get => visible;
            set {
                if (visible == value) return;
                visible = value;
                Owner?.MarkIndicesDirty();
            }
        }

        internal bool aboveOverlayUi;
        internal byte groupAlpha = 0xff;

        /// <summary>
        /// The opacity of the layer as one surface (a dragged window at 0x80): the layer is drawn opaque into a target of
        /// its own and that is blended once, like the client blits a window's surface. Per-vertex alpha would let the
        /// window's own parts show through each other (icons over cells over the frame came out nearly opaque, the frame
        /// alone nearly clear).
        /// </summary>
        public byte GroupAlpha {
            get => groupAlpha;
            set {
                if (groupAlpha == value) return;
                groupAlpha = value;
                Owner?.MarkIndicesDirty();
            }
        }

        /// <summary>
        /// Drawn after URP's screen-space overlay UGUI (windows, while legacy UGUI windows coexist) instead of before it
        /// (overhead gauges and balloons, which belong under every window).
        /// </summary>
        public bool AboveOverlayUi {
            get => aboveOverlayUi;
            set {
                if (aboveOverlayUi == value) return;
                aboveOverlayUi = value;
                Owner?.MarkIndicesDirty();
            }
        }

        public void MarkDirty() => Dirty = true;
    }

    /// <summary>
    /// The whole UI in one draw call: every layer owns a range of one GPU vertex buffer (a dirty layer uploads only its
    /// range with GraphicsBuffer.SetData — a Mesh would re-send the whole buffer), the index buffer lists visible layers
    /// in z-order, world overlays follow per-frame anchors read in the vertex shader (their geometry never re-uploads).
    /// <para>Drawn after the pipeline finished the target camera. <see cref="GammaExactBlending"/> renders the UI
    /// into a raw 8-bit target with premultiplied alpha (blending in gamma space like the original D3D7 client) and
    /// composites it over the scene in gamma space; otherwise it blends straight into the sRGB backbuffer (exact
    /// for opaque pixels, translucency blends in linear space).</para>
    /// </summary>
    public sealed class RoClassicUiRenderer : MonoBehaviour {
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("RO.ClassicUi.Upload");
        private static readonly ProfilerMarker DrawMarker = new ProfilerMarker("RO.ClassicUi.Draw");
        private static readonly int AtlasId = Shader.PropertyToID("_RoUiAtlas");
        private static readonly int AnchorsId = Shader.PropertyToID("_RoUiAnchors");
        private static readonly int SharpScaleId = Shader.PropertyToID("_RoUiSharpScale");
        private static readonly int LayerTexId = Shader.PropertyToID("_RoUiLayerTex");
        private static readonly int SceneTexId = Shader.PropertyToID("_RoUiSceneTex");
        private static readonly int VerticesId = Shader.PropertyToID("_RoUiVertices");
        private static readonly int TargetInfoId = Shader.PropertyToID("_RoUiTargetInfo");
        private static readonly int GroupTexId = Shader.PropertyToID("_RoUiGroupTex");
        private static readonly int GroupInfoId = Shader.PropertyToID("_RoUiGroupInfo");
        private const int VertexStride = 28;

        private const int PassDirect = 0;
        private const int PassGamma = 1;
        private const int PassComposite = 2;
        private const int PassGroupPremultiplied = 3;
        private const int PassGroupDirect = 4;

        private readonly List<RoClassicUiLayer> layers = new List<RoClassicUiLayer>();
        private GraphicsBuffer vertexBuffer;
        private int vertexCapacity;

        /// <summary>
        /// A run of visible layers' triangles in z-order: consecutive opaque layers share one; a layer with a group alpha
        /// is a run of its own (drawn through its own target). One run list per pass (before / after the overlay UGUI).
        /// </summary>
        private sealed class IndexList {
            public int[] Indices = new int[0];
            public GraphicsBuffer Buffer;
            public int Count;
            public int Capacity;
            public byte Alpha = 0xff;
        }

        private readonly List<IndexList> belowOverlay = new List<IndexList>();
        private readonly List<IndexList> aboveOverlay = new List<IndexList>();
        private readonly List<IndexList> spareLists = new List<IndexList>();
        private readonly List<RoClassicUiLayer> runScratch = new List<RoClassicUiLayer>();
        private bool layoutDirty = true;
        private bool indicesDirty = true;
        private CommandBuffer commands;
        private Material material;
        private RoClassicUiAtlas atlas;

        private Vector4[] anchors = new Vector4[256];
        private Vector4[] scaledAnchors = new Vector4[256];
        private float uiScale = 1f;
        private GraphicsBuffer anchorBuffer;
        private int anchorCount = 1;
        private bool anchorsDirty = true;
        private readonly Stack<int> freeAnchors = new Stack<int>();

        /// <summary>Set by the URP feature from its serialized reference (keeps the shader in builds).</summary>
        public static Shader DefaultShader;

        public UnityEngine.Camera TargetCamera;
        public bool GammaExactBlending = true;

        public int QuadCount => (IndexCount(belowOverlay) + IndexCount(aboveOverlay)) / 6;

        private static int IndexCount(List<IndexList> lists) {
            int n = 0;
            foreach (IndexList list in lists) n += list.Count;
            return n;
        }
        public int LastDrawCalls { get; private set; }

        public void Initialize(Shader shader, RoClassicUiAtlas uiAtlas, UnityEngine.Camera camera) {
            material = new Material(shader) { name = "RO classic UI", hideFlags = HideFlags.DontSave };
            atlas = uiAtlas;
            atlas.TextureChanged += tex => material.SetTexture(AtlasId, tex);
            material.SetTexture(AtlasId, atlas.Texture);
            TargetCamera = camera;
            anchors[0] = new Vector4(0f, 0f, 0f, 1f);
            SceneFlipY = false;
            LayerFlipY = SystemInfo.graphicsUVStartsAtTop; // verified by the bench colour/orientation checks on D3D11
        }

        /// <summary>The UI scale: client pixels (window layouts) to screen pixels.</summary>
        public float Scale => uiScale;

        /// <summary>
        /// Initialized and not torn down. A script reload during Play (a .cs saved while playing) keeps the components but
        /// clears these non-serialized fields, so the system stops drawing instead of failing every frame.
        /// </summary>
        public bool IsReady => atlas != null && material != null;

        /// <summary>
        /// The general UI scale (settings): the UI is laid out in client pixels and drawn at scale× through the projection;
        /// fractional values switch the atlas to sharp-bilinear. Anchors stay in screen pixels.
        /// </summary>
        public void SetScale(float scale) {
            if (!(scale > 0.01f)) scale = 1f;
            if (!Mathf.Approximately(uiScale, scale)) {
                uiScale = scale;
                anchorsDirty = true;
            }

            bool fractional = Mathf.Abs(scale - Mathf.Round(scale)) > 0.001f;
            atlas.FilterMode = fractional ? FilterMode.Bilinear : FilterMode.Point;
            if (fractional) material.EnableKeyword("RO_SHARP_BILINEAR");
            else material.DisableKeyword("RO_SHARP_BILINEAR");
            material.SetFloat(SharpScaleId, scale);
        }

        public RoClassicUiLayer CreateLayer(string name) {
            var layer = new RoClassicUiLayer { Name = name, Owner = this };
            layers.Add(layer);
            layoutDirty = true;
            return layer;
        }

        public void RemoveLayer(RoClassicUiLayer layer) {
            if (layers.Remove(layer)) {
                layer.Owner = null;
                layoutDirty = true;
            }
        }

        public void RemoveAllLayers() {
            foreach (RoClassicUiLayer layer in layers) layer.Owner = null;
            layers.Clear();
            layoutDirty = true;
        }

        /// <summary>Moves <paramref name="layer"/> to the top of the z-order (drawn last).</summary>
        public void BringToFront(RoClassicUiLayer layer) {
            int i = layers.IndexOf(layer);
            if (i < 0 || i == layers.Count - 1) return;
            layers.RemoveAt(i);
            layers.Add(layer);
            indicesDirty = true;
        }

        /// <summary>Moves <paramref name="layer"/> to the bottom of the z-order (overhead gauges and balloons).</summary>
        public void SendToBack(RoClassicUiLayer layer) {
            int i = layers.IndexOf(layer);
            if (i <= 0) return;
            layers.RemoveAt(i);
            layers.Insert(0, layer);
            layoutDirty = true;
        }

        internal void MarkIndicesDirty() => indicesDirty = true;

        public int AllocateAnchor() {
            int slot = freeAnchors.Count > 0 ? freeAnchors.Pop() : anchorCount++;
            if (slot >= anchors.Length) Array.Resize(ref anchors, anchors.Length * 2);
            anchors[slot] = default;
            anchorsDirty = true;
            return slot;
        }

        public void FreeAnchor(int slot) {
            if (slot <= 0) return;
            anchors[slot] = default;
            freeAnchors.Push(slot);
            anchorsDirty = true;
        }

        /// <summary>Places anchor <paramref name="slot"/> at integer screen pixels (top-left origin); hidden = collapsed.</summary>
        public void SetAnchor(int slot, int x, int y, bool visible) {
            var value = new Vector4(x, y, 0f, visible ? 1f : 0f);
            if (anchors[slot] != value) {
                anchors[slot] = value;
                anchorsDirty = true;
            }
        }

        private void OnEnable() {
            commands = new CommandBuffer { name = "RO classic UI" };
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Active.Add(this);
        }

        private void OnDisable() {
            Active.Remove(this);
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            commands?.Release();
            commands = null;
        }

        private void OnDestroy() {
            anchorBuffer?.Release();
            anchorBuffer = null;
            vertexBuffer?.Release();
            vertexBuffer = null;
            foreach (List<IndexList> lists in new[] { belowOverlay, aboveOverlay, spareLists }) {
                foreach (IndexList list in lists) {
                    list.Buffer?.Release();
                    list.Buffer = null;
                }
            }
            ReleaseTargets();
            if (material != null) Destroy(material);
        }

        /// <summary>Uploads atlas changes, dirty layer ranges, the index list and anchors. Call once per frame.</summary>
        public void Upload() {
            using (UploadMarker.Auto()) {
                atlas.Flush();
                foreach (RoClassicUiLayer layer in layers) {
                    if (layer.Quads.QuadCount > layer.RangeCapacityQuads) layoutDirty = true;
                }

                if (layoutDirty) Relayout();
                foreach (RoClassicUiLayer layer in layers) {
                    if (!layer.Dirty) continue;
                    layer.Dirty = false;
                    int count = layer.Quads.VertexCount;
                    if (count > 0) vertexBuffer.SetData(layer.Quads.Vertices, 0, layer.RangeStartQuad * 4, count);
                    if (layer.UploadedQuads != layer.Quads.QuadCount) indicesDirty = true;
                    layer.UploadedQuads = layer.Quads.QuadCount;
                }

                if (indicesDirty) RebuildIndices();
                if (anchorsDirty) UploadAnchors();
            }
        }

        private void Relayout() {
            layoutDirty = false;
            int start = 0;
            foreach (RoClassicUiLayer layer in layers) {
                int need = Mathf.Max(layer.Quads.QuadCount, 16);
                if (need > layer.RangeCapacityQuads) layer.RangeCapacityQuads = Mathf.NextPowerOfTwo(need);
                layer.RangeStartQuad = start;
                start += layer.RangeCapacityQuads;
                layer.Dirty = true;
            }

            int vertices = Mathf.Max(start * 4, 4);
            if (vertices > vertexCapacity || vertices < vertexCapacity / 4) {
                vertexCapacity = Mathf.NextPowerOfTwo(vertices);
                vertexBuffer?.Release();
                vertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCapacity, VertexStride);
                material.SetBuffer(VerticesId, vertexBuffer);
            }

            indicesDirty = true;
        }

        private void RebuildIndices() {
            indicesDirty = false;
            Fill(belowOverlay, false);
            Fill(aboveOverlay, true);
        }

        private void Fill(List<IndexList> lists, bool aboveOverlayUi) {
            foreach (IndexList old in lists) spareLists.Add(old);
            lists.Clear();
            runScratch.Clear();
            byte runAlpha = 0xff;
            foreach (RoClassicUiLayer layer in layers) {
                if (!layer.visible || layer.aboveOverlayUi != aboveOverlayUi || layer.Quads.QuadCount == 0) continue;
                // opaque layers run together; a layer with a group alpha stands alone
                if (runScratch.Count > 0 && (layer.groupAlpha != 0xff || runAlpha != 0xff)) {
                    lists.Add(FillRun(runScratch, runAlpha));
                    runScratch.Clear();
                }

                runAlpha = layer.groupAlpha;
                runScratch.Add(layer);
            }

            if (runScratch.Count > 0) lists.Add(FillRun(runScratch, runAlpha));
            runScratch.Clear();
        }

        private IndexList FillRun(List<RoClassicUiLayer> run, byte alpha) {
            IndexList list;
            if (spareLists.Count > 0) {
                list = spareLists[spareLists.Count - 1];
                spareLists.RemoveAt(spareLists.Count - 1);
            } else {
                list = new IndexList();
            }

            list.Alpha = alpha;
            int total = 0;
            foreach (RoClassicUiLayer layer in run) total += layer.Quads.QuadCount * 6;
            if (total > list.Indices.Length) list.Indices = new int[Mathf.NextPowerOfTwo(total)];
            int[] indices = list.Indices;
            int n = 0;
            foreach (RoClassicUiLayer layer in run) {
                int quads = layer.Quads.QuadCount;
                int v = layer.RangeStartQuad * 4;
                for (int q = 0; q < quads; q++, v += 4) {
                    indices[n++] = v;
                    indices[n++] = v + 1;
                    indices[n++] = v + 2;
                    indices[n++] = v;
                    indices[n++] = v + 2;
                    indices[n++] = v + 3;
                }
            }

            if (list.Buffer == null || indices.Length > list.Capacity) {
                list.Capacity = Mathf.Max(indices.Length, 6);
                list.Buffer?.Release();
                list.Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, list.Capacity, 4);
            }

            if (n > 0) list.Buffer.SetData(indices, 0, 0, n);
            list.Count = n;
            return list;
        }

        private void UploadAnchors() {
            anchorsDirty = false;
            if (anchorBuffer == null || anchorBuffer.count < anchors.Length) {
                anchorBuffer?.Release();
                anchorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, anchors.Length, 16);
                material.SetBuffer(AnchorsId, anchorBuffer);
            }

            // anchors arrive in screen pixels; the projection works in client pixels (screen / scale)
            if (scaledAnchors.Length < anchors.Length) scaledAnchors = new Vector4[anchors.Length];
            float inv = 1f / uiScale;
            for (int i = 0; i < anchorCount; i++) {
                Vector4 a = anchors[i];
                scaledAnchors[i] = new Vector4(a.x * inv, a.y * inv, a.z, a.w);
            }

            anchorBuffer.SetData(scaledAnchors, 0, 0, anchorCount);
        }

        private RenderTexture layerTarget;
        private RenderTexture sceneTarget;
        private RenderTexture groupTarget;

        /// <summary>Row mirroring of the Blit scene copy / the UI layer in the composite (graphics-API dependent).</summary>
        public bool SceneFlipY;
        public bool LayerFlipY;
        public RenderTexture DebugLayerTarget => layerTarget;
        public RenderTexture DebugSceneTarget => sceneTarget;

        private void EnsureTargets(int w, int h) {
            if (layerTarget != null && layerTarget.width == w && layerTarget.height == h) return;
            ReleaseTargets();
            layerTarget = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                name = "RO classic UI layer", filterMode = FilterMode.Point,
            };
            sceneTarget = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) {
                name = "RO classic UI scene copy", filterMode = FilterMode.Point,
            };
            layerTarget.Create();
            sceneTarget.Create();
        }

        private void ReleaseTargets() {
            // Release frees the GPU memory only; the RenderTexture objects go too (a new pair comes at every window resize)
            if (layerTarget != null) {
                layerTarget.Release();
                Destroy(layerTarget);
            }

            if (sceneTarget != null) {
                sceneTarget.Release();
                Destroy(sceneTarget);
            }

            if (groupTarget != null) {
                groupTarget.Release();
                Destroy(groupTarget);
            }

            layerTarget = sceneTarget = groupTarget = null;
        }

        /// <summary>The surface a layer with a group alpha is drawn into first (same layout as the UI layer target).</summary>
        private void EnsureGroupTarget(int w, int h) {
            if (groupTarget != null && groupTarget.width == w && groupTarget.height == h) return;
            if (groupTarget != null) {
                groupTarget.Release();
                Destroy(groupTarget);
            }

            groupTarget = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                name = "RO classic UI window surface", filterMode = FilterMode.Point,
            };
            groupTarget.Create();
        }

        private static readonly List<RoClassicUiRenderer> Active = new List<RoClassicUiRenderer>();
        private int featureFrame = -1;

        /// <summary>Diagnostics (!hud off): no camera gets the classic UI, for clean world captures.</summary>
        public static bool HiddenForCaptures;

        /// <summary>The renderer drawing into <paramref name="camera"/>, if any (used by the URP feature).</summary>
        public static RoClassicUiRenderer ForCamera(UnityEngine.Camera camera) {
            if (HiddenForCaptures) return null;
            for (int i = 0; i < Active.Count; i++) {
                if (Active[i].TargetCamera == camera) return Active[i];
            }

            return null;
        }

        /// <summary>
        /// Records a UI draw into <paramref name="cmd"/> targeting the final (backbuffer) colour: the layers below
        /// (overhead gauges, balloons) then the layers above (windows). The URP feature (Ragnarok.ClassicUiRender) draws
        /// it after all post-processing and before URP's screen-space overlay canvases, so the cursor canvas stays on
        /// top in the player too (URP draws those canvases itself there, before the camera ends); the end of the camera
        /// draws it only when no feature ran this frame.
        /// </summary>
        public bool RecordDraw(CommandBuffer cmd, RenderTargetIdentifier target, int w, int h, bool fromFeature) {
            if (fromFeature) {
                featureFrame = Time.frameCount;
            } else if (featureFrame == Time.frameCount) {
                return false;
            }

            if (material == null || vertexBuffer == null || IndexCount(belowOverlay) + IndexCount(aboveOverlay) == 0) return false;
            using (DrawMarker.Auto()) {
                if (anchorBuffer == null) UploadAnchors();
                Matrix4x4 ortho = Matrix4x4.Ortho(0, w / uiScale, h / uiScale, 0, -1, 1);
                // the UI layer is rendered unflipped and read back with Load at SV_Position (row mirroring per graphics
                // API, verified by the bench checks); the scene copy comes from Blit
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(ortho, false);
                if (GammaExactBlending) {
                    EnsureTargets(w, h);
                    cmd.Blit(target, sceneTarget);
                    cmd.SetRenderTarget(layerTarget);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                    int draws = DrawRuns(cmd, belowOverlay, PassGamma, layerTarget, w, h, true) +
                        DrawRuns(cmd, aboveOverlay, PassGamma, layerTarget, w, h, true);
                    cmd.SetRenderTarget(target);
                    cmd.SetGlobalTexture(LayerTexId, layerTarget);
                    cmd.SetGlobalTexture(SceneTexId, sceneTarget);
                    cmd.SetGlobalVector(TargetInfoId, new Vector4(w, h, SceneFlipY ? 1f : 0f, LayerFlipY ? 1f : 0f));
                    cmd.DrawProcedural(Matrix4x4.identity, material, PassComposite, MeshTopology.Triangles, 3);
                    LastDrawCalls = draws + 2;
                } else {
                    cmd.SetRenderTarget(target);
                    cmd.SetViewProjectionMatrices(Matrix4x4.identity, projection);
                    LastDrawCalls = DrawRuns(cmd, belowOverlay, PassDirect, target, w, h, false) +
                        DrawRuns(cmd, aboveOverlay, PassDirect, target, w, h, false);
                }
            }

            return true;
        }

        /// <summary>
        /// The runs in order: an opaque run straight into <paramref name="target"/>; a run with a group alpha into its own
        /// cleared surface (premultiplied gamma, opaque), then that surface blended once over <paramref name="target"/> —
        /// the UI layer target (premultiplied) or the camera target (the direct pass).
        /// </summary>
        private int DrawRuns(CommandBuffer cmd, List<IndexList> runs, int pass, RenderTargetIdentifier target, int w, int h, bool targetIsLayer) {
            int draws = 0;
            foreach (IndexList run in runs) {
                if (run.Count == 0) continue;
                if (run.Alpha == 0xff) {
                    draws += DrawList(cmd, run, pass);
                    continue;
                }

                EnsureGroupTarget(w, h);
                cmd.SetRenderTarget(groupTarget);
                cmd.ClearRenderTarget(false, true, Color.clear);
                draws += DrawList(cmd, run, PassGamma);
                cmd.SetRenderTarget(target);
                cmd.SetGlobalTexture(GroupTexId, groupTarget);
                // the window surface has the UI layer's memory layout: as is into the layer, mirrored like the layer
                // composite into the camera target
                cmd.SetGlobalVector(GroupInfoId, new Vector4(w, h, !targetIsLayer && LayerFlipY ? 1f : 0f, run.Alpha / 255f));
                cmd.DrawProcedural(Matrix4x4.identity, material, targetIsLayer ? PassGroupPremultiplied : PassGroupDirect, MeshTopology.Triangles, 3);
                draws++;
            }

            return draws;
        }

        private int DrawList(CommandBuffer cmd, IndexList list, int pass) {
            if (list == null || list.Count == 0) return 0;
            cmd.DrawProcedural(list.Buffer, Matrix4x4.identity, material, pass, MeshTopology.Triangles, list.Count);
            return 1;
        }

        private void OnEndCamera(ScriptableRenderContext context, UnityEngine.Camera camera) {
            // the whole UI when the URP feature did not run this frame
            if (camera != TargetCamera || commands == null || HiddenForCaptures) return;
            commands.Clear();
            if (RecordDraw(commands, BuiltinRenderTextureType.CameraTarget, camera.pixelWidth, camera.pixelHeight, false)) {
                context.ExecuteCommandBuffer(commands);
                context.Submit();
            }
        }
    }
}
