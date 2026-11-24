using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ragnarok.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ragnarok.Editor {
    internal static class RoWindSwayCandidateDumpMenu {
        private const string DumpMenu = "Ragnarok/Diagnostics/Dump Wind Sway Candidates";

        [MenuItem(DumpMenu, false, 203)]
        private static void DumpCandidates() {
            if (!Application.isPlaying) {
                Debug.LogWarning("Wind sway candidate dump requires Play Mode with a loaded map.");
                return;
            }

            GameObject modelRoot = ResolveModelRoot(out string mapName);
            List<RoModelRenderer.WindSwayCandidateBucket> buckets = modelRoot != null
                ? RoModelRenderer.CollectWindSwayCandidateBuckets(modelRoot)
                : CollectSceneCandidateBuckets();

            string safeMapName = SanitizeFileName(string.IsNullOrWhiteSpace(mapName)
                ? SceneManager.GetActiveScene().name
                : mapName);
            string outputDirectory = Path.Combine(ResolveRepositoryRoot(), "Artifacts", "WindSway");
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, $"candidates-{safeMapName}.json");

            RoWindSwayCatalog catalog = RoWindSwayCatalog.Load();
            File.WriteAllText(outputPath, BuildJson(mapName, buckets, catalog), Encoding.UTF8);
            Debug.Log($"Wind sway candidate dump wrote {buckets.Count} bucket(s) to {outputPath}");
        }

        [MenuItem(DumpMenu, true)]
        private static bool DumpCandidatesValidate() {
            return true;
        }

        private static GameObject ResolveModelRoot(out string mapName) {
            RoRuntimeMapController controller = UnityEngine.Object.FindFirstObjectByType<RoRuntimeMapController>();
            if (controller != null) {
                mapName = controller.MapName;
                if (controller.CurrentMap != null) {
                    return controller.CurrentMap;
                }
            } else {
                mapName = SceneManager.GetActiveScene().name;
            }

            return null;
        }

        private static List<RoModelRenderer.WindSwayCandidateBucket> CollectSceneCandidateBuckets() {
            RoModelSurfaceInfo[] surfaces = UnityEngine.Object.FindObjectsByType<RoModelSurfaceInfo>(FindObjectsSortMode.None);
            return RoModelRenderer.CollectWindSwayCandidateBuckets(surfaces);
        }

        private static string ResolveRepositoryRoot() {
            string assetsPath = Application.dataPath;
            string unityProjectRoot = Directory.GetParent(assetsPath)?.FullName ?? assetsPath;
            return Directory.GetParent(unityProjectRoot)?.FullName ?? unityProjectRoot;
        }

        private static string BuildJson(
            string mapName,
            IReadOnlyList<RoModelRenderer.WindSwayCandidateBucket> buckets,
            RoWindSwayCatalog catalog
        ) {
            var builder = new StringBuilder(4096);
            builder.AppendLine("{");
            AppendJsonString(builder, "mapName", mapName, 2, trailingComma: true);
            AppendJsonString(builder, "generatedUtc", DateTime.UtcNow.ToString("O"), 2, trailingComma: true);
            builder.AppendLine("  \"buckets\": [");

            for (int i = 0; i < buckets.Count; i++) {
                RoModelRenderer.WindSwayCandidateBucket bucket = buckets[i];
                bool matched = catalog != null && catalog.TryFindProfile(bucket.TextureName, out _);
                builder.AppendLine("    {");
                AppendJsonString(builder, "textureName", bucket.TextureName, 6, trailingComma: true);
                AppendJsonString(builder, "exampleModelFile", bucket.ExampleModelFile, 6, trailingComma: true);
                AppendJsonString(builder, "exampleNodeName", bucket.ExampleNodeName, 6, trailingComma: true);
                builder.AppendLine($"      \"surfaceCount\": {bucket.SurfaceCount},");
                builder.AppendLine($"      \"totalVertexCount\": {bucket.TotalVertexCount},");
                builder.AppendLine($"      \"alreadyCataloged\": {JsonBool(matched)}");
                builder.Append(i + 1 < buckets.Count ? "    }," : "    }");
                builder.AppendLine();
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"candidates\": [");
            bool wroteCandidate = false;
            for (int i = 0; i < buckets.Count; i++) {
                RoModelRenderer.WindSwayCandidateBucket bucket = buckets[i];
                if (catalog != null && catalog.TryFindProfile(bucket.TextureName, out _)) {
                    continue;
                }

                if (wroteCandidate) {
                    builder.AppendLine(",");
                }

                builder.AppendLine("    {");
                AppendJsonString(builder, "textureContains", bucket.TextureName, 6, trailingComma: true);
                AppendJsonString(builder, "mode", "Banner|Foliage|Sign", 6, trailingComma: true);
                builder.AppendLine("      \"amplitude\": 0.0,");
                builder.AppendLine("      \"frequency\": 0.0,");
                AppendJsonString(builder, "anchor", "Top|Bottom|Radial", 6, trailingComma: false);
                builder.Append("    }");
                wroteCandidate = true;
            }

            if (wroteCandidate) {
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendJsonString(StringBuilder builder, string key, string value, int indent, bool trailingComma) {
            builder.Append(' ', indent);
            builder.Append('"');
            builder.Append(EscapeJson(key));
            builder.Append("\": \"");
            builder.Append(EscapeJson(value ?? string.Empty));
            builder.Append('"');
            if (trailingComma) {
                builder.Append(',');
            }

            builder.AppendLine();
        }

        private static string EscapeJson(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                switch (c) {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string SanitizeFileName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "map";
            }

            string safe = value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                safe = safe.Replace(invalid, '_');
            }

            return safe.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)
                ? safe.Substring(0, safe.Length - 4)
                : safe;
        }

        private static string JsonBool(bool value) {
            return value ? "true" : "false";
        }
    }
}
