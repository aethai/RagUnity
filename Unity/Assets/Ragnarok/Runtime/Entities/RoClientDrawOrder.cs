using System.Collections.Generic;
using Ragnarok.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Entities {
    /// <summary>One actor sprite layer as the client's render lists see it: the list flags and the depth-sort key.</summary>
    public struct RoClientDrawEntry {
        public MeshRenderer Renderer;
        /// <summary>AddSprite flags: 0x101 = opaque sprite list 0x180, 1 = translucent list 0x18c.</summary>
        public int Flags;
        /// <summary>The top edge's rhw (the client's sort key of sprite quads) with the layer's AddSprite bias.</summary>
        public float Key;
        /// <summary>Diagnostics: the actor's ground point view depth (Unity units) and screen y (pixels from the bottom).</summary>
        public float FeetDepth, FeetScreenY;
    }

    /// <summary>
    /// The camera terms of one frame's draw entries, computed once per frame instead of per actor and layer (the
    /// camera's matrix getters and WorldToScreenPoint are native calls: ~4 per layer, ~2500 layers with 500 players).
    /// </summary>
    public readonly struct RoClientDrawFrame {
        public RoClientDrawFrame(UnityEngine.Camera camera) {
            View = camera.worldToCameraMatrix;
            ViewProjection = camera.projectionMatrix * View;
            NearClip = camera.nearClipPlane;
            Rect rect = camera.pixelRect;
            PixelRect = rect;
            PixelY = rect.y;
            PixelHeight = rect.height;
            PixelHeightInt = camera.pixelHeight;
            Transform cameraTransform = camera.transform;
            CameraUp = cameraTransform.up;
            // the camera direction the actors map their facing to (once a frame, not two native reads per actor)
            CameraDirection = RoSpriteActionMap.GetCameraDirectionFromYaw(cameraTransform.eulerAngles.y);
            Camera = camera;
            Frame = Time.frameCount;
        }

        public readonly Rect PixelRect;
        public readonly int PixelHeightInt;
        public readonly Vector3 CameraUp;
        public readonly RoActorDirection CameraDirection;
        public readonly UnityEngine.Camera Camera;
        public readonly int Frame;

        static RoClientDrawFrame cached;
        static bool hasCached;

        /// <summary>The frame data of <paramref name="camera"/> for this frame, computed at the first ask (actor gauges).</summary>
        public static RoClientDrawFrame ForCamera(UnityEngine.Camera camera) {
            if (!hasCached || cached.Frame != Time.frameCount || !ReferenceEquals(cached.Camera, camera)) {
                cached = new RoClientDrawFrame(camera);
                hasCached = true;
            }

            return cached;
        }

        /// <summary>Drops the cached frame data: the next ask reads the camera again (the camera may move later in a frame).</summary>
        public static void InvalidateCache() => hasCached = false;

        /// <summary>Camera.WorldToScreenPoint(world): pixels from the bottom left, z = the view depth.</summary>
        public Vector3 ScreenPoint(Vector3 world) {
            Matrix4x4 m = ViewProjection;
            float x = m.m00 * world.x + m.m01 * world.y + m.m02 * world.z + m.m03;
            float y = m.m10 * world.x + m.m11 * world.y + m.m12 * world.z + m.m13;
            float w = m.m30 * world.x + m.m31 * world.y + m.m32 * world.z + m.m33;
            return new Vector3((x / w * 0.5f + 0.5f) * PixelRect.width + PixelRect.x, (y / w * 0.5f + 0.5f) * PixelHeight + PixelY, w);
        }

        public readonly Matrix4x4 View;
        public readonly Matrix4x4 ViewProjection;
        public readonly float NearClip;
        public readonly float PixelY;
        public readonly float PixelHeight;

        /// <summary>The view depth of a world point (-z of the camera space, as worldToCameraMatrix gives it).</summary>
        public float ViewDepth(Vector3 world) => -View.MultiplyPoint3x4(world).z;

        /// <summary>Camera.WorldToScreenPoint(world).y: pixels from the bottom of the screen.</summary>
        public float ScreenY(Vector3 world) {
            Matrix4x4 m = ViewProjection;
            float y = m.m10 * world.x + m.m11 * world.y + m.m12 * world.z + m.m13;
            float w = m.m30 * world.x + m.m31 * world.y + m.m32 * world.z + m.m33;
            return (y / w * 0.5f + 0.5f) * PixelHeight + PixelY;
        }
    }

    /// <summary>
    /// The client's single draw order for actor sprites and effects (reference packages/sprite-effect-depth,
    /// render-lists, draw-order): every actor layer goes into the same render lists as the effect primitives, keyed by
    /// the client's sprite rhw, and CRenderer::Flush's order decides who is drawn first. The classic effect bridge feeds
    /// the layers in, replays Flush and hands every layer renderer its place (<see cref="Assign"/>); the orders are applied
    /// just before the camera renders, after every LateUpdate, so the presenter's screen-y sorting cannot undo them.
    /// Main thread only.
    /// </summary>
    public static class RoClientDrawOrder {
        /// <summary>Off = the old split (actors by screen-y sorting groups, effects above them all).</summary>
        public static bool Enabled {
            get => enabled;
            set {
                if (enabled == value) {
                    return;
                }

                enabled = value;
                // the actors' layers and sorting groups go back to (or leave) the presenter's screen-y order at once; the
                // presenter skipped that order while this one ran, so it computes it again for every actor
                RoActorLayeredVisual.ReapplySortingOrderForAll();
                Ragnarok.World.RoOnlineActorPresenter.RequestFullSync();
            }
        }

        static bool enabled = true;

        /// <summary>The sprite colour pass's pull toward the camera in view units (RoSpriteEnhanced _RoSpriteColorDepthPull), well
        /// under the layers' 0.01 steps.</summary>
        public const float ColorDepthPull = 0.003f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeShaderGlobals() => Shader.SetGlobalFloat("_RoSpriteColorDepthPull", ColorDepthPull);

        /// <summary>The first sorting order handed out (above the cell cursor at −1000, below the weather at 25000).</summary>
        public const int FirstSortingOrder = 100;

        /// <summary>The render queue every actor layer and every effect batch shares while the order is on (sorting order decides).</summary>
        public const int SharedRenderQueue = 3000;

        static readonly List<RoActorLayeredVisual> visuals = new List<RoActorLayeredVisual>(512);
        static readonly List<MeshRenderer> assignedRenderers = new List<MeshRenderer>(2048);
        static readonly List<int> assignedOrders = new List<int>(2048);
        static bool hooked;

        public static int VisualCount => visuals.Count;

        /// <summary>
        /// Diagnostics (!spritecut / !spritebias): overrides the depth cut / pull of every registered actor layer material;
        /// a negative value restores nothing (the next material rebuild does). Returns a summary of the first layers'
        /// vertex alpha range and material values.
        /// </summary>
        public static string DiagnoseDepthMaterials(float cut, float bias) {
            var text = new System.Text.StringBuilder();
            int layersSeen = 0;
            for (int i = 0; i < visuals.Count; i++) {
                RoActorLayeredVisual visual = visuals[i];
                if (visual == null) continue;
                foreach (MeshRenderer renderer in visual.GetComponentsInChildren<MeshRenderer>()) {
                    Material m = renderer.sharedMaterial;
                    if (m == null || !m.HasProperty("_RoSpriteDepthCutoff")) continue;
                    if (cut >= 0f) m.SetFloat("_RoSpriteDepthCutoff", cut);
                    if (bias >= 0f) m.SetFloat("_RoSpriteDepthBias", bias);
                    if (layersSeen++ < 6) {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        Mesh mesh = filter != null ? filter.sharedMesh : null;
                        float minA = 1f, maxA = 0f;
                        if (mesh != null && mesh.isReadable) {
                            foreach (Color32 c in mesh.colors32) { minA = Mathf.Min(minA, c.a / 255f); maxA = Mathf.Max(maxA, c.a / 255f); }
                        }
                        text.Append("\n  ").Append(renderer.name).Append(" cut ").Append(m.GetFloat("_RoSpriteDepthCutoff").ToString("F3"))
                            .Append(" bias ").Append(m.GetFloat("_RoSpriteDepthBias").ToString("F2"))
                            .Append(" baseA ").Append(m.GetColor("_BaseColor").a.ToString("F2"))
                            .Append(" vertexA ").Append(mesh == null ? "no mesh" : !mesh.isReadable ? "unreadable" : minA.ToString("F2") + ".." + maxA.ToString("F2"))
                            .Append(" feetOS ").Append(m.GetVector("_RoSpriteFeetOS").ToString("F2"))
                            .Append(" shader ").Append(m.shader.name);
                    }
                }
            }

            return layersSeen + " sprite layer materials" + text;
        }

        /// <summary>Diagnostics: the cart state of every registered actor with a cart, or null.</summary>
        public static string DescribeCartsForDiagnostics() {
            string text = null;
            for (int i = 0; i < visuals.Count; i++) {
                string cart = visuals[i] != null ? visuals[i].DescribeCartForDiagnostics() : null;
                if (cart != null) {
                    text = text == null ? cart : text + " | " + cart;
                }
            }

            return text;
        }

        internal static void Register(RoActorLayeredVisual visual) {
            if (visual != null && !visuals.Contains(visual)) {
                visuals.Add(visual);
            }

            Hook();
        }

        internal static void Unregister(RoActorLayeredVisual visual) {
            visuals.Remove(visual);
        }

        /// <summary>The draw entries of every registered actor this frame, for <paramref name="camera"/>.</summary>
        public static void Collect(UnityEngine.Camera camera, List<RoClientDrawEntry> into) {
            into.Clear();
            if (!Enabled || camera == null) {
                return;
            }

            var frame = new RoClientDrawFrame(camera);
            for (int i = 0; i < visuals.Count; i++) {
                RoActorLayeredVisual visual = visuals[i];
                if (visual != null) {
                    visual.CollectClientDrawEntries(camera, frame, into);
                }
            }
        }

        /// <summary>Starts a frame's assignment list.</summary>
        public static void BeginAssign() {
            assignedRenderers.Clear();
            assignedOrders.Clear();
        }

        /// <summary>Queues <paramref name="renderer"/> for <paramref name="sortingOrder"/> (applied before the camera renders).</summary>
        public static void Assign(MeshRenderer renderer, int sortingOrder) {
            assignedRenderers.Add(renderer);
            assignedOrders.Add(sortingOrder);
        }

        static void Hook() {
            if (hooked) {
                return;
            }

            hooked = true;
            RenderPipelineManager.beginContextRendering += ApplyAssignments;
        }

        static void ApplyAssignments(ScriptableRenderContext context, List<UnityEngine.Camera> cameras) {
            for (int i = 0; i < assignedRenderers.Count; i++) {
                MeshRenderer renderer = assignedRenderers[i];
                if (renderer != null && renderer.sortingOrder != assignedOrders[i]) {
                    renderer.sortingOrder = assignedOrders[i];
                }
            }
        }
    }
}
