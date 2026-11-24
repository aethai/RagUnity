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
    /// Where the main thread allocates managed memory during Play: records <c>frames</c> frames with the profiler and adds
    /// every GC.Alloc sample to the nearest script callback or named marker above it. Started by the editor command
    /// <c>gcprobe [frames]</c> (Temp/ro-editor-command.txt); writes Temp/ro-gc-alloc.txt and logs the top entries.
    /// </summary>
    internal static class RoGcAllocProbe {
        const string OutputPath = "Temp/ro-gc-alloc.txt";

        static int firstFrame = -1;
        static int framesWanted;
        static bool wasEnabled;
        static bool withStacks;
        static ProfilerMemoryRecordMode recordModeBefore;

        /// <summary><paramref name="stacks"/>: GC.Alloc call stacks (ProfilerMemoryRecordMode.GCAlloc), grouped by the first
        /// three project frames instead of the markers.</summary>
        public static void Start(int frames, bool stacks = false) {
            if (!EditorApplication.isPlaying) {
                Debug.LogWarning("[GcAllocProbe] needs Play mode");
                return;
            }

            framesWanted = Mathf.Clamp(frames, 10, 2000);
            withStacks = stacks;
            recordModeBefore = ProfilerDriver.memoryRecordMode;
            if (stacks) {
                ProfilerDriver.memoryRecordMode = ProfilerMemoryRecordMode.GCAlloc;
            }

            wasEnabled = ProfilerDriver.enabled;
            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = false;
            ProfilerDriver.enabled = true;
            firstFrame = -1;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Debug.Log($"[GcAllocProbe] recording {framesWanted} frames");
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
            ProfilerDriver.memoryRecordMode = recordModeBefore;
        }

        struct Total {
            public long Bytes;
            public int Count;
        }

        static void Report(int from, int to) {
            var byOwner = new Dictionary<string, Total>();
            var stack = new List<(int Marker, int ChildrenLeft)>();
            long allBytes = 0;
            int frames = 0;
            for (int f = from; f <= to; f++) {
                using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(f, 0)) {
                    if (view == null || !view.valid) {
                        continue;
                    }

                    frames++;
                    int gcMarker = view.GetMarkerId("GC.Alloc");
                    stack.Clear();
                    int count = view.sampleCount;
                    for (int i = 0; i < count; i++) {
                        // pop the parents whose children are all visited
                        while (stack.Count > 0 && stack[stack.Count - 1].ChildrenLeft == 0) {
                            stack.RemoveAt(stack.Count - 1);
                        }

                        if (stack.Count > 0) {
                            (int m, int left) = stack[stack.Count - 1];
                            stack[stack.Count - 1] = (m, left - 1);
                        }

                        int marker = view.GetSampleMarkerId(i);
                        if (marker == gcMarker) {
                            long bytes = view.GetSampleMetadataCount(i) > 0 ? view.GetSampleMetadataAsLong(i, 0) : 0;
                            allBytes += bytes;
                            string owner = withStacks ? StackOwner(view, i) ?? Owner(view, stack) : Owner(view, stack);
                            byOwner.TryGetValue(owner, out Total t);
                            t.Bytes += bytes;
                            t.Count++;
                            byOwner[owner] = t;
                        }

                        stack.Add((marker, view.GetSampleChildrenCount(i)));
                    }
                }
            }

            var sorted = new List<KeyValuePair<string, Total>>(byOwner);
            sorted.Sort((a, b) => b.Value.Bytes.CompareTo(a.Value.Bytes));
            var text = new StringBuilder();
            text.AppendLine($"GC alloc over {frames} frames: {allBytes / Math.Max(1, frames)} B/frame");
            foreach (KeyValuePair<string, Total> e in sorted) {
                text.Append((e.Value.Bytes / Math.Max(1, frames)).ToString(CultureInfo.InvariantCulture).PadLeft(9)).Append(" B/frame ")
                    .Append(((float) e.Value.Count / Math.Max(1, frames)).ToString("F1", CultureInfo.InvariantCulture).PadLeft(7))
                    .Append(" allocs/frame  ").AppendLine(e.Key);
            }

            File.WriteAllText(OutputPath, text.ToString());
            string[] lines = text.ToString().Split('\n');
            Debug.Log("[GcAllocProbe] " + string.Join("\n", lines, 0, Math.Min(lines.Length, 30)));
        }

        /// <summary>The two nearest markers above the allocation, skipping the profiler's generic ones.</summary>
        static readonly List<ulong> callstack = new List<ulong>(64);

        /// <summary>The first three project frames (Ragnarok.*) of the allocation's call stack, innermost first.</summary>
        static string StackOwner(RawFrameDataView view, int sample) {
            callstack.Clear();
            view.GetSampleCallstack(sample, callstack);
            var text = new StringBuilder();
            int taken = 0;
            for (int k = 0; k < callstack.Count && taken < 3; k++) {
                FrameDataView.MethodInfo method = view.ResolveMethodInfo(callstack[k]);
                string name = method.methodName;
                if (string.IsNullOrEmpty(name) || name.IndexOf("Ragnarok", StringComparison.Ordinal) < 0) {
                    if (taken == 0 && !string.IsNullOrEmpty(name)) {
                        text.Append('[').Append(name).Append("] ");
                    }

                    continue;
                }

                if (taken > 0) text.Append(" < ");
                text.Append(name);
                if (method.sourceFileLine > 0) text.Append(':').Append(method.sourceFileLine);
                taken++;
            }

            return taken > 0 ? text.ToString() : null;
        }

        static string Owner(RawFrameDataView view, List<(int Marker, int ChildrenLeft)> stack) {
            string inner = null, outer = null;
            for (int s = stack.Count - 1; s >= 0; s--) {
                string name = view.GetMarkerName(stack[s].Marker);
                if (string.IsNullOrEmpty(name) || name == "GC.Alloc") {
                    continue;
                }

                if (inner == null) {
                    inner = name;
                    continue;
                }

                outer = name;
                break;
            }

            return outer != null ? outer + " > " + inner : inner ?? "(root)";
        }
    }
}
