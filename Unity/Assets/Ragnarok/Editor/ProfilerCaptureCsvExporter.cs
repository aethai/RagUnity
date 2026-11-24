using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace Ragnarok.Editor {
    public static class ProfilerCaptureCsvExporter {
        private const int DefaultMaxThreadIndex = 256;
        private const int DefaultMaxFrameHistoryLength = 200000;
        private const int InvalidThreadBreakThreshold = 16;
        private static readonly char[] CsvSpecialChars = { ',', '"', '\r', '\n' };

        public static void ExportCommandLine() {
            try {
                var options = ExportOptions.FromCommandLine(Environment.GetCommandLineArgs());
                Export(options);
                EditorApplication.Exit(0);
            } catch (Exception ex) {
                Debug.LogError("[ProfilerCaptureCsvExporter] Export failed:\n" + ex);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Ragnarok/Profiler/Export Loaded Capture To CSV")]
        public static void ExportLoadedCaptureMenu() {
            string outputDir = EditorUtility.SaveFolderPanel("Profiler CSV output", "", "ProfilerCaptureCsv");
            if (string.IsNullOrWhiteSpace(outputDir))
                return;

            Export(new ExportOptions {
                InputPath = string.Empty,
                OutputDirectory = outputDir,
                LoadInputProfile = false
            });

            EditorUtility.RevealInFinder(outputDir);
        }

        public static void Export(ExportOptions options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (options.LoadInputProfile) {
                if (string.IsNullOrWhiteSpace(options.InputPath))
                    throw new ArgumentException("Missing -profileInput <path>.");
                if (!File.Exists(options.InputPath))
                    throw new FileNotFoundException("Profiler capture was not found.", options.InputPath);

                TrySetMaxFrameHistoryLength(options.MaxFrameHistoryLength);
                Debug.Log("[ProfilerCaptureCsvExporter] Loading profile: " + options.InputPath);
                if (!ProfilerDriver.LoadProfile(options.InputPath, false))
                    throw new InvalidOperationException("ProfilerDriver.LoadProfile returned false for: " + options.InputPath);
            }

            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            if (lastFrame < firstFrame)
                throw new InvalidOperationException("Loaded profiler capture has no frames.");

            if (options.FrameStart.HasValue)
                firstFrame = Math.Max(firstFrame, options.FrameStart.Value);
            if (options.FrameEnd.HasValue)
                lastFrame = Math.Min(lastFrame, options.FrameEnd.Value);
            if (lastFrame < firstFrame)
                throw new InvalidOperationException("Selected profiler frame range is empty.");

            string outputDirectory = options.ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            var metadata = new ExportMetadata {
                inputPath = options.InputPath ?? string.Empty,
                outputDirectory = outputDirectory,
                exportStartedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                firstFrame = firstFrame,
                lastFrame = lastFrame,
                frameCount = lastFrame - firstFrame + 1,
                maxThreadIndex = options.MaxThreadIndex,
                maxFrameHistoryLength = options.MaxFrameHistoryLength,
                includeMetadata = options.IncludeMetadata,
                includeCallstacks = options.IncludeCallstacks,
                includeFlows = options.IncludeFlows,
                includeHierarchyRawSampleIndices = options.IncludeHierarchyRawSampleIndices
            };

            WriteMetadata(Path.Combine(outputDirectory, "metadata.json"), metadata);

            var progressEvery = Math.Max(1, metadata.frameCount / 100);
            var reusableChildren = new List<int>(512);
            var reusableHierarchyStack = new Stack<HierarchyItemWork>(2048);
            var reusableRawStack = new Stack<RawSampleWork>(4096);
            var reusableCallstack = new List<ulong>(256);
            var reusableFlows = new List<RawFrameDataView.FlowEvent>(32);
            var reusableSampleIndices = new List<int>(256);

            using (var frameThreads = CsvFile.Create(Path.Combine(outputDirectory, "frame_threads.csv")))
            using (var rawSamples = CsvFile.Create(Path.Combine(outputDirectory, "raw_samples.csv")))
            using (var rawMetadata = CsvFile.Create(Path.Combine(outputDirectory, "raw_sample_metadata.csv")))
            using (var rawCallstacks = CsvFile.Create(Path.Combine(outputDirectory, "raw_sample_callstacks.csv")))
            using (var rawFlows = CsvFile.Create(Path.Combine(outputDirectory, "raw_sample_flows.csv")))
            using (var threadFlows = CsvFile.Create(Path.Combine(outputDirectory, "thread_flows.csv")))
            using (var hierarchyItems = CsvFile.Create(Path.Combine(outputDirectory, "hierarchy_items.csv")))
            using (var hierarchyRawIndices = CsvFile.Create(Path.Combine(outputDirectory, "hierarchy_item_raw_indices.csv"))) {

                WriteFrameThreadsHeader(frameThreads);
                WriteRawSamplesHeader(rawSamples);
                WriteRawMetadataHeader(rawMetadata);
                WriteRawCallstacksHeader(rawCallstacks);
                WriteRawFlowsHeader(rawFlows);
                WriteThreadFlowsHeader(threadFlows);
                WriteHierarchyItemsHeader(hierarchyItems);
                WriteHierarchyRawIndicesHeader(hierarchyRawIndices);

                for (int frame = firstFrame; frame <= lastFrame; frame++) {
                    int ordinal = frame - firstFrame;
                    int validThreads = 0;
                    int invalidAfterValid = 0;

                    for (int threadIndex = 0; threadIndex <= options.MaxThreadIndex; threadIndex++) {
                        using (var raw = ProfilerDriver.GetRawFrameDataView(frame, threadIndex)) {
                            if (raw == null || !raw.valid) {
                                if (validThreads > 0 && ++invalidAfterValid >= InvalidThreadBreakThreshold)
                                    break;
                                continue;
                            }

                            invalidAfterValid = 0;
                            validThreads++;
                            WriteFrameThreadRow(frameThreads, frame, ordinal, raw);
                            ExportRawSamples(raw, rawSamples, rawMetadata, rawCallstacks, rawFlows, threadFlows, options, reusableRawStack, reusableCallstack, reusableFlows);
                        }

                        using (var hierarchy = ProfilerDriver.GetHierarchyFrameDataView(
                                   frame,
                                   threadIndex,
                                   HierarchyFrameDataView.ViewModes.Default,
                                   HierarchyFrameDataView.columnTotalTime,
                                   false)) {
                            if (hierarchy != null && hierarchy.valid)
                                ExportHierarchy(hierarchy, hierarchyItems, hierarchyRawIndices, options, reusableChildren, reusableHierarchyStack, reusableSampleIndices);
                        }
                    }

                    if (ordinal % progressEvery == 0 || frame == lastFrame) {
                        float percent = (ordinal + 1) * 100f / metadata.frameCount;
                        Debug.Log(string.Format(
                            CultureInfo.InvariantCulture,
                            "[ProfilerCaptureCsvExporter] Exported frame {0}/{1} ({2:F1}%).",
                            ordinal + 1,
                            metadata.frameCount,
                            percent));
                    }
                }
            }

            metadata.exportFinishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteMetadata(Path.Combine(outputDirectory, "metadata.json"), metadata);
            Debug.Log("[ProfilerCaptureCsvExporter] Export complete: " + outputDirectory);
        }

        private static void ExportRawSamples(
            RawFrameDataView raw,
            CsvFile rawSamples,
            CsvFile rawMetadata,
            CsvFile rawCallstacks,
            CsvFile rawFlows,
            CsvFile threadFlows,
            ExportOptions options,
            Stack<RawSampleWork> stack,
            List<ulong> callstack,
            List<RawFrameDataView.FlowEvent> flows) {

            if (raw.sampleCount <= 0)
                return;

            stack.Clear();
            stack.Push(new RawSampleWork(0, -1, 0));

            while (stack.Count > 0) {
                RawSampleWork item = stack.Pop();
                if (item.SampleIndex < 0 || item.SampleIndex >= raw.sampleCount)
                    continue;

                int childCount = SafeInt(() => raw.GetSampleChildrenCount(item.SampleIndex));
                int recursiveChildCount = SafeInt(() => raw.GetSampleChildrenCountRecursive(item.SampleIndex));
                int markerId = SafeInt(() => raw.GetSampleMarkerId(item.SampleIndex));
                int metadataCount = SafeInt(() => raw.GetSampleMetadataCount(item.SampleIndex));
                int categoryIndex = SafeInt(() => raw.GetSampleCategoryIndex(item.SampleIndex));
                string name = SafeString(() => raw.GetSampleName(item.SampleIndex));
                string flags = SafeString(() => raw.GetSampleFlags(item.SampleIndex).ToString());

                rawSamples.BeginRow();
                rawSamples.Write(raw.frameIndex);
                rawSamples.Write(raw.threadIndex);
                rawSamples.Write(raw.threadGroupName);
                rawSamples.Write(raw.threadName);
                rawSamples.Write(raw.threadId);
                rawSamples.Write(item.SampleIndex);
                rawSamples.Write(item.ParentSampleIndex);
                rawSamples.Write(item.Depth);
                rawSamples.Write(childCount);
                rawSamples.Write(recursiveChildCount);
                rawSamples.Write(markerId);
                rawSamples.Write(categoryIndex);
                rawSamples.Write(flags);
                rawSamples.Write(name);
                rawSamples.Write(SafeDouble(() => raw.GetSampleStartTimeMs(item.SampleIndex)));
                rawSamples.Write(SafeUInt64(() => raw.GetSampleStartTimeNs(item.SampleIndex)));
                rawSamples.Write(SafeFloat(() => raw.GetSampleTimeMs(item.SampleIndex)));
                rawSamples.Write(SafeUInt64(() => raw.GetSampleTimeNs(item.SampleIndex)));
                rawSamples.Write(metadataCount);
                rawSamples.EndRow();

                if (options.IncludeMetadata && metadataCount > 0)
                    ExportRawSampleMetadata(raw, item.SampleIndex, metadataCount, rawMetadata);

                if (options.IncludeCallstacks)
                    ExportRawSampleCallstack(raw, item.SampleIndex, rawCallstacks, callstack);

                if (options.IncludeFlows)
                    ExportRawSampleFlows(raw, item.SampleIndex, rawFlows, flows);

                if (childCount <= 0)
                    continue;

                int nextChildIndex = item.SampleIndex + 1;
                var children = new List<int>(childCount);
                for (int i = 0; i < childCount && nextChildIndex < raw.sampleCount; i++) {
                    children.Add(nextChildIndex);
                    int descendants = SafeInt(() => raw.GetSampleChildrenCountRecursive(nextChildIndex));
                    nextChildIndex += descendants + 1;
                }

                for (int i = children.Count - 1; i >= 0; i--)
                    stack.Push(new RawSampleWork(children[i], item.SampleIndex, item.Depth + 1));
            }

            if (options.IncludeFlows) {
                flows.Clear();
                try {
                    raw.GetFlowEvents(flows);
                } catch (Exception ex) {
                    WriteExportWarning(threadFlows, raw.frameIndex, raw.threadIndex, "GetFlowEvents", ex);
                    return;
                }

                for (int i = 0; i < flows.Count; i++) {
                    RawFrameDataView.FlowEvent flow = flows[i];
                    threadFlows.BeginRow();
                    threadFlows.Write(raw.frameIndex);
                    threadFlows.Write(raw.threadIndex);
                    threadFlows.Write(raw.threadGroupName);
                    threadFlows.Write(raw.threadName);
                    threadFlows.Write(i);
                    threadFlows.Write(flow.ParentSampleIndex);
                    threadFlows.Write(flow.FlowId);
                    threadFlows.Write(flow.FlowEventType.ToString());
                    threadFlows.EndRow();
                }
            }
        }

        private static void ExportRawSampleMetadata(RawFrameDataView raw, int sampleIndex, int metadataCount, CsvFile rawMetadata) {
            for (int metadataIndex = 0; metadataIndex < metadataCount; metadataIndex++) {
                rawMetadata.BeginRow();
                rawMetadata.Write(raw.frameIndex);
                rawMetadata.Write(raw.threadIndex);
                rawMetadata.Write(sampleIndex);
                rawMetadata.Write(metadataIndex);
                rawMetadata.Write(TryReadMetadataString(raw, sampleIndex, metadataIndex));
                rawMetadata.Write(TryReadMetadataInt(raw, sampleIndex, metadataIndex));
                rawMetadata.Write(TryReadMetadataLong(raw, sampleIndex, metadataIndex));
                rawMetadata.Write(TryReadMetadataFloat(raw, sampleIndex, metadataIndex));
                rawMetadata.Write(TryReadMetadataDouble(raw, sampleIndex, metadataIndex));
                rawMetadata.EndRow();
            }
        }

        private static string TryReadMetadataString(RawFrameDataView raw, int sampleIndex, int metadataIndex) {
            try {
                return raw.GetSampleMetadataAsString(sampleIndex, metadataIndex);
            } catch {
                return string.Empty;
            }
        }

        private static string TryReadMetadataInt(RawFrameDataView raw, int sampleIndex, int metadataIndex) {
            try {
                return raw.GetSampleMetadataAsInt(sampleIndex, metadataIndex).ToString(CultureInfo.InvariantCulture);
            } catch {
                return string.Empty;
            }
        }

        private static string TryReadMetadataLong(RawFrameDataView raw, int sampleIndex, int metadataIndex) {
            try {
                return raw.GetSampleMetadataAsLong(sampleIndex, metadataIndex).ToString(CultureInfo.InvariantCulture);
            } catch {
                return string.Empty;
            }
        }

        private static string TryReadMetadataFloat(RawFrameDataView raw, int sampleIndex, int metadataIndex) {
            try {
                return raw.GetSampleMetadataAsFloat(sampleIndex, metadataIndex).ToString("R", CultureInfo.InvariantCulture);
            } catch {
                return string.Empty;
            }
        }

        private static string TryReadMetadataDouble(RawFrameDataView raw, int sampleIndex, int metadataIndex) {
            try {
                return raw.GetSampleMetadataAsDouble(sampleIndex, metadataIndex).ToString("R", CultureInfo.InvariantCulture);
            } catch {
                return string.Empty;
            }
        }

        private static void ExportRawSampleCallstack(RawFrameDataView raw, int sampleIndex, CsvFile rawCallstacks, List<ulong> callstack) {
            callstack.Clear();
            try {
                raw.GetSampleCallstack(sampleIndex, callstack);
            } catch {
                return;
            }

            for (int i = 0; i < callstack.Count; i++) {
                rawCallstacks.BeginRow();
                rawCallstacks.Write(raw.frameIndex);
                rawCallstacks.Write(raw.threadIndex);
                rawCallstacks.Write(sampleIndex);
                rawCallstacks.Write(i);
                rawCallstacks.Write(callstack[i]);
                rawCallstacks.EndRow();
            }
        }

        private static void ExportRawSampleFlows(RawFrameDataView raw, int sampleIndex, CsvFile rawFlows, List<RawFrameDataView.FlowEvent> flows) {
            flows.Clear();
            try {
                raw.GetSampleFlowEvents(sampleIndex, flows);
            } catch {
                return;
            }

            for (int i = 0; i < flows.Count; i++) {
                RawFrameDataView.FlowEvent flow = flows[i];
                rawFlows.BeginRow();
                rawFlows.Write(raw.frameIndex);
                rawFlows.Write(raw.threadIndex);
                rawFlows.Write(sampleIndex);
                rawFlows.Write(i);
                rawFlows.Write(flow.ParentSampleIndex);
                rawFlows.Write(flow.FlowId);
                rawFlows.Write(flow.FlowEventType.ToString());
                rawFlows.EndRow();
            }
        }

        private static void ExportHierarchy(
            HierarchyFrameDataView hierarchy,
            CsvFile hierarchyItems,
            CsvFile hierarchyRawIndices,
            ExportOptions options,
            List<int> children,
            Stack<HierarchyItemWork> stack,
            List<int> sampleIndices) {

            int root = hierarchy.GetRootItemID();
            children.Clear();
            hierarchy.GetItemChildren(root, children);

            stack.Clear();
            for (int i = children.Count - 1; i >= 0; i--)
                stack.Push(new HierarchyItemWork(children[i], root, 0));

            while (stack.Count > 0) {
                HierarchyItemWork work = stack.Pop();
                int id = work.ItemId;

                int markerId = SafeInt(() => hierarchy.GetItemMarkerID(id));
                int categoryIndex = SafeInt(() => hierarchy.GetItemCategoryIndex(id));
                int metadataCount = SafeInt(() => hierarchy.GetItemMetadataCount(id));
                int mergedSamples = SafeInt(() => hierarchy.GetItemMergedSamplesCount(id));

                hierarchyItems.BeginRow();
                hierarchyItems.Write(hierarchy.frameIndex);
                hierarchyItems.Write(hierarchy.threadIndex);
                hierarchyItems.Write(hierarchy.threadGroupName);
                hierarchyItems.Write(hierarchy.threadName);
                hierarchyItems.Write(hierarchy.threadId);
                hierarchyItems.Write(id);
                hierarchyItems.Write(work.ParentItemId);
                hierarchyItems.Write(work.Depth);
                hierarchyItems.Write(markerId);
                hierarchyItems.Write(categoryIndex);
                hierarchyItems.Write(SafeString(() => hierarchy.GetItemMarkerFlags(id).ToString()));
                hierarchyItems.Write(SafeString(() => hierarchy.GetItemName(id)));
                hierarchyItems.Write(SafeString(() => hierarchy.GetItemPath(id)));
                hierarchyItems.Write(SafeFloat(() => hierarchy.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime)));
                hierarchyItems.Write(SafeFloat(() => hierarchy.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime)));
                hierarchyItems.Write(SafeFloat(() => hierarchy.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory)));
                hierarchyItems.Write(SafeFloat(() => hierarchy.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls)));
                hierarchyItems.Write(SafeString(() => hierarchy.GetItemEntityId(id).ToString()));
                hierarchyItems.Write(metadataCount);
                hierarchyItems.Write(mergedSamples);
                hierarchyItems.EndRow();

                if (options.IncludeHierarchyRawSampleIndices && mergedSamples > 0) {
                    sampleIndices.Clear();
                    try {
                        hierarchy.GetItemRawFrameDataViewIndices(id, sampleIndices);
                    } catch {
                        sampleIndices.Clear();
                    }

                    for (int i = 0; i < sampleIndices.Count; i++) {
                        hierarchyRawIndices.BeginRow();
                        hierarchyRawIndices.Write(hierarchy.frameIndex);
                        hierarchyRawIndices.Write(hierarchy.threadIndex);
                        hierarchyRawIndices.Write(id);
                        hierarchyRawIndices.Write(i);
                        hierarchyRawIndices.Write(sampleIndices[i]);
                        hierarchyRawIndices.EndRow();
                    }
                }

                children.Clear();
                hierarchy.GetItemChildren(id, children);
                if (children.Count <= 0)
                    continue;

                for (int i = children.Count - 1; i >= 0; i--)
                    stack.Push(new HierarchyItemWork(children[i], id, work.Depth + 1));
            }
        }

        private static void WriteFrameThreadRow(CsvFile csv, int frame, int ordinal, RawFrameDataView raw) {
            csv.BeginRow();
            csv.Write(frame);
            csv.Write(ordinal);
            csv.Write(raw.threadIndex);
            csv.Write(raw.threadGroupName);
            csv.Write(raw.threadName);
            csv.Write(raw.threadId);
            csv.Write(raw.frameStartTimeMs);
            csv.Write(raw.frameStartTimeNs);
            csv.Write(raw.frameTimeMs);
            csv.Write(raw.frameTimeNs);
            csv.Write(raw.frameGpuTimeMs);
            csv.Write(raw.frameGpuTimeNs);
            csv.Write(raw.frameFps);
            csv.Write(raw.sampleCount);
            csv.Write(raw.maxDepth);
            csv.EndRow();
        }

        private static void WriteExportWarning(CsvFile csv, int frame, int threadIndex, string operation, Exception ex) {
            csv.BeginRow();
            csv.Write(frame);
            csv.Write(threadIndex);
            csv.Write(string.Empty);
            csv.Write(string.Empty);
            csv.Write(-1);
            csv.Write(-1);
            csv.Write(0);
            csv.Write(operation + " failed: " + ex.GetType().Name);
            csv.EndRow();
        }

        private static int SafeInt(Func<int> read) {
            try {
                return read();
            } catch {
                return -1;
            }
        }

        private static int SafeInt(Func<ushort> read) {
            try {
                return read();
            } catch {
                return -1;
            }
        }

        private static ulong SafeUInt64(Func<ulong> read) {
            try {
                return read();
            } catch {
                return 0;
            }
        }

        private static float SafeFloat(Func<float> read) {
            try {
                return read();
            } catch {
                return float.NaN;
            }
        }

        private static double SafeDouble(Func<double> read) {
            try {
                return read();
            } catch {
                return double.NaN;
            }
        }

        private static string SafeString(Func<string> read) {
            try {
                return read() ?? string.Empty;
            } catch {
                return string.Empty;
            }
        }

        private static void WriteMetadata(string path, ExportMetadata metadata) {
            File.WriteAllText(path, JsonUtility.ToJson(metadata, true));
        }

        private static void TrySetMaxFrameHistoryLength(int frameCount) {
            try {
                MethodInfo method = typeof(ProfilerDriver).GetMethod(
                    "SetMaxFrameHistoryLength",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) {
                    Debug.LogWarning("[ProfilerCaptureCsvExporter] ProfilerDriver.SetMaxFrameHistoryLength was not found. Loaded capture may be capped by the Editor history limit.");
                    return;
                }

                method.Invoke(null, new object[] { frameCount });
            } catch (Exception ex) {
                Debug.LogWarning("[ProfilerCaptureCsvExporter] Failed to set profiler frame history length: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void WriteFrameThreadsHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "frameOrdinal", "threadIndex", "threadGroupName", "threadName", "threadId", "frameStartTimeMs", "frameStartTimeNs", "frameTimeMs", "frameTimeNs", "frameGpuTimeMs", "frameGpuTimeNs", "frameFps", "sampleCount", "maxDepth");
        }

        private static void WriteRawSamplesHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "threadGroupName", "threadName", "threadId", "sampleIndex", "parentSampleIndex", "depth", "childrenCount", "recursiveChildrenCount", "markerId", "categoryIndex", "markerFlags", "sampleName", "startTimeMs", "startTimeNs", "durationMs", "durationNs", "metadataCount");
        }

        private static void WriteRawMetadataHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "sampleIndex", "metadataIndex", "asString", "asInt", "asLong", "asFloat", "asDouble");
        }

        private static void WriteRawCallstacksHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "sampleIndex", "callstackIndex", "address");
        }

        private static void WriteRawFlowsHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "sampleIndex", "flowIndex", "parentSampleIndex", "flowId", "flowEventType");
        }

        private static void WriteThreadFlowsHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "threadGroupName", "threadName", "flowIndex", "parentSampleIndex", "flowId", "flowEventType");
        }

        private static void WriteHierarchyItemsHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "threadGroupName", "threadName", "threadId", "itemId", "parentItemId", "depth", "markerId", "categoryIndex", "markerFlags", "itemName", "itemPath", "totalTimeMs", "selfTimeMs", "gcAllocBytes", "calls", "entityId", "metadataCount", "mergedSamplesCount");
        }

        private static void WriteHierarchyRawIndicesHeader(CsvFile csv) {
            csv.WriteHeader("frameIndex", "threadIndex", "itemId", "rawSampleOrdinal", "sampleIndex");
        }

        [Serializable]
        private sealed class ExportMetadata {
            public string inputPath;
            public string outputDirectory;
            public string exportStartedUtc;
            public string exportFinishedUtc;
            public string unityVersion;
            public int firstFrame;
            public int lastFrame;
            public int frameCount;
            public int maxThreadIndex;
            public int maxFrameHistoryLength;
            public bool includeMetadata;
            public bool includeCallstacks;
            public bool includeFlows;
            public bool includeHierarchyRawSampleIndices;
        }

        public sealed class ExportOptions {
            public string InputPath;
            public string OutputDirectory;
            public bool LoadInputProfile = true;
            public int MaxThreadIndex = DefaultMaxThreadIndex;
            public int MaxFrameHistoryLength = DefaultMaxFrameHistoryLength;
            public int? FrameStart;
            public int? FrameEnd;
            public bool IncludeMetadata = true;
            public bool IncludeCallstacks = true;
            public bool IncludeFlows = true;
            public bool IncludeHierarchyRawSampleIndices = true;

            public string ResolveOutputDirectory() {
                if (!string.IsNullOrWhiteSpace(OutputDirectory))
                    return Path.GetFullPath(OutputDirectory);

                if (string.IsNullOrWhiteSpace(InputPath))
                    return Path.GetFullPath("ProfilerCaptureCsv");

                string parent = Path.GetDirectoryName(Path.GetFullPath(InputPath)) ?? Directory.GetCurrentDirectory();
                string name = Path.GetFileNameWithoutExtension(InputPath) + "_csv";
                return Path.Combine(parent, name);
            }

            public static ExportOptions FromCommandLine(string[] args) {
                var options = new ExportOptions();
                for (int i = 0; i < args.Length; i++) {
                    string arg = args[i];
                    switch (arg) {
                        case "-profileInput":
                        case "-profilerInput":
                            options.InputPath = RequireValue(args, ref i, arg);
                            break;
                        case "-profileOutputDir":
                        case "-profilerOutputDir":
                            options.OutputDirectory = RequireValue(args, ref i, arg);
                            break;
                        case "-maxThreadIndex":
                            options.MaxThreadIndex = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                            break;
                        case "-maxFrameHistoryLength":
                            options.MaxFrameHistoryLength = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                            break;
                        case "-frameStart":
                            options.FrameStart = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                            break;
                        case "-frameEnd":
                            options.FrameEnd = int.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                            break;
                        case "-skipMetadata":
                            options.IncludeMetadata = false;
                            break;
                        case "-skipCallstacks":
                            options.IncludeCallstacks = false;
                            break;
                        case "-skipFlows":
                            options.IncludeFlows = false;
                            break;
                        case "-skipHierarchyRawSampleIndices":
                            options.IncludeHierarchyRawSampleIndices = false;
                            break;
                    }
                }

                return options;
            }

            private static string RequireValue(string[] args, ref int index, string option) {
                if (index + 1 >= args.Length)
                    throw new ArgumentException("Missing value for " + option);
                index++;
                return args[index];
            }
        }

        private readonly struct RawSampleWork {
            public readonly int SampleIndex;
            public readonly int ParentSampleIndex;
            public readonly int Depth;

            public RawSampleWork(int sampleIndex, int parentSampleIndex, int depth) {
                SampleIndex = sampleIndex;
                ParentSampleIndex = parentSampleIndex;
                Depth = depth;
            }
        }

        private readonly struct HierarchyItemWork {
            public readonly int ItemId;
            public readonly int ParentItemId;
            public readonly int Depth;

            public HierarchyItemWork(int itemId, int parentItemId, int depth) {
                ItemId = itemId;
                ParentItemId = parentItemId;
                Depth = depth;
            }
        }

        private sealed class CsvFile : IDisposable {
            private readonly StreamWriter writer;
            private bool firstField = true;

            private CsvFile(string path) {
                writer = new StreamWriter(path, false, new UTF8Encoding(false), 1024 * 1024);
            }

            public static CsvFile Create(string path) {
                return new CsvFile(path);
            }

            public void WriteHeader(params string[] headers) {
                BeginRow();
                for (int i = 0; i < headers.Length; i++)
                    Write(headers[i]);
                EndRow();
            }

            public void BeginRow() {
                firstField = true;
            }

            public void EndRow() {
                writer.WriteLine();
            }

            public void Write(string value) {
                if (!firstField)
                    writer.Write(',');
                firstField = false;

                value = value ?? string.Empty;
                bool quote = value.IndexOfAny(CsvSpecialChars) >= 0;
                if (!quote) {
                    writer.Write(value);
                    return;
                }

                writer.Write('"');
                for (int i = 0; i < value.Length; i++) {
                    char c = value[i];
                    if (c == '"')
                        writer.Write('"');
                    writer.Write(c);
                }
                writer.Write('"');
            }

            public void Write(int value) {
                Write(value.ToString(CultureInfo.InvariantCulture));
            }

            public void Write(uint value) {
                Write(value.ToString(CultureInfo.InvariantCulture));
            }

            public void Write(ulong value) {
                Write(value.ToString(CultureInfo.InvariantCulture));
            }

            public void Write(float value) {
                Write(value.ToString("R", CultureInfo.InvariantCulture));
            }

            public void Write(double value) {
                Write(value.ToString("R", CultureInfo.InvariantCulture));
            }

            public void Dispose() {
                writer.Dispose();
            }
        }
    }
}
