using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ragnarok.Rendering {
    /// <summary>
    /// The enhanced look: one global post-processing volume (tonemapping, bloom, grading, vignette) and the lighting
    /// globals of RoLookLighting.hlsl, created once and kept across scenes. The camera turns post-processing and HDR on
    /// only in the Enhanced mode (RoRuntimeMapController), so the Original mode's picture is untouched.
    /// Live tuning: !lookset (RoLookState.Tune).
    /// </summary>
    public sealed class RoLookRig : MonoBehaviour {
        static readonly int SkyId = Shader.PropertyToID("_RoLookSky");
        static readonly int GroundId = Shader.PropertyToID("_RoLookGround");
        static readonly int SunId = Shader.PropertyToID("_RoLookSun");
        static readonly int ParamsId = Shader.PropertyToID("_RoLookParams");
        static readonly int SpriteId = Shader.PropertyToID("_RoLookSprite");
        static readonly int SpriteShapeId = Shader.PropertyToID("_RoLookSpriteShape");
        static readonly int AmbientId = Shader.PropertyToID("_RoLookAmbient");
        static readonly int FxBoostId = Shader.PropertyToID("_RoLookFxBoost");
        static readonly int IndoorId = Shader.PropertyToID("_RoLookIndoor");
        static readonly int FxAlphaGammaId = Shader.PropertyToID("_RoLookFxAlphaGamma");
        static readonly int FxGammaBlendId = Shader.PropertyToID("_RoLookFxGammaBlend");
        static readonly int WaterId = Shader.PropertyToID("_RoLookWater");
        static readonly int ReliefId = Shader.PropertyToID("_RoLookRelief");
        static readonly int WaterDeepId = Shader.PropertyToID("_RoLookWaterDeep");
        static readonly int CloudShapeId = Shader.PropertyToID("_RoCloudShadowShape");
        static readonly int CloudLookId = Shader.PropertyToID("_RoCloudShadowLook");
        static readonly int RimId = Shader.PropertyToID("_RoLookRim");

        // sky ambient: a cool sky tint times the map's RSW ambient, scaled (linear space)
        public Vector4 Sky = new Vector4(0.75f, 0.88f, 1.15f, 0.5f);
        // the sky tint and scale at night (lerped by the day / night controller's daylight factor): a deep blue night
        // with the lamps as warm islands
        public Vector4 NightSky = new Vector4(0.55f, 0.7f, 1.4f, 1.6f);
        // ground bounce tint, and how far the hemisphere leans to it on surfaces facing down
        public Vector4 Ground = new Vector4(1.04f, 0.96f, 0.86f, 0.6f);
        // sun tint and intensity (on top of the URP main light the map sets up)
        public Vector4 Sun = new Vector4(1.0f, 0.96f, 0.9f, 1.15f);
        // lightmap light, lightmap occlusion, sun wrap, lamp scale
        public Vector4 Params = new Vector4(1.0f, 0.45f, 0.12f, 3.0f);
        // sprite sun share, sprite ambient scale, sprite lamp scale, model ambient floor
        public Vector4 Sprite = new Vector4(0.7f, 1.0f, 1.0f, 0.0f);
        // sprite volume: body half-width (world units) of the cylinder the card is shaded as, edge bevel radius (texels),
        // edge bevel strength, cylinder strength
        public Vector4 SpriteShape = new Vector4(0.9f, 2f, 0.55f, 0.6f);
        // the sun's shadow strength (the map sets 0.85 for the old look; with the sky ambient filling it, a full shadow)
        public float ShadowStrength = 1f;
        // classic effects' intensity: above 1 their bright parts reach the bloom
        public float FxBoost = 1.0f;
        // effect translucency as the original's gamma blending (see RoClassicEffect.shader)
        public float FxAlphaGamma = 2.2f;
        // linear project: effects blend as the original's gamma frame buffer (RoClassicEffect.shader); 0 = the alpha curve above
        public float FxGammaBlend = 1f;
        // water: on, depth absorption, refraction strength, foam width (world units)
        public Vector4 Water = new Vector4(1f, 0.55f, 0.03f, 0.7f);
        // relief from texture luminance: ground, models
        public Vector4 Relief = new Vector4(0f, 0f, 0f, 0f);
        // deep water colour (linear) and how much of the RO water texture tints the body
        public Vector4 WaterDeep = new Vector4(0.008f, 0.07f, 0.24f, 0.75f);
        // sprites on a map with no sky: they have no lightmap, so the ambient carries them
        public float IndoorSpriteAmbient = 3f;
        // the GND lightmap added on top on a map with no sky (the original's composite), in linear
        public float IndoorBakedAdd = 1.0f;
        // diagnostics / profiles: -1 = from the map (no sky = indoor), 0 / 1 forced
        public float ForceIndoor = -1f;
        // a profile's own scene ambient (rgb), blended over the map's RSW / day-night ambient by w (0 = the map's)
        public Vector4 AmbientOverride = new Vector4(0.5f, 0.5f, 0.5f, 0f);
        // cloud shadow shape (size, warp, macro swing, edge detail) and strength scale; zero = the shader's defaults
        public Vector4 CloudShape = Vector4.zero;
        public float CloudStrength;
        // models: sky rim strength, rim power, foliage wrap; zero = the shader's defaults
        public Vector4 Rim = Vector4.zero;
        // the map whose profile is applied (look-presets/maps/<map>.txt, else look-presets/default.txt)
        string profileMap;
        public string ActiveProfile { get; private set; } = "none";
        // with no sky: how strongly the albedo is lit (the rest is the bake's colour, as in the original)
        public float IndoorAlbedoLight = 1.0f;
        // with no sky: stops added to the exposure (a dungeon gets no sun or sky fill, the grading alone would leave it murky)
        public float IndoorExposure = 0f;
        float exposureBase = -0.55f;
        // depth of field: 0 = off, else the aperture (f-stop, lower = blurrier); focus on the camera's target
        public float DofAperture = 0f;
        public float DofFocalLength = 120f;
        float indoor;
        Ragnarok.Maps.RoRuntimeMapController mapController;

        VolumeProfile profile;
        Material glowMaterial;
        float nextLampScan;
        Tonemapping tonemapping;
        Bloom bloom;
        ColorAdjustments color;
        WhiteBalance whiteBalance;
        Vignette vignette;
        DepthOfField depthOfField;
        SplitToning splitToning;
        RoLookMotes motes;
        RoLookVolumetrics volumetrics;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            if (FindFirstObjectByType<RoLookRig>() != null) return;
            var go = new GameObject("RO Look Rig");
            DontDestroyOnLoad(go);
            go.AddComponent<RoLookRig>();
        }

        void Awake() {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RO Look";
            tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.3f;
            bloom.intensity.value = 0.2f;
            bloom.scatter.value = 0.62f;
            bloom.highQualityFiltering.value = true;
            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = -0.55f;
            color.contrast.value = 18f;
            color.saturation.value = 14f;
            whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.temperature.value = 0f;
            whiteBalance.tint.value = 0f;
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.16f;
            vignette.smoothness.value = 0.45f;

            depthOfField = profile.Add<DepthOfField>(true);
            depthOfField.mode.value = DepthOfFieldMode.Off;
            depthOfField.highQualitySampling.value = true;
            splitToning = profile.Add<SplitToning>(true);
            splitToning.shadows.value = new Color(0.5f, 0.5f, 0.5f);
            splitToning.highlights.value = new Color(0.5f, 0.5f, 0.5f);
            splitToning.balance.value = 0f;
            motes = gameObject.AddComponent<RoLookMotes>();
            volumetrics = gameObject.AddComponent<RoLookVolumetrics>();

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            PushGlobals();
            RoLookState.Tune = Tune;
            RoLookState.Describe = Describe;
        }

        void OnDestroy() {
            if (RoLookState.Tune == (Func<string, string>) Tune) RoLookState.Tune = null;
            if (profile != null) Destroy(profile);
        }

        float daylight = 1f;
        Ragnarok.Effects.RoDayNightCycleController dayNight;

        void PushGlobals() {
            Shader.SetGlobalVector(SkyId, Vector4.Lerp(NightSky, Sky, daylight));
            Shader.SetGlobalVector(GroundId, Ground);
            Shader.SetGlobalVector(SunId, Sun);
            Shader.SetGlobalVector(ParamsId, Params);
            Shader.SetGlobalVector(SpriteId, Sprite);
            Shader.SetGlobalVector(SpriteShapeId, SpriteShape);
            Shader.SetGlobalFloat(FxBoostId, FxBoost);
            Shader.SetGlobalVector(WaterId, Water);
            Shader.SetGlobalVector(ReliefId, Relief);
            Shader.SetGlobalVector(CloudShapeId, CloudShape);
            Shader.SetGlobalVector(CloudLookId, new Vector4(CloudStrength, 0f, 0f, 0f));
            Shader.SetGlobalVector(RimId, Rim);
            Shader.SetGlobalVector(WaterDeepId, WaterDeep);
            Shader.SetGlobalFloat(FxAlphaGammaId, QualitySettings.activeColorSpace == ColorSpace.Linear ? FxAlphaGamma : 1f);
            Shader.SetGlobalFloat(FxGammaBlendId, QualitySettings.activeColorSpace == ColorSpace.Linear ? FxGammaBlend : 0f);
            Shader.SetGlobalVector(IndoorId, new Vector4(indoor, IndoorSpriteAmbient, IndoorBakedAdd, IndoorAlbedoLight));
        }

        void Update() {
            if (Time.unscaledTime < nextLampScan) return;
            nextLampScan = Time.unscaledTime + 2f;
            if (glowMaterial == null) {
                Shader glowShader = Shader.Find("Ragnarok/RO Look Glow");
                if (glowShader == null) return;
                glowMaterial = new Material(glowShader) { name = "RO Look Glow" };
            }

            // every model lamp (lantern glass of the feature database) gets its glow and flicker
            foreach (Ragnarok.Maps.RoMapModelLampLightInfo info in FindObjectsByType<Ragnarok.Maps.RoMapModelLampLightInfo>(FindObjectsSortMode.None)) {
                RoLookLampGlow.Attach(info.GetComponent<Light>(), glowMaterial);
            }

            // map fires (torches): a stronger flicker, no lamp range / intensity rescale (made for this look)
            foreach (Ragnarok.Maps.RoMapFireLightInfo info in FindObjectsByType<Ragnarok.Maps.RoMapFireLightInfo>(FindObjectsSortMode.None)) {
                RoLookLampGlow.Attach(info.GetComponent<Light>(), glowMaterial, fire: true);
            }
        }

        Ragnarok.Camera.RoCameraController cameraController;

        void UpdateDepthOfField() {
            if (DofAperture <= 0f) {
                depthOfField.mode.value = DepthOfFieldMode.Off;
                return;
            }

            if (cameraController == null && Time.frameCount % 30 == 0) cameraController = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
            depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            depthOfField.focusDistance.value = cameraController != null ? cameraController.Distance : 70f;
            depthOfField.aperture.value = DofAperture;
            depthOfField.focalLength.value = DofFocalLength;
        }

        void LateUpdate() {
            UpdateDepthOfField();
            if (dayNight == null && Time.frameCount % 60 == 0) dayNight = FindFirstObjectByType<Ragnarok.Effects.RoDayNightCycleController>();
            if (mapController == null && Time.frameCount % 60 == 0) mapController = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
            float currentIndoor = ForceIndoor >= 0f ? ForceIndoor
                : mapController != null && mapController.CloudShadows != null && mapController.CloudShadows.IsSkylessMap ? 1f : 0f;
            if (currentIndoor != indoor) {
                indoor = currentIndoor;
                ApplyExposure();
                PushGlobals();
            }

            float currentDaylight = dayNight != null && dayNight.isActiveAndEnabled ? Mathf.Clamp01(dayNight.CurrentDaylightFactor) : 1f;
            if (Mathf.Abs(currentDaylight - daylight) > 0.002f) {
                daylight = currentDaylight;
                PushGlobals();
            }

            ApplyMapProfileIfChanged();
            // the ambient follows the map (RSW) and day / night, both of which set RenderSettings; a profile may override it
            Color ambient = Color.Lerp(RenderSettings.ambientLight, new Color(AmbientOverride.x, AmbientOverride.y, AmbientOverride.z), Mathf.Clamp01(AmbientOverride.w));
            Shader.SetGlobalColor(AmbientId, ambient);
            Light sun = RenderSettings.sun;
            if (sun != null && !Mathf.Approximately(sun.shadowStrength, ShadowStrength)) sun.shadowStrength = ShadowStrength;
        }

        string Tune(string args) {
            string[] p = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0) return Describe();
            float F(int i, float fallback) => p.Length > i && p[i] != "-" ? float.Parse(p[i], CultureInfo.InvariantCulture) : fallback;
            Vector4 V(Vector4 v) => new Vector4(F(1, v.x), F(2, v.y), F(3, v.z), F(4, v.w));
            switch (p[0]) {
                case "sky": Sky = V(Sky); break;
                case "nightsky": NightSky = V(NightSky); break;
                case "ground": Ground = V(Ground); break;
                case "sun": Sun = V(Sun); break;
                case "params": Params = V(Params); break;
                case "sprite": Sprite = V(Sprite); break;
                case "shape": SpriteShape = V(SpriteShape); break;
                case "water": Water = V(Water); break;
                case "relief": Relief = V(Relief); break;
                case "waterdeep": WaterDeep = V(WaterDeep); break;
                case "shadow": ShadowStrength = F(1, ShadowStrength); break;
                case "fx": FxBoost = F(1, FxBoost); FxAlphaGamma = F(2, FxAlphaGamma); break;
                case "fxblend": FxGammaBlend = F(1, FxGammaBlend); break;
                case "fire":
                    RoLookLampGlow.FireGlow = F(1, RoLookLampGlow.FireGlow);
                    RoLookLampGlow.FireIntensityScale = F(2, RoLookLampGlow.FireIntensityScale);
                    RoLookLampGlow.FireRangeScale = F(3, RoLookLampGlow.FireRangeScale);
                    RoLookLampGlow.FireFlicker = F(4, RoLookLampGlow.FireFlicker);
                    RoLookLampGlow.FireGlowRadius = F(5, RoLookLampGlow.FireGlowRadius);
                    break;
                case "ambient": AmbientOverride = V(AmbientOverride); break;
                case "sunmax": Ragnarok.Effects.RoDayNightCycleSettings.LookMaxElevationDegrees = F(1, 90f); break;
                // cloud shadows (RoCloudShadows.hlsl): clouds <size> <warp> <macro swing> <edge detail> (all 0 = the shader's
                // defaults), cloudstr <strength scale> (0 = the default)
                case "clouds": CloudShape = V(CloudShape); break;
                case "cloudstr": CloudStrength = F(1, CloudStrength); break;
                // models: rim <sky rim strength> <falloff power> <foliage wrap> (0 0 0 = the shader's defaults)
                case "rim": Rim = V(Rim); break;
                case "indoorflag": ForceIndoor = F(1, ForceIndoor); break;
                case "indoor": IndoorSpriteAmbient = F(1, IndoorSpriteAmbient); IndoorBakedAdd = F(2, IndoorBakedAdd); IndoorAlbedoLight = F(3, IndoorAlbedoLight); break;
                case "dof": DofAperture = F(1, DofAperture); DofFocalLength = F(2, DofFocalLength); break;
                case "split":
                    // split <shadow r g b> <highlight r g b> <balance -100..100>
                    splitToning.shadows.value = new Color(F(1, 0.5f), F(2, 0.5f), F(3, 0.5f));
                    splitToning.highlights.value = new Color(F(4, 0.5f), F(5, 0.5f), F(6, 0.5f));
                    splitToning.balance.value = F(7, 0f);
                    break;
                case "vol": volumetrics.Fog = V(volumetrics.Fog); break;
                case "volamb": volumetrics.Ambient = V(volumetrics.Ambient); break;
                case "volp": volumetrics.Params = V(volumetrics.Params); break;
                case "lightshadows": volumetrics.ShadowedLights = (int) F(1, volumetrics.ShadowedLights); volumetrics.ShadowStrength = F(2, volumetrics.ShadowStrength); break;
                case "motes": motes.Configure(F(1, motes.Rate), F(2, motes.Intensity), F(3, motes.Size)); break;
                case "preset": return "preset: " + ApplyPreset(p.Length > 1 ? p[1] : "default");
                case "glow":
                    RoLookLampGlow.GlowStrength = F(1, RoLookLampGlow.GlowStrength);
                    RoLookLampGlow.GlowRadius = F(2, RoLookLampGlow.GlowRadius);
                    RoLookLampGlow.Flicker = F(3, RoLookLampGlow.Flicker);
                    RoLookLampGlow.RangeScale = F(4, RoLookLampGlow.RangeScale);
                    RoLookLampGlow.IntensityScale = F(5, RoLookLampGlow.IntensityScale);
                    break;
                case "exposure": exposureBase = F(1, exposureBase); ApplyExposure(); break;
                case "indoorexposure": IndoorExposure = F(1, IndoorExposure); ApplyExposure(); break;
                case "contrast": color.contrast.value = F(1, color.contrast.value); break;
                case "saturation": color.saturation.value = F(1, color.saturation.value); break;
                case "temp": whiteBalance.temperature.value = F(1, whiteBalance.temperature.value); whiteBalance.tint.value = F(2, whiteBalance.tint.value); break;
                case "vignette": vignette.intensity.value = F(1, vignette.intensity.value); break;
                case "bloom":
                    bloom.intensity.value = F(1, bloom.intensity.value);
                    bloom.threshold.value = F(2, bloom.threshold.value);
                    bloom.scatter.value = F(3, bloom.scatter.value);
                    break;
                case "tonemap":
                    tonemapping.mode.value = p.Length > 1 && p[1] == "aces" ? TonemappingMode.ACES : p.Length > 1 && p[1] == "none" ? TonemappingMode.None : TonemappingMode.Neutral;
                    break;
                case "post":
                    // post on|off: the whole volume (A/B of the grading)
                    GetComponent<Volume>().weight = p.Length > 1 && p[1] == "off" ? 0f : 1f;
                    break;
                case "ssao":
                    return "ssao: " + TuneSsao(p);
                default:
                    return "lookset: unknown '" + p[0] + "' (sky|nightsky|ground|sun|params|sprite|shape x y z w, shadow v, fx v, glow strength radius flicker rangeScale intensityScale, exposure|contrast|saturation|vignette v, temp t tint, bloom i threshold scatter, tonemap neutral|aces|none, post on|off, ssao on|off|intensity|radius v)";
            }

            PushGlobals();
            return Describe();
        }

        static string TuneSsao(string[] p) {
            ScriptableRendererFeature feature = FindRendererFeature("ScreenSpaceAmbientOcclusion");
            if (feature == null) return "no SSAO feature";
            if (p.Length > 1 && (p[1] == "on" || p[1] == "off")) feature.SetActive(p[1] == "on");
            object settings = feature.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(feature);
            if (settings != null && p.Length > 2) {
                string field = p[1] == "intensity" ? "Intensity" : p[1] == "radius" ? "Radius" : p[1] == "falloff" ? "Falloff" : p[1] == "direct" ? "DirectLightingStrength" : null;
                FieldInfo info = field != null ? settings.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                info?.SetValue(settings, float.Parse(p[2], CultureInfo.InvariantCulture));
                feature.Create();
            }

            var text = new StringBuilder(feature.isActive ? "on" : "off");
            if (settings != null) {
                foreach (string name in new[] { "Intensity", "Radius", "Falloff", "DirectLightingStrength", "Source", "Downsample", "AfterOpaque" }) {
                    FieldInfo info = settings.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (info != null) text.Append(' ').Append(name).Append('=').Append(info.GetValue(settings));
                }
            }

            return text.ToString();
        }

        static ScriptableRendererFeature FindRendererFeature(string typeName) {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset)) return null;
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(asset) as ScriptableRendererData[];
            if (list == null) return null;
            foreach (ScriptableRendererData data in list) {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures) {
                    if (feature != null && feature.GetType().Name == typeName) return feature;
                }
            }

            return null;
        }

        /// <summary>
        /// Every map has its own look (a cave is not a town square): on a map change the profile
        /// look-presets/maps/&lt;map&gt;.txt is applied, a map without one gets look-presets/default.txt.
        /// </summary>
        void ApplyMapProfileIfChanged() {
            if (Time.frameCount % 15 != 0) return;
            if (mapController == null) mapController = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
            string map = mapController != null ? mapController.MapName : null;
            if (string.IsNullOrEmpty(map) || map == profileMap) return;
            profileMap = map;
            string name = System.IO.Path.GetFileNameWithoutExtension(map).ToLowerInvariant();
            string mapProfile = System.IO.Path.Combine("maps", name);
            ActiveProfile = PresetExists(mapProfile) ? mapProfile : PresetExists("default") ? "default" : "built-in";
            if (ActiveProfile != "built-in") {
                AmbientOverride.w = 0f;
                ApplyPreset(ActiveProfile);
            }

            Debug.Log("[RoLook] map " + name + ": profile " + ActiveProfile);
        }

        static bool PresetExists(string name) {
            return ResolvePreset(name) != null;
        }

        /// <summary>
        /// Where a preset lives: Temp/look-presets/&lt;name&gt;.txt next to the build's / project's Assets first (live tuning;
        /// Unity wipes the project's Temp when the Editor closes), then the shipped StreamingAssets/Ragnarok/look-presets
        /// (in every build, so the map profiles apply from the first start).
        /// </summary>
        static string ResolvePreset(string name) {
            string file = name + ".txt";
            string temp = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "look-presets", file));
            if (System.IO.File.Exists(temp)) return temp;
            string shipped = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.streamingAssetsPath, "Ragnarok", "look-presets", file));
            return System.IO.File.Exists(shipped) ? shipped : null;
        }

        /// <summary>
        /// A look variant: every line of the preset (<see cref="ResolvePreset"/>) is one !lookset command (a preset lists
        /// every value it relies on, so switching variants never leaves one behind).
        /// </summary>
        void ApplyExposure() {
            if (color != null) color.postExposure.value = exposureBase + indoor * IndoorExposure;
        }

        string ApplyPreset(string name) {
            string path = ResolvePreset(name);
            if (path == null) return "no preset " + name;
            int applied = 0;
            foreach (string raw in System.IO.File.ReadAllLines(path)) {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                Tune(line);
                applied++;
            }

            return name + ": " + applied + " lines | " + Describe();
        }

        static string DescribeCamera() {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return "camera none";
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            VolumeStack stack = VolumeManager.instance.stack;
            var ca = stack.GetComponent<ColorAdjustments>();
            var tm = stack.GetComponent<Tonemapping>();
            var st = stack.GetComponent<SplitToning>();
            var bl = stack.GetComponent<Bloom>();
            var vg = stack.GetComponent<Vignette>();
            int volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None).Length;
            return "camera " + cam.name + "#" + cam.GetInstanceID() + " hdr=" + cam.allowHDR + " post=" + (data != null && data.renderPostProcessing) +
                   " volMask=" + (data != null ? data.volumeLayerMask.value : -1) + " volumes=" + volumes +
                   " stack: exp=" + ca.postExposure.value.ToString("0.##", CultureInfo.InvariantCulture) + " con=" + ca.contrast.value.ToString("0", CultureInfo.InvariantCulture) +
                   " sat=" + ca.saturation.value.ToString("0", CultureInfo.InvariantCulture) + " filter=" + ca.colorFilter.value + " tm=" + tm.mode.value +
                   " split=" + st.shadows.value + "/" + st.highlights.value + "/" + st.balance.value.ToString("0", CultureInfo.InvariantCulture) +
                   " bloom=" + bl.intensity.value.ToString("0.##", CultureInfo.InvariantCulture) + " vig=" + vg.intensity.value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        string Describe() {
            string Vec(Vector4 v) => v.x.ToString("0.###", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                                     v.z.ToString("0.###", CultureInfo.InvariantCulture) + "," + v.w.ToString("0.###", CultureInfo.InvariantCulture);
            return "profile " + ActiveProfile + " | ambient " + Vec(AmbientOverride) + " | sky " + Vec(Sky) + " nightsky " + Vec(NightSky) + " daylight " + daylight.ToString("0.##", CultureInfo.InvariantCulture) + " ground " + Vec(Ground) + " sun " + Vec(Sun) + " params " + Vec(Params) + " sprite " + Vec(Sprite) + " shape " + Vec(SpriteShape) + " water " + Vec(Water) + " waterdeep " + Vec(WaterDeep) + " shadow " + ShadowStrength.ToString("0.##", CultureInfo.InvariantCulture) + " fx " + FxBoost.ToString("0.##", CultureInfo.InvariantCulture) + " indoor " + indoor.ToString("0", CultureInfo.InvariantCulture) + (ForceIndoor >= 0f ? "(forced)" : "") + "/" + IndoorSpriteAmbient.ToString("0.##", CultureInfo.InvariantCulture) +
                   " glow " + RoLookLampGlow.GlowStrength.ToString("0.##", CultureInfo.InvariantCulture) + "/" + RoLookLampGlow.GlowRadius.ToString("0.##", CultureInfo.InvariantCulture) +
                   "/" + RoLookLampGlow.Flicker.ToString("0.##", CultureInfo.InvariantCulture) +
                   " lamps " + RoLookLampGlow.RangeScale.ToString("0.##", CultureInfo.InvariantCulture) + "x" + RoLookLampGlow.IntensityScale.ToString("0.##", CultureInfo.InvariantCulture) +
                   " indoor-albedo " + IndoorAlbedoLight.ToString("0.##", CultureInfo.InvariantCulture) + " dof " + DofAperture.ToString("0.##", CultureInfo.InvariantCulture) +
                   " vol " + Vec(volumetrics.Fog) + " volamb " + Vec(volumetrics.Ambient) + " volp " + Vec(volumetrics.Params) +
                   " | tonemap " + tonemapping.mode.value + " exposure " + color.postExposure.value.ToString("0.##", CultureInfo.InvariantCulture) +
                   " contrast " + color.contrast.value.ToString("0", CultureInfo.InvariantCulture) + " saturation " + color.saturation.value.ToString("0", CultureInfo.InvariantCulture) +
                   " temp " + whiteBalance.temperature.value.ToString("0", CultureInfo.InvariantCulture) + " bloom " + bloom.intensity.value.ToString("0.##", CultureInfo.InvariantCulture) +
                   "/" + bloom.threshold.value.ToString("0.##", CultureInfo.InvariantCulture) + " vignette " + vignette.intensity.value.ToString("0.##", CultureInfo.InvariantCulture) +
                   " | ssao " + TuneSsao(new string[0]) + " | " + DescribeCamera();
        }
    }
}
