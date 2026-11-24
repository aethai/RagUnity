using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// Where a Play frame's time goes: records <c>frames</c> frames with the profiler and sums every marker's time on the
    /// main thread and the render thread — self time (minus its children) and total time (with them, counted once per
    /// nesting) — per frame. Started by the editor command <c>cpuprobe [frames]</c> (Temp/ro-editor-command.txt); writes
    /// Temp/ro-cpu-probe.txt.
    /// </summary>
    internal static class RoCpuProbe {
        const string OutputPath = "Temp/ro-cpu-probe.txt";
        const int TopEntries = 60;

        static int firstFrame = -1;
        static int framesWanted;
        static bool wasEnabled;

        public static void Start(int frames) {
            if (!EditorApplication.isPlaying) {
                Debug.LogWarning("[CpuProbe] needs Play mode");
                return;
            }

            framesWanted = Mathf.Clamp(frames, 10, 2000);
            wasEnabled = ProfilerDriver.enabled;
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            firstFrame = -1;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Debug.Log($"[CpuProbe] recording {framesWanted} frames");
        }

        static void Poll() {
            if (!EditorApplication.isPlaying) {
                EditorApplication.update -= Poll;
                ProfilerDriver.enabled = wasEnabled;
                return;
            }

            if (ProfilerDriver.lastFrameIndex < 0) {
                return;
            }

            if (firstFrame < 0) {
                firstFrame = ProfilerDriver.lastFrameIndex + 1;
                return;
            }

            if (ProfilerDriver.lastFrameIndex - firstFrame + 1 < framesWanted) {
                return;
            }

            EditorApplication.update -= Poll;
            int last = ProfilerDriver.lastFrameIndex;
            ProfilerDriver.enabled = wasEnabled;
            Report(Math.Max(firstFrame, ProfilerDriver.firstFrameIndex), last);
        }

        sealed class Totals {
            public readonly Dictionary<string, double> Self = new Dictionary<string, double>();
            public readonly Dictionary<string, double> Total = new Dictionary<string, double>();
            public readonly Dictionary<string, double> Calls = new Dictionary<string, double>();
            public double FrameMs;
            public int Frames;
            /// <summary>The slowest frame (by the thread's frame time) and its own breakdown.</summary>
            public Totals Worst;
            public double WorstMs;
            public int WorstFrame;
        }

        static void Report(int from, int to) {
            var threads = new SortedDictionary<string, Totals>();
            var stack = new List<(int Marker, int ChildrenLeft, double Ms, double ChildMs)>();
            var open = new Dictionary<int, int>();   // marker → how many of it are on the stack (total time counted at the outermost)
            for (int f = from; f <= to; f++) {
                for (int threadIndex = 0; ; threadIndex++) {
                    using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(f, threadIndex)) {
                        if (view == null || !view.valid) {
                            break;
                        }

                        string thread = view.threadGroupName + "/" + view.threadName;
                        if (thread != "/Main Thread" && thread != "/Render Thread") {
                            continue;
                        }

                        if (!threads.TryGetValue(thread, out Totals totals)) {
                            threads[thread] = totals = new Totals();
                        }

                        totals.Frames++;
                        totals.FrameMs += view.frameTimeMs;
                        var frame = new Totals();
                        stack.Clear();
                        open.Clear();
                        int count = view.sampleCount;
                        for (int i = 0; i < count; i++) {
                            while (stack.Count > 0 && stack[stack.Count - 1].ChildrenLeft == 0) {
                                Pop(view, stack, open, totals, frame);
                            }

                            if (stack.Count > 0) {
                                var top = stack[stack.Count - 1];
                                stack[stack.Count - 1] = (top.Marker, top.ChildrenLeft - 1, top.Ms, top.ChildMs);
                            }

                            int marker = view.GetSampleMarkerId(i);
                            double ms = view.GetSampleTimeMs(i);
                            if (stack.Count > 0) {
                                var parent = stack[stack.Count - 1];
                                stack[stack.Count - 1] = (parent.Marker, parent.ChildrenLeft, parent.Ms, parent.ChildMs + ms);
                            }

                            open.TryGetValue(marker, out int depth);
                            open[marker] = depth + 1;
                            stack.Add((marker, view.GetSampleChildrenCount(i), ms, 0));
                        }

                        while (stack.Count > 0) {
                            Pop(view, stack, open, totals, frame);
                        }

                        if (view.frameTimeMs > totals.WorstMs) {
                            totals.WorstMs = view.frameTimeMs;
                            totals.Worst = frame;
                            totals.WorstFrame = f;
                        }
                    }
                }
            }

            var text = new StringBuilder();
            foreach (KeyValuePair<string, Totals> thread in threads) {
                Totals t = thread.Value;
                double frames = Math.Max(1, t.Frames);
                text.Append(thread.Key).Append(": ").Append(t.Frames).Append(" frames, ")
                    .Append((t.FrameMs / frames).ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms/frame");
                text.AppendLine("  self ms/frame  total ms/frame  calls/frame  marker");
                var sorted = new List<KeyValuePair<string, double>>(t.Self);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < sorted.Count && i < TopEntries; i++) {
                    string name = sorted[i].Key;
                    text.Append((sorted[i].Value / frames).ToString("F3", CultureInfo.InvariantCulture).PadLeft(15))
                        .Append((t.Total[name] / frames).ToString("F3", CultureInfo.InvariantCulture).PadLeft(16))
                        .Append((t.Calls[name] / frames).ToString("F1", CultureInfo.InvariantCulture).PadLeft(13))
                        .Append("  ").AppendLine(name);
                }

                if (t.Worst != null) {
                    text.Append("  worst frame ").Append(t.WorstFrame).Append(": ").Append(t.WorstMs.ToString("F2", CultureInfo.InvariantCulture)).AppendLine(" ms (self / total ms, calls)");
                    var worst = new List<KeyValuePair<string, double>>(t.Worst.Self);
                    worst.Sort((a, b) => b.Value.CompareTo(a.Value));
                    for (int i = 0; i < worst.Count && i < 25; i++) {
                        string name = worst[i].Key;
                        text.Append(worst[i].Value.ToString("F3", CultureInfo.InvariantCulture).PadLeft(15))
                            .Append(t.Worst.Total[name].ToString("F3", CultureInfo.InvariantCulture).PadLeft(16))
                            .Append(t.Worst.Calls[name].ToString("F0", CultureInfo.InvariantCulture).PadLeft(13))
                            .Append("  ").AppendLine(name);
                    }
                }

                text.AppendLine();
            }

            File.WriteAllText(OutputPath, text.ToString());
            Debug.Log("[CpuProbe] written " + Path.GetFullPath(OutputPath));
        }

        static void Pop(RawFrameDataView view, List<(int Marker, int ChildrenLeft, double Ms, double ChildMs)> stack,
            Dictionary<int, int> open, Totals totals, Totals frame) {
            var s = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            string name = view.GetMarkerName(s.Marker) ?? "?";
            totals.Self.TryGetValue(name, out double self);
            totals.Self[name] = self + Math.Max(0, s.Ms - s.ChildMs);
            totals.Calls.TryGetValue(name, out double calls);
            totals.Calls[name] = calls + 1;
            int depth = open[s.Marker] - 1;
            open[s.Marker] = depth;
            totals.Total.TryGetValue(name, out double total);
            totals.Total[name] = depth == 0 ? total + s.Ms : total;
            frame.Self.TryGetValue(name, out double fSelf);
            frame.Self[name] = fSelf + Math.Max(0, s.Ms - s.ChildMs);
            frame.Calls.TryGetValue(name, out double fCalls);
            frame.Calls[name] = fCalls + 1;
            frame.Total.TryGetValue(name, out double fTotal);
            frame.Total[name] = depth == 0 ? fTotal + s.Ms : fTotal;
        }
    }
}
