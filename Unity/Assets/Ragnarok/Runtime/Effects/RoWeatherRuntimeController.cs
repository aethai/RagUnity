using UnityEngine;
using Ragnarok.Audio;
using Ragnarok.Maps;

namespace Ragnarok.Effects {
    [DisallowMultipleComponent]
    public sealed class RoWeatherRuntimeController : MonoBehaviour {
        private const float RuntimeSyncIntervalSeconds = 0.25f;

        [Header("Wind")]
        [SerializeField] private RoWeatherWindSettings wind = new RoWeatherWindSettings();

        [Header("Rain")]
        [SerializeField] private RoRainWeatherSettings rain = new RoRainWeatherSettings();

        [Header("Day/Night")]
        [SerializeField] private RoDayNightCycleSettings dayNight = new RoDayNightCycleSettings();

        [Header("Cloud Shadows")]
        [SerializeField] private RoCloudShadowSettings cloudShadows = new RoCloudShadowSettings();

        [Header("Ambient Particles")]
        [SerializeField] private RoAmbientParticleSettings ambientParticles = new RoAmbientParticleSettings();

        private RoEffectManager effectManager;
        private GameObject groundRoot;
        private GameObject modelRoot;
        private UnityEngine.Camera renderCamera;
        private RoAudioSettings audioSettings;
        private RoRainWeatherController rainController;
        private RoDayNightCycleController dayNightController;
        private RoCloudShadowController cloudShadowController;
        private RoAmbientParticleController ambientParticleController;
        private bool allowDayNightRuntime;
        private bool isSkylessMap;
        private bool initialized;
        private float nextScheduledSyncTime;

        public RoRainWeatherSettings RainSettings {
            get {
                rain ??= new RoRainWeatherSettings();
#if UNITY_EDITOR
                rain.EnsureEditorDefaults();
#else
                rain.EnsureCurrentDefaults();
#endif
                return rain;
            }
        }
        public RoWeatherWindSettings WindSettings {
            get {
                wind ??= new RoWeatherWindSettings();
                wind.EnsureCurrentDefaults();
                return wind;
            }
        }
        public RoDayNightCycleSettings DayNightSettings {
            get {
                dayNight ??= new RoDayNightCycleSettings();
                dayNight.EnsureCurrentDefaults();
                return dayNight;
            }
        }
        public RoCloudShadowSettings CloudShadowSettings {
            get {
                cloudShadows ??= new RoCloudShadowSettings();
                cloudShadows.EnsureCurrentDefaults();
                return cloudShadows;
            }
        }
        public RoAmbientParticleSettings AmbientParticleSettings {
            get {
                ambientParticles ??= new RoAmbientParticleSettings();
                ambientParticles.EnsureCurrentDefaults();
                return ambientParticles;
            }
        }
        public Vector3 WindDirectionWorld {
            get {
                Vector2 direction = WindSettings.EffectiveDirection;
                return new Vector3(direction.x, 0f, direction.y);
            }
        }
        public RoRainWeatherController RainRuntime => rainController;
        public RoDayNightCycleController DayNightRuntime => dayNightController;
        public RoCloudShadowController CloudShadowRuntime => cloudShadowController;
        public RoAmbientParticleController AmbientParticleRuntime => ambientParticleController;
        public bool AllowsDayNightRuntime => allowDayNightRuntime;
        public bool IsSkylessMap => isSkylessMap;

        private void OnValidate() {
            wind ??= new RoWeatherWindSettings();
            wind.EnsureCurrentDefaults();

            rain ??= new RoRainWeatherSettings();
#if UNITY_EDITOR
            rain.EnsureEditorDefaults();
#else
            rain.EnsureCurrentDefaults();
#endif

            dayNight ??= new RoDayNightCycleSettings();
            dayNight.EnsureCurrentDefaults();

            cloudShadows ??= new RoCloudShadowSettings();
            cloudShadows.EnsureCurrentDefaults();

            ambientParticles ??= new RoAmbientParticleSettings();
            ambientParticles.EnsureCurrentDefaults();
        }

        private void LateUpdate() {
            if (!ShouldRunScheduledSync()) {
                return;
            }

            SyncRuntime();
        }

        private void OnDisable() {
            rainController?.StopRain(immediate: true);
            dayNightController?.StopCycle(restoreLighting: true);
            RoWeatherWindSettings.ResetGlobals();
            RoCloudShadowController.ResetGlobals();
            RoWindSwayGlobals.ResetEnvironmentScale();
            ambientParticleController?.StopAmbient();
        }

