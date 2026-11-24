using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ragnarok.UI.Classic {
    /// <summary>
    /// Glyphs rasterised by Windows GDI exactly like the original client's TextOut (same face, height, weight,
    /// quality flags), cached one by one into the UI atlas. Pen positions follow GDI advance widths, so composed
    /// strings match a GDI TextOut of the whole string (verified by <see cref="VerifyAgainstGdi"/>).
    /// </summary>
    public sealed class RoClassicGdiFont : IDisposable {
        public struct Glyph {
            public int OffsetX;
            public int OffsetY;
            public int Width;
            public int Height;
            public int Advance;
            public Vector4 Uv;
            public float Page;
            public bool Empty;
        }

        public const uint NonAntialiasedQuality = 3;
        public const uint AntialiasedQuality = 4;

        private readonly RoClassicUiAtlas atlas;
        private readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>(256);
        private readonly Glyph[] ascii = new Glyph[128];
        private readonly bool[] asciiReady = new bool[128];
        private readonly int cellWidth;
        private readonly int cellHeight;
        private readonly int penX;
        private readonly int penY;
        private readonly int[] cell;
        private IntPtr dc;
        private IntPtr dib;
        private IntPtr bits;
        private IntPtr font;
        private IntPtr oldBitmap;
        private IntPtr oldFont;

        public RoClassicGdiFont(RoClassicUiAtlas atlas, string face, int height, int weight = 400, uint quality = NonAntialiasedQuality,
            uint charset = 1) {
            this.atlas = atlas;
            Face = face;
            Height = height;
            int px = Math.Abs(height);
            cellWidth = px * 4 + 8;
            cellHeight = px * 3 + 8;
            penX = px + 4;
            penY = px / 2 + 4;
            cell = new int[cellWidth * cellHeight];

            dc = CreateCompatibleDC(IntPtr.Zero);
            var bmi = new BITMAPINFO {
                biSize = 40, biWidth = cellWidth, biHeight = -cellHeight, biPlanes = 1, biBitCount = 32, biCompression = 0,
            };
            dib = CreateDIBSection(dc, ref bmi, 0, out bits, IntPtr.Zero, 0);
            oldBitmap = SelectObject(dc, dib);
            font = CreateFontW(height, 0, 0, 0, weight, 0, 0, 0, charset, 0, 0, quality, 0, face);
            oldFont = SelectObject(dc, font);
            SetBkMode(dc, 1); // TRANSPARENT
            SetTextColor(dc, 0x00FFFFFF);
            GetTextMetricsW(dc, out TEXTMETRICW tm);
            Ascent = tm.tmAscent;
            LineHeight = tm.tmHeight;
        }

        public string Face { get; }
        public int Height { get; }
        public int Ascent { get; }
        public int LineHeight { get; }

        public void GetGlyph(char c, out Glyph glyph) {
            if (c < 128) {
                if (!asciiReady[c]) {
                    ascii[c] = Rasterize(c);
                    asciiReady[c] = true;
                }

                glyph = ascii[c];
                return;
            }

            if (!glyphs.TryGetValue(c, out glyph)) {
                glyph = Rasterize(c);
                glyphs.Add(c, glyph);
            }
        }

        public int Measure(string text) {
            int width = 0;
            for (int i = 0; i < text.Length; i++) {
                GetGlyph(text[i], out Glyph g);
                width += g.Advance;
            }

            return width;
        }

        private Glyph Rasterize(char c) {
            PatBlt(dc, 0, 0, cellWidth, cellHeight, 0x00000042); // BLACKNESS
            string s = new string(c, 1);
            TextOutW(dc, penX, penY, s, 1);
            GetTextExtentPoint32W(dc, s, 1, out SIZE extent);
            GdiFlush();
            Marshal.Copy(bits, cell, 0, cell.Length);

            int x0 = cellWidth, y0 = cellHeight, x1 = -1, y1 = -1;
            for (int y = 0; y < cellHeight; y++) {
                int row = y * cellWidth;
                for (int x = 0; x < cellWidth; x++) {
                    if ((cell[row + x] & 0xFF) != 0) {
                        if (x < x0) x0 = x;
                        if (x > x1) x1 = x;
                        if (y < y0) y0 = y;
                        if (y > y1) y1 = y;
                    }
                }
            }

            var glyph = new Glyph { Advance = extent.cx };
            if (x1 < 0) {
                glyph.Empty = true;
                return glyph;
            }

            int w = x1 - x0 + 1;
            int h = y1 - y0 + 1;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    byte a = (byte) (cell[(y0 + y) * cellWidth + x0 + x] & 0xFF);
                    pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
            }

            glyph.OffsetX = x0 - penX;
            glyph.OffsetY = y0 - penY;
            glyph.Width = w;
            glyph.Height = h;
            if (atlas.TryAdd(w, h, pixels, out RoClassicUiImage image)) {
                glyph.Uv = image.Uv;
                glyph.Page = image.Page;
            } else {
                glyph.Empty = true;
            }
            return glyph;
        }

        /// <summary>
        /// Draws <paramref name="text"/> once with a single GDI TextOut and once composed from cached glyphs; returns
        /// the number of differing pixels (0 = the glyph path is bit-identical to GDI for this string).
        /// </summary>
        public int VerifyAgainstGdi(string text, out int width, out int height) {
            GetTextExtentPoint32W(dc, text, text.Length, out SIZE extent);
            width = extent.cx + Math.Abs(Height) * 2;
            height = cellHeight;
            var bmi = new BITMAPINFO { biSize = 40, biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32 };
            IntPtr testDc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr testDib = CreateDIBSection(testDc, ref bmi, 0, out IntPtr testBits, IntPtr.Zero, 0);
            IntPtr prevBmp = SelectObject(testDc, testDib);
            IntPtr prevFont = SelectObject(testDc, font);
            SetBkMode(testDc, 1);
            SetTextColor(testDc, 0x00FFFFFF);
            PatBlt(testDc, 0, 0, width, height, 0x00000042);
            int ox = Math.Abs(Height);
            TextOutW(testDc, ox, penY, text, text.Length);
            GdiFlush();
            var reference = new int[width * height];
            Marshal.Copy(testBits, reference, 0, reference.Length);
            SelectObject(testDc, prevFont);
            SelectObject(testDc, prevBmp);
            DeleteObject(testDib);
            DeleteDC(testDc);

            var composed = new int[width * height];
            int pen = ox;
            for (int i = 0; i < text.Length; i++) {
                GetGlyph(text[i], out Glyph g);
                if (!g.Empty) {
                    // re-rasterise into the cell to read the glyph bits back (the atlas copy is GPU-side)
                    PatBlt(dc, 0, 0, cellWidth, cellHeight, 0x00000042);
                    TextOutW(dc, penX, penY, new string(text[i], 1), 1);
                    GdiFlush();
                    Marshal.Copy(bits, cell, 0, cell.Length);
                    for (int y = 0; y < g.Height; y++) {
                        for (int x = 0; x < g.Width; x++) {
                            int dx = pen + g.OffsetX + x;
                            int dy = penY + g.OffsetY + y;
                            if (dx < 0 || dy < 0 || dx >= width || dy >= height) continue;
                            int v = cell[(penY + g.OffsetY + y) * cellWidth + penX + g.OffsetX + x] & 0xFF;
                            int cur = composed[dy * width + dx];
                            composed[dy * width + dx] = Math.Max(cur, v);
                        }
                    }
                }

                pen += g.Advance;
            }

            int diff = 0;
            for (int i = 0; i < reference.Length; i++) {
                if ((reference[i] & 0xFF) != composed[i]) diff++;
            }

            return diff;
        }

        public void Dispose() {
            if (dc != IntPtr.Zero) {
                SelectObject(dc, oldFont);
                SelectObject(dc, oldBitmap);
                DeleteObject(font);
                DeleteObject(dib);
                DeleteDC(dc);
                dc = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
            public int bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TEXTMETRICW {
            public int tmHeight;
            public int tmAscent;
            public int tmDescent;
            public int tmInternalLeading;
            public int tmExternalLeading;
            public int tmAveCharWidth;
            public int tmMaxCharWidth;
            public int tmWeight;
            public int tmOverhang;
            public int tmDigitizedAspectX;
            public int tmDigitizedAspectY;
            public char tmFirstChar;
            public char tmLastChar;
            public char tmDefaultChar;
            public char tmBreakChar;
            public byte tmItalic;
            public byte tmUnderlined;
            public byte tmStruckOut;
            public byte tmPitchAndFamily;
            public byte tmCharSet;
        }

        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool GdiFlush();
        [DllImport("gdi32.dll")] private static extern bool PatBlt(IntPtr hdc, int x, int y, int w, int h, uint rop);
        [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
        [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits,
            IntPtr section, uint offset);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight,
            uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision,
            uint quality, uint pitchAndFamily, string face);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool TextOutW(IntPtr hdc, int x, int y, string text, int length);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetTextExtentPoint32W(IntPtr hdc, string text, int length, out SIZE size);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetTextMetricsW(IntPtr hdc, out TEXTMETRICW metrics);
    }
}
