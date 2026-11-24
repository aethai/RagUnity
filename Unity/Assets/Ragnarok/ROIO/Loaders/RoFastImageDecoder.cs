using System;

namespace ROIO.Loaders
{
    /// <summary>
    /// Direct byte-array decoders for the image formats RO data uses (uncompressed 8/24/32-bit BMP, uncompressed
    /// 24/32-bit TGA), writing RGBA32 pixels straight into a texture's memory. The output is identical to
    /// <see cref="B83.Image.BMP.BMPLoader"/> + <c>BMPImage.ToTexture2D</c> and <see cref="TGALoader"/>: the same row
    /// order (file order, no flip), palette fallback (index past the palette → entry 0, palette alpha 255), BMP
    /// magenta key (r ≥ 252, g ≤ 4, b ≥ 252 → transparent black) and TGA channel order. Anything else (RLE,
    /// bitfields, 1/4/16-bit, truncated data) is rejected by the Try* parsers and goes through those loaders.
    /// No UnityEngine types here, so the decoders can be checked against the old loaders outside the Editor.
    /// </summary>
    public static unsafe class RoFastImageDecoder
    {
        public struct ImageInfo
        {
            public int Width;
            public int Height;
            internal bool IsTga;
            internal int BitsPerPixel;
            internal int PixelOffset;
            internal int RowStride;
            internal int PaletteOffset;
            internal int PaletteCount;
        }

        const int MaxPixels = 64 * 1024 * 1024;

        static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);
        static int I32(byte[] d, int o) => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        /// <summary>An uncompressed (BI_RGB) 8, 24 or 32-bit BMP whose pixel rows are all inside <paramref name="data"/>.</summary>
        public static bool TryParseBmp(byte[] data, out ImageInfo info)
        {
            info = default;
            if (data == null || data.Length < 54 || data[0] != 0x42 || data[1] != 0x4D)
            {
                return false;
            }

            int pixelOffset = I32(data, 10);
            int infoSize = I32(data, 14);
            int width = Math.Abs(I32(data, 18));
            int height = Math.Abs(I32(data, 22));
            int bpp = U16(data, 28);
            int compression = I32(data, 30);
            int paletteColors = I32(data, 46);
            if (infoSize < 40 || compression != 0 || width <= 0 || height <= 0 || (long)width * height > MaxPixels)
            {
                return false;
            }

            int rowStride;
            if (bpp == 8)
            {
                rowStride = (8 * width + 31) / 32 * 4;
                int count = paletteColors != 0 ? paletteColors : 256;
                int paletteOffset = 14 + infoSize;
                if (count < 0 || count > 256 || paletteOffset < 0 || (long)paletteOffset + count * 4L > data.Length)
                {
                    return false;
                }

                info.PaletteOffset = paletteOffset;
                info.PaletteCount = count;
            }
            else if (bpp == 24 || bpp == 32)
            {
                if (paletteColors != 0)
                {
                    return false;   // the old loader reads a palette here too; leave that oddity to it
                }

                rowStride = bpp == 24 ? (24 * width + 31) / 32 * 4 : width * 4;
            }
            else
            {
                return false;
            }

            if (pixelOffset < 0 || (long)pixelOffset + (long)rowStride * height > data.Length)
            {
                return false;
            }

            info.Width = width;
            info.Height = height;
            info.BitsPerPixel = bpp;
            info.PixelOffset = pixelOffset;
            info.RowStride = rowStride;
            return true;
        }

        /// <summary>A 24 or 32-bit TGA read the way <see cref="TGALoader"/> reads it: pixels from byte 18, no flip.</summary>
        public static bool TryParseTga(byte[] data, out ImageInfo info)
        {
            info = default;
            if (data == null || data.Length < 18)
            {
                return false;
            }

            int width = (short)U16(data, 12);
            int height = (short)U16(data, 14);
            int bpp = data[16];
            if ((bpp != 24 && bpp != 32) || width <= 0 || height <= 0 || 18L + (long)width * height * (bpp / 8) > data.Length)
            {
                return false;
            }

            info.IsTga = true;
            info.Width = width;
            info.Height = height;
            info.BitsPerPixel = bpp;
            info.PixelOffset = 18;
            info.RowStride = width * (bpp / 8);
            return true;
        }

        /// <summary>
        /// Writes <c>Width·Height</c> RGBA32 pixels to <paramref name="rgba"/>. Returns false when an 8-bit BMP used
        /// an index past its palette (drawn as entry 0, like the old loader, which warns about it).
        /// </summary>
        public static bool Decode(byte[] data, in ImageInfo info, byte* rgba)
        {
            bool indicesInRange = true;
            int w = info.Width, h = info.Height;
            uint* dst = (uint*)rgba;
            fixed (byte* src = data)
            {
                if (info.IsTga)
                {
                    byte* p = src + info.PixelOffset;
                    int n = w * h;
                    if (info.BitsPerPixel == 32)
                    {
                        for (int i = 0; i < n; i++, p += 4)
                        {
                            // B G R A in the file → R G B A
                            dst[i] = p[2] | ((uint)p[1] << 8) | ((uint)p[0] << 16) | ((uint)p[3] << 24);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < n; i++, p += 3)
                        {
                            dst[i] = p[2] | ((uint)p[1] << 8) | ((uint)p[0] << 16) | 0xff000000u;
                        }
                    }

                    return true;
                }

                if (info.BitsPerPixel == 8)
                {
                    uint* palette = stackalloc uint[256];
                    byte* pal = src + info.PaletteOffset;
                    for (int i = 0; i < info.PaletteCount; i++, pal += 4)
                    {
                        palette[i] = Key(pal[2], pal[1], pal[0]);
                    }

                    int count = info.PaletteCount;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = src + info.PixelOffset + y * info.RowStride;
                        uint* outRow = dst + y * w;
                        for (int x = 0; x < w; x++)
                        {
                            int index = row[x];
                            if (index >= count)
                            {
                                indicesInRange = false;
                                index = 0;
                            }

                            outRow[x] = palette[index];
                        }
                    }

                    return indicesInRange;
                }

                if (info.BitsPerPixel == 24)
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* p = src + info.PixelOffset + y * info.RowStride;
                        uint* outRow = dst + y * w;
                        for (int x = 0; x < w; x++, p += 3)
                        {
                            outRow[x] = Key(p[2], p[1], p[0]);
                        }
                    }

                    return true;
                }

                // 32-bit BI_RGB: X8R8G8B8, the fourth byte is not alpha (no alpha mask)
                byte* q = src + info.PixelOffset;
                int pixels = w * h;
                for (int i = 0; i < pixels; i++, q += 4)
                {
                    dst[i] = Key(q[2], q[1], q[0]);
                }
            }

            return true;
        }

        /// <summary>RGBA32 of an opaque BMP colour after the magenta key of <c>BMPImage.ToTexture2D</c>.</summary>
        static uint Key(byte r, byte g, byte b)
        {
            if (r >= 252 && g <= 4 && b >= 252)
            {
                return 0u;
            }

            return r | ((uint)g << 8) | ((uint)b << 16) | 0xff000000u;
        }
    }
}
