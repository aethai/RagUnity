using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using ROIO.Utils;
using UnityEngine;

namespace Ragnarok.Maps {
    public sealed class RoModelAnimationController : MonoBehaviour {
        private const float DefaultFramesPerSecond = 30f;
        private const float RobrowserFrameDurationScale = 1.14f;
        private readonly Dictionary<RSM.Node, Transform> nodeTransforms = new Dictionary<RSM.Node, Transform>();
        private readonly Dictionary<string, RSM.Node> nodesByName = new Dictionary<string, RSM.Node>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<RSM.Node, Mat4> frameMatrices = new Dictionary<RSM.Node, Mat4>();
        private readonly Dictionary<RSM.Node, int> frameMatrixVersions = new Dictionary<RSM.Node, int>();
        private readonly Mat4 finalMatrix = new Mat4();
        private readonly Mat4 localMatrixScratch = new Mat4();
        private RSM model;
        private float startTimeSeconds;
        private float animationLength;
        private float frameDurationMilliseconds;
        private readonly List<Renderer> renderers = new List<Renderer>(8);
        private bool hasAnimation;
        private int lastAnimationStep = -1;
        private int frameMatrixVersion;

        // RSM2 (C3dActorV2): animation type / speed of the RSW instance, length in ms, node pass order.
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        private int rsm2AnimType;
        private float rsm2AnimSpeed = 1f;
        private float rsm2AnimLengthMs;
        private uint rsm2LastMs = uint.MaxValue;
        private RSM.Node[] rsm2Nodes;
        private int[] rsm2ParentIndices;
        private float[][] rsm2Worlds;
        private Transform[] rsm2Transforms;
        private readonly List<Rsm2TextureTarget> rsm2TextureTargets = new List<Rsm2TextureTarget>();
        private MaterialPropertyBlock rsm2PropertyBlock;

        private sealed class Rsm2TextureTarget {
            public Renderer Renderer;
            public RSM.Node Node;
            public int Slot;
            /// <summary>The slot has a rotation (type 4): its UVs are computed here, on an own copy of the mesh.</summary>
            public Mesh RotatedMesh;
            public Vector2[] BaseUvs;
            public List<Vector2> Uvs;
        }

        public void Initialize(RSM rsm, IReadOnlyDictionary<RSM.Node, Transform> transforms) {
            Initialize(rsm, transforms, null);
        }

        /// <summary>
        /// <paramref name="descriptor"/> is the RSW instance; an RSM2 model animates by its animation type and
        /// speed (C3dActorV2::vf06 0x9e8800).
        /// </summary>
        public void Initialize(RSM rsm, IReadOnlyDictionary<RSM.Node, Transform> transforms, RSW.ModelDescriptor descriptor) {
            model = rsm;
            rsm2AnimType = descriptor?.animType ?? 0;
            rsm2AnimSpeed = descriptor?.animSpeed ?? 1f;
            rsm2LastMs = uint.MaxValue;
            startTimeSeconds = Time.timeSinceLevelLoad;
            hasAnimation = HasAnimation(model) || (model != null && model.isRsm2 && HasRsm2TextureAnimation(model));
            animationLength = ResolveAnimationLength(model);
            frameDurationMilliseconds = GetRobrowserFrameDurationMilliseconds();
            renderers.Clear();
            GetComponentsInChildren(true, renderers);
            lastAnimationStep = -1;
            nodeTransforms.Clear();
            nodesByName.Clear();
            frameMatrices.Clear();
            frameMatrixVersions.Clear();
            frameMatrixVersion = 0;

            if (model?.nodes != null) {
                for (int i = 0; i < model.nodes.Length; i++) {
                    RSM.Node node = model.nodes[i];
                    if (node != null && !string.IsNullOrEmpty(node.name) && !nodesByName.ContainsKey(node.name)) {
                        nodesByName.Add(node.name, node);
                    }
                }
            }

            if (transforms != null) {
                foreach (KeyValuePair<RSM.Node, Transform> entry in transforms) {
                    if (entry.Key != null && entry.Value != null) {
                        nodeTransforms[entry.Key] = entry.Value;
                    }
                }
            }

            PrimeFrameMatrixCache();
            if (model != null && model.isRsm2) {
                InitializeRsm2();
            }

            ApplyFrameForTests(0f);
        }

