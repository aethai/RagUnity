using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.UI.Classic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// An ACT/SPR pair drawn on the screen like DrawActScreen 0xa031c0: every layer of (action, frame) at the point plus its
    /// layer offset, centred on the layer's image, scaled, tinted. The palette images become atlas images once (a key per
    /// sprite and image), so drawing is a quad per layer.
    /// </summary>
    internal sealed class RoClassicActSprite {
        private static readonly Dictionary<string, RoClassicActSprite> Cache = new Dictionary<string, RoClassicActSprite>(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        private ACT act;
        private SPR spr;
        private string[] keys;
        private Vector2Int[] sizes;
        private bool failed;

        /// <summary>The sprite at <paramref name="path"/> ("data/sprite/이팩트/RankFont", no extension), loaded once; null while the game's assets are not ready.</summary>
        public static RoClassicActSprite Get(string path) => Get(path, null);

        /// <summary>As <see cref="Get(string)"/>, read from <paramref name="assets"/> while the game's are not up yet (the login cursor).</summary>
        public static RoClassicActSprite Get(string path, RoAssetDatabase assets) {
            if (Cache.TryGetValue(path, out RoClassicActSprite cached)) return cached;
            assets = RoClassicUiHost.GameAssets() ?? assets;
            if (assets == null || !assets.IsInitialized || RoClassicUiSystem.Instance == null) return null;
            var sprite = new RoClassicActSprite();
            try {
                sprite.spr = assets.Load<SPR>(path + ".spr");
                sprite.act = assets.Load<ACT>(path + ".act");
            } catch (Exception e) {
                Debug.LogWarning("Classic ACT sprite " + path + ": " + e.Message);
            }

            sprite.failed = sprite.spr == null || sprite.act == null || sprite.act.actions == null;
            if (!sprite.failed) {
                int n = sprite.spr.frames != null ? sprite.spr.frames.Length : 0;
                sprite.keys = new string[n];
                sprite.sizes = new Vector2Int[n];
                string stem = "@act-" + path.Replace('\\', '/') + "-";
                for (int i = 0; i < n; i++) sprite.Register(i, stem + i.ToString(CultureInfo.InvariantCulture));
            }

            Cache[path] = sprite;
            return sprite;
        }

        private void Register(int index, string key) {
            if (!spr.TryGetFrame(index, out SPR.Frame image) || image == null || image.data == null) return;
            int w = image.originalWidth > 0 ? image.originalWidth : image.width;
            int h = image.originalHeight > 0 ? image.originalHeight : image.height;
            var pixels = new Color32[w * h];
            if (image.type == SPR.TYPE_PAL) {
                byte[] palette = spr.palette;
                for (int p = 0; p < pixels.Length && p < image.data.Length; p++) {
                    int c = image.data[p];
                    if (c != 0) pixels[p] = new Color32(palette[c * 4], palette[c * 4 + 1], palette[c * 4 + 2], 255);
                }
            } else if (image.data.Length >= pixels.Length * 4) {
                for (int p = 0; p < pixels.Length; p++) pixels[p] = new Color32(image.data[p * 4], image.data[p * 4 + 1], image.data[p * 4 + 2], image.data[p * 4 + 3]);
            }

            if (!RoClassicUiSystem.Instance.Images.SetDynamic(key, w, h, pixels)) return;
            keys[index] = key;
            sizes[index] = new Vector2Int(w, h);
        }

        public bool Ready => !failed;

        public int FrameCount(int action) =>
            failed || action < 0 || action >= act.actions.Length || act.actions[action].frames == null ? 0 : act.actions[action].frames.Length;

        /// <summary>The size of layer 0's image of (action, frame), or zero.</summary>
        public Vector2Int Size(int action, int frame) {
            ACT.Layer layer = Layer0(action, frame);
            return layer != null && layer.index >= 0 && layer.index < sizes.Length ? sizes[layer.index] : Vector2Int.zero;
        }

        private ACT.Layer Layer0(int action, int frame) {
            if (failed || action < 0 || action >= act.actions.Length) return null;
            ACT.Frame[] frames = act.actions[action].frames;
            if (frames == null || frame < 0 || frame >= frames.Length || frames[frame]?.layers == null || frames[frame].layers.Length == 0) return null;
            return frames[frame].layers[0];
        }

        /// <summary>Window drawing: every layer of (action, frame) centred at window pixel (x + layer x, y + layer y), unscaled (a UISpriteButton).</summary>
        public void DrawIn(RoClassicWindow w, int action, int frame, int x, int y) {
            if (failed || action < 0 || action >= act.actions.Length) return;
            ACT.Frame[] frames = act.actions[action].frames;
            if (frames == null || frame < 0 || frame >= frames.Length || frames[frame]?.layers == null) return;
            foreach (ACT.Layer layer in frames[frame].layers) {
                int index = layer.sprType == 1 && spr.indexedCount > 0 ? layer.index + spr.indexedCount : layer.index;
                if (index < 0 || index >= keys.Length || keys[index] == null) continue;
                Vector2Int size = sizes[index];
                w.DrawBitmap(x + (int) layer.pos.x - size.x / 2, y + (int) layer.pos.y - size.y / 2, keys[index]);
            }
        }

        /// <summary>
        /// UISpriteButton::vf20 0x7bec60: layer 0's image centred in the button rect, every other layer's image at layer 0's
        /// corner plus the difference of the layer offsets, unscaled, clipped to the rect (the caller sets the clip).
        /// </summary>
        public void DrawButton(RoClassicWindow w, int action, int frame, RectInt rect) {
            if (failed || action < 0 || action >= act.actions.Length) return;
            ACT.Frame[] frames = act.actions[action].frames;
            if (frames == null || frame < 0 || frame >= frames.Length || frames[frame]?.layers == null) return;
            int x0 = 0, y0 = 0, lx0 = 0, ly0 = 0;
            bool first = true;
            foreach (ACT.Layer layer in frames[frame].layers) {
                int index = layer.sprType == 1 && spr.indexedCount > 0 ? layer.index + spr.indexedCount : layer.index;
                if (index < 0 || index >= keys.Length || keys[index] == null) continue;
                Vector2Int size = sizes[index];
                int x, y;
                if (first) {
                    x0 = x = rect.x + rect.width / 2 - size.x / 2;
                    y0 = y = rect.y + rect.height / 2 - size.y / 2;
                    lx0 = (int) layer.pos.x;
                    ly0 = (int) layer.pos.y;
                    first = false;
                } else {
                    x = x0 + (int) layer.pos.x - lx0;
                    y = y0 + (int) layer.pos.y - ly0;
                }

                w.DrawBitmap(x, y, keys[index]);
            }
        }

        /// <summary>
        /// 0x9acb90: (action, frame) fitted into a window box: the union of its layers centred in the box, scaled down
        /// uniformly by the limiting axis when it is larger.
        /// </summary>
        public void DrawFitted(RoClassicWindow w, int action, int frame, RectInt box) {
            if (failed || action < 0 || action >= act.actions.Length) return;
            ACT.Frame[] frames = act.actions[action].frames;
            if (frames == null || frame < 0 || frame >= frames.Length || frames[frame]?.layers == null) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (ACT.Layer layer in frames[frame].layers) {
                int index = layer.sprType == 1 && spr.indexedCount > 0 ? layer.index + spr.indexedCount : layer.index;
                if (index < 0 || index >= keys.Length || keys[index] == null) continue;
                float lw = sizes[index].x * (layer.scale.x != 0 ? layer.scale.x : 1f), lh = sizes[index].y * (layer.scale.y != 0 ? layer.scale.y : 1f);
                minX = Mathf.Min(minX, layer.pos.x - lw / 2f);
                minY = Mathf.Min(minY, layer.pos.y - lh / 2f);
                maxX = Mathf.Max(maxX, layer.pos.x + lw / 2f);
                maxY = Mathf.Max(maxY, layer.pos.y + lh / 2f);
            }

            if (minX > maxX) return;
            float uw = maxX - minX, uh = maxY - minY, scale = Mathf.Min(1f, Mathf.Min(box.width / uw, box.height / uh));
            float ox = box.x + (box.width - uw * scale) / 2f - minX * scale, oy = box.y + (box.height - uh * scale) / 2f - minY * scale;
            Color32 white = new Color32(255, 255, 255, 255);
            foreach (ACT.Layer layer in frames[frame].layers) {
                int index = layer.sprType == 1 && spr.indexedCount > 0 ? layer.index + spr.indexedCount : layer.index;
                if (index < 0 || index >= keys.Length || keys[index] == null) continue;
                float lw = sizes[index].x * (layer.scale.x != 0 ? layer.scale.x : 1f) * scale, lh = sizes[index].y * (layer.scale.y != 0 ? layer.scale.y : 1f) * scale;
                float cx = ox + layer.pos.x * scale, cy = oy + layer.pos.y * scale;
                w.DrawStretched((int) (cx - lw / 2f), (int) (cy - lh / 2f), (int) lw, (int) lh, keys[index], white);
            }
        }

        /// <summary>Every layer of (action, frame) centred at (x + layer x, y + layer y), scaled by <paramref name="scale"/> and tinted.</summary>
        public void Emit(RoClassicUiQuads q, int action, int frame, float x, float y, float scale, Color32 color) {
            if (failed || action < 0 || action >= act.actions.Length) return;
            ACT.Frame[] frames = act.actions[action].frames;
            if (frames == null || frame < 0 || frame >= frames.Length || frames[frame]?.layers == null) return;
            RoClassicUiImages images = RoClassicUiSystem.Instance.Images;
            foreach (ACT.Layer layer in frames[frame].layers) {
                int index = layer.sprType == 1 && spr.indexedCount > 0 ? layer.index + spr.indexedCount : layer.index;
                if (index < 0 || index >= keys.Length || keys[index] == null || !images.TryGet(keys[index], out RoClassicUiImage image)) continue;
                Vector2Int size = sizes[index];
                float w = size.x * scale * (layer.scale.x != 0 ? layer.scale.x : 1f), h = size.y * scale * (layer.scale.y != 0 ? layer.scale.y : 1f);
                float cx = x + layer.pos.x * scale, cy = y + layer.pos.y * scale;
                q.Image((int) (cx - w / 2f), (int) (cy - h / 2f), (int) w, (int) h, image, color);
            }
        }
    }
}
