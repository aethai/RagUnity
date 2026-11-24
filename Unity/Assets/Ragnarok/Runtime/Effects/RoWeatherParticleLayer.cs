using System;
using UnityEngine;

namespace Ragnarok.Effects {
    public sealed class RoWeatherParticleLayer : MonoBehaviour {
        private const int RainOverlayDropCount = 220;
        private const float RainOverlayDistance = 8f;
        private static readonly bool UseRainScreenOverlay = false;

        private ParticleSystem particles;
        private ParticleSystemRenderer particleRenderer;
        private GameObject rainOverlayObject;
        private MeshFilter rainOverlayFilter;
        private MeshRenderer rainOverlayRenderer;
        private Mesh rainOverlayMesh;
        private Vector3[] rainOverlayVertices;
        private Vector2[] rainOverlayUvs;
        private Color32[] rainOverlayColors;
        private int[] rainOverlayTriangles;
        private Material material;
        private Texture2D materialTexture;
        private bool ownsMaterialTexture;
        private RoWeatherParticleSpec spec;
        private Transform followTarget;
        private Vector3 followLocalOffset;
        private Vector3 worldPosition;
        private UnityEngine.Camera renderCamera;
        private float currentIntensity = 1f;
        private bool initialized;
        private bool finished;

        public event Action<RoWeatherParticleLayer> Finished;

        public bool IsInitialized => initialized;
        public bool IsFinished => finished;
        public RoWeatherParticleType CurrentType => spec.Type;
        public int CurrentMaxParticles => spec.EffectiveMaxParticles;
        public float CurrentIntensity => currentIntensity;
        public float CurrentRateOverTime => spec.EffectiveRateOverTime * currentIntensity;
        public int LiveParticleCount => particles != null ? particles.particleCount : 0;

        private void Update() {
            if (!initialized || finished) {
                return;
            }

            ApplyPosition();
            if (UseRainScreenOverlay) {
                UpdateRainOverlay();
            }
        }

        private void OnDestroy() {
            if (rainOverlayMesh != null) {
                DestroyRuntimeObject(rainOverlayMesh);
                rainOverlayMesh = null;
            }

            if (material != null) {
                DestroyRuntimeObject(material);
                material = null;
            }

            if (ownsMaterialTexture && materialTexture != null) {
                DestroyRuntimeObject(materialTexture);
                materialTexture = null;
            }
        }

        public void Initialize(
            RoWeatherParticleSpec weatherSpec,
            Vector3 spawnWorldPosition,
            Transform target = null,
            Vector3 targetOffset = default,
            UnityEngine.Camera camera = null
        ) {
            EnsureComponents();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Clear(true);

            spec = weatherSpec;
            followTarget = target;
            followLocalOffset = targetOffset;
            worldPosition = spawnWorldPosition;
            renderCamera = camera;
            currentIntensity = 1f;
            finished = false;
            initialized = true;
            gameObject.SetActive(true);

            ConfigureParticles();
            ApplyPosition();
            particles.Clear(true);
            particles.Play(true);
            if (UseRainScreenOverlay) {
                UpdateRainOverlay();
            }
        }

        public void SetIntensity(float intensity) {
            currentIntensity = Mathf.Clamp01(intensity);
            if (!initialized || particles == null) {
                return;
            }

            ApplyIntensity();
        }

        public void SyncSpec(RoWeatherParticleSpec weatherSpec) {
            if (!initialized || particles == null) {
                spec = weatherSpec;
                return;
            }

            RoWeatherParticleType previousType = spec.Type;
            spec = weatherSpec;
            ApplyRuntimeSpec();
            if (previousType != spec.Type) {
                ApplyWeatherTexture();
            }

            ApplyPosition();
            ApplyIntensity();
        }

        public void SetFollowTarget(Transform target, Vector3 targetOffset = default, UnityEngine.Camera camera = null) {
            followTarget = target;
            followLocalOffset = targetOffset;
            if (camera != null) {
                renderCamera = camera;
            }

            if (initialized && !finished) {
                ApplyPosition();
            }
        }

        public void Stop() {
            bool wasFinished = finished;
            finished = true;
            if (particles != null) {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Clear(true);
            }

            SetRainOverlayEnabled(false);
            gameObject.SetActive(false);
            if (wasFinished) {
                return;
            }

            Finished?.Invoke(this);
        }

        private void EnsureComponents() {
            particles = GetComponent<ParticleSystem>();
            if (particles == null) {
                particles = gameObject.AddComponent<ParticleSystem>();
            }

            particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null) {
                particleRenderer = gameObject.AddComponent<ParticleSystemRenderer>();
            }