        /// <summary>
        /// Whether an instance of <paramref name="rsm"/> needs the animation controller: RSM1 when it has keys;
        /// RSM2 only with RSW animation type 1 (play once) or 2 (loop) (C3dActorV2::vf06 0x9e8800; type 0 keeps
        /// the frame-0 pose AssignModel set) and node keys or a texture animation.
        /// </summary>
        public static bool ShouldAnimate(RSM rsm, RSW.ModelDescriptor descriptor) {
            if (rsm == null || !rsm.isRsm2) {
                return HasAnimation(rsm);
            }

            int animType = descriptor?.animType ?? 0;
            return (animType == 1 || animType == 2) && (HasAnimation(rsm) || HasRsm2TextureAnimation(rsm));
        }

        public static bool HasRsm2TextureAnimation(RSM rsm) {
            if (rsm?.nodes == null) {
                return false;
            }

            for (int i = 0; i < rsm.nodes.Length; i++) {
                if (rsm.nodes[i]?.rsm2TextureAnimations != null && rsm.nodes[i].rsm2TextureAnimations.Length > 0) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The RSM2 animation time for an elapsed time (C3dActorV2::vf06 0x9e8800): ms = (uint)(elapsed * speed);
        /// from the length on, type 1 holds at (uint)(length - 1) and type 2 restarts (ms 0, the start time moves
        /// to now: <paramref name="restart"/>). Returns the keyframe time fps * ms / 1000 (C3dActorV2::vf05
        /// 0x9e9e90).
        /// </summary>
        public static float ResolveRsm2Frame(int animType, float animSpeed, float fps, float lengthMs, float elapsedMs, out uint ms, out bool restart) {
            restart = false;
            float scaled = elapsedMs * animSpeed;
            ms = scaled <= 0f ? 0u : scaled >= uint.MaxValue ? uint.MaxValue : (uint) scaled;
            if (ms >= (double) lengthMs) {
                if (animType == 2) {
                    restart = true;
                    ms = 0;
                } else {
                    double held = lengthMs - 1.0;
                    ms = held <= 0.0 ? 0u : (uint) held;
                }
            }

            return (float) (fps * (double) ms / 1000.0);
        }

        /// <summary>C3dActorV2::vf01 (0x9e7d40): the animation length in ms, animLen * 1000 / fps.</summary>
        public static float ResolveRsm2AnimationLengthMilliseconds(RSM rsm) {
            if (rsm == null || rsm.fps <= 0f) {
                return float.PositiveInfinity;
            }

            return (float) (rsm.animLen * 1000) * (1f / rsm.fps);
        }

        private void InitializeRsm2() {
            rsm2AnimLengthMs = ResolveRsm2AnimationLengthMilliseconds(model);
            int count = model.nodes.Length;
            var order = new List<RSM.Node>(count);
            var visited = new HashSet<RSM.Node>();
            for (int i = 0; i < count; i++) {
                AddRsm2Node(model.nodes[i], order, visited);
            }

            rsm2Nodes = order.ToArray();
            rsm2ParentIndices = new int[rsm2Nodes.Length];
            rsm2Worlds = new float[rsm2Nodes.Length][];
            rsm2Transforms = new Transform[rsm2Nodes.Length];
            for (int i = 0; i < rsm2Nodes.Length; i++) {
                rsm2ParentIndices[i] = rsm2Nodes[i].parent != null ? order.IndexOf(rsm2Nodes[i].parent) : -1;
                rsm2Worlds[i] = new float[Rsm2Pose.MatrixLength];
                nodeTransforms.TryGetValue(rsm2Nodes[i], out rsm2Transforms[i]);
            }

            rsm2TextureTargets.Clear();
            for (int i = 0; i < rsm2Nodes.Length; i++) {
                RSM.Node node = rsm2Nodes[i];
                if (rsm2Transforms[i] == null || node.rsm2TextureAnimations == null || node.rsm2TextureAnimations.Length == 0) {
                    continue;
                }

                RoModelSurfaceInfo[] surfaces = rsm2Transforms[i].GetComponentsInChildren<RoModelSurfaceInfo>(true);
                for (int s = 0; s < surfaces.Length; s++) {
                    int slot = FindAnimatedSlot(node, surfaces[s].TextureName);
                    Renderer renderer = surfaces[s].GetComponent<Renderer>();
                    if (slot >= 0 && renderer != null) {
                        var target = new Rsm2TextureTarget { Renderer = renderer, Node = node, Slot = slot };
                        if (HasRsm2TextureRotation(node, slot) && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) {
                            // _BaseMap_ST has no rotation: the mesh is shared between instances, so this one gets a copy
                            target.RotatedMesh = Instantiate(filter.sharedMesh);
                            target.RotatedMesh.name = filter.sharedMesh.name + " (texture rotation)";
                            filter.sharedMesh = target.RotatedMesh;
                            target.BaseUvs = target.RotatedMesh.uv;
                            target.Uvs = new List<Vector2>(target.BaseUvs.Length);
                        }

                        rsm2TextureTargets.Add(target);
                    }
                }
            }
        }

        private static bool HasRsm2TextureRotation(RSM.Node node, int slot) {
            for (int a = 0; a < node.rsm2TextureAnimations.Length; a++) {
                if (node.rsm2TextureAnimations[a].textureSlot == slot && node.rsm2TextureAnimations[a].type == 4) {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy() {
            for (int i = 0; i < rsm2TextureTargets.Count; i++) {
                if (rsm2TextureTargets[i].RotatedMesh != null) {
                    Destroy(rsm2TextureTargets[i].RotatedMesh);
                }
            }
        }

        private static void AddRsm2Node(RSM.Node node, List<RSM.Node> order, HashSet<RSM.Node> visited) {
            if (node == null || !visited.Add(node)) {
                return;
            }

            if (node.parent != null) {
                AddRsm2Node(node.parent, order, visited);
            }

            order.Add(node);
        }

        /// <summary>The first texture slot of <paramref name="node"/> showing <paramref name="textureName"/>
        /// that has a texture animation (the meshes are per texture, the animations per slot).</summary>
        private int FindAnimatedSlot(RSM.Node node, string textureName) {
            for (int slot = 0; slot < node.textures.Length; slot++) {
                long textureIndex = node.textures[slot];
                if (textureIndex < 0 || textureIndex >= model.textures.Length ||
                    !string.Equals(model.textures[textureIndex], textureName, StringComparison.Ordinal)) {
                    continue;
                }

                for (int a = 0; a < node.rsm2TextureAnimations.Length; a++) {
                    if (node.rsm2TextureAnimations[a].textureSlot == slot) {
                        return slot;
                    }
                }
            }

            return -1;
        }

        private void PrimeFrameMatrixCache() {
            if (model?.nodes == null) {
                return;
            }

            for (int i = 0; i < model.nodes.Length; i++) {
                RSM.Node node = model.nodes[i];
                if (node == null) {
                    continue;
                }

                if (!frameMatrices.ContainsKey(node)) {
                    frameMatrices[node] = new Mat4();
                }

                if (!frameMatrixVersions.ContainsKey(node)) {
                    frameMatrixVersions[node] = 0;
                }
            }
        }

        public static bool HasAnimation(RSM rsm) {
            if (rsm?.nodes == null) {
                return false;
            }

            for (int i = 0; i < rsm.nodes.Length; i++) {
                RSM.Node node = rsm.nodes[i];
                if (node == null) {
                    continue;
                }

                if (node.rotKeyframes.Count > 0 || node.posKeyframes.Count > 0 || node.scaleKeyframes.Count > 0) {
                    return true;
                }
            }

            return false;
        }

        public static float ResolveAnimationLength(RSM rsm) {
            if (rsm == null) {
                return 0f;
            }

            if (rsm.isRsm2) {
                return ResolveRsm2AnimationLengthMilliseconds(rsm);
            }

            if (rsm.animLen > 0) {
                return rsm.animLen;
            }

            int maxFrame = 0;
            if (rsm.nodes != null) {
                for (int i = 0; i < rsm.nodes.Length; i++) {
                    RSM.Node node = rsm.nodes[i];
                    if (node == null) {
                        continue;
                    }

                    maxFrame = Mathf.Max(maxFrame, MaxFrame(node.rotKeyframes));
                    maxFrame = Mathf.Max(maxFrame, MaxFrame(node.posKeyframes));
                    maxFrame = Mathf.Max(maxFrame, MaxFrame(node.scaleKeyframes));
                }
            }

            return maxFrame > 0 ? maxFrame + 1 : 0f;
        }

        public void ApplyFrameForTests(float frame) {
            ApplyFrame(frame);
        }

        private void Update() {
            if (model == null || !hasAnimation) {
                enabled = false;
                return;
            }

            if (animationLength <= 0f) {
                return;
            }

            if (Application.isPlaying && !HasVisibleRenderer()) {
                return;
            }

            if (model.isRsm2) {
                UpdateRsm2();
                return;
            }

            float elapsedMilliseconds = (Time.timeSinceLevelLoad - startTimeSeconds) * 1000f;
            float frame = Mathf.Repeat(elapsedMilliseconds, animationLength);
            int animationStep = Mathf.FloorToInt(frame / frameDurationMilliseconds);
            if (animationStep == lastAnimationStep) {
                return;
            }

            lastAnimationStep = animationStep;
            ApplyFrame(frame);
        }

        private bool HasVisibleRenderer() {
            if (renderers.Count == 0) {
                return true;
            }

            for (int i = 0; i < renderers.Count; i++) {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.isVisible) {
                    return true;
                }
            }

            return false;
        }

        private void UpdateRsm2() {
            float elapsedMilliseconds = (Time.timeSinceLevelLoad - startTimeSeconds) * 1000f;
            float frame = ResolveRsm2Frame(rsm2AnimType, rsm2AnimSpeed, model.fps, rsm2AnimLengthMs, elapsedMilliseconds, out uint ms, out bool restart);
            if (restart) {
                startTimeSeconds = Time.timeSinceLevelLoad;
            }

            if (ms == rsm2LastMs) {
                return;
            }

            rsm2LastMs = ms;
            ApplyRsm2Frame(frame);
        }

        /// <summary>
        /// C3dNodeV2::vf02 (0x9ea000) for every node at keyframe time <paramref name="frame"/>: world =
        /// M(frame) * world(parent), applied with the actor's Y flip folded in (Rsm2Pose). Texture animations go
        /// to the renderers' _BaseMap_ST (offset and scale about the texture centre); a slot with a rotation
        /// (type 4) gets its UVs computed on an own copy of the mesh instead.
        /// </summary>
        private void ApplyRsm2Frame(float frame) {
            if (rsm2Nodes == null) {
                return;
            }

            for (int i = 0; i < rsm2Nodes.Length; i++) {
                float[] world = rsm2Worlds[i];
                Rsm2Pose.EvaluateLocal(rsm2Nodes[i], frame, world);
                int parent = rsm2ParentIndices[i];
                if (parent >= 0) {
                    Rsm2Pose.Multiply(world, world, rsm2Worlds[parent]);
                }

                if (rsm2Transforms[i] != null && !rsm2Nodes[i].rsm2Baked) {
                    Rsm2Pose.Decompose(Rsm2Pose.ToUnityMatrix(world), out Vector3 position, out Quaternion rotation, out Vector3 scale);
                    Transform transform = rsm2Transforms[i];
                    transform.localPosition = position;
                    transform.localRotation = rotation;
                    transform.localScale = scale;
                }
            }

            if (rsm2TextureTargets.Count == 0) {
                return;
            }

            if (rsm2PropertyBlock == null) {
                rsm2PropertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < rsm2TextureTargets.Count; i++) {
                Rsm2TextureTarget target = rsm2TextureTargets[i];
                if (target.Renderer == null) {
                    continue;
                }

                Rsm2Pose.EvaluateTextureAnimation(target.Node, target.Slot, frame, out float offsetU, out float offsetV, out float scaleU, out float scaleV, out float rotation);
                target.Renderer.GetPropertyBlock(rsm2PropertyBlock);
                if (target.RotatedMesh != null) {
                    // the whole transform per vertex (C3dNodeV2::vf03 0x9e92a0); the mesh stores (u, -v)
                    target.Uvs.Clear();
                    for (int v = 0; v < target.BaseUvs.Length; v++) {
                        Vector2 uv = target.BaseUvs[v];
                        Vector2 animated = Rsm2Pose.ApplyTextureAnimation(new Vector2(uv.x, -uv.y), offsetU, offsetV, scaleU, scaleV, rotation);
                        target.Uvs.Add(new Vector2(animated.x, -animated.y));
                    }

                    target.RotatedMesh.SetUVs(0, target.Uvs);
                    rsm2PropertyBlock.SetVector(BaseMapStId, new Vector4(1f, 1f, 0f, 0f));
                } else {
                    rsm2PropertyBlock.SetVector(BaseMapStId, ResolveRsm2TextureSt(offsetU, offsetV, scaleU, scaleV));
                }

                target.Renderer.SetPropertyBlock(rsm2PropertyBlock);
            }
        }

        /// <summary>
        /// The texture animation as a TRANSFORM_TEX scale / offset of the mesh UVs (u, -v): u' = (u - 0.5) * sU +
        /// 0.5 + oU and v' likewise (C3dNodeV2::vf03 0x9e92a0 without the rotation), the V terms negated.
        /// </summary>
        public static Vector4 ResolveRsm2TextureSt(float offsetU, float offsetV, float scaleU, float scaleV) {
            return new Vector4(scaleU, scaleV, 0.5f - 0.5f * scaleU + offsetU, -(0.5f - 0.5f * scaleV + offsetV));
        }

        private void ApplyFrame(float frame) {
            if (model != null && model.isRsm2) {
                ApplyRsm2Frame(frame);
                return;
            }

            if (model?.nodes == null || model.box == null) {
                return;
            }

            BeginFrameMatrixPass();
            for (int i = 0; i < model.nodes.Length; i++) {
                RSM.Node node = model.nodes[i];
                if (node == null || !nodeTransforms.TryGetValue(node, out Transform transform)) {
                    continue;
                }

                Mat4 global = CalculateGlobalMatrix(node, frame);
                Mat4 final = finalMatrix;
                final.SetIdentity();
                Mat4.Translate(final, final, new Vector3(-model.box.center.x, -model.box.max.y, -model.box.center.z));
                Mat4.Multiply(final, final, global);
                ApplyMatrix(transform, final.ToMatrix4x4());
            }
        }

        private void BeginFrameMatrixPass() {
            if (frameMatrixVersion == int.MaxValue) {
                frameMatrixVersions.Clear();
                frameMatrixVersion = 0;
            }

            frameMatrixVersion++;
        }

        private Mat4 CalculateGlobalMatrix(RSM.Node node, float frame) {
            if (frameMatrixVersions.TryGetValue(node, out int version) &&
                version == frameMatrixVersion &&
                frameMatrices.TryGetValue(node, out Mat4 cached)) {
                return cached;
            }

            Mat4 global = GetOrCreateFrameMatrix(node);
            if (TryGetParent(node, out RSM.Node parent)) {
                global.CopyFrom(CalculateGlobalMatrix(parent, frame));
            } else {
                global.SetIdentity();
            }

            Mat4 local = BuildLocalMatrix(node, frame, localMatrixScratch);
            Mat4.Multiply(global, global, local);
            frameMatrixVersions[node] = frameMatrixVersion;
            return global;
        }

        private Mat4 GetOrCreateFrameMatrix(RSM.Node node) {
            if (!frameMatrices.TryGetValue(node, out Mat4 matrix)) {
                matrix = new Mat4();
                frameMatrices[node] = matrix;
            }

            return matrix;
        }

        private bool TryGetParent(RSM.Node node, out RSM.Node parent) {
            parent = null;
            return node != null
                && !string.IsNullOrEmpty(node.parentName)
                && !string.Equals(node.parentName, node.name, StringComparison.OrdinalIgnoreCase)
                && nodesByName.TryGetValue(node.parentName, out parent);
        }

        private static Mat4 BuildLocalMatrix(RSM.Node node, float frame, Mat4 matrix) {
            matrix.SetIdentity();
            Mat4.Translate(matrix, matrix, TryInterpolate(node.posKeyframes, frame, out Vector3 position) ? position : node.pos);

            if (TryInterpolate(node.rotKeyframes, frame, out Quaternion rotation)) {
                Mat4.RotateQuat(matrix, matrix, new Vector4(rotation.x, rotation.y, rotation.z, rotation.w));
            } else if (node.rotAxis.sqrMagnitude > 0.000001f && Mathf.Abs(node.rotAngle) > 0.000001f) {
                Mat4.Rotate(matrix, matrix, node.rotAngle, node.rotAxis);
            }

            Mat4.Scale(matrix, matrix, TryInterpolate(node.scaleKeyframes, frame, out Vector3 scale) ? scale : node.scale);
            return matrix;
        }

        private static bool TryInterpolate(SortedList<int, Vector3> keyframes, float frame, out Vector3 value) {
            value = default;
            if (keyframes == null || keyframes.Count == 0) {
                return false;
            }

            if (keyframes.Count == 1) {
                value = keyframes.Values[0];
                return true;
            }

            int previousIndex = 0;
            int nextIndex = -1;
            for (int i = 0; i < keyframes.Count; i++) {
                if (keyframes.Keys[i] > frame) {
                    nextIndex = i;
                    break;
                }

                previousIndex = i;
            }

            if (nextIndex < 0) {
                value = keyframes.Values[previousIndex];
                return true;
            }

            int previousFrame = keyframes.Keys[previousIndex];
            int nextFrame = keyframes.Keys[nextIndex];
            if (nextFrame == previousFrame) {
                value = keyframes.Values[previousIndex];
                return true;
            }

            float t = (frame - previousFrame) / (nextFrame - previousFrame);
            value = Vector3.LerpUnclamped(keyframes.Values[previousIndex], keyframes.Values[nextIndex], t);
            return true;
        }

        private static bool TryInterpolate(SortedList<int, Quaternion> keyframes, float frame, out Quaternion value) {
            value = default;
            if (keyframes == null || keyframes.Count == 0) {
                return false;
            }

            if (keyframes.Count == 1) {
                value = keyframes.Values[0];
                return true;
            }

            int previousIndex = 0;
            int nextIndex = -1;
            for (int i = 0; i < keyframes.Count; i++) {
                if (keyframes.Keys[i] > frame) {
                    nextIndex = i;
                    break;
                }

                previousIndex = i;
            }

            if (nextIndex < 0) {
                value = keyframes.Values[previousIndex];
                return true;
            }

            int previousFrame = keyframes.Keys[previousIndex];
            int nextFrame = keyframes.Keys[nextIndex];
            if (nextFrame == previousFrame) {
                value = keyframes.Values[previousIndex];
                return true;
            }

            float t = (frame - previousFrame) / (nextFrame - previousFrame);
            value = Quaternion.SlerpUnclamped(keyframes.Values[previousIndex], keyframes.Values[nextIndex], t);
            return true;
        }

        private static void ApplyMatrix(Transform transform, Matrix4x4 matrix) {
            transform.localPosition = matrix.GetColumn(3);
            transform.localRotation = ExtractRotation(matrix);
            transform.localScale = ExtractScale(matrix);
        }

        private static Quaternion ExtractRotation(Matrix4x4 matrix) {
            Vector3 forward = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            Vector3 upwards = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            if (forward.sqrMagnitude <= 0.000001f || upwards.sqrMagnitude <= 0.000001f) {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(forward, upwards);
        }

        private static Vector3 ExtractScale(Matrix4x4 matrix) {
            return new Vector3(
                new Vector3(matrix.m00, matrix.m10, matrix.m20).magnitude,
                new Vector3(matrix.m01, matrix.m11, matrix.m21).magnitude,
                new Vector3(matrix.m02, matrix.m12, matrix.m22).magnitude
            );
        }

        private static int MaxFrame<T>(SortedList<int, T> keyframes) {
            return keyframes != null && keyframes.Count > 0 ? keyframes.Keys[keyframes.Count - 1] : 0;
        }

        private static float GetRobrowserFrameDurationMilliseconds() {
            return 1000f / (DefaultFramesPerSecond / RobrowserFrameDurationScale);
        }
    }
}
