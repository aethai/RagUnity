using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// EditMode test entry points: <see cref="RunEditModeTests"/> for batchmode CI, and an in-editor trigger for when
    /// the project is already open in the Editor (batchmode cannot open it then): write a filter (comma-separated
    /// fixture/namespace names, empty = all) to Temp/ro-run-editmode-tests.txt and the open Editor runs those tests
    /// and writes Temp/RegressionTests/editor-results.xml, without exiting.
    /// Temp/ro-editor-command.txt drives the open Editor without keyboard focus: <c>refresh</c> (import changed
    /// scripts), <c>play</c>, <c>stop</c>, <c>strays</c> / <c>strays-clean</c> (DontSave leftovers, see
    /// <see cref="ReportStrayObjects"/>); the line is consumed when it has been carried out.
    /// </summary>
    [InitializeOnLoad]
    internal static class RoCiEditModeTestRunner {
        private const string DefaultResultPath = "Temp/RegressionTests/editmode-results.xml";
        private const string EditorTriggerPath = "Temp/ro-run-editmode-tests.txt";
        private const string EditorResultPath = "Temp/RegressionTests/editor-results.xml";
        private const string EditorCommandPath = "Temp/ro-editor-command.txt";
        private static TestRunnerApi api;
        private static CiCallbacks callbacks;
        private static double nextTriggerPoll;

        static RoCiEditModeTestRunner() {
            EditorApplication.update += PollEditorTrigger;
        }

        /// <summary>
        /// The version the player reports (Application.version, the first log line, crash reports): -roBuildVersion when given
        /// (a release names its version), else the project's version plus the commit, e.g. "0.1.0+3633fcb3".
        /// </summary>
        private static string ResolveBuildVersion() {
            string given = ResolveCommandLineValue("-roBuildVersion");
            if (!string.IsNullOrWhiteSpace(given)) return given.Trim();
            string commit = null;
            try {
                var git = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short=8 HEAD") {
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                    WorkingDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                };
                using (var process = System.Diagnostics.Process.Start(git)) {
                    commit = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);
                }
            } catch (Exception) {
                commit = null;
            }

            return string.IsNullOrEmpty(commit) ? PlayerSettings.bundleVersion : PlayerSettings.bundleVersion + "+" + commit;
        }

        private static void BuildPlayer(string directory, bool development, string exeName = "Ragnarok", string productName = null, string[] defines = null) {
            string report;
            bool frameTimingStats = PlayerSettings.enableFrameTimingStats;
            string bundleVersion = PlayerSettings.bundleVersion;
            string projectProductName = PlayerSettings.productName;
            try {
                // the window title and the player's data folder (LocalLow\<company>\<product>) for this build only
                if (!string.IsNullOrWhiteSpace(productName)) PlayerSettings.productName = productName;
                // GPU frame times for !perf, for this build only
                PlayerSettings.enableFrameTimingStats = true;
                PlayerSettings.bundleVersion = ResolveBuildVersion();
                string[] scenes = System.Array.ConvertAll(
                    System.Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                    scene => scene.path);
                var options = new BuildPlayerOptions {
                    scenes = scenes,
                    locationPathName = System.IO.Path.Combine(directory, exeName + ".exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = defines ?? Array.Empty<string>()
                };
                UnityEditor.Build.Reporting.BuildReport result = BuildPipeline.BuildPlayer(options);
                report = result.summary.result + " " + result.summary.totalSize / (1024 * 1024) + " MB in " +
                    result.summary.totalTime.TotalSeconds.ToString("F0") + " s, " + result.summary.totalErrors + " errors, defines [" + string.Join(";", options.extraScriptingDefines) + "], version " +
                    PlayerSettings.bundleVersion;
            } catch (Exception ex) {
                report = "Failed " + ex.Message;
            } finally {
                PlayerSettings.enableFrameTimingStats = frameTimingStats;
                PlayerSettings.bundleVersion = bundleVersion;
                PlayerSettings.productName = projectProductName;
            }

            System.IO.File.WriteAllText("Temp/ro-build-player.txt", report);
            Debug.Log("[RO] build-player: " + report);
        }

        /// <summary>
        /// Batchmode build of a second checkout (no shared Editor): Unity -batchmode -quit -projectPath &lt;Unity&gt;
        /// -executeMethod Ragnarok.Editor.RoCiEditModeTestRunner.BuildPlayerFromCommandLine -roBuildDir &lt;dir&gt; [-roBuildDev] [-roExeName &lt;name&gt;] [-roProductName &lt;name&gt;] [-roDefines &lt;A;B&gt;];
        /// the result line goes to Temp/ro-build-player.txt and the exit code is 0 only when the build succeeded.
        /// </summary>
        public static void BuildPlayerFromCommandLine() {
            string directory = ResolveCommandLineValue("-roBuildDir");
            bool development = Array.IndexOf(Environment.GetCommandLineArgs(), "-roBuildDev") >= 0;
            directory = string.IsNullOrWhiteSpace(directory) ? "Builds/Dev" : directory;
            // -roExeName <name>: the player exe (and its <name>_Data folder), Ragnarok by default
            string exeName = ResolveCommandLineValue("-roExeName");
            exeName = string.IsNullOrWhiteSpace(exeName) ? "Ragnarok" : exeName.Trim();
            // -roProductName <name>: the product name of this build (window title, data folder), the project's by default
            // -roDefines A;B: extra scripting defines of this build only
            string[] defines = ResolveCommandLineValue("-roDefines").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            BuildPlayer(directory, development, exeName, ResolveCommandLineValue("-roProductName"), defines);
            bool succeeded = System.IO.File.ReadAllText("Temp/ro-build-player.txt").StartsWith("Succeeded", StringComparison.Ordinal);
            // -roServerProfile <cfg>: the server this build connects to (a release's), instead of the repo's lab profile
            string serverProfile = ResolveCommandLineValue("-roServerProfile");
            if (succeeded && !string.IsNullOrWhiteSpace(serverProfile)) {
                string target = Path.Combine(directory, exeName + "_Data", "StreamingAssets", "Ragnarok", "server-profile.cfg");
                try {
                    File.Copy(serverProfile.Trim(), target, overwrite: true);
                    Debug.Log("[RO] build-player: server profile " + serverProfile.Trim() + " -> " + target);
                } catch (Exception ex) {
                    Debug.LogError("[RO] build-player: server profile not copied: " + ex.Message);
                    succeeded = false;
                }
            }

            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static void PollEditorTrigger() {
            if (EditorApplication.timeSinceStartup < nextTriggerPoll) {
                return;
            }

            nextTriggerPoll = EditorApplication.timeSinceStartup + 1.0;
            PollEditorCommand();
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                !File.Exists(EditorTriggerPath)) {
                return;
            }

            string rawFilter;
            try {
                rawFilter = File.ReadAllText(EditorTriggerPath);
                File.Delete(EditorTriggerPath);
            } catch (IOException) {
                return;
            }

            string resultPath = Path.GetFullPath(EditorResultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            if (File.Exists(resultPath)) {
                File.Delete(resultPath);
            }

            Execute(BuildTestGroupFilters(rawFilter), resultPath, exitWhenDone: false);
        }

        private static void PollEditorCommand() {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                !File.Exists(EditorCommandPath)) {
                return;
            }

            string command;
            try {
                command = File.ReadAllText(EditorCommandPath).Trim().ToLowerInvariant();
            } catch (IOException) {
                return;
            }

            bool done;
            switch (command) {
                case "refresh":
                    AssetDatabase.Refresh();
                    done = true;
                    break;
                case "play":
                    done = EditorApplication.isPlaying;
                    if (!done && !EditorApplication.isPlayingOrWillChangePlaymode) {
                        EditorApplication.isPlaying = true;
                    }

                    break;
                case "stop":
                    done = !EditorApplication.isPlayingOrWillChangePlaymode;
                    if (!done) {
                        EditorApplication.isPlaying = false;
                    }

                    break;
                case var probe when probe.StartsWith("gcprobe", StringComparison.Ordinal):
                    // gcprobe [frames]: GC allocations per script callback over that many Play frames (RoGcAllocProbe)
                    string[] gcArgs = probe.Substring(7).Trim().Split(new[] { (char) 32 }, StringSplitOptions.RemoveEmptyEntries);
                    RoGcAllocProbe.Start(gcArgs.Length > 0 && int.TryParse(gcArgs[0], out int frames) ? frames : 300,
                        System.Array.IndexOf(gcArgs, "stacks") >= 0);
                    done = true;
                    break;
                case var probe when probe.StartsWith("cpuprobe", StringComparison.Ordinal):
                    // cpuprobe [frames]: self / total time per profiler marker over that many Play frames (RoCpuProbe)
                    RoCpuProbe.Start(int.TryParse(probe.Substring(8).Trim(), out int cpuFrames) ? cpuFrames : 300);
                    done = true;
                    break;
                case var build when build.StartsWith("build-player ", StringComparison.Ordinal):
                    // build-player <directory> [dev]: a Windows player of the build-settings scenes (measurements in a real
                    // build; dev = development player, profiler markers recordable); the result goes to Temp/ro-build-player.txt
                    string[] buildArgs = build.Substring(13).Trim().Split(' ');
                    BuildPlayer(buildArgs[0], buildArgs.Length > 1 && buildArgs[1] == "dev");
                    done = true;
                    break;
                case var open when open.StartsWith("open-scene ", StringComparison.Ordinal):
                    // open-scene <Assets/... .unity>: opens that scene (not while playing)
                    done = true;
                    if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(open.Substring(11).Trim());
                    }

                    break;
                case "sprite-depth-feature":
                    // the actor sprites' depth prepass as one renderer step before the transparents (RoSpriteDepthPrepassSetup)
                    Debug.Log("[RO] sprite-depth-feature: " + RoSpriteDepthPrepassSetup.Apply());
                    done = true;
                    break;
                case "strays":
                case "strays-clean":
                    ReportStrayObjects(command == "strays-clean");
                    done = true;
                    break;
                default:
                    Debug.LogWarning("RO editor command ignored: " + command);
                    done = true;
                    break;
            }

            if (done) {
                try {
                    File.Delete(EditorCommandPath);
                } catch (IOException) {
                }
            }
        }

        /// <summary>
        /// Renderers of GameObjects marked HideFlags.DontSave outside Play mode: helpers that runtime code or tests
        /// created in edit mode and never destroyed. They survive Play sessions and scene loads and are invisible to
        /// FindObjectsByType, so a leaked effect mesh keeps drawing on top of every map. Writes Temp/ro-strays.txt;
        /// <paramref name="destroy"/> removes the leftover roots whose name starts with "RO ".
        /// </summary>
        private static void ReportStrayObjects(bool destroy) {
            var lines = new List<string>();
            var roots = new HashSet<GameObject>();
            foreach (Renderer r in Resources.FindObjectsOfTypeAll<Renderer>()) {
                if (r == null || EditorUtility.IsPersistent(r)) {
                    continue;
                }

                GameObject go = r.gameObject;
                if ((go.hideFlags & HideFlags.DontSave) == 0 && (go.transform.root.gameObject.hideFlags & HideFlags.DontSave) == 0) {
                    continue;
                }

                string path = go.name;
                for (Transform p = go.transform.parent; p != null; p = p.parent) {
                    path = p.name + "/" + path;
                }

                Material m = r.sharedMaterial;
                Texture t = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m != null ? m.mainTexture : null;
                lines.Add($"{path} flags={go.hideFlags} enabled={r.enabled} active={go.activeInHierarchy} scene='{go.scene.name}' " +
                    $"center={r.bounds.center} size={r.bounds.size} mat={(m != null ? m.name : "-")} tex={(t != null ? t.name : "-")}");
                if (go.transform.root.name.StartsWith("RO ", StringComparison.Ordinal)) {
                    roots.Add(go.transform.root.gameObject);
                }
            }

            if (destroy) {
                foreach (GameObject root in roots) {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            lines.Insert(0, $"{lines.Count} DontSave renderer(s), playing={EditorApplication.isPlaying}" +
                (destroy ? $", destroyed {roots.Count} 'RO ' root(s)" : ""));
            File.WriteAllLines("Temp/ro-strays.txt", lines);
            Debug.Log("RO strays: " + lines[0]);
        }

        public static void RunEditModeTests() {
            string resultPath = ResolveCommandLineValue("-testResults");
            if (string.IsNullOrWhiteSpace(resultPath)) {
                resultPath = Path.GetFullPath(DefaultResultPath);
            }

            string resultDirectory = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrWhiteSpace(resultDirectory)) {
                Directory.CreateDirectory(resultDirectory);
            }

            Execute(BuildTestGroupFilters(ResolveCommandLineValue("-testFilter")), resultPath, exitWhenDone: true);
        }

        private static void Execute(string[] groupFilters, string resultPath, bool exitWhenDone) {
            if (api == null) {
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
            }

            if (callbacks != null) {
                api.UnregisterCallbacks(callbacks);
            }

            callbacks = new CiCallbacks(resultPath, exitWhenDone);
            api.RegisterCallbacks(callbacks);

            var filter = new Filter {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "Ragnarok.Tests.EditMode" }
            };

            if (groupFilters.Length > 0) {
                filter.groupNames = groupFilters;
                Debug.Log($"RO CI EditMode test filter: {string.Join(", ", groupFilters)}");
            }

            Debug.Log($"RO CI EditMode test run starting. Results: {resultPath}");
            api.Execute(new ExecutionSettings(filter));
        }

        private static string ResolveCommandLineValue(string key) {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private static string[] BuildTestGroupFilters(string rawFilter) {
            if (string.IsNullOrWhiteSpace(rawFilter)) {
                return Array.Empty<string>();
            }

            string[] parts = rawFilter.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var filters = new List<string>(parts.Length);
            foreach (string part in parts) {
                string trimmed = part.Trim();
                if (trimmed.Length == 0) {
                    continue;
                }

                filters.Add(trimmed.StartsWith("^", StringComparison.Ordinal) || trimmed.EndsWith("$", StringComparison.Ordinal)
                    ? trimmed
                    : "^" + Regex.Escape(trimmed) + @"(\.|$)");
            }

            return filters.ToArray();
        }

        private sealed class CiCallbacks : ICallbacks {
            private readonly string resultPath;
            private readonly bool exitWhenDone;

            public CiCallbacks(string resultPath, bool exitWhenDone) {
                this.resultPath = resultPath;
                this.exitWhenDone = exitWhenDone;
            }

            public void RunStarted(ITestAdaptor testsToRun) {
                int count = testsToRun != null ? testsToRun.TestCaseCount : 0;
                Debug.Log($"RO CI EditMode test run discovered {count} tests.");
            }

            public void RunFinished(ITestResultAdaptor result) {
                int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                int exitCode = result.FailCount > 0 || result.InconclusiveCount > 0 || total <= 0 ? 1 : 0;

                try {
                    WriteResultsXml(result, resultPath);
                    Debug.Log($"RO CI EditMode test run finished. total={total} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                } catch (Exception ex) {
                    Debug.LogException(ex);
                    exitCode = 1;
                }

                if (exitWhenDone) {
                    EditorApplication.delayCall += () => EditorApplication.Exit(exitCode);
                }
            }

            public void TestStarted(ITestAdaptor test) {
            }

            public void TestFinished(ITestResultAdaptor result) {
            }

            private static void WriteResultsXml(ITestResultAdaptor result, string path) {
                var settings = new XmlWriterSettings {
                    Indent = true,
                    NewLineOnAttributes = false
                };

                using (XmlWriter writer = XmlWriter.Create(path, settings)) {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("test-run");
                    int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                    writer.WriteAttributeString("id", "2");
                    writer.WriteAttributeString("testcasecount", total.ToString());
                    writer.WriteAttributeString("result", result.ResultState);
                    writer.WriteAttributeString("total", total.ToString());
                    writer.WriteAttributeString("passed", result.PassCount.ToString());
                    writer.WriteAttributeString("failed", result.FailCount.ToString());
                    writer.WriteAttributeString("inconclusive", result.InconclusiveCount.ToString());
                    writer.WriteAttributeString("skipped", result.SkipCount.ToString());
                    writer.WriteAttributeString("asserts", result.AssertCount.ToString());
                    writer.WriteAttributeString("start-time", result.StartTime.ToString("u"));
                    writer.WriteAttributeString("end-time", result.EndTime.ToString("u"));
                    writer.WriteAttributeString("duration", result.Duration.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    result.ToXml().WriteTo(writer);
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
            }
        }
    }
}
