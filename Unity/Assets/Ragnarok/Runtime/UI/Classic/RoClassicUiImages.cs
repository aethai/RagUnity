using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// UI bitmaps by the path the client requests (e.g. <c>유저인터페이스\basic_interface\basewin_bg2.bmp</c>, relative to
    /// <c>data\texture\</c>), decoded from the GRFs into the classic atlas once. BMPs get the client's colour key:
    /// the upload turns every pixel whose RGB555 value is (31,0,31) transparent (R &gt;= 248, G &lt;= 7, B &gt;= 248).
    /// </summary>
    public sealed partial class RoClassicUiImages {
        private readonly RoClassicUiAtlas atlas;
        private readonly Dictionary<string, RoClassicUiImage> images = new Dictionary<string, RoClassicUiImage>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public RoClassicUiImages(RoClassicUiAtlas atlas) => this.atlas = atlas;

        /// <summary>
        /// Reads a client file by its path (e.g. <c>data/texture/.../x.bmp</c>); null when missing. Resolved lazily through
        /// <see cref="DefaultSourceProvider"/> when unset; tools plug in their own.
        /// </summary>
        public Func<string, byte[]> Source { get; set; }

        /// <summary>
        /// The host's file source (the game's GRF stack), asked until it has one: the core has no reference to the
        /// game, the host assembly installs this at startup.
        /// </summary>
        public static Func<Func<string, byte[]>> DefaultSourceProvider { get; set; }

        public IReadOnlyCollection<string> Missing => missing;

        /// <summary>
        /// The path of a bitmap tinted like the client's 0x9ac3e0 → 0x6c4a60 (per pixel, exact magenta kept): v = max of
        /// the channel mean and 120 (mode 1) or 100; mode 1 grey (v,v,v), 2 red (200,v,v), 4 blue (v,v,200).
        /// </summary>
        public static string Tinted(string texturePath, int mode) =>
            texturePath + "|" + mode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static void ApplyTint(Color32[] pixels, int mode) {
            for (int i = 0; i < pixels.Length; i++) {
                Color32 c = pixels[i];
                if (IsExactKey(c)) continue;
                byte v = (byte) Math.Max((c.r + c.g + c.b) / 3, mode == 1 ? 120 : 100);
                pixels[i] = mode == 2 ? new Color32(200, v, v, c.a) : mode == 4 ? new Color32(v, v, 200, c.a) : new Color32(v, v, v, c.a);
            }
        }

        /// <summary>
        /// Puts game-made pixels (top-down RGBA, alpha 0 = transparent) under <paramref name="key"/> for DrawBitmap: the
        /// first call allocates atlas space, later calls of the same size rewrite it in place.
        /// </summary>
        public bool SetDynamic(string key, int width, int height, Color32[] topDownPixels) {
            if (string.IsNullOrEmpty(key) || topDownPixels == null || width <= 0 || height <= 0) return false;
            if (images.TryGetValue(key, out RoClassicUiImage existing) && existing.Width == width && existing.Height == height) {
                return atlas.TryUpdate(existing, topDownPixels);
            }

            if (!atlas.TryAdd(width, height, topDownPixels, out RoClassicUiImage image)) return false;
            images[key] = image;
            missing.Remove(key);
            return true;
        }

        public bool TryGet(string texturePath, out RoClassicUiImage image) {
            image = default;
            if (string.IsNullOrEmpty(texturePath)) return false;
            if (images.TryGetValue(texturePath, out image)) return true;
            if (missing.Contains(texturePath)) return false;
            if (Source == null) Source = DefaultSourceProvider?.Invoke();
            if (Source == null) return false; // assets not ready yet: try again later
            int tint = 0;
            string file = texturePath;
            int bar = texturePath.IndexOf('|');
            if (bar >= 0) {
                file = texturePath.Substring(0, bar);
                int.TryParse(texturePath.Substring(bar + 1), out tint);
            }

            byte[] data = Source("data\\texture\\" + file);
            bool tga = file.EndsWith(".tga", StringComparison.OrdinalIgnoreCase);
            int w = 0, h = 0;
            Color32[] pixels = null;
            bool decoded = data != null && (tga ? TryDecodeTga(data, out w, out h, out pixels) : TryDecodeRaw(data, out w, out h, out pixels));
            if (decoded && tint != 0) ApplyTint(pixels, tint);
            if (decoded && !tga) {
                exactKeyRects[texturePath] = FindKeyRects(w, h, pixels, IsExactKey);
                nearKeyRects[texturePath] = FindKeyRects(w, h, pixels, c => !IsExactKey(c) && RoClassicUiAtlas.IsClassicColorKey(c));
                ApplyKey(pixels);
            }

            if (!decoded || !atlas.TryAdd(w, h, pixels, out image)) {
                missing.Add(texturePath);
                return false;
            }

            // TGAs stay A8R8G8B8 in the client (no RGB555 upload): a negative page marks full colour for the shader
            if (tga) image = new RoClassicUiImage(image.Uv, -image.Page - 1f, image.Width, image.Height);
            images.Add(texturePath, image);
            return true;
        }

        private readonly Dictionary<string, RectInt[]> exactKeyRects = new Dictionary<string, RectInt[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RectInt[]> nearKeyRects = new Dictionary<string, RectInt[]>(StringComparer.OrdinalIgnoreCase);

        private static bool IsExactKey(Color32 c) => c.r == 255 && c.g == 0 && c.b == 255;

        /// <summary>
        /// The bitmap's exact 0xFF00FF pixels as rectangles (image-local, top-down), null when none. Keyed blits skip
        /// them; a raw copy leaves them in the surface it draws into, where they turn transparent.
        /// </summary>
        public RectInt[] KeyRects(string texturePath) =>
            texturePath != null && exactKeyRects.TryGetValue(texturePath, out RectInt[] rects) ? rects : null;

        /// <summary>
        /// Pixels that only become the key at the RGB555 upload (e.g. 255,1,255): no blit skips them, so they travel
        /// through every composition into the window's surface and leave a hole on screen.
        /// </summary>
        public RectInt[] NearKeyRects(string texturePath) =>
            texturePath != null && nearKeyRects.TryGetValue(texturePath, out RectInt[] rects) ? rects : null;

        private static void ApplyKey(Color32[] pixels) {
            for (int i = 0; i < pixels.Length; i++) {
                if (RoClassicUiAtlas.IsClassicColorKey(pixels[i])) pixels[i] = default;
            }
        }

        /// <summary>Matching pixels as row runs, merged downwards while a run repeats exactly.</summary>
        private static RectInt[] FindKeyRects(int w, int h, Color32[] pixels, Func<Color32, bool> isKey) {
            var done = new List<RectInt>();
            var open = new List<RectInt>();
            var row = new List<RectInt>();
            for (int y = 0; y < h; y++) {
                row.Clear();
                for (int x = 0; x < w; x++) {
                    if (!isKey(pixels[y * w + x])) continue;
                    int start = x;
                    while (x < w && isKey(pixels[y * w + x])) x++;
                    row.Add(new RectInt(start, y, x - start, 1));
                }

                // extend open rectangles whose run continues unchanged on this row
                for (int i = open.Count - 1; i >= 0; i--) {
                    RectInt o = open[i];
                    int match = row.FindIndex(r => r.x == o.x && r.width == o.width);
                    if (match >= 0) {
                        o.height++;
                        open[i] = o;
                        row.RemoveAt(match);
                    } else {
                        done.Add(o);
                        open.RemoveAt(i);
                    }
                }

                open.AddRange(row);
            }

            done.AddRange(open);
            return done.Count > 0 ? done.ToArray() : null;
        }

        /// <summary>TGA (true colour 24/32-bit, raw or RLE) to top-down RGBA, alpha as stored.</summary>
        public static bool TryDecodeTga(byte[] data, out int width, out int height, out Color32[] pixels) {
            width = height = 0;
            pixels = null;
            if (data == null || data.Length < 18) return false;
            int idLength = data[0], colorMapType = data[1], type = data[2];
            int w = BitConverter.ToUInt16(data, 12), h = BitConverter.ToUInt16(data, 14), bpp = data[16];
            bool topDown = (data[17] & 0x20) != 0;
            if (colorMapType != 0 || (type != 2 && type != 10) || (bpp != 24 && bpp != 32) || w == 0 || h == 0) return false;
            int bytes = bpp / 8, p = 18 + idLength, count = w * h;
            var result = new Color32[count];
            int i = 0;
            while (i < count) {
                int run = 1;
                bool repeat = false;
                if (type == 10) {
                    if (p >= data.Length) return false;
                    byte header = data[p++];
                    run = (header & 0x7f) + 1;
                    repeat = (header & 0x80) != 0;
                }

                for (int k = 0; k < run && i < count; k++, i++) {
                    if (p + bytes > data.Length) return false;
                    var c = new Color32(data[p + 2], data[p + 1], data[p], bytes == 4 ? data[p + 3] : (byte) 255);
                    int x = i % w, y = i / w;
                    result[(topDown ? y : h - 1 - y) * w + x] = c;
                    if (!repeat) p += bytes;
                }

                if (repeat) p += bytes;
            }

            width = w;
            height = h;
            pixels = result;
            return true;
        }

        /// <summary>BMP (1/4/8-bit paletted, 24/32-bit) to top-down RGBA with the classic key applied.</summary>
        public static bool TryDecode(byte[] data, out int width, out int height, out Color32[] pixels) {
            if (!TryDecodeRaw(data, out width, out height, out pixels)) return false;
            ApplyKey(pixels);
            return true;
        }

        /// <summary>BMP to top-down RGBA as stored (no key).</summary>
        private static bool TryDecodeRaw(byte[] data, out int width, out int height, out Color32[] pixels) {
            width = height = 0;
            pixels = null;
            if (data == null || data.Length < 54 || data[0] != 'B' || data[1] != 'M') return false;
            int offBits = BitConverter.ToInt32(data, 10);
            int headerSize = BitConverter.ToInt32(data, 14);
            int w = BitConverter.ToInt32(data, 18);
            int h = BitConverter.ToInt32(data, 22);
            int bpp = BitConverter.ToInt16(data, 28);
            int compression = BitConverter.ToInt32(data, 30);
            int colorsUsed = BitConverter.ToInt32(data, 46);
            if ((compression != 0 && compression != 3) || w <= 0 || h == 0) return false;
            bool bottomUp = h > 0;
            h = Math.Abs(h);
            var palette = new Color32[256];
            if (bpp <= 8) {
                int count = colorsUsed != 0 ? Math.Min(colorsUsed, 256) : 1 << bpp;
                int p = 14 + headerSize;
                for (int i = 0; i < count && p + 3 < data.Length; i++, p += 4) {
                    palette[i] = new Color32(data[p + 2], data[p + 1], data[p], 255);
                }
            }

            int stride = ((w * bpp + 31) / 32) * 4;
            if (offBits + stride * h > data.Length) return false;
            var result = new Color32[w * h];
            for (int y = 0; y < h; y++) {
                int row = offBits + (bottomUp ? h - 1 - y : y) * stride;
                for (int x = 0; x < w; x++) {
                    Color32 c;
                    switch (bpp) {
                        case 32:
                        case 24: {
                            int p = row + x * (bpp / 8);
                            c = new Color32(data[p + 2], data[p + 1], data[p], 255);
                            break;
                        }
                        case 8: c = palette[data[row + x]]; break;
                        case 4: c = palette[(data[row + (x >> 1)] >> ((x & 1) == 0 ? 4 : 0)) & 0xF]; break;
                        case 1: c = palette[(data[row + (x >> 3)] >> (7 - (x & 7))) & 1]; break;
                        default: return false;
                    }

                    result[y * w + x] = c;
                }
            }

            width = w;
            height = h;
            pixels = result;
            return true;
        }
    }
}
