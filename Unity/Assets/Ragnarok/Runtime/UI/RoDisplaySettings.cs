using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Frame pacing of this client, an addition with no counterpart in the original (which paces itself near 60 fps):
    /// VSync, a frame limit while VSync is off, and the refresh rate of full screen ("Desktop" = the borderless desktop
    /// mode at the desktop's rate, a rate = exclusive full screen at it). Kept in PlayerPrefs and applied at start, so the
    /// client no longer renders as many frames as the GPU allows. The Game Settings window's Graphics tab edits it.
    /// </summary>
    public static class RoDisplaySettings {
        /// <summary>The Frame Limit choices in fps; 0 = unlimited.</summary>
        public static readonly int[] FrameLimits = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };

        public const int DefaultFrameLimit = 60;
        private const string Prefs = "ro.settings.display.";

        private static bool loaded;
        private static bool vSync = true;
        private static int frameLimit = DefaultFrameLimit;
        private static int refreshRate;

        /// <summary>While a performance capture runs it owns VSync and the frame rate.</summary>
        public static bool PerfOverride { get; set; }

        public static bool VSync { get { Load(); return vSync; } }

        /// <summary>The fps limit while VSync is off; 0 = unlimited.</summary>
        public static int FrameLimit { get { Load(); return frameLimit; } }

        /// <summary>The full screen refresh rate in Hz; 0 = the desktop's (borderless).</summary>
        public static int RefreshRate { get { Load(); return refreshRate; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            loaded = false;
            vSync = true;
            frameLimit = DefaultFrameLimit;
            refreshRate = 0;
            PerfOverride = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAtStart() {
            Apply();
            if (refreshRate > 0 && Screen.fullScreen) {
                Screen.SetResolution(Screen.width, Screen.height, FullScreenMode.ExclusiveFullScreen, ToRefreshRate(refreshRate));
            }
        }

        public static void Set(bool vSyncOn, int limit, int hz) {
            Load();
            vSync = vSyncOn;
            frameLimit = Math.Max(0, limit);
            refreshRate = Math.Max(0, hz);
            try {
                PlayerPrefs.SetInt(Prefs + "vsync", vSync ? 1 : 0);
                PlayerPrefs.SetInt(Prefs + "frameLimit", frameLimit);
                PlayerPrefs.SetInt(Prefs + "refreshRate", refreshRate);
                PlayerPrefs.Save();
            } catch (UnityException) {
                // outside the main thread / before the engine is up: the values still apply for this run
            }

            Apply();
        }

        /// <summary>VSync on: one frame per refresh, no limit of its own. Off: the frame limit (or none).</summary>
        public static void Apply() {
            Load();
            if (PerfOverride) return;
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            Application.targetFrameRate = vSync || frameLimit <= 0 ? -1 : frameLimit;
        }

        /// <summary>The refresh rates of the display's modes (rounded Hz, highest first), for the Refresh Rate list.</summary>
        public static List<int> AvailableRefreshRates() {
            var rates = new List<int>();
            foreach (Resolution r in Screen.resolutions) {
                int hz = (int) Math.Round(r.refreshRateRatio.value);
                if (hz > 0 && !rates.Contains(hz)) rates.Add(hz);
            }

            rates.Sort((a, b) => b.CompareTo(a));
            return rates;
        }

        public static RefreshRate ToRefreshRate(int hz) => new RefreshRate { numerator = (uint) Math.Max(1, hz), denominator = 1 };

        private static void Load() {
            if (loaded) return;
            try {
                vSync = PlayerPrefs.GetInt(Prefs + "vsync", 1) != 0;
                frameLimit = Math.Max(0, PlayerPrefs.GetInt(Prefs + "frameLimit", DefaultFrameLimit));
                refreshRate = Math.Max(0, PlayerPrefs.GetInt(Prefs + "refreshRate", 0));
                loaded = true;
            } catch (UnityException) {
                // PlayerPrefs is not available yet (a static initializer): the defaults stand, the next use loads
            }
        }
    }
}
