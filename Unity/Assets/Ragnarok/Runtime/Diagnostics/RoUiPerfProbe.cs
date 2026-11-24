using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Focused frame recorder for UI cost: per-frame values of a list of profiler markers/counters (resolved by name,
    /// whatever their category), summarised as avg/p50/p95/p99/max into PerformanceCaptures/ui/&lt;stamp&gt;_&lt;label&gt;.json.
    /// Started from code (<see cref="Begin"/>) — the skill-sweep console exposes it as <c>!uiperf</c>.
    /// </summary>
    public sealed class RoUiPerfProbe : MonoBehaviour {
        public static readonly string[] DefaultMarkers = {
            "Main Thread",
            "Render Thread",
            "GC Allocated In Frame",
            "Draw Calls Count",
            "Batches Count",
            "SetPass Calls Count",
            "Update.ScriptRunBehaviourUpdate",
            "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "PostLateUpdate.PlayerUpdateCanvases",
            "PostLateUpdate.PlayerEmitCanvasGeometry",
            "PostLateUpdate.UpdateRectTransform",
            "Canvas.SendWillRenderCanvases",
            "Canvas.BuildBatch",
            "Canvas.RenderOverlays",
            "UGUI.Rendering.RenderOverlays",
            "UGUI.Rendering.EmitWorldScreenspaceCameraGeometry",
            "Layout.RebuildLayout",
            "EventSystem.Update",
            "GUI.Repaint",
            "GUI.ProcessEvents",
            "GUIUtility.ProcessEvent",
            "PostLateUpdate.PlayerSendFrameComplete",
            "PreUpdate.SendMouseEvents",
            "RO.UiFast.Build",
            "RO.UiFast.Submit",
            "RO.ClassicUi.Tick",
            "RO.ClassicUi.Upload",
            "RO.ClassicUi.Draw",
        };

        private sealed class Series {
            public string Name;
            public ProfilerMarkerDataUnit Unit;
            public ProfilerRecorder Recorder;
            public double[] Values;
        }

        private readonly List<Series> series = new List<Series>();
        private readonly List<string> missing = new List<string>();
        private string label;
        private string outputPath;
        private float remainingSeconds;
        private int frames;
        private int capacity;
        private float startTime;

        public static bool IsRunning { get; private set; }

        public static string OutputRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "ui"));

        /// <summary>Records <paramref name="seconds"/> of frames; returns the JSON path that will be written.</summary>
        public static string Begin(float seconds, string captureLabel, IReadOnlyList<string> markers = null) {
            if (IsRunning) {
                return null;
            }

            var go = new GameObject("RO UI perf probe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<RoUiPerfProbe>();
            probe.Configure(seconds, captureLabel, markers ?? DefaultMarkers);
            return probe.outputPath;
        }

        /// <summary>Writes every available profiler stat whose name matches one of the needles (case-insensitive).</summary>
        public static string DumpAvailable(string[] needles) {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var sb = new StringBuilder();
            foreach (ProfilerRecorderHandle handle in handles) {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                string name = d.Name;
                bool match = needles == null || needles.Length == 0;
                for (int i = 0; !match && i < needles.Length; i++) {
                    match = name.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (match) {
                    sb.Append(d.Category.Name).Append('\t').Append(name).Append('\t').Append(d.UnitType).Append('\n');
                }
            }

            Directory.CreateDirectory(OutputRoot);
            string path = Path.Combine(OutputRoot, "available-markers.tsv");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private void Configure(float seconds, string captureLabel, IReadOnlyList<string> markers) {
            IsRunning = true;
            label = string.IsNullOrWhiteSpace(captureLabel) ? "capture" : captureLabel.Trim();
            remainingSeconds = Mathf.Max(1f, seconds);
            capacity = Mathf.CeilToInt(remainingSeconds * 400f) + 16;
            startTime = Time.realtimeSinceStartup;
            Directory.CreateDirectory(OutputRoot);
            outputPath = Path.Combine(OutputRoot,
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" + Sanitize(label) + ".json");

            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var byName = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.Ordinal);
            foreach (ProfilerRecorderHandle handle in handles) {
                string name = ProfilerRecorderHandle.GetDescription(handle).Name;
                if (!byName.ContainsKey(name)) {
                    byName.Add(name, handle);
                }
            }

            foreach (string marker in markers) {
                if (!byName.TryGetValue(marker, out ProfilerRecorderHandle handle)) {
                    missing.Add(marker);
                    continue;
                }

                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default);
                recorder.Start();
                series.Add(new Series { Name = marker, Unit = d.UnitType, Recorder = recorder, Values = new double[capacity] });
            }
        }

        private void Update() {
            if (!IsRunning) {
                return;
            }

            // the first frame after Start has no completed sample yet
            if (Time.realtimeSinceStartup - startTime > 0.05f && frames < capacity) {
                for (int i = 0; i < series.Count; i++) {
                    series[i].Values[frames] = series[i].Recorder.LastValueAsDouble;
                }

                frames++;
            }

            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds <= 0f || frames >= capacity) {
                Finish();
            }
        }

        private void OnDestroy() {
            if (IsRunning) {
                Finish();
            }
        }

        private void Finish() {
            IsRunning = false;
            float elapsed = Time.realtimeSinceStartup - startTime;
            var sb = new StringBuilder();
            sb.Append("{\n  \"label\": \"").Append(Escape(label)).Append("\",\n");
            sb.Append("  \"frames\": ").Append(frames).Append(",\n");
            sb.Append("  \"seconds\": ").Append(F(elapsed)).Append(",\n");
            sb.Append("  \"fps\": ").Append(F(elapsed > 0f ? frames / elapsed : 0f)).Append(",\n");
            sb.Append("  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height).Append("\",\n");
            sb.Append("  \"editor\": ").Append(Application.isEditor ? "true" : "false").Append(",\n");
            sb.Append("  \"missing\": [");
            for (int i = 0; i < missing.Count; i++) {
                sb.Append(i == 0 ? "\"" : ", \"").Append(Escape(missing[i])).Append('"');
            }

            sb.Append("],\n  \"series\": [\n");
            for (int s = 0; s < series.Count; s++) {
                Series item = series[s];
                double scale = item.Unit == ProfilerMarkerDataUnit.TimeNanoseconds ? 1e-6 : 1.0;
                string unit = item.Unit == ProfilerMarkerDataUnit.TimeNanoseconds ? "ms"
                    : item.Unit == ProfilerMarkerDataUnit.Bytes ? "bytes" : "count";
                var values = new double[frames];
                double sum = 0;
                for (int i = 0; i < frames; i++) {
                    values[i] = item.Values[i] * scale;
                    sum += values[i];
                }

                Array.Sort(values);
                sb.Append("    {\"name\": \"").Append(Escape(item.Name)).Append("\", \"unit\": \"").Append(unit)
                    .Append("\", \"avg\": ").Append(F(frames > 0 ? sum / frames : 0))
                    .Append(", \"p50\": ").Append(F(Pct(values, 0.50)))
                    .Append(", \"p95\": ").Append(F(Pct(values, 0.95)))
                    .Append(", \"p99\": ").Append(F(Pct(values, 0.99)))
                    .Append(", \"max\": ").Append(F(frames > 0 ? values[frames - 1] : 0))
                    .Append(s + 1 < series.Count ? "},\n" : "}\n");
                item.Recorder.Dispose();
            }

            sb.Append("  ]\n}\n");
            try {
                File.WriteAllText(outputPath, sb.ToString());
                Debug.Log("[UiPerf] wrote " + outputPath);
            } catch (Exception e) {
                Debug.LogError("[UiPerf] could not write " + outputPath + ": " + e.Message);
            }

            series.Clear();
            Destroy(gameObject);
        }

        private static double Pct(double[] sorted, double p) {
            if (sorted.Length == 0) {
                return 0;
            }

            int index = (int) Math.Min(sorted.Length - 1, Math.Round(p * (sorted.Length - 1)));
            return sorted[index];
        }

        private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private static string Sanitize(string value) {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value) {
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }

            return sb.ToString();
        }
    }
}