            if (material == null) {
                Shader shader = Shader.Find("Ragnarok/RO Weather Particle");
                if (shader == null) {
                    shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                }

                if (shader == null) {
                    shader = Shader.Find("Particles/Standard Unlit");
                }

                if (shader == null) {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                if (shader == null) {
                    shader = Shader.Find("Sprites/Default");
                }

                material = new Material(shader) {
                    name = "RO Weather Particles",
                    hideFlags = HideFlags.DontSave
                };
                ConfigureWeatherMaterial(material);
            }
        }

        private void ConfigureParticles() {
            var main = particles.main;
            main.loop = true;
            main.duration = 10f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = spec.EffectiveMaxParticles;
            main.startLifetime = spec.EffectiveStartLifetime;
            main.startSpeed = 0f;
            main.startRotation3D = spec.Type == RoWeatherParticleType.Leaves;
            ApplyParticleSize(main);
            main.startColor = spec.EffectiveStartColor;

            var emission = particles.emission;
            emission.enabled = !spec.ManualEmission;
            emission.rateOverTime = spec.ManualEmission ? 0f : spec.EffectiveRateOverTime;

            var shape = particles.shape;
            shape.enabled = !spec.ManualEmission;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spec.EffectiveWidth, spec.EffectiveHeight, spec.EffectiveDepth);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = !spec.ManualEmission;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = spec.WindVelocity.x;
            velocity.y = spec.FallVelocity;
            velocity.z = spec.WindVelocity.y;

            ApplyLeafModules();

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            Color start = spec.EffectiveStartColor;
            Color end = start;
            end.a = 0f;
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(start, 0.8f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(start.a, 0.8f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            ApplyRendererMode();
            ApplyParticleRendererMaterial();
            particleRenderer.enabled = true;
            particleRenderer.sortingOrder = 25000;
            particleRenderer.sortingFudge = 100f;
            ApplyWeatherTexture();
            SyncRainOverlayMaterial();
            ApplyIntensity();
        }

        private void ApplyRuntimeSpec() {
            var main = particles.main;
            main.maxParticles = spec.EffectiveMaxParticles;
            main.startLifetime = spec.EffectiveStartLifetime;
            main.startRotation3D = spec.Type == RoWeatherParticleType.Leaves;
            ApplyParticleSize(main);

            var emission = particles.emission;
            emission.enabled = !spec.ManualEmission && currentIntensity > 0.0001f;
            emission.rateOverTime = spec.ManualEmission ? 0f : spec.EffectiveRateOverTime * currentIntensity;

            var shape = particles.shape;
            shape.enabled = !spec.ManualEmission;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spec.EffectiveWidth, spec.EffectiveHeight, spec.EffectiveDepth);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = !spec.ManualEmission;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = spec.WindVelocity.x;
            velocity.y = spec.FallVelocity;
            velocity.z = spec.WindVelocity.y;

            ApplyLeafModules();

            ApplyRendererMode();
            ApplyParticleRendererMaterial();
            particleRenderer.enabled = true;
            particleRenderer.sortingOrder = 25000;
            particleRenderer.sortingFudge = 100f;
        }

        private void ApplyParticleRendererMaterial() {
            if (particleRenderer != null && particleRenderer.sharedMaterial != material) {
                particleRenderer.sharedMaterial = material;
            }
        }

        private void ApplyParticleSize(ParticleSystem.MainModule main) {
            if (spec.Type == RoWeatherParticleType.Rain) {
                main.startSize3D = true;
                main.startSizeX = spec.EffectiveStartSize * 0.34f;
                main.startSizeY = spec.EffectiveStartSize * 3.8f;
                main.startSizeZ = spec.EffectiveStartSize;
                return;
            }

            main.startSize3D = false;
            main.startSize = spec.EffectiveStartSize;
        }

        private void ApplyRendererMode() {
            if (particleRenderer == null) {
                return;
            }

            if (spec.Type == RoWeatherParticleType.Rain) {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.velocityScale = 0f;
                particleRenderer.lengthScale = 1f;
                particleRenderer.alignment = ParticleSystemRenderSpace.Facing;
                return;
            }

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.velocityScale = 0f;
            particleRenderer.lengthScale = 1f;
            particleRenderer.alignment = ParticleSystemRenderSpace.View;
        }