        public void Initialize(
            RoEffectManager manager,
            GameObject ground,
            GameObject models,
            UnityEngine.Camera camera,
            RoAudioSettings audio = null,
            bool allowDayNight = false,
            bool skylessMap = false
        ) {
            effectManager = manager;
            groundRoot = ground;
            modelRoot = models;
            renderCamera = RoEffectCameraCache.Resolve(camera);
            audioSettings = audio;
            allowDayNightRuntime = allowDayNight;
            isSkylessMap = skylessMap;
            initialized = true;

            RainSettings.TryApplyLegacyWindSettings(WindSettings);
            WindSettings.ApplyGlobals();
            ApplyWindSwayEnvironmentScale();
            EnsureRainController();
            rainController.Initialize(effectManager, groundRoot, modelRoot, renderCamera, RainSettings, audioSettings, WindSettings);
            EnsureDayNightController();
            dayNightController.Initialize(DayNightSettings, groundRoot, modelRoot, allowDayNightRuntime);
            EnsureCloudShadowController();
            cloudShadowController.Initialize(CloudShadowSettings, isSkylessMap, ResolveCloudShadowWindDirectionDegrees());
            EnsureAmbientParticleController();
            ambientParticleController.Initialize(effectManager, modelRoot, renderCamera, AmbientParticleSettings, isSkylessMap, WindSettings);
            SyncRuntime();
        }

        public void SetWindSettingsReference(RoWeatherWindSettings settings) {
            if (settings == null) {
                return;
            }

            wind = settings;
            wind.EnsureCurrentDefaults();
        }

        public void SetWindDirectionFromWorld(Vector3 directionWorld) {
            Vector3 flattened = new Vector3(directionWorld.x, 0f, directionWorld.z);
            if (flattened.sqrMagnitude <= 0.0001f) {
                return;
            }

            Vector3 normalized = flattened.normalized;
            WindSettings.direction = new Vector2(normalized.x, normalized.z);
            WindSettings.EnsureCurrentDefaults();
            WindSettings.ApplyGlobals();
        }

        public void SetRainSettingsReference(RoRainWeatherSettings settings) {
            if (settings == null) {
                return;
            }

            rain = settings;
#if UNITY_EDITOR
            rain.EnsureEditorDefaults();
#else
            rain.EnsureCurrentDefaults();
#endif
        }

        public void SetDayNightSettingsReference(RoDayNightCycleSettings settings) {
            if (settings == null) {
                return;
            }

            dayNight = settings;
            dayNight.EnsureCurrentDefaults();
        }

        public void SetCloudShadowSettingsReference(RoCloudShadowSettings settings) {
            if (settings == null) {
                return;
            }

            cloudShadows = settings;
            cloudShadows.EnsureCurrentDefaults();
        }

        public void SetAmbientParticleSettingsReference(RoAmbientParticleSettings settings) {
            if (settings == null) {
                return;
            }

            ambientParticles = settings;
            ambientParticles.EnsureCurrentDefaults();
        }

        public void SetDayNightRuntimeAllowed(bool allowed) {
            allowDayNightRuntime = allowed;
            if (dayNightController != null) {
                dayNightController.SyncSettings(DayNightSettings, groundRoot, modelRoot, allowDayNightRuntime);
            }
        }

        public void StartDayNightCycle(bool reset = false) {
            DayNightSettings.enabled = true;
            EnsureDayNightController();
            dayNightController.SyncSettings(DayNightSettings, groundRoot, modelRoot, allowDayNightRuntime);
            dayNightController.StartCycle(reset);
        }

        public void ResetDayNightCycle() {
            EnsureDayNightController();
            dayNightController.ResetCycle();
        }

        public void StopDayNightCycle() {
            DayNightSettings.enabled = false;
            dayNightController?.StopCycle(restoreLighting: true);
        }

        /// <summary>
        /// Drives the day/night cycle from the server clock: configures the day/night window lengths,
        /// keeps the runtime enabled (so the periodic sync does not stop it) and feeds the current
        /// position inside the cycle. <paramref name="cycleSeconds"/> is a position in [0, total cycle).
        /// </summary>
        public void ApplyServerDrivenDayNight(
            float cycleSeconds,
            float dayMinutes,
            float nightMinutes,
            float transitionFraction
        ) {
            RoDayNightCycleSettings settings = DayNightSettings;
            settings.dayMinutes = Mathf.Max(0.1f, dayMinutes);
            settings.nightMinutes = Mathf.Max(0.1f, nightMinutes);
            settings.nightTransitionFraction = Mathf.Clamp(transitionFraction, 0.001f, 0.49f);
            settings.enabled = true;
            allowDayNightRuntime = true;
            EnsureDayNightController();
            dayNightController.SetServerDrivenCycleSeconds(cycleSeconds);
        }

        public void ClearServerDrivenDayNight(bool restoreLighting = true) {
            dayNightController?.ClearServerDriven(restoreLighting);
        }

