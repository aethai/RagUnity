using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ragnarok.Assets;
using Ragnarok.Content;
using Ragnarok.Effects;
using Ragnarok.Effects.Classic;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// Renders contact sheets of effects run by the ported original-client engine (Effects/Classic).
    /// Batch: Unity -batchmode -projectPath ... -executeMethod Ragnarok.Editor.RoClassicFxCapture.CaptureFromEnvironment -quit
    /// Env: RO_FX_IDS="89,83" (effect ids), RO_FX_OUT (output directory).
    /// </summary>
    public static class RoClassicFxCapture {
        const int FrameWidth = 480, FrameHeight = 360, Columns = 4;
        static readonly int[] SnapshotTicks = { 5, 20, 40, 60, 90, 120, 160, 200 };

        [MenuItem("Ragnarok/Diagnostics/Classic Effects/Capture Storm Gust")]
        public static void CaptureStormGust() => Capture(new[] { 89 }, DefaultOutput());

        public static void CaptureFromEnvironment() {
            string ids = Environment.GetEnvironmentVariable("RO_FX_IDS") ?? "89";
            string output = Environment.GetEnvironmentVariable("RO_FX_OUT") ?? DefaultOutput();
            Capture(ids.Split(',').Select(s => int.Parse(s.Trim())).ToArray(), output);
        }

        static string DefaultOutput() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Temp", "ClassicFxCaptures"));

        public static void Capture(int[] effectIds, string outputDirectory) {
            Directory.CreateDirectory(outputDirectory);
            var settings = AssetDatabase.LoadAssetAtPath<RagnarokContentSettings>("Assets/LocalRagnarokContent.asset");
            using var assets = new RoAssetDatabase();
            assets.Initialize(settings.RootPath, settings.GrfFiles);

            foreach (int id in effectIds) {
                CaptureOne(assets, id, outputDirectory);
            }
        }

        static void CaptureOne(RoAssetDatabase assets, int effectId, string outputDirectory) {
            var root = new GameObject($"Classic FX capture {effectId}");
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            try {
                var cameraObject = new GameObject("Capture Camera");
                cameraObject.transform.SetParent(root.transform, false);
                UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.2f, 0.18f, 1f);
                camera.fieldOfView = 15f;
                camera.nearClipPlane = 0.2f;
                camera.farClipPlane = 500f;
                const float distance = 70f, pitch = 50f;
                camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
                camera.transform.position = Vector3.zero - camera.transform.forward * distance;
                target = new RenderTexture(FrameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;

                CreateGroundGrid(root.transform);

                var bridge = RoClassicEffectBridge.GetOrCreate(root.transform);
                bridge.Initialize(assets, camera);
                bridge.SetMapFrame(Vector3.zero);
                // a cast from the origin at a target 4 cells north-east, skill level 5 (RO_FX_LEVEL overrides)
                int level = int.TryParse(Environment.GetEnvironmentVariable("RO_FX_LEVEL"), out int l) ? l : 5;
                var options = new RoEffectSpawnOptions {
                    SkillLevel = level,
                    TargetWorldPosition = new Vector3(3f, 0f, 3f),
                    HasTargetWorldPosition = true,
                };
                if (!bridge.TrySpawn(effectId, Vector3.zero, options)) {
                    Debug.LogWarning($"[ClassicFxCapture] effect {effectId} is not ported");
                    return;
                }

                var frame = new Texture2D(FrameWidth, FrameHeight, TextureFormat.RGBA32, false);
                int tick = 0, shot = 0;
                var log = new List<string>();

                // RO_FX_MODE=frames: a frame sequence (every RO_FX_STEP ticks, at most RO_FX_FRAMES frames, stops
                // a few frames after the effect dies) into <out>/<id>/NNN.png — for video encoding
                if (Environment.GetEnvironmentVariable("RO_FX_MODE") == "frames") {
                    int step = int.TryParse(Environment.GetEnvironmentVariable("RO_FX_STEP"), out int st) ? st : 3;
                    int maxFrames = int.TryParse(Environment.GetEnvironmentVariable("RO_FX_FRAMES"), out int mf) ? mf : 80;
                    string dir = Path.Combine(outputDirectory, effectId.ToString());
                    Directory.CreateDirectory(dir);
                    int deadFrames = 0;
                    for (int i = 0; i < maxFrames; i++) {
                        bridge.RunFrame(camera, step * (double) FxWorld.TickMilliseconds);
                        camera.Render();
                        RenderTexture.active = target;
                        frame.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0);
                        frame.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{i:D3}.png"), frame.EncodeToPNG());
                        if (bridge.ActiveEffects == 0 && bridge.ActivePrims == 0 && ++deadFrames >= 4) {
                            break;
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(frame);
                    return;
                }

                var sheet = new Texture2D(FrameWidth * Columns, FrameHeight * ((SnapshotTicks.Length + Columns - 1) / Columns), TextureFormat.RGBA32, false);
                while (shot < SnapshotTicks.Length) {
                    int advance = SnapshotTicks[shot] - tick;
                    bridge.RunFrame(camera, advance * (double) FxWorld.TickMilliseconds);
                    tick = SnapshotTicks[shot];
                    camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, FrameWidth, FrameHeight), 0, 0);
                    frame.Apply();
                    int col = shot % Columns, row = (SnapshotTicks.Length - 1) / Columns - shot / Columns;
                    sheet.SetPixels(col * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight, frame.GetPixels());
                    log.Add($"tick {tick}: effects {bridge.ActiveEffects}, prims {bridge.ActivePrims}");
                    shot++;
                }

                sheet.Apply();
                string path = Path.Combine(outputDirectory, $"classic-fx-{effectId}.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                File.WriteAllLines(Path.ChangeExtension(path, ".txt"), log);
                Debug.Log($"[ClassicFxCapture] {path}\n{string.Join("\n", log)}");
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(frame);
            } finally {
                RenderTexture.active = previous;
                if (target != null) {
                    target.Release();
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>A 1-unit (one GAT cell) grid on y = 0 for scale reference.</summary>
        static void CreateGroundGrid(Transform parent) {
            var go = new GameObject("Grid");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int i = -10; i <= 10; i++) {
                vertices.Add(new Vector3(i, 0f, -10f)); vertices.Add(new Vector3(i, 0f, 10f));
                vertices.Add(new Vector3(-10f, 0f, i)); vertices.Add(new Vector3(10f, 0f, i));
            }

            for (int i = 0; i < vertices.Count; i++) {
                indices.Add(i);
            }

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) {
                color = new Color(0.35f, 0.4f, 0.35f, 1f)
            };
        }
    }
}
