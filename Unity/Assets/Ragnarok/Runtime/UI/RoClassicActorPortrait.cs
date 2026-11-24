using System;
using Ragnarok.Assets;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using Ragnarok.UI.Classic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.UI {
    /// <summary>
    /// A character drawn once into classic dynamic images: the game's own layered actor renders into a render texture with
    /// a throw-away orthographic camera (one pixel per SPR pixel), the result is read back asynchronously and stored under
    /// a key. Costs one render and one read-back per image, nothing per frame.
    /// </summary>
    public static class RoClassicActorPortrait {
        private const int PreviewLayer = 30;

        /// <summary>
        /// Renders <paramref name="request"/> into a <paramref name="width"/> × <paramref name="height"/> image whose feet sit at
        /// (width / 2, <paramref name="feetY"/>); <paramref name="done"/> gets whether the image was stored.
        /// </summary>
        public static bool Render(RoAssetDatabase assets, RoActorAppearanceRequest request, string key, int width, int height, int feetY, Action<bool> done) {
            return Render(assets, request, key, width, height, feetY, false, count => done?.Invoke(count > 0));
        }

        /// <summary>
        /// Every frame of the request's action, stored as <c>key#0</c> … <c>key#n−1</c> (the body's frame index on all layers,
        /// as the original's two-layer draws do); <paramref name="done"/> gets n, or 0 when nothing was stored.
        /// </summary>
        public static bool RenderFrames(RoAssetDatabase assets, RoActorAppearanceRequest request, string key, int width, int height, int feetY, Action<int> done) {
            return Render(assets, request, key, width, height, feetY, true, done);
        }

        public static string FrameKey(string key, int frame) => key + "#" + frame;

        private static bool Render(RoAssetDatabase assets, RoActorAppearanceRequest request, string key, int width, int height, int feetY, bool allFrames, Action<int> done) {
            if (assets == null || request == null || RoClassicUiSystem.Instance == null) return false;
            const float baseX = 20000f;
            var cameraObject = new GameObject("ClassicPortraitCamera", typeof(UnityEngine.Camera)) { hideFlags = HideFlags.HideAndDontSave, layer = PreviewLayer };
            cameraObject.transform.SetPositionAndRotation(new Vector3(baseX, 0f, -10f), Quaternion.identity);
            UnityEngine.Camera camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = height / (2f * ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT);
            camera.aspect = (float) width / height;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << PreviewLayer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            // the feet: feetY pixels from the top of the image
            float feetWorldY = (height * 0.5f - feetY) / ROIO.Models.FileTypes.SPR.PIXELS_PER_UNIT;
            var actorObject = new GameObject("ClassicPortraitActor", typeof(RoActorLayeredVisual)) { hideFlags = HideFlags.HideAndDontSave, layer = PreviewLayer };
            actorObject.transform.SetPositionAndRotation(new Vector3(baseX, feetWorldY, 0f), Quaternion.identity);
            RenderTexture[] textures = null;
            try {
                RoActorLayeredVisual visual = actorObject.GetComponent<RoActorLayeredVisual>();
                if (!visual.Initialize(assets, request)) return false;
                foreach (Transform t in actorObject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PreviewLayer;
                RoActorSpriteVisual[] layers = actorObject.GetComponentsInChildren<RoActorSpriteVisual>(true);
                for (int i = 0; i < layers.Length; i++) {
                    layers[i].SetFaceCamera(false);
                    layers[i].SetMapLight(Color.white, Color.clear);
                    layers[i].SetRenderQueue((int) RenderQueue.Transparent);
                    layers[i].SetSortingOrder(1000 + i);
                }

                // the anchors along this camera's axes, not the tilted game camera's
                visual.UseCamera(camera);

                int frames = allFrames ? FrameCount(visual.PrimaryVisual) : 1;
                if (frames <= 0) return false;
                textures = new RenderTexture[frames];
                for (int f = 0; f < frames; f++) {
                    if (allFrames) {
                        for (int i = 0; i < layers.Length; i++) layers[i].SetFramePlayback(RoActFramePlaybackMode.FixedFrame, f);
                        visual.UseCamera(camera);
                    }

                    textures[f] = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    textures[f].Create();
                    camera.targetTexture = textures[f];
                    camera.Render();
                }
            } finally {
                camera.targetTexture = null;
                // Destroy waits for the end of the frame: another portrait rendered meanwhile would see this actor too
                actorObject.SetActive(false);
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(actorObject);
            }

            int pending = textures.Length, stored = 0;
            bool failed = false;
            for (int f = 0; f < textures.Length; f++) {
                RenderTexture texture = textures[f];
                string frameKey = allFrames ? FrameKey(key, f) : key;
                AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32, r => {
                    if (!r.hasError && RoClassicUiSystem.Instance != null) {
                        var data = r.GetData<Color32>();
                        var pixels = new Color32[width * height];
                        // the read-back is bottom-up
                        for (int y = 0; y < height; y++) {
                            for (int x = 0; x < width; x++) pixels[y * width + x] = data[(height - 1 - y) * width + x];
                        }

                        if (RoClassicUiSystem.Instance.Images.SetDynamic(frameKey, width, height, pixels)) stored++;
                        else failed = true;
                    } else {
                        failed = true;
                    }

                    texture.Release();
                    UnityEngine.Object.Destroy(texture);
                    if (--pending == 0) done?.Invoke(failed ? 0 : stored);
                });
            }

            return true;
        }

        private static int FrameCount(RoActorSpriteVisual body) {
            ROIO.Models.FileTypes.ACT act = body?.Animator?.Act;
            if (act?.actions == null || act.actions.Length == 0) return 0;
            int action = Mathf.Clamp(body.ActionIndex, 0, act.actions.Length - 1);
            return act.actions[action]?.frames?.Length ?? 0;
        }
    }
}
