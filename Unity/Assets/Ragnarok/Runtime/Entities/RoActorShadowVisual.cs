using Ragnarok.Assets;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Entities {
    public enum RoActorShadowOrientation {
        CameraBillboard,
        FixedGroundPlane
    }

    public sealed class RoActorShadowVisual : MonoBehaviour, IRoActorLateTick, IRoActorTickBudget {
        private const string DefaultShadowBasePath = "data/sprite/shadow";
        private const int FallbackTextureSize = 64;
        private const float ShadowCameraRotationEpsilonDegrees = 0.01f;
        public const CompareFunction ShadowDepthTest = CompareFunction.LessEqual;
        private static readonly int[] ShadowQuadTriangles = { 0, 1, 2, 1, 3, 2 };
        private static readonly Vector3[] ShadowQuadNormals = { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        private static readonly Color[] ShadowQuadColors = { Color.white, Color.white, Color.white, Color.white };
        private static readonly Vector2[] FallbackShadowUvs = {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        private static readonly Vector3[] FallbackShadowVertices = {
            new Vector3(-0.55f, -0.28f, 0f),
            new Vector3(0.55f, -0.28f, 0f),
            new Vector3(-0.55f, 0.28f, 0f),
            new Vector3(0.55f, 0.28f, 0f)
        };
        private static readonly Vector3[] ShadowSpriteVertexScratch = new Vector3[4];

        [SerializeField] private string shadowBasePath = DefaultShadowBasePath;
        [SerializeField] private bool useSpriteShadow = true;
        [SerializeField] private RoActorShadowOrientation orientation = RoActorShadowOrientation.CameraBillboard;
        [SerializeField] private float groundOffset;
        [SerializeField] private float lift = 0.04f;
        [SerializeField] private float shadowScale = 1f;
        [SerializeField] private Color shadowTint = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color spriteShadowTint = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private Vector2 spriteShadowScreenOffset = new Vector2(0f, 0.22f);

        private Material material;
        private Texture2D fallbackTexture;
        private Mesh fallbackMesh;
        private RoSpriteAtlas spriteAtlas;
        private Mesh spriteShadowMesh;
        private string loadedShadowBasePath;
        private bool loadedSpriteShadow;
        private bool ownsSpriteAtlas;
        private UnityEngine.Camera _camera;
        private bool hasLateCameraRotation;
        private Quaternion lastLateCameraRotation;
        private UnityEngine.Camera GetCamera() => _camera != null ? _camera : (_camera = UnityEngine.Camera.main);

        public bool UsesFallback { get; private set; }

        public void Initialize(RoAssetDatabase assets, float actorGroundOffset) {
            groundOffset = actorGroundOffset;
            transform.localPosition = new Vector3(0f, -groundOffset + lift, 0f);
            transform.localScale = Vector3.one * Mathf.Max(0.0001f, shadowScale);
            ApplyStableOrientation(TryGetMainCameraRotation());

            ClearFallbackObjects();
            UsesFallback = true;
            loadedSpriteShadow = false;
            loadedShadowBasePath = RoActorSpriteVisual.NormalizeBasePath(shadowBasePath);

            if (useSpriteShadow && TryInitializeSpriteShadow(assets)) {
                UsesFallback = false;
                return;
            }

            InitializeFallbackBlob();
        }

        private void OnEnable() {
            RoActorTickService.Register((IRoActorLateTick) this);
        }

        private void OnDisable() {
            RoActorTickService.Unregister((IRoActorLateTick) this);
        }

        public void ActorLateTick(float now) {
            Quaternion? cameraRotation = TryGetMainCameraRotation();
            ApplyStableOrientation(cameraRotation);
            lastLateCameraRotation = cameraRotation ?? Quaternion.identity;
            hasLateCameraRotation = true;
        }

        public bool ShouldRunActorTick(float now, bool lateTick) {
            if (!lateTick) {
                return true;
            }

            if (orientation == RoActorShadowOrientation.FixedGroundPlane) {
                return false;
            }

            Quaternion cameraRotation = TryGetMainCameraRotation() ?? Quaternion.identity;
            return !hasLateCameraRotation ||
                Quaternion.Angle(cameraRotation, lastLateCameraRotation) > ShadowCameraRotationEpsilonDegrees;
        }

        public void ApplyStableOrientation(Quaternion? cameraRotation) {
            transform.rotation = CalculateShadowWorldRotation(orientation, cameraRotation);
        }

        public static Quaternion CalculateShadowWorldRotation(
            RoActorShadowOrientation shadowOrientation,
            Quaternion? cameraRotation
        ) {
            return shadowOrientation switch {
                RoActorShadowOrientation.CameraBillboard => cameraRotation ?? Quaternion.identity,
                RoActorShadowOrientation.FixedGroundPlane => Quaternion.Euler(90f, 0f, 0f),
                _ => cameraRotation ?? Quaternion.identity
            };
        }

        private bool TryInitializeSpriteShadow(RoAssetDatabase assets) {
            if (assets == null) {
                return false;
            }

            string basePath = RoActorSpriteVisual.NormalizeBasePath(shadowBasePath);
            loadedShadowBasePath = basePath;
            if (RoSpriteAtlasCache.TryGetOrBuild(assets, basePath, out RoSpriteAtlasCacheEntry cachedEntry)) {
                spriteAtlas = cachedEntry.Atlas;
                ownsSpriteAtlas = false;
            } else {
                SPR spr = assets.Load<SPR>($"{basePath}.spr");
                ACT act = assets.Load<ACT>($"{basePath}.act");
                if (spr == null || act == null) {
                    return false;
                }

                spriteAtlas = RoSpriteAtlasBuilder.Build(spr);
                ownsSpriteAtlas = true;
            }

            if (spriteAtlas == null || spriteAtlas.Sprites.Length == 0 || spriteAtlas.Sprites[0] == null) {
                return false;
            }

            var filter = GetComponent<MeshFilter>();
            if (filter == null) {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            material = CreateShadowMaterial(spriteShadowTint);
            material.mainTexture = spriteAtlas.Texture;
            SetTextureIfPropertyExists(material, "_BaseMap", spriteAtlas.Texture);
            SetTextureIfPropertyExists(material, "_MainTex", spriteAtlas.Texture);
            spriteAtlas.Texture.filterMode = FilterMode.Bilinear;
            spriteAtlas.Texture.wrapMode = TextureWrapMode.Clamp;

            spriteShadowMesh = BuildShadowSpriteMesh(spriteAtlas.Sprites[0], spriteShadowScreenOffset);
            filter.sharedMesh = spriteShadowMesh;
            renderer.sharedMaterial = material;

            loadedSpriteShadow = true;
            return true;
        }

        private void InitializeFallbackBlob() {
            fallbackTexture = CreateFallbackShadowTexture(FallbackTextureSize, shadowTint);
            material = CreateShadowMaterial(shadowTint);
            material.mainTexture = fallbackTexture;
            SetTextureIfPropertyExists(material, "_BaseMap", fallbackTexture);
            SetTextureIfPropertyExists(material, "_MainTex", fallbackTexture);

            var filter = GetComponent<MeshFilter>();
            if (filter == null) {
                filter = gameObject.AddComponent<MeshFilter>();
            }

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            fallbackMesh = CreateFallbackShadowMesh();
            filter.sharedMesh = fallbackMesh;
            renderer.sharedMaterial = material;
        }

        public static Texture2D CreateFallbackShadowTexture(int size, Color tint) {
            size = Mathf.Max(4, size);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "RO Actor Shadow Fallback",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = Mathf.Max(1f, center);
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    pixels[x + y * size] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Mesh BuildShadowSpriteMesh(UnityEngine.Sprite sprite, Vector2 screenOffset) {
            if (sprite == null) {
                throw new ArgumentNullException(nameof(sprite));
            }

            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUvs = sprite.uv;
            if (spriteVertices.Length != 4 || spriteUvs.Length != 4) {
                throw new ArgumentException("Shadow sprite must use a full-rect quad.", nameof(sprite));
            }

            var mesh = new Mesh {
                name = "RO Actor Shadow Sprite Quad"
            };
            ShadowSpriteVertexScratch[0] = new Vector3(spriteVertices[0].x + screenOffset.x, spriteVertices[0].y + screenOffset.y, 0f);
            ShadowSpriteVertexScratch[1] = new Vector3(spriteVertices[1].x + screenOffset.x, spriteVertices[1].y + screenOffset.y, 0f);
            ShadowSpriteVertexScratch[2] = new Vector3(spriteVertices[2].x + screenOffset.x, spriteVertices[2].y + screenOffset.y, 0f);
            ShadowSpriteVertexScratch[3] = new Vector3(spriteVertices[3].x + screenOffset.x, spriteVertices[3].y + screenOffset.y, 0f);
            mesh.SetVertices(ShadowSpriteVertexScratch);
            mesh.SetUVs(0, spriteUvs);
            mesh.SetTriangles(ShadowQuadTriangles, 0);
            mesh.SetNormals(ShadowQuadNormals);
            mesh.SetColors(ShadowQuadColors);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateFallbackShadowMesh() {
            var mesh = new Mesh {
                name = "RO Actor Shadow Fallback Quad"
            };
            mesh.SetVertices(FallbackShadowVertices);
            mesh.SetUVs(0, FallbackShadowUvs);
            mesh.SetTriangles(ShadowQuadTriangles, 0);
            mesh.SetNormals(ShadowQuadNormals);
            mesh.SetColors(ShadowQuadColors);
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateShadowMaterial(Color tint) {
            Shader shader = Shader.Find("Ragnarok/RO Sprite Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var shadowMaterial = new Material(shader) {
                name = "RO Actor Shadow Material",
                color = tint,
                renderQueue = (int) RenderQueue.Transparent - 10
            };
            SetColorIfPropertyExists(shadowMaterial, "_BaseColor", tint);
            SetColorIfPropertyExists(shadowMaterial, "_Color", tint);
            SetFloatIfPropertyExists(shadowMaterial, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(shadowMaterial, "_ZWrite", 0f);
            SetFloatIfPropertyExists(shadowMaterial, "_ZTest", (float) ShadowDepthTest);
            SetFloatIfPropertyExists(shadowMaterial, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(shadowMaterial, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(shadowMaterial, "_Cutoff", 0f);
            SetFloatIfPropertyExists(shadowMaterial, "_Surface", 1f);
            return shadowMaterial;
        }

        [ContextMenu("Print Shadow Visual Audit")]
        private void PrintShadowVisualAudit() {
            Debug.Log(BuildDiagnosticReport(), this);
        }

        public string BuildDiagnosticReport() {
            var lines = new List<string> {
                "RO actor shadow audit:",
                $"- basePath='{loadedShadowBasePath ?? RoActorSpriteVisual.NormalizeBasePath(shadowBasePath)}', useSpriteShadow={useSpriteShadow}, loadedSpriteShadow={loadedSpriteShadow}, usesFallback={UsesFallback}",
                $"- orientation={orientation}, lift={lift:0.###}, scale={shadowScale:0.###}, spriteOffset={FormatVector(spriteShadowScreenOffset)}, groundOffset={groundOffset:0.###}",
                $"- localPosition={FormatVector(transform.localPosition)}, localEuler={FormatVector(transform.localEulerAngles)}, worldEuler={FormatVector(transform.eulerAngles)}"
            };

            var filter = GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            lines.Add(mesh != null
                ? $"- mesh='{mesh.name}', vertices={mesh.vertexCount}, boundsCenter={FormatVector(mesh.bounds.center)}, boundsSize={FormatVector(mesh.bounds.size)}"
                : "- mesh=<none>");

            var renderer = GetComponent<MeshRenderer>();
            Material rendererMaterial = renderer != null ? renderer.sharedMaterial : material;
            if (rendererMaterial != null) {
                string cutoff = FormatMaterialFloat(rendererMaterial, "_Cutoff");
                string zWrite = FormatMaterialFloat(rendererMaterial, "_ZWrite");
                string zTest = FormatMaterialFloat(rendererMaterial, "_ZTest");
                string cull = FormatMaterialFloat(rendererMaterial, "_Cull");
                lines.Add(
                    $"- material='{rendererMaterial.name}', shader='{rendererMaterial.shader?.name}', queue={rendererMaterial.renderQueue}, color={FormatColor(GetMaterialColor(rendererMaterial))}, cutoff={cutoff}, zWrite={zWrite}, zTest={zTest}, cull={cull}"
                );
                lines.Add($"- texture={FormatTexture(rendererMaterial.mainTexture)}");
            } else {
                lines.Add("- material=<none>");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void OnDestroy() {
            ClearFallbackObjects();
        }

        private void ClearFallbackObjects() {
            DestroyRuntimeObject(fallbackMesh);
            DestroyRuntimeObject(fallbackTexture);
            DestroyRuntimeObject(spriteShadowMesh);
            DestroyRuntimeObject(material);
            if (ownsSpriteAtlas) {
                spriteAtlas?.Dispose();
            }

            fallbackMesh = null;
            fallbackTexture = null;
            spriteShadowMesh = null;
            material = null;
            spriteAtlas = null;
            ownsSpriteAtlas = false;
        }

        private Quaternion? TryGetMainCameraRotation() {
            UnityEngine.Camera camera = GetCamera();
            return camera != null ? camera.transform.rotation : (Quaternion?) null;
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target != null && target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void SetColorIfPropertyExists(Material target, string property, Color value) {
            if (target != null && target.HasProperty(property)) {
                target.SetColor(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture texture) {
            if (target != null && target.HasProperty(property)) {
                target.SetTexture(property, texture);
            }
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

        private static string FormatVector(Vector3 value) {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatVector(Vector2 value) {
            return $"({value.x:0.###}, {value.y:0.###})";
        }

        private static string FormatColor(Color value) {
            return $"({value.r:0.###}, {value.g:0.###}, {value.b:0.###}, {value.a:0.###})";
        }

        private static Color GetMaterialColor(Material target) {
            if (target == null) {
                return Color.clear;
            }

            if (target.HasProperty("_BaseColor")) {
                return target.GetColor("_BaseColor");
            }

            if (target.HasProperty("_Color")) {
                return target.GetColor("_Color");
            }

            return target.color;
        }

        private static string FormatMaterialFloat(Material target, string property) {
            return target != null && target.HasProperty(property) ? target.GetFloat(property).ToString("0.###") : "<missing>";
        }

        private static string FormatTexture(Texture texture) {
            if (texture == null) {
                return "<none>";
            }

            return $"'{texture.name}' {texture.width}x{texture.height}, filter={texture.filterMode}, wrap={texture.wrapMode}";
        }
    }
}