        private void ApplyLeafModules() {
            if (particles == null) {
                return;
            }

            var textureSheet = particles.textureSheetAnimation;
            if (spec.Type == RoWeatherParticleType.Leaves) {
                textureSheet.enabled = true;
                textureSheet.mode = ParticleSystemAnimationMode.Grid;
                textureSheet.numTilesX = 3;
                textureSheet.numTilesY = 1;
                textureSheet.animation = ParticleSystemAnimationType.SingleRow;
                textureSheet.rowMode = ParticleSystemAnimationRowMode.Custom;
                textureSheet.rowIndex = 0;
                textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
                textureSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 2.999f);
            } else {
                textureSheet.enabled = false;
            }

            var rotation = particles.rotationOverLifetime;
            if (spec.Type == RoWeatherParticleType.Leaves) {
                rotation.enabled = true;
                rotation.separateAxes = true;
                rotation.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
                rotation.y = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
                rotation.z = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);
            } else {
                rotation.enabled = false;
            }
        }

        private void ApplyIntensity() {
            if (particles == null) {
                return;
            }

            Color color = spec.EffectiveStartColor;
            color.a *= spec.Type == RoWeatherParticleType.Rain ? 1f : currentIntensity;

            var main = particles.main;
            main.startColor = color;

            var emission = particles.emission;
            emission.enabled = !spec.ManualEmission && currentIntensity > 0.0001f;
            emission.rateOverTime = spec.ManualEmission ? 0f : spec.EffectiveRateOverTime * currentIntensity;

            SetRainOverlayEnabled(UseRainScreenOverlay && spec.Type == RoWeatherParticleType.Rain && currentIntensity > 0.0001f);
            if (material != null) {
                material.renderQueue = Mathf.Clamp(spec.EffectiveRenderQueue, 2500, 5000);
                material.color = color;
                SetColorIfPropertyExists(material, "_BaseColor", color);
                SetColorIfPropertyExists(material, "_Color", color);
                SetColorIfPropertyExists(material, "_TintColor", color);
            }
        }

        private void ApplyPosition() {
            if (followTarget != null) {
                Quaternion rotation = spec.Type == RoWeatherParticleType.Rain ? Quaternion.identity : followTarget.rotation;
                transform.SetPositionAndRotation(followTarget.TransformPoint(followLocalOffset), rotation);
                ApplyCameraFrustumShapeScale();
                return;
            }

            if (spec.FollowCamera) {
                UnityEngine.Camera camera = ResolveCamera();
                if (camera != null) {
                    Quaternion rotation = spec.Type == RoWeatherParticleType.Rain ? Quaternion.identity : camera.transform.rotation;
                    transform.SetPositionAndRotation(camera.transform.TransformPoint(followLocalOffset), rotation);
                    ApplyCameraFrustumShapeScale();
                    return;
                }
            }

            transform.position = worldPosition + followLocalOffset;
        }

        private void ApplyCameraFrustumShapeScale() {
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null || particles == null || !spec.FollowCamera) {
                return;
            }

            float distance = Mathf.Abs(followLocalOffset.z);
            if (distance <= 0.001f) {
                distance = Mathf.Max(camera.nearClipPlane + 1f, 8f);
            }

            float frustumHeight;
            float frustumWidth;
            if (camera.orthographic) {
                frustumHeight = camera.orthographicSize * 2f;
                frustumWidth = frustumHeight * Mathf.Max(0.1f, camera.aspect);
            } else {
                frustumHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                frustumWidth = frustumHeight * Mathf.Max(0.1f, camera.aspect);
            }

            var shape = particles.shape;
            shape.scale = new Vector3(
                Mathf.Max(spec.EffectiveWidth, frustumWidth * 1.35f),
                Mathf.Max(spec.EffectiveHeight, frustumHeight * 1.35f),
                Mathf.Max(spec.EffectiveDepth, distance * 0.75f));
        }

        private UnityEngine.Camera ResolveCamera() {
            return RoEffectCameraCache.Resolve(renderCamera);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }

        private static void ConfigureWeatherMaterial(Material target) {
            if (target == null) {
                return;
            }

            SetFloatIfPropertyExists(target, "_Surface", 1f);
            SetFloatIfPropertyExists(target, "_Blend", 0f);
            SetFloatIfPropertyExists(target, "_SrcBlend", (float) UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(target, "_DstBlend", (float) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(target, "_ZWrite", 0f);
            SetFloatIfPropertyExists(target, "_ZTest", (float) UnityEngine.Rendering.CompareFunction.Always);
            SetFloatIfPropertyExists(target, "_Cull", (float) UnityEngine.Rendering.CullMode.Off);
            target.SetOverrideTag("RenderType", "Transparent");
        }

        private void ApplyWeatherTexture() {
            if (material == null) {
                return;
            }

            if (ownsMaterialTexture && materialTexture != null) {
                DestroyRuntimeObject(materialTexture);
            }

            materialTexture = ResolveWeatherTexture(spec.Type, out ownsMaterialTexture);
            if (ownsMaterialTexture) {
                materialTexture.hideFlags = HideFlags.DontSave;
            }

            material.mainTexture = materialTexture;
            SetTextureIfPropertyExists(material, "_BaseMap", materialTexture);
            SetTextureIfPropertyExists(material, "_MainTex", materialTexture);
            SyncRainOverlayMaterial();
        }

        public void Emit(ParticleSystem.EmitParams emitParams, int count) {
            if (!initialized || finished || particles == null || count <= 0) {
                return;
            }

            particles.Emit(emitParams, count);
        }

        public int GetParticles(ParticleSystem.Particle[] buffer) {
            if (!initialized || finished || particles == null || buffer == null || buffer.Length == 0) {
                return 0;
            }

            return particles.GetParticles(buffer);
        }

        public void SetParticles(ParticleSystem.Particle[] buffer, int count) {
            if (!initialized || finished || particles == null || buffer == null || count <= 0) {
                return;
            }

            particles.SetParticles(buffer, count);
        }

        private void EnsureRainOverlay() {
            if (rainOverlayObject == null) {
                rainOverlayObject = new GameObject("RO Rain Screen Overlay") {
                    hideFlags = HideFlags.DontSave
                };
                rainOverlayObject.layer = gameObject.layer;
                rainOverlayObject.transform.SetParent(transform, false);
                rainOverlayFilter = rainOverlayObject.AddComponent<MeshFilter>();
                rainOverlayRenderer = rainOverlayObject.AddComponent<MeshRenderer>();
            }

            if (rainOverlayObject.layer != gameObject.layer) {
                rainOverlayObject.layer = gameObject.layer;
            }

            if (rainOverlayFilter == null) {
                rainOverlayFilter = rainOverlayObject.GetComponent<MeshFilter>() ?? rainOverlayObject.AddComponent<MeshFilter>();
            }

            if (rainOverlayRenderer == null) {
                rainOverlayRenderer = rainOverlayObject.GetComponent<MeshRenderer>() ?? rainOverlayObject.AddComponent<MeshRenderer>();
            }

            if (rainOverlayMesh == null) {
                rainOverlayMesh = new Mesh {
                    name = "RO Rain Overlay Mesh",
                    hideFlags = HideFlags.DontSave
                };
                rainOverlayMesh.MarkDynamic();
                rainOverlayFilter.sharedMesh = rainOverlayMesh;
            }

            if (rainOverlayVertices == null || rainOverlayVertices.Length != RainOverlayDropCount * 4) {
                rainOverlayVertices = new Vector3[RainOverlayDropCount * 4];
                rainOverlayUvs = new Vector2[RainOverlayDropCount * 4];
                rainOverlayColors = new Color32[RainOverlayDropCount * 4];
                rainOverlayTriangles = new int[RainOverlayDropCount * 6];

                for (int i = 0; i < RainOverlayDropCount; i++) {
                    int vertex = i * 4;
                    int triangle = i * 6;
                    rainOverlayUvs[vertex] = new Vector2(0f, 0f);
                    rainOverlayUvs[vertex + 1] = new Vector2(1f, 0f);
                    rainOverlayUvs[vertex + 2] = new Vector2(1f, 1f);
                    rainOverlayUvs[vertex + 3] = new Vector2(0f, 1f);
                    rainOverlayTriangles[triangle] = vertex;
                    rainOverlayTriangles[triangle + 1] = vertex + 1;
                    rainOverlayTriangles[triangle + 2] = vertex + 2;
                    rainOverlayTriangles[triangle + 3] = vertex;
                    rainOverlayTriangles[triangle + 4] = vertex + 2;
                    rainOverlayTriangles[triangle + 5] = vertex + 3;
                }
            }

            if (rainOverlayRenderer != null && rainOverlayRenderer.sharedMaterial != material) {
                rainOverlayRenderer.sharedMaterial = material;
            }
        }

        private void SyncRainOverlayMaterial() {
            if (!UseRainScreenOverlay || spec.Type != RoWeatherParticleType.Rain) {
                SetRainOverlayEnabled(false);
                return;
            }

            EnsureRainOverlay();
            rainOverlayRenderer.sharedMaterial = material;
            rainOverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rainOverlayRenderer.receiveShadows = false;
            rainOverlayRenderer.sortingOrder = 25000;
            SetRainOverlayEnabled(currentIntensity > 0.0001f);
        }

        private void SetRainOverlayEnabled(bool enabled) {
            if (rainOverlayRenderer != null) {
                rainOverlayRenderer.enabled = enabled;
            }

            if (rainOverlayObject != null && rainOverlayObject.activeSelf != enabled) {
                rainOverlayObject.SetActive(enabled);
            }
        }

        private void UpdateRainOverlay() {
            if (!UseRainScreenOverlay || spec.Type != RoWeatherParticleType.Rain || currentIntensity <= 0.0001f) {
                SetRainOverlayEnabled(false);
                return;
            }

            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null) {
                SetRainOverlayEnabled(false);
                return;
            }

            EnsureRainOverlay();
            SetRainOverlayEnabled(true);

            float distance = Mathf.Max(camera.nearClipPlane + 0.5f, RainOverlayDistance);
            rainOverlayObject.transform.SetPositionAndRotation(
                camera.transform.TransformPoint(new Vector3(0f, 0f, distance)),
                camera.transform.rotation);

            float viewHeight;
            float viewWidth;
            if (camera.orthographic) {
                viewHeight = camera.orthographicSize * 2f;
                viewWidth = viewHeight * Mathf.Max(0.1f, camera.aspect);
            } else {
                viewHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                viewWidth = viewHeight * Mathf.Max(0.1f, camera.aspect);
            }

            float halfWidth = viewWidth * 0.5f;
            float halfHeight = viewHeight * 0.5f;
            float dropWidth = Mathf.Max(viewHeight * 0.012f, 0.08f);
            float dropHeight = Mathf.Max(viewHeight * 0.075f, 0.55f);
            float time = Time.time * 0.82f;
            float visualIntensity = Mathf.Sqrt(Mathf.Clamp01(currentIntensity));
            byte baseAlpha = (byte) Mathf.RoundToInt(255f * visualIntensity);

            for (int i = 0; i < RainOverlayDropCount; i++) {
                float seedA = Hash01(i * 2 + 11);
                float seedB = Hash01(i * 2 + 29);
                float seedC = Hash01(i * 2 + 47);
                float x = Mathf.Lerp(-halfWidth - dropHeight, halfWidth + dropHeight, seedA);
                float yTravel = Mathf.Repeat(seedB - time * Mathf.Lerp(0.75f, 1.35f, seedC), 1f);
                float y = Mathf.Lerp(-halfHeight - dropHeight, halfHeight + dropHeight, yTravel);
                float localWidth = dropWidth * Mathf.Lerp(0.75f, 1.35f, seedC);
                float localHeight = dropHeight * Mathf.Lerp(0.72f, 1.25f, seedB);
                byte alpha = (byte) Mathf.RoundToInt(baseAlpha * Mathf.Lerp(0.55f, 1f, seedC));

                int vertex = i * 4;
                rainOverlayVertices[vertex] = new Vector3(x - localWidth, y - localHeight, 0f);
                rainOverlayVertices[vertex + 1] = new Vector3(x + localWidth, y - localHeight, 0f);
                rainOverlayVertices[vertex + 2] = new Vector3(x + localWidth, y + localHeight, 0f);
                rainOverlayVertices[vertex + 3] = new Vector3(x - localWidth, y + localHeight, 0f);
                rainOverlayColors[vertex] = new Color32(255, 255, 255, alpha);
                rainOverlayColors[vertex + 1] = rainOverlayColors[vertex];
                rainOverlayColors[vertex + 2] = rainOverlayColors[vertex];
                rainOverlayColors[vertex + 3] = rainOverlayColors[vertex];
            }

            rainOverlayMesh.Clear(false);
            rainOverlayMesh.vertices = rainOverlayVertices;
            rainOverlayMesh.uv = rainOverlayUvs;
            rainOverlayMesh.colors32 = rainOverlayColors;
            rainOverlayMesh.triangles = rainOverlayTriangles;
            rainOverlayMesh.bounds = new Bounds(Vector3.zero, new Vector3(viewWidth * 2f, viewHeight * 2f, 1f));
        }

        private static float Hash01(int value) {
            unchecked {
                uint x = (uint) value;
                x ^= x >> 16;
                x *= 0x7feb352dU;
                x ^= x >> 15;
                x *= 0x846ca68bU;
                x ^= x >> 16;
                return (x & 0x00ffffffU) / 16777215f;
            }
        }

        private static Texture2D CreateRainTexture() {
            var texture = new Texture2D(32, 128, TextureFormat.RGBA32, false, true) {
                name = "RO Rain Drop Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[32 * 128];
            for (int y = 0; y < 128; y++) {
                float v = y / 127f;
                float verticalAlpha = Mathf.SmoothStep(0.0f, 0.18f, v) * (1f - Mathf.SmoothStep(0.86f, 1f, v));
                float highlight = Mathf.SmoothStep(0.34f, 0.58f, v) * (1f - Mathf.SmoothStep(0.72f, 0.94f, v));
                float streakCenter = Mathf.Lerp(0.64f, 0.36f, v);
                for (int x = 0; x < 32; x++) {
                    float u = (x + 0.5f) / 32f;
                    float dist = Mathf.Abs(u - streakCenter);
                    float core = 1f - Mathf.SmoothStep(0.0f, 0.055f, dist);
                    float edge = 1f - Mathf.SmoothStep(0.055f, 0.18f, dist);
                    float alpha = verticalAlpha * Mathf.Clamp01(core * 0.95f + edge * 0.48f + highlight * 0.18f);
                    byte a = (byte) Mathf.RoundToInt(255f * alpha);
                    byte r = (byte) Mathf.RoundToInt(Mathf.Lerp(136f, 255f, core));
                    byte g = (byte) Mathf.RoundToInt(Mathf.Lerp(166f, 255f, core));
                    byte b = (byte) Mathf.RoundToInt(Mathf.Lerp(220f, 255f, core));
                    pixels[x + y * 32] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D ResolveWeatherTexture(RoWeatherParticleType type, out bool ownsTexture) {
            ownsTexture = true;
            switch (type) {
                case RoWeatherParticleType.Rain:
                    return CreateRainTexture();
                case RoWeatherParticleType.Leaves:
                    Texture2D leafAtlas = Resources.Load<Texture2D>("Weather/RoLeafParticleAtlas");
                    if (leafAtlas != null) {
                        ownsTexture = false;
                        return leafAtlas;
                    }

                    return CreateLeafFallbackTexture();
                default:
                    return CreateSoftDotTexture();
            }
        }

        private static Texture2D CreateLeafFallbackTexture() {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, false) {
                name = "RO Leaf Particle Fallback Atlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = new Color32(0, 0, 0, 0);
            }

            DrawLeaf(pixels, 64, 10, 32, 11, 21, new Color32(117, 139, 45, 255));
            DrawLeaf(pixels, 64, 32, 32, 10, 19, new Color32(145, 106, 42, 255));
            DrawLeaf(pixels, 64, 52, 32, 9, 18, new Color32(83, 125, 53, 255));
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DrawLeaf(Color32[] pixels, int size, int centerX, int centerY, int radiusX, int radiusY, Color32 color) {
            for (int y = -radiusY; y <= radiusY; y++) {
                for (int x = -radiusX; x <= radiusX; x++) {
                    float nx = x / Mathf.Max(1f, radiusX);
                    float ny = y / Mathf.Max(1f, radiusY);
                    float shape = nx * nx + ny * ny;
                    if (shape > 1f) {
                        continue;
                    }

                    int px = Mathf.Clamp(centerX + x, 0, size - 1);
                    int py = Mathf.Clamp(centerY + y, 0, size - 1);
                    float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.72f, 1f, shape));
                    byte a = (byte)Mathf.RoundToInt(alpha * color.a);
                    pixels[px + py * size] = new Color32(color.r, color.g, color.b, a);
                }
            }
        }

        private static Texture2D CreateSoftDotTexture() {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false, true) {
                name = "RO Weather Soft Dot Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[16 * 16];
            for (int y = 0; y < 16; y++) {
                for (int x = 0; x < 16; x++) {
                    float dx = ((x + 0.5f) / 16f - 0.5f) * 2f;
                    float dy = ((y + 0.5f) / 16f - 0.5f) * 2f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte) Mathf.RoundToInt(255f * Mathf.Clamp01(1f - radius));
                    pixels[x + y * 16] = new Color32(220, 235, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void SetColorIfPropertyExists(Material target, string property, Color value) {
            if (target.HasProperty(property)) {
                target.SetColor(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture value) {
            if (target.HasProperty(property)) {
                target.SetTexture(property, value);
            }
        }
    }
}
