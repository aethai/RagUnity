using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Look captures for the visual work: the same places, camera and time of day every run, so a rendering change is
    /// judged on before / after pairs. !look &lt;tag&gt; [only] walks the shot list (Temp/look-shots.txt next to the build
    /// when present, else <see cref="DefaultLookShots"/>) and writes PerformanceCaptures/look/&lt;tag&gt;/&lt;shot&gt;.png.
    /// </summary>
    public sealed partial class RoSkillSweepRunner {
        // name map x y yaw pitch distance daytime (hours into the cycle: day 0..14, night 14..22; "-" = server clock)
        const string DefaultLookShots =
            "prt_fountain prontera 156 185 0 50 75 7\n" +
            "prt_fountain_night prontera 156 185 0 50 75 18\n" +
            "prt_street prontera 156 120 45 40 60 9\n" +
            "payon payon 162 233 0 50 75 8\n" +
            "geffen geffen 119 59 0 50 75 12.5\n" +
            "alberta_port alberta 192 147 315 45 75 6\n" +
            "comodo_night comodo 209 143 0 50 75 19\n" +
            "field_grass prt_fild08 170 375 0 50 75 10\n" +
            "lutie xmas 147 134 0 50 75 7\n" +
            "juno yuno 157 51 0 50 75 7\n" +
            "morroc morocc 156 93 0 50 75 4\n" +
            "dungeon pay_dun00 21 183 0 50 60 -\n" +
            "close_detail prontera 156 185 30 35 35 3\n";

        const float LookSettleSeconds = 8f;
        // every look shot at this size whatever the window (the Editor's game view, a player window): pairs compare 1:1
        const int LookShotWidth = 1600, LookShotHeight = 900;

        /// <summary>
        /// The main camera rendered once into a width x height target (post-processing included, no UI) and written
        /// as a lossless PNG.
        /// </summary>
        IEnumerator CaptureLookShot(string path, int width, int height) {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) {
                ScreenCapture.CaptureScreenshot(path);
                yield return null;
                yield break;
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = target;
            // two frames: the first renders into the new target with its new aspect, the second is read back
            yield return null;
            yield return new WaitForEndOfFrame();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply(false);
            RenderTexture.active = active;
            cam.targetTexture = previous;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Destroy(pixels);
            target.Release();
            Destroy(target);
        }

        bool TryLookCommand(string line) {
            if (line.StartsWith("!look ", StringComparison.Ordinal)) {
                // !look <tag> [shot,shot,...]: the look capture set
                string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                StartCoroutine(LookCaptures(p[1], p.Length > 2 ? new HashSet<string>(p[2].Split(',')) : null));
                return true;
            }

            if (line.StartsWith("!cam ", StringComparison.Ordinal)) {
                // !cam <yaw> [pitch] [distance]: the camera at once, without easing
                string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Log("cam: " + SetLookCamera(ParseLook(p, 1, float.NaN), ParseLook(p, 2, float.NaN), ParseLook(p, 3, float.NaN)));
                return true;
            }

            if (line.StartsWith("!daytime", StringComparison.Ordinal)) {
                // !daytime <hours into the cycle>|off: a client-side time of day (day 0..14 h, night 14..22 h)
                string arg = line.Length > 9 ? line.Substring(9).Trim() : "off";
                Log("daytime: " + SetLookDaytime(arg));
                return true;
            }

            if (line.StartsWith("!lookset", StringComparison.Ordinal)) {
                // !lookset [key values]: live tuning of the enhanced look (RoLookRig): lighting globals, post, SSAO
                Func<string, string> tune = Ragnarok.Rendering.RoLookState.Tune;
                Log("lookset: " + (tune != null ? tune(line.Length > 9 ? line.Substring(9).Trim() : string.Empty) : "no look rig"));
                return true;
            }

            if (line.StartsWith("!lookdump", StringComparison.Ordinal)) {
                // !lookdump: what the frame is lit with right now (scene light, look globals, the ground material)
                Log("lookdump: " + DescribeLookState());
                return true;
            }

            if (line.StartsWith("!hdexport", StringComparison.Ordinal)) {
                // !hdexport: the current map's ground and model textures as PNGs + manifest-src.tsv for the upscaler
                Log("hdexport: " + ExportHdSources());
                return true;
            }

            if (line.StartsWith("!hd ", StringComparison.Ordinal)) {
                // !hd on|off|reload: the HD texture pack (takes effect on the next map load)
                string arg = line.Substring(4).Trim();
                if (arg == "on" || arg == "off") Ragnarok.Assets.RoHdTextures.Enabled = arg == "on";
                Ragnarok.Assets.RoHdTextures.Reload();
                // the atlases and prepared model textures are cached across maps: rebuilt with the new state on the next load
                Ragnarok.Assets.RoHdTextures.CachesStale = true;
                Log("hd: " + (Ragnarok.Assets.RoHdTextures.Enabled ? "on" : "off") + ", " + Ragnarok.Assets.RoHdTextures.Count + " textures in " + Ragnarok.Assets.RoHdTextures.Root);
                return true;
            }

            if (line.StartsWith("!hud ", StringComparison.Ordinal)) {
                // !hud on|off: the classic UI and the UGUI canvases, for clean world captures
                SetLookHud(line.EndsWith("on", StringComparison.Ordinal));
                Log("hud: " + (Ragnarok.UI.Classic.RoClassicUiRenderer.HiddenForCaptures ? "off" : "on"));
                return true;
            }

            return false;
        }

        static string DescribeLookState() {
            var text = new System.Text.StringBuilder();
            text.Append("ambient=").Append(RenderSettings.ambientLight).Append(" mode=").Append(RenderSettings.ambientMode);
            Light sun = RenderSettings.sun;
            text.Append(" sun=").Append(sun != null ? sun.name + " col=" + sun.color + " int=" + sun.intensity.ToString("F2", CultureInfo.InvariantCulture) +
                " rot=" + sun.transform.eulerAngles + " enabled=" + sun.enabled + " shadows=" + sun.shadows : "none");
            foreach (string g in new[] { "_RoLookAmbient", "_RoLookSky", "_RoLookSun", "_RoLookParams", "_RoLookIndoor", "_RoLightAmbient", "_RoLightDiffuse", "_RoFogColor", "_RoFogParams" }) {
                text.Append("\n  ").Append(g).Append('=').Append(Shader.GetGlobalVector(g));
            }

            int pointLights = 0, shadowed = 0;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (l.type != LightType.Point) continue;
                pointLights++;
                if (l.shadows != LightShadows.None) shadowed++;
            }

            text.Append("\n  point lights=").Append(pointLights).Append(" shadowed=").Append(shadowed);
            var groundMaterials = new HashSet<Material>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                Material m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.name.Contains("Ground") || !groundMaterials.Add(m)) continue;
                Texture lm = m.HasProperty("_Lightmap") ? m.GetTexture("_Lightmap") : null;
                Texture tm = m.HasProperty("_Tintmap") ? m.GetTexture("_Tintmap") : null;
                Texture bm = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                text.Append("\n  ground ").Append(r.name).Append(" mat=").Append(m.name).Append(" shader=").Append(m.shader.name)
                    .Append(" enabled=").Append(r.enabled).Append(" useLightmap=").Append(m.GetFloat("_UseLightmap"))
                    .Append(" useTint=").Append(m.GetFloat("_UseTint")).Append(" useLighting=").Append(m.HasProperty("_RoUseLighting") ? m.GetFloat("_RoUseLighting") : -1)
                    .Append(" lightmap=").Append(lm != null ? lm.name + " " + lm.width + "x" + lm.height : "null")
                    .Append(" tint=").Append(tm != null ? tm.name + " " + tm.width + "x" + tm.height : "null")
                    .Append(" base=").Append(bm != null ? bm.name + " " + bm.width + "x" + bm.height : "null");
            }

            return text.ToString();
        }

        static string ExportHdSources() {
            var map = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
            Ragnarok.Assets.RoAssetDatabase assets = map != null && map.Content != null ? map.Content.Assets : null;
            if (map == null || assets == null) return "no map";
            var lines = new List<string>();
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (map.CurrentGroundTextures != null) {
                foreach (string name in map.CurrentGroundTextures) {
                    // GND names are asset paths already (the atlas loads them as they are)
                    string path = name;
                    if (!done.Add(path)) continue;
                    string entry = Ragnarok.Assets.RoHdTextures.Export("ground", path, assets.LoadTexture(path));
                    if (entry != null) lines.Add(entry);
                }
            }

            foreach (Ragnarok.Maps.RoModelSurfaceInfo surface in FindObjectsByType<Ragnarok.Maps.RoModelSurfaceInfo>(FindObjectsSortMode.None)) {
                string path = "data/texture/" + surface.TextureName;
                if (string.IsNullOrEmpty(surface.TextureName) || done.Contains(path)) continue;
                var renderer = surface.GetComponent<Renderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Texture2D texture = material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") as Texture2D : null;
                if (texture == null) continue;
                done.Add(path);
                string entry = Ragnarok.Assets.RoHdTextures.Export("model", path, texture);
                if (entry != null) lines.Add(entry);
            }

            string manifest = Path.Combine(Ragnarok.Assets.RoHdTextures.Root, "manifest-src.tsv");
            File.WriteAllLines(manifest, lines);
            return lines.Count + " textures -> " + manifest;
        }

        static float ParseLook(string[] p, int index, float fallback) {
            return p.Length > index && p[index] != "-" ? float.Parse(p[index], CultureInfo.InvariantCulture) : fallback;
        }

        string SetLookCamera(float yawDegrees, float pitchDegrees, float distance) {
            var cam = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
            if (cam == null) return "no camera";
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Type type = cam.GetType();
            if (!float.IsNaN(yawDegrees)) {
                foreach (string field in new[] { "yaw", "targetYaw" }) type.GetField(field, flags)?.SetValue(cam, Mathf.Repeat(yawDegrees, 360f));
            }

            if (!float.IsNaN(pitchDegrees)) {
                foreach (string field in new[] { "pitch", "targetPitch" }) type.GetField(field, flags)?.SetValue(cam, pitchDegrees);
            }

            if (!float.IsNaN(distance)) {
                if (distance < cam.MinDistance) type.GetField("minDistance", flags)?.SetValue(cam, distance);
                foreach (string field in new[] { "distance", "targetDistance" }) type.GetField(field, flags)?.SetValue(cam, distance);
            }

            return "yaw " + cam.YawDegrees.ToString("F0", CultureInfo.InvariantCulture) + " pitch " + cam.PitchDegrees.ToString("F0", CultureInfo.InvariantCulture) +
                " distance " + cam.Distance.ToString("F0", CultureInfo.InvariantCulture);
        }

        static string SetLookDaytime(string arg) {
            if (arg == "off" || arg == "-") {
                RoOnlineWorldController.DiagnosticCyclePosition = -1f;
                return "server clock";
            }

            float hours = float.Parse(arg, CultureInfo.InvariantCulture);
            RoOnlineWorldController.DiagnosticCyclePosition = Mathf.Repeat(hours, 22f) / 22f;
            return hours.ToString("F1", CultureInfo.InvariantCulture) + " h (" + (Mathf.Repeat(hours, 22f) >= 14f ? "night" : "day") + ")";
        }

        static void SetLookHud(bool visible) {
            Ragnarok.UI.Classic.RoClassicUiRenderer.HiddenForCaptures = !visible;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) canvas.enabled = visible;
            }
        }

        IEnumerator LookCaptures(string tag, HashSet<string> only) {
            string listPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "look-shots.txt"));
            string list = File.Exists(listPath) ? File.ReadAllText(listPath) : DefaultLookShots;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "look", tag));
            Directory.CreateDirectory(dir);
            var mapController = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
            int done = 0;
            foreach (string raw in list.Split('\n')) {
                string[] s = raw.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (s.Length < 4 || s[0].StartsWith("#", StringComparison.Ordinal) || (only != null && !only.Contains(s[0]))) continue;
                string map = s[1];
                int x = int.Parse(s[2], CultureInfo.InvariantCulture), y = int.Parse(s[3], CultureInfo.InvariantCulture);
                if (mapController == null) mapController = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
                bool sameSpot = mapController != null && string.Equals(mapController.MapName, map, StringComparison.OrdinalIgnoreCase) &&
                                TryGetSelf(out RoOnlineActorSnapshot here) && here.CellX == x && here.CellY == y;
                if (!sameSpot) {
                    shell?.SweepSendChat("@warp " + map + " " + x.ToString(CultureInfo.InvariantCulture) + " " + y.ToString(CultureInfo.InvariantCulture));
                    float deadline = Now + 30f;
                    yield return new WaitForSecondsRealtime(1.5f);
                    while (Now < deadline) {
                        mapController = FindFirstObjectByType<Ragnarok.Maps.RoRuntimeMapController>();
                        if (shell != null && shell.SweepStage == Ragnarok.UI.RoLoginShellStage.MapEntered && !shell.SweepIsBusy && mapController != null &&
                            string.Equals(mapController.MapName, map, StringComparison.OrdinalIgnoreCase) && TryGetSelf(out _)) {
                            break;
                        }

                        yield return new WaitForSecondsRealtime(0.5f);
                    }

                    yield return new WaitForSecondsRealtime(LookSettleSeconds);
                }

                Log("look " + s[0] + ": daytime " + SetLookDaytime(s.Length > 7 ? s[7] : "-") + ", cam " +
                    SetLookCamera(ParseLook(s, 4, 0f), ParseLook(s, 5, 50f), ParseLook(s, 6, 75f)));
                SetLookHud(false);
                // the day/night push and the lights follow a frame or two later; the camera settles its follow ease
                yield return new WaitForSecondsRealtime(2.5f);
                string shot = Path.Combine(dir, s[0] + ".png");
                yield return CaptureLookShot(shot, LookShotWidth, LookShotHeight);
                string where = TryGetSelf(out RoOnlineActorSnapshot me) ? me.CellX + "," + me.CellY : "?";
                Log("look " + s[0] + ": " + shot + " (map " + (mapController != null ? mapController.MapName : "?") + " at " + where + ")");
                done++;
            }

            SetLookHud(true);
            SetLookDaytime("off");
            Func<string> describe = Ragnarok.Rendering.RoLookState.Describe;
            if (describe != null) File.WriteAllText(Path.Combine(dir, "look.txt"), describe());
            Log("look " + tag + " done: " + done + " shots in " + dir);
        }
    }
}
