using System;
using Ragnarok.Assets;
using Ragnarok.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    [ExecuteAlways]
    public sealed class RoMapSky : MonoBehaviour {
        public const int CloudTextureCount = 7;
        public const int MaxClouds = 48;
        public const float CloudSize = 24f;
        public const float CloudSpeed = 15f;
        public const float CloudAlphaScale = 0.45f;
        public const float CloudPlaneY = -10f;
        public const float CloudBoundsPlaneY = -30f;
        public const float BoundsPadding = 80f;
        public const float FadeInDuration = 1f;
        public const float FadeOutDuration = 0.1f;

        private static readonly SkyPrefs[] SkyList = {
            new SkyPrefs("airplane.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("airplane_01.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("gonryun.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("gon_dun02.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("himinn.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("ra_temsky.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("rwc01.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("sch_gld.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("valkyrie.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("yuno.rsw", new Color(0.4f, 0.6f, 0.8f, 1f), Color.white),
            new SkyPrefs("5@tower.rsw", new Color(0.2f, 0f, 0.2f, 1f), new Color(1f, 0.7f, 0.7f, 1f)),
            new SkyPrefs("thana_boss.rsw", new Color(0.88f, 0.83f, 0.76f, 1f), new Color(0.37f, 0f, 0f, 1f))
        };

        [SerializeField] private string mapName;
        [SerializeField] private SkyPrefs prefs;
        [SerializeField] private Texture2D[] cloudTextures;

        private Cloud[] clouds;
        private Mesh cloudMesh;
        private Material cloudMaterial;
        private UnityEngine.Camera targetCamera;
        private Color previousBackgroundColor;
        private CameraClearFlags previousClearFlags;
        private bool hasPreviousCameraState;
        private float[] bounds;
        private MaterialPropertyBlock propertyBlock;

        [Serializable]
        public struct SkyPrefs {
            [SerializeField] public string mapfile;
            [SerializeField] public Color skyColor;
            [SerializeField] public Color cloudColor;

            public SkyPrefs(string mapfile, Color skyColor, Color cloudColor) {
                this.mapfile = mapfile;
                this.skyColor = skyColor;
                this.cloudColor = cloudColor;
            }
        }

        private sealed class Cloud {
            public Vector3 direction;
            public GameObject obj;
            public MeshRenderer renderer;
            public Vector3 position;
            public float seed;
            public int texture;
            public float spawnTime;
            public float fadeOutAt;
            public bool fadeOut;
            public int lastAppliedTexture = -1;
            public float lastAppliedAlpha = float.NaN;
        }

        public static bool TryGetSkyPrefs(string mapName, out SkyPrefs result) {
            string normalized = NormalizeMapName(mapName);
            for (int i = 0; i < SkyList.Length; i++) {
                SkyPrefs prefs = SkyList[i];
                if (string.Equals(prefs.mapfile, normalized, StringComparison.OrdinalIgnoreCase)) {
                    result = prefs;
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static bool TryGetSkyPrefs(string mapName, RoAssetDatabase assets, out SkyPrefs result) {
            RoMapSkyDataCatalog catalog = RoMapSkyDataLoader.Load(assets);
            if (catalog.TryGet(mapName, out RoMapSkyDataEntry entry)) {
                result = new SkyPrefs(entry.MapId, entry.SkyColor, entry.CloudColor);
                return true;
            }

            return TryGetSkyPrefs(mapName, out result);
        }

        public void Initialize(string mapName, RoAssetDatabase assets, UnityEngine.Camera camera) {
            if (!TryGetSkyPrefs(mapName, assets, out SkyPrefs mapPrefs)) {
                enabled = false;
                return;
            }

            this.mapName = NormalizeMapName(mapName);
            prefs = mapPrefs;
            targetCamera = camera != null ? camera : UnityEngine.Camera.main;
            if (targetCamera == null) {
                enabled = false;
                return;
            }

            CaptureCameraState();
            ApplyCameraState();

            cloudTextures = LoadCloudTextures(assets);
            if (cloudTextures.Length == 0) {
                return;
            }

            cloudMesh = CreateCloudMesh();
            cloudMaterial = CreateCloudMaterial();
            SetupClouds();
        }

        private void Update() {
            if (targetCamera == null || clouds == null || clouds.Length == 0 || cloudTextures == null || cloudTextures.Length == 0 || cloudMaterial == null || cloudMesh == null) {
                return;
            }

            bounds ??= GetCameraPlaneBounds(targetCamera, CloudBoundsPlaneY);
            float now = Time.realtimeSinceStartup;
            float deltaTime = Application.isPlaying ? Time.deltaTime : 0f;

            for (int i = 0; i < clouds.Length; i++) {
                Cloud cloud = clouds[i];
                if (cloud == null) {
                    continue;
                }

                EnsureCloudObject(i, cloud);
                if (cloud.obj == null) {
                    continue;
                }

                float modifier = Mathf.Pow(Mathf.Sin(cloud.seed + now / 4f), 2f) * 0.5f;
                cloud.position.x += cloud.direction.x * deltaTime * (1f - modifier);
                cloud.position.z += cloud.direction.z * deltaTime;
                cloud.position.y = CloudPlaneY;

                if (cloud.fadeOut) {
                    if (now > cloud.fadeOutAt) {
                        RespawnCloud(cloud, now);
                    }
                } else if (!IsInsideBounds(cloud.position)) {
                    cloud.fadeOut = true;
                    cloud.fadeOutAt = now + FadeOutDuration;
                }

                Transform cloudTransform = cloud.obj.transform;
                cloudTransform.position = cloud.position;
                if (cloudTransform.rotation != Quaternion.identity) {
                    cloudTransform.rotation = Quaternion.identity;
                }

                ApplyCloudProperties(cloud, CalculateAlpha(cloud, now));
            }
        }

        private void FixedUpdate() {
            if (targetCamera != null) {
                bounds = GetCameraPlaneBounds(targetCamera, CloudBoundsPlaneY);
            }
        }

        private void OnDestroy() {
            RestoreCameraState();
            DestroyGeneratedObject(cloudMesh);
            DestroyGeneratedObject(cloudMaterial);
        }

        private static string NormalizeMapName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            return value.Replace("\\", "/").Trim().ToLowerInvariant();
        }

        private void CaptureCameraState() {
            if (targetCamera == null || hasPreviousCameraState) {
                return;
            }

            previousClearFlags = targetCamera.clearFlags;
            previousBackgroundColor = targetCamera.backgroundColor;
            hasPreviousCameraState = true;
        }

        private void ApplyCameraState() {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = prefs.skyColor;
        }

        private void RestoreCameraState() {
            if (!hasPreviousCameraState || targetCamera == null) {
                return;
            }

            targetCamera.clearFlags = previousClearFlags;
            targetCamera.backgroundColor = previousBackgroundColor;
            hasPreviousCameraState = false;
        }

        private static Texture2D[] LoadCloudTextures(RoAssetDatabase assets) {
            if (assets == null) {
                return Array.Empty<Texture2D>();
            }

            var textures = new Texture2D[CloudTextureCount];
            int count = 0;
            for (int i = 0; i < CloudTextureCount; i++) {
                if (!TryLoadCloudTexture(assets, i + 1, out Texture2D texture)) {
                    continue;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                textures[count++] = texture;
            }

            if (count == textures.Length) {
                return textures;
            }

            var compact = new Texture2D[count];
            Array.Copy(textures, compact, count);
            return compact;
        }

        private static bool TryLoadCloudTexture(RoAssetDatabase assets, int id, out Texture2D texture) {
            string tga = $"data/texture/effect/cloud{id}.tga";
            if (assets.Exists(tga)) {
                texture = assets.LoadTexture(tga);
                return texture != null;
            }

            string png = $"data/texture/effect/cloud{id}.png";
            if (assets.Exists(png)) {
                texture = assets.LoadTexture(png);
                return texture != null;
            }

            texture = null;
            return false;
        }

        private static Mesh CreateCloudMesh() {
            var mesh = new Mesh {
                name = "RO Cloud Quad"
            };

            mesh.SetVertices(new[] {
                new Vector3(0f, 0f, 0f),
                new Vector3(CloudSize, 0f, 0f),
                new Vector3(0f, 0f, CloudSize),
                new Vector3(CloudSize, 0f, CloudSize)
            });
            mesh.SetUVs(0, new[] {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateCloudMaterial() {
            Shader shader = Shader.Find("Ragnarok/RO Cloud Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) {
                name = "RO Cloud Material",
                renderQueue = (int) RenderQueue.Transparent
            };
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            return material;
        }

        private void SetupClouds() {
            clouds = new Cloud[MaxClouds];
            bounds = GetCameraPlaneBounds(targetCamera, CloudBoundsPlaneY);
            float now = Time.realtimeSinceStartup;
            for (int i = 0; i < clouds.Length; i++) {
                var cloud = new Cloud {
                    texture = UnityEngine.Random.Range(0, cloudTextures.Length),
                    seed = UnityEngine.Random.Range(0f, 2f * Mathf.PI),
                    spawnTime = now - UnityEngine.Random.Range(0f, FadeInDuration)
                };
                cloud.direction.Set(
                    UnityEngine.Random.Range(0.5f * CloudSpeed, CloudSpeed),
                    0f,
                    UnityEngine.Random.Range(-1f, 1f) * 0.1f
                );
                RespawnCloud(cloud, now);
                cloud.spawnTime = now - UnityEngine.Random.Range(0f, FadeInDuration);
                clouds[i] = cloud;
            }
        }

        private void EnsureCloudObject(int id, Cloud cloud) {
            if (cloud.obj != null) {
                return;
            }

            cloud.obj = new GameObject($"RO Cloud [{id}]");
            cloud.obj.transform.SetParent(transform, true);

            var filter = cloud.obj.AddComponent<MeshFilter>();
            cloud.renderer = cloud.obj.AddComponent<MeshRenderer>();
            filter.sharedMesh = cloudMesh;
            cloud.renderer.sharedMaterial = cloudMaterial;
            cloud.renderer.shadowCastingMode = ShadowCastingMode.Off;
            cloud.renderer.receiveShadows = false;
            cloud.obj.transform.rotation = Quaternion.identity;
            ApplyCloudProperties(cloud, 0f);
        }

        private void RespawnCloud(Cloud cloud, float now) {
            bounds ??= GetCameraPlaneBounds(targetCamera, CloudBoundsPlaneY);
            cloud.position = new Vector3(
                UnityEngine.Random.Range(bounds[0] - BoundsPadding, bounds[1] + BoundsPadding * 0.5f),
                CloudPlaneY,
                UnityEngine.Random.Range(bounds[2] - BoundsPadding * 0.5f, bounds[3] + BoundsPadding * 0.5f)
            );
            cloud.texture = UnityEngine.Random.Range(0, cloudTextures.Length);
            cloud.spawnTime = now;
            cloud.fadeOut = false;
            cloud.lastAppliedTexture = -1;
            cloud.lastAppliedAlpha = float.NaN;
        }

        private bool IsInsideBounds(Vector3 position) {
            bounds ??= GetCameraPlaneBounds(targetCamera, CloudBoundsPlaneY);
            return position.x >= bounds[0] - BoundsPadding
                && position.x <= bounds[1] + BoundsPadding
                && position.z >= bounds[2] - BoundsPadding
                && position.z <= bounds[3] + BoundsPadding;
        }

        private float CalculateAlpha(Cloud cloud, float now) {
            if (cloud.fadeOut) {
                return Mathf.Clamp01(Mathf.Abs(cloud.fadeOutAt - now) / FadeOutDuration);
            }

            return Mathf.Clamp01((now - cloud.spawnTime) / FadeInDuration);
        }

        private void ApplyCloudProperties(Cloud cloud, float alpha) {
            if (cloud?.renderer == null || cloudTextures == null || cloudTextures.Length == 0) {
                return;
            }

            int textureIndex = Mathf.Clamp(cloud.texture, 0, cloudTextures.Length - 1);
            Texture2D texture = cloudTextures[textureIndex];
            if (texture == null) {
                return;
            }

            Color color = prefs.cloudColor;
            float scaledAlpha = alpha * CloudAlphaScale;
            if (textureIndex == cloud.lastAppliedTexture &&
                Mathf.Abs(cloud.lastAppliedAlpha - scaledAlpha) < 0.001f) {
                return;
            }

            color.a *= scaledAlpha;
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetTexture("_BaseMap", texture);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetTexture("_MainTex", texture);
            propertyBlock.SetColor("_Color", color);
            cloud.renderer.SetPropertyBlock(propertyBlock);
            cloud.lastAppliedTexture = textureIndex;
            cloud.lastAppliedAlpha = scaledAlpha;
        }

        public static float[] GetCameraPlaneBounds(UnityEngine.Camera camera, float planeY) {
            if (camera == null) {
                return new[] { -BoundsPadding, BoundsPadding, -BoundsPadding, BoundsPadding };
            }

            Vector3 bottomLeft = GetPointAtHeight(camera.ViewportPointToRay(new Vector3(0f, 0f, 0f)), planeY);
            Vector3 topLeft = GetPointAtHeight(camera.ViewportPointToRay(new Vector3(0f, 1f, 0f)), planeY);
            Vector3 topRight = GetPointAtHeight(camera.ViewportPointToRay(new Vector3(1f, 1f, 0f)), planeY);
            Vector3 bottomRight = GetPointAtHeight(camera.ViewportPointToRay(new Vector3(1f, 0f, 0f)), planeY);

            float minX = Mathf.Min(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x);
            float maxX = Mathf.Max(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x);
            float minZ = Mathf.Min(bottomLeft.z, topLeft.z, topRight.z, bottomRight.z);
            float maxZ = Mathf.Max(bottomLeft.z, topLeft.z, topRight.z, bottomRight.z);
            return new[] { minX, maxX, minZ, maxZ };
        }

        public static Vector3 GetPointAtHeight(Ray ray, float height) {
            if (Mathf.Abs(ray.direction.y) < 0.0001f) {
                return ray.origin;
            }

            return ray.origin + ((ray.origin.y - height) / -ray.direction.y) * ray.direction;
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void DestroyGeneratedObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }
    }
}