        public void SyncRuntime(
            UnityEngine.Camera camera = null,
            RoAudioSettings audio = null,
            bool? allowDayNight = null,
            bool? skylessMap = null
        ) {
            if (!initialized) {
                return;
            }

            if (Application.isPlaying) {
                nextScheduledSyncTime = Time.unscaledTime + RuntimeSyncIntervalSeconds;
            }

            if (allowDayNight.HasValue) {
                allowDayNightRuntime = allowDayNight.Value;
            }

            if (skylessMap.HasValue) {
                isSkylessMap = skylessMap.Value;
            }

            if (audio != null) {
                audioSettings = audio;
            }

            renderCamera = RoEffectCameraCache.Resolve(camera != null ? camera : renderCamera);

            EnsureRainController();
            RainSettings.TryApplyLegacyWindSettings(WindSettings);
            WindSettings.ApplyGlobals();
            ApplyWindSwayEnvironmentScale();
            rainController.SyncSettings(RainSettings, renderCamera, audioSettings, WindSettings);
            EnsureDayNightController();
            dayNightController.SyncSettings(DayNightSettings, groundRoot, modelRoot, allowDayNightRuntime);
            EnsureCloudShadowController();
            cloudShadowController.SetDaylightFactor(dayNightController != null ? dayNightController.CurrentDaylightFactor : 1f);
            cloudShadowController.SetOvercastFactor(rainController != null ? rainController.CurrentIntensity : 0f);
            cloudShadowController.SyncSettings(CloudShadowSettings, isSkylessMap, ResolveCloudShadowWindDirectionDegrees());
            EnsureAmbientParticleController();
            ambientParticleController.SyncSettings(AmbientParticleSettings, modelRoot, renderCamera, isSkylessMap, WindSettings);
        }

        private bool ShouldRunScheduledSync() {
            if (!initialized) {
                return false;
            }

            if (!Application.isPlaying) {
                return true;
            }

            float now = Time.unscaledTime;
            if (now < nextScheduledSyncTime) {
                return false;
            }

            nextScheduledSyncTime = now + RuntimeSyncIntervalSeconds;
            return true;
        }

        public void ClearRuntime() {
            initialized = false;
            if (rainController != null) {
                rainController.StopRain(immediate: true);
            }

            if (dayNightController != null) {
                dayNightController.ClearRuntime();
            }

            if (cloudShadowController != null) {
                cloudShadowController.ClearRuntime();
            }

            if (ambientParticleController != null) {
                ambientParticleController.ClearRuntime();
            }

            RoWeatherWindSettings.ResetGlobals();
            RoCloudShadowController.ResetGlobals();
            RoWindSwayGlobals.ResetEnvironmentScale();
        }

        private void ApplyWindSwayEnvironmentScale() {
            RoWindSwayGlobals.ApplyEnvironmentScale(
                isSkylessMap
                    ? RoWindSwayGlobals.SkylessEnvironmentScale
                    : RoWindSwayGlobals.DefaultEnvironmentScale);
        }

        private void EnsureRainController() {
            if (rainController != null) {
                return;
            }

            Transform existing = transform.Find("Rain");
            GameObject rainObject = existing != null ? existing.gameObject : new GameObject("Rain");
            rainObject.transform.SetParent(transform, false);
            rainController = rainObject.GetComponent<RoRainWeatherController>();
            if (rainController == null) {
                rainController = rainObject.AddComponent<RoRainWeatherController>();
            }
        }

        private void EnsureDayNightController() {
            if (dayNightController != null) {
                return;
            }

            Transform existing = transform.Find("Day Night");
            GameObject dayNightObject = existing != null ? existing.gameObject : new GameObject("Day Night");
            dayNightObject.transform.SetParent(transform, false);
            dayNightController = dayNightObject.GetComponent<RoDayNightCycleController>();
            if (dayNightController == null) {
                dayNightController = dayNightObject.AddComponent<RoDayNightCycleController>();
            }
        }

        private void EnsureCloudShadowController() {
            if (cloudShadowController != null) {
                return;
            }

            Transform existing = transform.Find("Cloud Shadows");
            GameObject cloudObject = existing != null ? existing.gameObject : new GameObject("Cloud Shadows");
            cloudObject.transform.SetParent(transform, false);
            cloudShadowController = cloudObject.GetComponent<RoCloudShadowController>();
            if (cloudShadowController == null) {
                cloudShadowController = cloudObject.AddComponent<RoCloudShadowController>();
            }
        }

        private void EnsureAmbientParticleController() {
            if (ambientParticleController != null) {
                return;
            }

            Transform existing = transform.Find("Ambient Particles");
            GameObject ambientObject = existing != null ? existing.gameObject : new GameObject("Ambient Particles");
            ambientObject.transform.SetParent(transform, false);
            ambientParticleController = ambientObject.GetComponent<RoAmbientParticleController>();
            if (ambientParticleController == null) {
                ambientParticleController = ambientObject.AddComponent<RoAmbientParticleController>();
            }
        }

        private float? ResolveCloudShadowWindDirectionDegrees() {
            Vector2 direction = WindSettings.EffectiveDirection;
            if (direction.sqrMagnitude <= 0.0001f) {
                return null;
            }

            return RoCloudShadowMath.ResolveWindDirectionDegrees(direction);
        }
    }
}
