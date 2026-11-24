using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>An image placed in the UI atlas: UVs (u0, vTop, u1, vBottom) on page <see cref="Page"/>.</summary>
    public readonly struct RoClassicUiImage {
        public RoClassicUiImage(Vector4 uv, float page, int width, int height) {
            Uv = uv;
            Page = page;
            Width = width;
            Height = height;
        }

        public Vector4 Uv { get; }
        public float Page { get; }
        public int Width { get; }
        public int Height { get; }
        public bool IsValid => Width > 0;
    }

    /// <summary>
    /// Paged UI atlas: a Texture2DArray of 2048² RGBA32 pages holding raw (gamma-space) bytes, point sampled, no mips.
    /// Every image gets a 1px border repeating its edge so sharp-bilinear sampling at fractional scales never bleeds.
    /// CPU mirrors per page; only dirty rectangles go to the GPU (staging texture + CopyTexture), so adding a glyph
    /// mid-game costs a few hundred bytes of upload instead of the page.
    /// </summary>
    public sealed class RoClassicUiAtlas : IDisposable {
        public const int PageSize = 2048;

        private sealed class Page {
            public readonly Color32[] Pixels = new Color32[PageSize * PageSize]; // bottom-up rows
            public int ShelfX;
            public int ShelfY;
            public int ShelfHeight;
            public bool Dirty;
            public int DirtyX0 = int.MaxValue;
            public int DirtyY0 = int.MaxValue;
            public int DirtyX1;
            public int DirtyY1;
        }

        private readonly List<Page> pages = new List<Page>();
        private readonly Dictionary<int, Texture2D> staging = new Dictionary<int, Texture2D>();
        private int slices;

        public RoClassicUiAtlas(int initialPages = 2) {
            Allocate(Math.Max(1, initialPages));
        }

        public Texture2DArray Texture { get; private set; }
        public int PageCount => pages.Count;

        /// <summary>Raised when the array had to grow (the renderer rebinds it).</summary>
        public event Action<Texture2DArray> TextureChanged;

        public FilterMode FilterMode {
            get => Texture.filterMode;
            set => Texture.filterMode = value;
        }

        /// <summary>
        /// Classic BMP colour key as applied by the original's texture upload: a pixel is transparent when its RGB555
        /// value is (31, 0, 31), i.e. R >= 248, G <= 7, B >= 248 — not only exact magenta.
        /// </summary>
        public static bool IsClassicColorKey(Color32 c) => c.r >= 248 && c.g <= 7 && c.b >= 248;

        public bool TryAdd(int width, int height, Color32[] topDownPixels, out RoClassicUiImage image) {
            image = default;
            int pw = width + 2;
            int ph = height + 2;
            if (width <= 0 || height <= 0 || pw > PageSize || ph > PageSize) {
                return false;
            }

            int pageIndex = -1;
            int ox = 0, oy = 0;
            for (int i = 0; i < pages.Count && pageIndex < 0; i++) {
                if (TryPlace(pages[i], pw, ph, out ox, out oy)) pageIndex = i;
            }

            if (pageIndex < 0) {
                pages.Add(new Page());
                if (pages.Count > slices) Allocate(slices * 2);
                pageIndex = pages.Count - 1;
                TryPlace(pages[pageIndex], pw, ph, out ox, out oy);
            }

            Blit(pages[pageIndex], ox, oy, width, height, topDownPixels);
            const float inv = 1f / PageSize;
            image = new RoClassicUiImage(
                new Vector4((ox + 1) * inv, (oy + 1 + height) * inv, (ox + 1 + width) * inv, (oy + 1) * inv),
                pageIndex, width, height);
            return true;
        }

        /// <summary>
        /// Rewrites an image added earlier with pixels of the same size, in place (e.g. a picture the game renders
        /// and refreshes, like the equipment window's character preview).
        /// </summary>
        public bool TryUpdate(in RoClassicUiImage image, Color32[] topDownPixels) {
            int pageIndex = (int) image.Page;
            if (!image.IsValid || pageIndex < 0 || pageIndex >= pages.Count || topDownPixels == null ||
                topDownPixels.Length < image.Width * image.Height) {
                return false;
            }

            int ox = Mathf.RoundToInt(image.Uv.x * PageSize) - 1, oy = Mathf.RoundToInt(image.Uv.w * PageSize) - 1;
            Blit(pages[pageIndex], ox, oy, image.Width, image.Height, topDownPixels);
            return true;
        }

        /// <summary>The image with a 1-px edge of its own border pixels (sharp bilinear at fractional scales), bottom-up.</summary>
        private static void Blit(Page page, int ox, int oy, int width, int height, Color32[] topDownPixels) {
            int pw = width + 2, ph = height + 2;
            Color32[] dst = page.Pixels;
            for (int by = 0; by < ph; by++) {
                int sy = Mathf.Clamp(height - by, 0, height - 1); // bottom-up block row -> top-down source row
                int row = (oy + by) * PageSize + ox;
                for (int bx = 0; bx < pw; bx++) {
                    dst[row + bx] = topDownPixels[sy * width + Mathf.Clamp(bx - 1, 0, width - 1)];
                }
            }

            page.Dirty = true;
            page.DirtyX0 = Math.Min(page.DirtyX0, ox);
            page.DirtyY0 = Math.Min(page.DirtyY0, oy);
            page.DirtyX1 = Math.Max(page.DirtyX1, ox + pw);
            page.DirtyY1 = Math.Max(page.DirtyY1, oy + ph);
        }

        private static bool TryPlace(Page page, int pw, int ph, out int x, out int y) {
            if (page.ShelfX + pw > PageSize) {
                page.ShelfX = 0;
                page.ShelfY += page.ShelfHeight;
                page.ShelfHeight = 0;
            }

            x = page.ShelfX;
            y = page.ShelfY;
            if (page.ShelfY + ph > PageSize) {
                return false;
            }

            page.ShelfX += pw;
            page.ShelfHeight = Math.Max(page.ShelfHeight, ph);
            return true;
        }

        /// <summary>Uploads the dirty rectangle of every page touched since the last flush.</summary>
        public void Flush() {
            for (int p = 0; p < pages.Count; p++) {
                Page page = pages[p];
                if (!page.Dirty) continue;
                int x0 = page.DirtyX0, y0 = page.DirtyY0;
                int w = page.DirtyX1 - x0, h = page.DirtyY1 - y0;
                Texture2D stage = Staging(w, h);
                var block = new Color32[w * h];
                for (int y = 0; y < h; y++) {
                    Array.Copy(page.Pixels, (y0 + y) * PageSize + x0, block, y * w, w);
                }

                stage.SetPixels32(0, 0, w, h, block);
                stage.Apply(false, false);
                Graphics.CopyTexture(stage, 0, 0, 0, 0, w, h, Texture, p, 0, x0, y0);
                page.Dirty = false;
                page.DirtyX0 = page.DirtyY0 = int.MaxValue;
                page.DirtyX1 = page.DirtyY1 = 0;
            }
        }

        private Texture2D Staging(int w, int h) {
            int sw = Mathf.NextPowerOfTwo(w), sh = Mathf.NextPowerOfTwo(h);
            int key = sw * 65536 + sh;
            if (!staging.TryGetValue(key, out Texture2D tex)) {
                tex = new Texture2D(sw, sh, TextureFormat.RGBA32, false, true) { name = "RO UI atlas staging" };
                staging.Add(key, tex);
            }

            return tex;
        }

        private void Allocate(int sliceCount) {
            Texture2DArray previous = Texture;
            var next = new Texture2DArray(PageSize, PageSize, sliceCount, TextureFormat.RGBA32, false, true) {
                name = "RO UI atlas", filterMode = previous != null ? previous.filterMode : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
            };
            var clear = new Color32[PageSize * PageSize];
            for (int s = 0; s < sliceCount; s++) next.SetPixels32(clear, s);
            next.Apply(false, true);
            if (previous != null) {
                for (int s = 0; s < slices; s++) Graphics.CopyTexture(previous, s, next, s);
                UnityEngine.Object.Destroy(previous);
            }

            slices = sliceCount;
            Texture = next;
            TextureChanged?.Invoke(next);
        }

        public void Dispose() {
            foreach (Texture2D tex in staging.Values) UnityEngine.Object.Destroy(tex);
            staging.Clear();
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }
    }
}
