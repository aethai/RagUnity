using System;
using System.Collections;
using System.IO;
using Ragnarok.Entities;
using Ragnarok.Maps;
using Ragnarok.Sprites;
using Ragnarok.UI;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Opt-in player regression check: -ro-verify-rendering loads the startup map and renders
    /// character-select portraits without connecting to a server. Exits with 0 on success.
    /// RO_RENDER_PROBE_OUTPUT optionally selects the directory for the portrait PNGs.
    /// Run with a graphics device (without -nographics).
    /// </summary>
    public sealed class RoBuildRenderingProbe : MonoBehaviour {
        private float deadline;
        private bool finished;
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-ro-verify-rendering") < 0) return;
            new GameObject("RO build rendering probe").AddComponent<RoBuildRenderingProbe>();
        }

        private IEnumerator Start() {
            deadline = Time.realtimeSinceStartup + 120f;
            Application.logMessageReceived += OnLog;
            output = Environment.GetEnvironmentVariable("RO_RENDER_PROBE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) {
                output = Path.Combine(Application.persistentDataPath, "RagnarokDiagnostics", "BuildRenderingProbe");
            }
            Directory.CreateDirectory(output);

            foreach (string name in new[] {
                "Ragnarok/RO Sprite Unlit", "Ragnarok/RO Sprite Enhanced",
                "Ragnarok/RO Ground Unlit", "Ragnarok/RO Ground Enhanced",
                "Ragnarok/RO Model Unlit AlphaClip", "Ragnarok/RO Model Enhanced",
                "Ragnarok/RO Water Unlit", "Ragnarok/RO Cloud Unlit",
                "Ragnarok/RO Cursor Unlit", "Ragnarok/RO Weather Particle",
                "Ragnarok/RO Classic Effect", "Hidden/RO/ClassicUi"
            }) {
                Shader shader = Shader.Find(name);
                if (shader == null || !shader.isSupported) {
                    Finish(false, "Missing or unsupported shader: " + name);
                    yield break;
                }
            }

            RoRuntimeMapController map = FindFirstObjectByType<RoRuntimeMapController>();
            if (map == null) {
                Finish(false, "The build scene has no runtime map controller.");
                yield break;
            }
            while (map.CurrentMap == null || RoClassicUiSystem.Instance == null) yield return null;
            Renderer[] renderers = map.CurrentMap.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || map.CompiledGround == null) {
                Finish(false, "The startup map has no ground or renderers.");
                yield break;
            }
            foreach (Renderer renderer in renderers) {
                foreach (Material material in renderer.sharedMaterials) {
                    if (material == null || material.shader == null || !material.shader.isSupported) {
                        Finish(false, "Missing map material/shader on " + renderer.name);
                        yield break;
                    }
                }
            }
            Debug.Log($"[RO build rendering] Map {map.MapName}: {renderers.Length} renderers with valid shaders.");

            foreach (int job in new[] { 0, 8 }) {
                string key = "@build-portrait-" + job;
                bool rendered = false, renderDone = false;
                var appearance = new RoActorAppearanceRequest {
                    kind = RoActorAppearanceKind.Player, classId = job, isMale = true,
                    headId = 1, hairId = 1, motionProfile = RoSpriteMotionProfile.Player,
                    motion = RoSpriteMotion.Idle, facingDirection = RoActorDirection.South,
                    headFacing = RoActorHeadFacing.Center
                };
                bool started = RoClassicActorPortrait.Render(map.Content.Assets, appearance, key, 120, 150, 140,
                    ok => { rendered = ok; renderDone = true; });
                if (!started) {
                    Finish(false, "Could not start character-select portrait for job " + job);
                    yield break;
                }
                while (!renderDone) yield return null;
                RoClassicUiSystem ui = RoClassicUiSystem.Instance;
                if (!rendered || !ui.Images.TryGet(key, out RoClassicUiImage image)) {
                    Finish(false, "Character-select portrait was not stored for job " + job);
                    yield break;
                }

                ui.Atlas.Flush();
                int x = Mathf.RoundToInt(image.Uv.x * RoClassicUiAtlas.PageSize);
                int y = Mathf.RoundToInt(image.Uv.w * RoClassicUiAtlas.PageSize);
                bool readDone = false, readOk = false;
                int visiblePixels = 0;
                AsyncGPUReadback.Request(ui.Atlas.Texture, 0, x, image.Width, y, image.Height,
                    (int)image.Page, 1, TextureFormat.RGBA32, result => {
                        if (!result.hasError) {
                            Color32[] pixels = result.GetData<Color32>().ToArray();
                            foreach (Color32 pixel in pixels) if (pixel.a > 16) visiblePixels++;
                            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
                            texture.SetPixels32(pixels);
                            texture.Apply();
                            File.WriteAllBytes(Path.Combine(output, "character-job-" + job + ".png"), texture.EncodeToPNG());
                            Destroy(texture);
                            readOk = true;
                        }
                        readDone = true;
                    });
                while (!readDone) yield return null;
                if (!readOk || visiblePixels < 100 || visiblePixels == image.Width * image.Height) {
                    Finish(false, $"Character-select portrait job {job} is empty/invalid ({visiblePixels} visible pixels).");
                    yield break;
                }
                Debug.Log($"[RO build rendering] Character-select portrait job {job}: {visiblePixels} visible pixels.");
            }
            Finish(true, "Map and character-select portraits rendered in the player.");
        }

        private void Update() {
            if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline) Finish(false, "Rendering check timed out.");
        }

        private void OnLog(string message, string stack, LogType type) {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(false, message);
        }

        private void Finish(bool success, string message) {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            Debug.Log("[RO build rendering] " + (success ? "PASS: " : "FAIL: ") + message);
            Application.Quit(success ? 0 : 1);
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
