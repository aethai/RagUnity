using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ragnarok.Assets;
using Ragnarok.Maps;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Granny {
    /// <summary>
    /// A Granny (.gr2) actor the way the client draws it (C3dGrannyGameActor, RE notes decomp/gr2): the WoE guardians,
    /// the Emperium, the guild flag and the treasure boxes. One model per actor, CPU skinning (GrannyDeformVertices) into
    /// a plain mesh per Granny mesh, opaque with an alpha test (ALPHAREF 0xCF), no culling, grey per-vertex light
    /// 127 + 128 * max(N.L, 0) (0.5 + 0.5 N.L through the RSM model shader).
    /// <para>
    /// Animations (6d0e30): "stand" comes from the model file, move / attack / dead / damage from
    /// <c>model\3dmob_bone\&lt;class&gt;_&lt;action&gt;.gr2</c> where the class is the digits after the first '_' of the
    /// file name (Empelium90_0, Guildflag90_1, TREASUREBOX_2, Kguardian90_7, Aguardian90_8, Sguardian90_9); class 1 (the
    /// flag) gets none. The clock runs at twice real time (be98e0); an attack also runs at 2 / f with
    /// f = min(amotion, 432) / 432 (bf0610, be97d0) and falls back to a looping stand when it ends; dead plays once and
    /// holds. Placement (row vectors): M = RotX(θ) · RotY(φ) · T with θ = π/2 for "90" files and π otherwise,
    /// φ = (yaw + 180)°, yaw = 180 − 45·dir, T = position − (2, 0, 2) RO units (none for the flag); 1 Granny unit = 1 RO
    /// unit = 0.2 Unity units. The model turns 6° per 16 ms tick toward the facing with a 12° dead zone (be9510).
    /// </para>
    /// </summary>
    public sealed class RoGrannyActorModel : MonoBehaviour {
        private const string ModelFolder = "data\\model\\3dmob\\";
        private const string BoneFolder = "data\\model\\3dmob_bone\\";
        private static readonly string[] ActionNames = { "stand", "move", "attack", "dead", "damage" };
        private const int ActionStand = 0, ActionMove = 1, ActionAttack = 2, ActionDead = 3, ActionDamage = 4, ActionNone = -1;
        private const float RoUnitsToUnity = 0.2f;
        private const float AlphaRef = 0xCF / 255f;
        private const float TickSeconds = 0.016f;
        private const float TurnDegreesPerTick = 6f;
        private const float TurnDeadZoneDegrees = 12f;
        private const int FlagClass = 1;
        private const int FlagEmblemMesh = 1;

        private static readonly Dictionary<string, Asset> Cache = new Dictionary<string, Asset>(StringComparer.OrdinalIgnoreCase);
        private static Shader modelShader;

        /// <summary>One loaded .gr2 with its actions, shared by every actor of the file.</summary>
        private sealed class Asset {
            public string ResourceName;
            public int ClassIndex;
            public bool HalfTurn; // θ = π/2 ("90" files), otherwise π
            public Task<bool> Load;
            public string Error;
            public Gr2Content Content;
            public Gr2Skeleton Skeleton;
            public readonly Gr2Animation[] Actions = new Gr2Animation[ActionNames.Length];
            public int[][] MeshBones; // per mesh: binding slot -> skeleton bone
            public int[] MeshOrder; // drawn meshes: Models[0]'s mesh bindings (file mesh indices)
            public Mesh[] SharedMeshes; // uvs + triangles, vertices filled per instance
            public Material[] Materials; // per mesh, null when the mesh has no texture
            public bool UnityReady;
        }

        private Asset asset;
        private Gr2PoseSampler sampler;
        private MeshFilter[] filters;
        private MeshRenderer[] renderers;
        private Mesh[] meshes;
        private Vector3[][] positions;
        private Vector3[][] normals;
        private Material emblemMaterial;
        private Texture emblem;
        private bool built;
        private int action = ActionStand;
        private bool loop = true;
        private double clock;
        private float attackDelayScale = 1f;
        private float targetYaw;
        private float yaw;
        private bool facingSet;
        private float turnAccumulator;
        private bool poseDirty = true;
        private Renderer[] rendererView = Array.Empty<Renderer>();

        /// <summary>The model's renderers (empty until the file has loaded; the presenter's cursor shapes use them).</summary>
        public Renderer[] Renderers => rendererView;

        /// <summary>World bounds of the drawn meshes, or a one-cell box at the actor while the file loads.</summary>
        public Bounds WorldBounds {
            get {
                Bounds bounds = default;
                bool any = false;
                if (renderers != null) {
                    foreach (MeshRenderer r in renderers) {
                        if (r == null || !r.enabled) continue;
                        if (!any) {
                            bounds = r.bounds;
                            any = true;
                        } else {
                            bounds.Encapsulate(r.bounds);
                        }
                    }
                }

                return any ? bounds : new Bounds(transform.position + new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));
            }
        }

        public static bool IsGrannyResource(string resourceName) =>
            !string.IsNullOrWhiteSpace(resourceName) && resourceName.Trim().EndsWith(".gr2", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the model for a jobname resource ("Empelium90_0.gr2") under <paramref name="parent"/>. False when the
        /// name is not a .gr2 or the file is not in the client data; the file then parses on a worker thread and the
        /// model appears once it is ready (the sprite fallback is not needed meanwhile: nothing is drawn).
        /// </summary>
        public static bool TryCreate(RoAssetDatabase assets, string resourceName, Transform parent, out RoGrannyActorModel model) {
            model = null;
            if (assets == null || !IsGrannyResource(resourceName)) return false;
            string file = Path.GetFileName(resourceName.Trim().Replace('/', '\\').Replace('\\', Path.DirectorySeparatorChar));
            if (!Cache.TryGetValue(file, out Asset a)) {
                if (!assets.Exists(ModelFolder + file)) return false;
                a = StartLoad(assets, file);
                Cache[file] = a;
            }

            if (a.Error != null) return false;
            var go = new GameObject("RO Granny " + file);
            go.transform.SetParent(parent, false);
            model = go.AddComponent<RoGrannyActorModel>();
            model.asset = a;
            model.ApplyPlacement();
            return true;
        }

        private static Asset StartLoad(RoAssetDatabase assets, string file) {
            var a = new Asset {
                ResourceName = file,
                ClassIndex = ParseClassIndex(file),
                HalfTurn = file.IndexOf("90", StringComparison.Ordinal) >= 0
            };
            a.Load = Task.Run(() => {
                try {
                    byte[] bytes = assets.ReadBytesQuiet(ModelFolder + file);
                    if (bytes == null) {
                        a.Error = "missing";
                        return false;
                    }

                    Gr2Content content = Gr2Reader.Parse(bytes, decodeTextures: true);
                    a.Content = content;
                    int skeletonIndex = content.Models != null && content.Models.Length > 0 ? content.Models[0].SkeletonIndex : 0;
                    a.Skeleton = content.Skeletons != null && skeletonIndex >= 0 && skeletonIndex < content.Skeletons.Length
                        ? content.Skeletons[skeletonIndex]
                        : null;
                    if (a.ClassIndex != FlagClass) {
                        if (content.Animations != null && content.Animations.Length > 0) a.Actions[ActionStand] = content.Animations[0];
                        for (int i = 1; i < ActionNames.Length; i++) {
                            byte[] anim = assets.ReadBytesQuiet(BoneFolder + a.ClassIndex + "_" + ActionNames[i] + ".gr2");
                            if (anim == null) continue;
                            Gr2Content animContent = Gr2Reader.Parse(anim, decodeTextures: false);
                            // 6d0f50: a bone file must hold exactly one animation
                            if (animContent.Animations != null && animContent.Animations.Length == 1) a.Actions[i] = animContent.Animations[0];
                        }
                    }

                    a.MeshBones = BindMeshes(content, a.Skeleton);
                    a.MeshOrder = ResolveMeshOrder(content);
                    return true;
                } catch (Exception ex) {
                    a.Error = ex.Message;
                    return false;
                }
            });
            return a;
        }

        // the digits after the first '_' of the file name: Kguardian90_7 -> 7
        private static int ParseClassIndex(string file) {
            string stem = Path.GetFileNameWithoutExtension(file);
            int us = stem.IndexOf('_');
            if (us < 0) return -1;
            int value = 0, digits = 0;
            for (int i = us + 1; i < stem.Length && char.IsDigit(stem[i]); i++, digits++) value = value * 10 + (stem[i] - '0');
            return digits > 0 ? value : -1;
        }

        private static int[] ResolveMeshOrder(Gr2Content content) {
            int total = content.Meshes?.Length ?? 0;
            int[] bound = content.Models != null && content.Models.Length > 0 ? content.Models[0].MeshIndices : null;
            var order = new List<int>(total);
            if (bound != null) {
                foreach (int m in bound) {
                    if (m >= 0 && m < total) order.Add(m);
                }
            }

            if (order.Count == 0) {
                for (int m = 0; m < total; m++) order.Add(m);
            }

            return order.ToArray();
        }

        private static int[][] BindMeshes(Gr2Content content, Gr2Skeleton skeleton) {
            var result = new int[content.Meshes?.Length ?? 0][];
            for (int m = 0; m < result.Length; m++) {
                string[] names = content.Meshes[m].BoneBindings ?? Array.Empty<string>();
                var map = new int[names.Length];
                for (int i = 0; i < names.Length; i++) {
                    map[i] = -1;
                    if (skeleton == null) continue;
                    for (int b = 0; b < skeleton.Bones.Length; b++) {
                        if (string.Equals(skeleton.Bones[b].Name, names[i], StringComparison.Ordinal)) {
                            map[i] = b;
                            break;
                        }
                    }
                }

                result[m] = map;
            }

            return result;
        }

        // main thread, once per file: textures, materials and the shared triangle / uv meshes
        private static void BuildUnityAssets(Asset a) {
            Gr2Content c = a.Content;
            int count = c.Meshes?.Length ?? 0;
            a.SharedMeshes = new Mesh[count];
            a.Materials = new Material[count];
            var textures = new Dictionary<Gr2Texture, Texture2D>();
            for (int m = 0; m < count; m++) {
                Gr2Mesh mesh = c.Meshes[m];
                var shared = new Mesh { name = a.ResourceName + "/" + mesh.Name };
                shared.indexFormat = mesh.VertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                var uv = new Vector2[mesh.VertexCount];
                if (mesh.UVs != null) {
                    for (int v = 0; v < mesh.VertexCount; v++) uv[v] = new Vector2(mesh.UVs[v * 2], mesh.UVs[v * 2 + 1]);
                }

                shared.vertices = new Vector3[mesh.VertexCount];
                shared.uv = uv;
                shared.triangles = mesh.Indices ?? Array.Empty<int>();
                a.SharedMeshes[m] = shared;

                Gr2Texture texture = FindMeshTexture(c, mesh);
                if (texture == null) continue;
                if (!textures.TryGetValue(texture, out Texture2D tex)) {
                    tex = UploadTexture(texture);
                    textures[texture] = tex;
                }

                a.Materials[m] = CreateMaterial(tex, a.ResourceName + "/" + mesh.Name, a.ClassIndex == FlagClass);
            }

            a.UnityReady = true;
        }

        // 6d2960: the texture of triangle group 0's material (diffuse), matched by file name against the embedded ones
        private static Gr2Texture FindMeshTexture(Gr2Content c, Gr2Mesh mesh) {
            if (c.Textures == null || c.Textures.Length == 0) return null;
            Gr2Material material = null;
            if (mesh.TriangleGroups != null && mesh.TriangleGroups.Length > 0 && mesh.Materials != null) {
                int index = mesh.TriangleGroups[0].MaterialIndex;
                if (index >= 0 && index < mesh.Materials.Length) material = mesh.Materials[index];
            }

            string wanted = material?.TextureFile ?? string.Empty;
            string wantedName = Path.GetFileName(wanted.Replace('\\', '/'));
            Gr2Texture fallback = null;
            foreach (Gr2Texture t in c.Textures) {
                if (t.Rgba == null || t.ImageIndex != 0 || t.MipIndex != 0) continue;
                fallback ??= t;
                string from = t.FromFileName ?? string.Empty;
                if (wanted.Length > 0 && (string.Equals(from, wanted, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(from.Replace('\\', '/')), wantedName, StringComparison.OrdinalIgnoreCase))) {
                    return t;
                }
            }

            return wanted.Length > 0 ? fallback : null;
        }

        // 6d2bb0: RGBA, alpha only when the texture has it (otherwise opaque); rows are top-down and the UVs address them
        // that way, so the bytes go up unchanged
        private static Texture2D UploadTexture(Gr2Texture t) {
            byte[] rgba = t.Rgba;
            if (!t.HasAlpha) {
                rgba = (byte[]) rgba.Clone();
                for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
            }

            var tex = new Texture2D(t.Width, t.Height, TextureFormat.RGBA32, true) {
                name = t.Name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            tex.SetPixelData(rgba, 0);
            tex.Apply(true, true);
            return tex;
        }

        private static Material CreateMaterial(Texture texture, string name, bool flagLight) {
            if (modelShader == null) modelShader = Shader.Find("Ragnarok/RO Model Unlit AlphaClip");
            var material = new Material(modelShader) { name = "RO Granny " + name };
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.SetFloat("_Cutoff", AlphaRef);
            material.SetFloat("_UseAlphaClip", 1f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 0f);
            // grey light 127 + 128 * max(N.L, 0) (beace0), the flag 155 + 100 * max(N.L, 0) (bea450); no map colour
            material.SetFloat("_RoUseLighting", 1f);
            Vector3 light = RoMapLighting.ToUnityLightDirection(45, 45);
            material.SetVector("_RoLightDirection", new Vector4(light.x, light.y, light.z, 0f));
            float ambient = flagLight ? 155f / 255f : 127f / 255f, diffuse = flagLight ? 100f / 255f : 128f / 255f;
            material.SetColor("_RoLightAmbient", new Color(ambient, ambient, ambient, 1f));
            material.SetColor("_RoLightDiffuse", new Color(diffuse, diffuse, diffuse, 1f));
            material.SetFloat("_RoLightOpacity", 1f);
            material.SetFloat("_RoLightingStrength", 1f);
            material.SetFloat("_RoModelMinLight", 0f);
            return material;
        }

        private void ApplyPlacement() {
            // T = position - (2, 0, 2) RO units for every 3D actor but the flag (be98e0 / bea450)
            bool flag = asset != null && asset.ClassIndex == FlagClass;
            transform.localPosition = flag ? Vector3.zero : new Vector3(-2f * RoUnitsToUnity, 0f, -2f * RoUnitsToUnity);
            transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// The presenter's motion (stand / walk / attack / hit / dead …) and facing. Restart replays the action from 0;
        /// <paramref name="speedScale"/> is the action's frame-delay scale (amotion based, below 1 = faster attack).
        /// </summary>
        public void SetMotion(RoSpriteMotion motion, RoActorDirection facing, bool restart, float speedScale, bool loop) {
            int next = MapMotion(motion);
            if (next != action || restart) {
                action = next;
                clock = 0d;
                poseDirty = true;
            }

            this.loop = loop || next == ActionStand || next == ActionMove;
            attackDelayScale = Mathf.Clamp(speedScale > 0f ? speedScale : 1f, 0.05f, 1f);
            if (facing != RoActorDirection.None) {
                // yaw = 180 - roty, φ = yaw + 180 -> 360 - 45 * dir
                targetYaw = Mathf.Repeat(-45f * (int) facing, 360f);
                if (!facingSet) {
                    yaw = targetYaw;
                    facingSet = true;
                    ApplyPlacement();
                }
            }

            if (sampler != null) sampler.SetAnimation(CurrentAnimation());
        }

        /// <summary>Walk distance (cells) of the current move; the original plays "move" by time, so this is kept for later.</summary>
        public void SetWalkDistance(float cells) { }

        /// <summary>The guild emblem on the flag's mesh 1 (bea450); the mesh is hidden while there is none.</summary>
        public void SetEmblem(Texture emblemTexture) {
            emblem = emblemTexture;
            ApplyEmblem();
        }

        private static int MapMotion(RoSpriteMotion motion) {
            switch (motion) {
                case RoSpriteMotion.Idle:
                case RoSpriteMotion.Standby:
                case RoSpriteMotion.Sit:
                case RoSpriteMotion.PickUp:
                    return ActionStand;
                case RoSpriteMotion.Walk:
                    return ActionMove;
                case RoSpriteMotion.Attack1:
                case RoSpriteMotion.Attack2:
                case RoSpriteMotion.Attack3:
                case RoSpriteMotion.Throwing:
                    return ActionAttack;
                case RoSpriteMotion.Hit:
                    return ActionDamage;
                case RoSpriteMotion.Dead:
                    return ActionDead;
                default:
                    // casting / performances map to actions without a file: no control plays, the rest pose shows
                    return ActionNone;
            }
        }

        private Gr2Animation CurrentAnimation() => action >= 0 ? asset.Actions[action] : null;

        private void Update() {
            if (asset == null) return;
            if (!built && !TryBuild()) return;

            float dt = Time.deltaTime;
            Gr2Animation anim = CurrentAnimation();
            if (anim != null) {
                // the clock runs at twice real time; an attack with f < 1 also advances 2 * dt / f (be97d0)
                double speed = action == ActionAttack && attackDelayScale < 1f ? 2d + 2d / attackDelayScale : 2d;
                clock += dt * speed;
                double duration = Math.Max(anim.Duration, 0.0001f);
                if (clock >= duration) {
                    if (action == ActionAttack || action == ActionDamage) {
                        action = ActionStand;
                        clock = 0d;
                        sampler.SetAnimation(CurrentAnimation());
                    } else if (loop && action != ActionDead) {
                        clock %= duration;
                    } else {
                        clock = duration;
                    }
                }

                poseDirty = true;
            }

            Turn(dt);
            if (poseDirty) {
                Skin();
                poseDirty = false;
            }
        }

        private void Turn(float dt) {
            if (!facingSet) return;
            turnAccumulator += dt;
            bool moved = false;
            while (turnAccumulator >= TickSeconds) {
                turnAccumulator -= TickSeconds;
                float diff = Mathf.DeltaAngle(yaw, targetYaw);
                if (Mathf.Abs(diff) <= TurnDeadZoneDegrees) break;
                yaw = Mathf.Repeat(yaw + Mathf.Sign(diff) * TurnDegreesPerTick, 360f);
                moved = true;
            }

            if (turnAccumulator > TickSeconds * 4f) turnAccumulator = 0f;
            if (moved) transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        }

        private bool TryBuild() {
            if (!asset.Load.IsCompleted) return false;
            if (!asset.Load.Result || asset.Content == null) {
                enabled = false;
                return false;
            }

            if (!asset.UnityReady) BuildUnityAssets(asset);
            Gr2Content c = asset.Content;
            // slots follow Models[0]'s mesh bindings (6d2960): the flag's emblem is slot 1, not the file's mesh 1
            int count = asset.MeshOrder.Length;
            filters = new MeshFilter[count];
            renderers = new MeshRenderer[count];
            meshes = new Mesh[count];
            positions = new Vector3[count][];
            normals = new Vector3[count][];
            var view = new List<Renderer>(count);
            for (int s = 0; s < count; s++) {
                int m = asset.MeshOrder[s];
                if (asset.Materials[m] == null && !(asset.ClassIndex == FlagClass && s == FlagEmblemMesh)) continue;
                var child = new GameObject(c.Meshes[m].Name);
                child.transform.SetParent(transform, false);
                Mesh mesh = Instantiate(asset.SharedMeshes[m]);
                mesh.name = asset.SharedMeshes[m].name;
                mesh.MarkDynamic();
                meshes[s] = mesh;
                positions[s] = new Vector3[c.Meshes[m].VertexCount];
                normals[s] = new Vector3[c.Meshes[m].VertexCount];
                filters[s] = child.AddComponent<MeshFilter>();
                filters[s].sharedMesh = mesh;
                renderers[s] = child.AddComponent<MeshRenderer>();
                renderers[s].sharedMaterial = asset.Materials[m];
                renderers[s].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[s].receiveShadows = false;
                view.Add(renderers[s]);
            }

            rendererView = view.ToArray();
            sampler = asset.Skeleton != null ? new Gr2PoseSampler(asset.Skeleton) : null;
            sampler?.SetAnimation(CurrentAnimation());
            built = true;
            poseDirty = true;
            ApplyEmblem();
            return true;
        }

        private void ApplyEmblem() {
            if (!built || asset.ClassIndex != FlagClass || renderers == null || renderers.Length <= FlagEmblemMesh) return;
            MeshRenderer r = renderers[FlagEmblemMesh];
            if (r == null) return;
            if (emblem == null) {
                r.enabled = false;
                return;
            }

            if (emblemMaterial == null) emblemMaterial = CreateMaterial(emblem, asset.ResourceName + "/emblem", true);
            emblemMaterial.SetTexture("_BaseMap", emblem);
            emblemMaterial.mainTexture = emblem;
            r.sharedMaterial = emblemMaterial;
            r.enabled = true;
        }

        // GrannyDeformVertices, then the axis change of RotX(θ) and RO -> Unity (y flips, 1/5 scale)
        private void Skin() {
            Gr2Content c = asset.Content;
            float[] skin = null;
            if (sampler != null) {
                sampler.Sample(clock);
                skin = sampler.SkinningMatrices;
            }

            bool halfTurn = asset.HalfTurn;
            for (int s = 0; s < meshes.Length; s++) {
                Mesh mesh = meshes[s];
                if (mesh == null) continue;
                int m = asset.MeshOrder[s];
                Gr2Mesh src = c.Meshes[m];
                Vector3[] pos = positions[s];
                Vector3[] nrm = normals[s];
                int[] boneMap = asset.MeshBones[m];
                int k = src.InfluencesPerVertex;
                int rigidBone = boneMap.Length > 0 ? boneMap[0] : -1;
                for (int v = 0; v < src.VertexCount; v++) {
                    float px = src.Positions[v * 3], py = src.Positions[v * 3 + 1], pz = src.Positions[v * 3 + 2];
                    float nx = 0f, ny = 1f, nz = 0f;
                    if (src.Normals != null) {
                        nx = src.Normals[v * 3];
                        ny = src.Normals[v * 3 + 1];
                        nz = src.Normals[v * 3 + 2];
                    }

                    float ox, oy, oz, onx, ony, onz;
                    if (skin == null) {
                        ox = px; oy = py; oz = pz; onx = nx; ony = ny; onz = nz;
                    } else if (k == 0) {
                        if (rigidBone >= 0) {
                            SkinVertex(skin, rigidBone * 16, 1f, px, py, pz, nx, ny, nz, out ox, out oy, out oz, out onx, out ony, out onz);
                        } else {
                            ox = px; oy = py; oz = pz; onx = nx; ony = ny; onz = nz;
                        }
                    } else {
                        ox = oy = oz = onx = ony = onz = 0f;
                        bool any = false;
                        for (int i = 0; i < k; i++) {
                            float w = src.BoneWeights[v * k + i];
                            if (w <= 0f) continue;
                            int slot = src.BoneIndices[v * k + i];
                            int bone = slot >= 0 && slot < boneMap.Length ? boneMap[slot] : -1;
                            if (bone < 0) continue;
                            SkinVertex(skin, bone * 16, w, px, py, pz, nx, ny, nz, out float wx, out float wy, out float wz, out float wnx, out float wny, out float wnz);
                            ox += wx; oy += wy; oz += wz; onx += wnx; ony += wny; onz += wnz;
                            any = true;
                        }

                        if (!any) {
                            ox = px; oy = py; oz = pz; onx = nx; ony = ny; onz = nz;
                        }
                    }

                    // RotX(π/2): (x, -z, y), RotX(π): (x, -y, -z) in RO; RO -> Unity flips y
                    if (halfTurn) {
                        pos[v] = new Vector3(ox, oz, oy) * RoUnitsToUnity;
                        nrm[v] = new Vector3(onx, onz, ony);
                    } else {
                        pos[v] = new Vector3(ox, oy, -oz) * RoUnitsToUnity;
                        nrm[v] = new Vector3(onx, ony, -onz);
                    }
                }

                mesh.vertices = pos;
                mesh.normals = nrm;
                mesh.RecalculateBounds();
            }
        }

        // weight * (M p) and weight * (M3x3 n); M column-major, column vectors
        private static void SkinVertex(float[] m, int o, float w, float px, float py, float pz, float nx, float ny, float nz,
            out float ox, out float oy, out float oz, out float onx, out float ony, out float onz) {
            ox = w * (m[o] * px + m[o + 4] * py + m[o + 8] * pz + m[o + 12]);
            oy = w * (m[o + 1] * px + m[o + 5] * py + m[o + 9] * pz + m[o + 13]);
            oz = w * (m[o + 2] * px + m[o + 6] * py + m[o + 10] * pz + m[o + 14]);
            onx = w * (m[o] * nx + m[o + 4] * ny + m[o + 8] * nz);
            ony = w * (m[o + 1] * nx + m[o + 5] * ny + m[o + 9] * nz);
            onz = w * (m[o + 2] * nx + m[o + 6] * ny + m[o + 10] * nz);
        }

        private void OnDestroy() {
            if (meshes != null) {
                foreach (Mesh mesh in meshes) {
                    if (mesh != null) Destroy(mesh);
                }
            }

            if (emblemMaterial != null) Destroy(emblemMaterial);
        }
    }
}
