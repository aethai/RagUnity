// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyTextureIGC.js, igc-pipeline.js, igc-plane.js, igc-yuv.js (clean-room port of granny2.dll's
// _GrannyDecompressIGCTexture@12, RAD BinkTC). See LICENSE-granny-ro-js.txt.

using System;

namespace Ragnarok.Granny {
    /// <summary>
    /// IGC (RAD BinkTC) texture decoder: per plane (Y, U, V, then A when present) an arithmetic + varbits coded
    /// 4-level sub-band image, 4 inverse-wavelet passes, then the reversible YUV -> RGB transform.
    /// Output is RGBA8, straight alpha, rows in stored (top-down) order.
    /// </summary>
    internal static class Gr2IgcDecoder {
        const int SmallestDwtRow = 16;
        const int SmallestDwtCol = 16;
        const long MaxPixels = 4096L * 4096;
        const long MaxPixelsPerByte = 4096;

        public static byte[] Decode(ReadOnlySpan<byte> source, int width, int height, bool alpha) {
            long pixelCount = (long)width * height;

            // granny2.dll skips BinkTC for images of at most 256 pixels and copies the RGBA bytes through.
            if (pixelCount <= 256) {
                long expected = pixelCount * 4;
                if (width < 0 || height < 0 || source.Length < expected) {
                    throw new Gr2FormatException("igc small-image fallback expects " + expected + " RGBA bytes, got " + source.Length);
                }

                return source.Slice(0, (int)expected).ToArray();
            }

            if (width < SmallestDwtRow || height < SmallestDwtCol || (width & 15) != 0 || (height & 15) != 0) {
                throw new Gr2FormatException("igc texture " + width + "x" + height + " is not 16-aligned (unsupported fallback)");
            }

            if (pixelCount > MaxPixels || pixelCount > source.Length * MaxPixelsPerByte) {
                throw new Gr2FormatException("igc texture " + width + "x" + height + " exceeds the size cap");
            }

            byte[] src = source.ToArray();
            int count = (int)pixelCount;
            int planeCount = alpha ? 4 : 3;
            var planes = new short[4][];
            for (int i = 0; i < 4; i++) {
                planes[i] = new short[count];
            }

            var rowMask = new byte[height];
            var temp = new short[count];
            var wavelet = new Gr2IgcWavelet();
            long cursor = 4;
            for (int p = 0; p < planeCount; p++) {
                cursor += PlaneDecode(src, cursor, planes[p], width, height, p == 0 ? rowMask : null);
                wavelet.Inverse2D(planes[p], width * 8, width >> 3, height >> 3, null, temp);
                wavelet.Inverse2D(planes[p], width * 4, width >> 2, height >> 2, null, temp);
                wavelet.Inverse2D(planes[p], width * 2, width >> 1, height >> 1, null, temp);
                wavelet.Inverse2D(planes[p], width, width, height, p == 0 ? rowMask : null, temp);
            }

            if (!alpha) {
                Array.Fill(planes[3], (short)255);
            }

            return YuvToRgba(planes[0], planes[1], planes[2], planes[3], count);
        }

        /// <summary>granny2.dll @ 0x10009a30: g -= (r + b) / 4 (rounding toward zero), r += g, b += g, clamp.</summary>
        static byte[] YuvToRgba(short[] yp, short[] up, short[] vp, short[] ap, int count) {
            var output = new byte[count * 4];
            int o = 0;
            for (int i = 0; i < count; i++) {
                int r = up[i];
                int g = yp[i];
                int b = vp[i];
                int a = ap[i];
                g -= (r + b) / 4;
                r += g;
                b += g;
                output[o] = Clamp255(r);
                output[o + 1] = Clamp255(g);
                output[o + 2] = Clamp255(b);
                output[o + 3] = Clamp255(a);
                o += 4;
            }

            return output;
        }

        static byte Clamp255(int v) {
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
        }

        internal static uint ReadU32(byte[] buf, long offset) {
            if (offset >= 0 && offset + 4 <= buf.Length) {
                int o = (int)offset;
                return (uint)(buf[o] | (buf[o + 1] << 8) | (buf[o + 2] << 16) | (buf[o + 3] << 24));
            }

            uint v = 0;
            for (int i = 0; i < 4; i++) {
                long p = offset + i;
                if (p >= 0 && p < buf.Length) {
                    v |= (uint)buf[p] << (8 * i);
                }
            }

            return v;
        }

        // --- plane decode (encode.c plane_decode / decode_low / decode_high_1) -----------------------------------

        const int MinZeroLength = 3;
        const int LitLengthBits = 6;
        const int ZeroLengthBits = 8;
        const int LitLengthLimit = (1 << LitLengthBits) - 1;
        const int ZeroLengthLimit = (1 << ZeroLengthBits) - 1;
        const int ExtraLengths = 4;
        static readonly int[] ExtraLitLengths = { 128, 256, 512, 1024 };
        static readonly int[] ExtraZeroLengths = { 512, 1024, 2048, 3072 };

        /// <summary>LSB-first reader for the raw side stream (signs, escapes, 16-bit values).</summary>
        sealed class VarBits {
            readonly byte[] buf;
            long cur;
            uint bits;
            int bitlen;

            public VarBits(byte[] buf, long offset) {
                this.buf = buf;
                cur = offset;
            }

            public int Get1() {
                if (bitlen == 0) {
                    uint word = ReadU32(buf, cur);
                    cur += 4;
                    bits = word >> 1;
                    bitlen = 31;
                    return (int)(word & 1);
                }

                uint value = bits;
                bits = value >> 1;
                bitlen -= 1;
                return (int)(value & 1);
            }

            public uint Get(int len) {
                uint mask = len == 32 ? 0xFFFFFFFF : (1u << len) - 1;
                if (bitlen < len) {
                    uint nb = ReadU32(buf, cur);
                    cur += 4;
                    uint merged = (bits | (nb << bitlen)) & mask;
                    bits = nb >> (len - bitlen);
                    bitlen = bitlen + 32 - len;
                    return merged;
                }

                uint result = bits & mask;
                bits >>= len;
                bitlen -= len;
                return result;
            }
        }

        static int S16(uint v) {
            return unchecked((short)(ushort)v);
        }

        static int GetBitLevel(uint n) {
            if (n == 0) {
                return 0;
            }

            if (n >= 16384) {
                return 15;
            }

            int r = 0;
            while (n > 0) {
                r++;
                n >>= 1;
            }

            return r;
        }

        static void Store(short[] plane, long index, int value) {
            if (index >= 0 && index < plane.Length) {
                plane[index] = unchecked((short)value);
            }
        }

        static int Load(short[] plane, long index) {
            return index >= 0 && index < plane.Length ? plane[index] : 0;
        }

        static void FillRect(short[] outp, long outOffset, int pitch, int width, int height, int value) {
            long yadj = pitch - width;
            long o = outOffset;
            for (int h = 0; h < height; h++) {
                for (int w = 0; w < width; w++) {
                    Store(outp, o++, value);
                }

                o += yadj;
            }
        }

        static int ReadSymbol(Gr2IgcArithModel model, Gr2IgcArithBits ab, int escapeScale) {
            int value = model.Decompress(ab);
            if (value < 0) {
                int escaped = ab.GetValue(escapeScale);
                model.SetDecompressed(value, escaped);
                return escaped;
            }

            return value;
        }

        static int ApplySign(int value, VarBits vb) {
            if (value == 0) {
                return 0;
            }

            int v = -vb.Get1();
            return (value ^ v) - v;
        }

        /// <summary>Returns the bytes consumed: arith length + varbits length + 8.</summary>
        static long PlaneDecode(byte[] buf, long srcOffset, short[] output, int width, int height, byte[] rowMask) {
            uint arithLen = ReadU32(buf, srcOffset);
            long varbitsStart = srcOffset + 8 + arithLen;
            var ab = new Gr2IgcArithBits(buf, srcOffset + 8);
            var vb = new VarBits(buf, varbitsStart);

            // Sub-band layout (S16 elements): LLn at 0, HLn at W >> (n + 1), LHn at W * 2^n, HHn at both.
            DecodeLow(ab, vb, output, 0, width * 16, width >> 4, height >> 4);
            DecodeHigh1(ab, vb, output, width >> 4, width * 16, width >> 4, height >> 4);
            DecodeHigh1(ab, vb, output, width * 8, width * 16, width >> 4, height >> 4);
            DecodeHigh1(ab, vb, output, (width >> 4) + width * 8, width * 16, width >> 4, height >> 4);

            DecodeHigh1(ab, vb, output, width >> 3, width * 8, width >> 3, height >> 3);
            DecodeHigh1(ab, vb, output, width * 4, width * 8, width >> 3, height >> 3);
            DecodeHigh1(ab, vb, output, (width >> 3) + width * 4, width * 8, width >> 3, height >> 3);

            DecodeHigh1(ab, vb, output, width >> 2, width * 4, width >> 2, height >> 2);
            DecodeHigh1(ab, vb, output, width * 2, width * 4, width >> 2, height >> 2);
            DecodeHigh1(ab, vb, output, (width >> 2) + width * 2, width * 4, width >> 2, height >> 2);

            DecodeHigh1(ab, vb, output, width >> 1, width * 2, width >> 1, height >> 1);
            DecodeHigh1(ab, vb, output, width, width * 2, width >> 1, height >> 1);
            DecodeHigh1(ab, vb, output, (width >> 1) + width, width * 2, width >> 1, height >> 1);

            if (rowMask != null) {
                ReadEscapes(ab, rowMask, height);
            }

            uint varbitsLen = ReadU32(buf, srcOffset + 4);
            return (long)arithLen + varbitsLen + 8;
        }

        // encode.c read_escapes: per-row zero flags for the final inverse-wavelet row pass.
        static void ReadEscapes(Gr2IgcArithBits ab, byte[] mask, int count) {
            int zeros = ab.GetValue(count + 1);
            for (int i = 0; i < count; i++) {
                if (ab.Get(count) >= zeros) {
                    mask[i] = 1;
                    ab.Remove(zeros, count - zeros, count);
                } else {
                    mask[i] = 0;
                    ab.Remove(0, zeros, count);
                }
            }
        }

        // encode.c decode_low: low-pass band, first row predicted from the left, then from the left/top average.
        static void DecodeLow(Gr2IgcArithBits ab, VarBits vb, short[] outp, long outOffset, int pixelPitch, int encWidth, int encHeight) {
            if (vb.Get1() != 0) {
                FillRect(outp, outOffset, pixelPitch, encWidth, encHeight, S16(vb.Get(16)));
                return;
            }

            int max = (int)vb.Get(16);
            int num = max + 1;
            var model = new Gr2IgcArithModel(num);
            long yadj = pixelPitch - encWidth;

            int prev = S16(vb.Get(16));
            Store(outp, outOffset++, prev);
            for (int w = 0; w < encWidth - 1; w++) {
                int cur = ApplySign(ReadSymbol(model, ab, num), vb);
                prev = unchecked(cur + prev);
                Store(outp, outOffset++, prev);
            }

            for (int h = 0; h < encHeight - 1; h++) {
                outOffset += yadj;
                long from = outOffset - pixelPitch;
                int cur = ApplySign(ReadSymbol(model, ab, num), vb);
                prev = unchecked(cur + Load(outp, from));
                Store(outp, outOffset++, prev);
                ++from;
                for (int w = 0; w < encWidth - 1; w++) {
                    cur = ApplySign(ReadSymbol(model, ab, num), vb);
                    prev = unchecked(cur + (int)(((long)prev + Load(outp, from)) / 2));
                    Store(outp, outOffset++, prev);
                    ++from;
                }
            }
        }

        /// <summary>Context from the neighbours: bit level of (|a| + |b| + |c| + |d|) / 4 taken as u32.</summary>
        static int Context4(int prev, int aboveLeft, int above, int aboveRight) {
            long sum = Math.Abs((long)prev) + Math.Abs((long)aboveLeft) + Math.Abs((long)above) + Math.Abs((long)aboveRight);
            return GetBitLevel(unchecked((uint)sum) / 4);
        }

        static int ReadLength(Gr2IgcArithModel model, Gr2IgcArithBits ab, VarBits vb, int escapeBits) {
            int value = model.Decompress(ab);
            if (value < 0) {
                int escaped = (int)vb.Get(escapeBits);
                model.SetDecompressed(value, escaped);
                return escaped;
            }

            return value;
        }

        // encode.c decode_high_1: high-pass band with order-1 context, coded as alternating literal / zero runs.
        static void DecodeHigh1(Gr2IgcArithBits ab, VarBits vb, short[] outp, long outOffset, int pixelPitch, int encWidth, int encHeight) {
            int qlevel = (int)vb.Get(16);
            if (vb.Get1() != 0) {
                int v = S16(vb.Get(16));
                FillRect(outp, outOffset, pixelPitch, encWidth, encHeight, unchecked(v * qlevel));
                return;
            }

            int max = (int)vb.Get(16);
            int num = max + 1;
            int numl = GetBitLevel(unchecked((uint)((long)max * qlevel))) + 1;
            var contexts = new Gr2IgcArithModel[numl];
            for (int i = 0; i < numl; i++) {
                contexts[i] = new Gr2IgcArithModel(num);
            }

            var lits = new Gr2IgcArithModel(LitLengthLimit + 1);
            var zeros = new Gr2IgcArithModel(ZeroLengthLimit + 1);
            long yadj = pixelPitch - encWidth;
            int h = encHeight;

            int above = ab.GetValue(num);
            if (above != 0) {
                int v = -vb.Get1();
                above = (above ^ v) - v;
                above = unchecked(above * qlevel);
            }

            Store(outp, outOffset, above);
            int aboveLeft = above;
            int prev = above;
            long fromOffset = outOffset;
            ++outOffset;

            // encWidth == 1 enters at the after_first label, with the JS's alternate first-column context formula.
            bool narrow = encWidth == 1;
            int w;
            if (narrow) {
                if (--h == 0) {
                    return;
                }

                w = encWidth;
                outOffset += yadj;
                fromOffset = outOffset - pixelPitch;
                above = Load(outp, fromOffset++);
                aboveLeft = above;
                prev = above;
            } else {
                w = encWidth - 1;
            }

            int idleIter = 0;
            for (;;) {
                int litLen = ReadLength(lits, ab, vb, LitLengthBits);
                if (litLen >= LitLengthLimit - ExtraLengths + 1) {
                    litLen = ExtraLitLengths[litLen - (LitLengthLimit - ExtraLengths + 1)];
                }

                int zeroLen = ReadLength(zeros, ab, vb, ZeroLengthBits);
                if (zeroLen >= ZeroLengthLimit - ExtraLengths + 1) {
                    zeroLen = ExtraZeroLengths[zeroLen - (ZeroLengthLimit - ExtraLengths + 1)] + MinZeroLength - 1;
                } else if (zeroLen != 0) {
                    zeroLen += MinZeroLength - 1;
                }

                // Both runs empty never advances (granny2.dll spins forever); seen when an RGB stream is decoded as RGBA.
                if (litLen == 0 && zeroLen == 0) {
                    if (++idleIter > 64) {
                        throw new Gr2FormatException("igc decodeHigh1 stuck at h=" + h + " w=" + w + " (" + encWidth + "x" + encHeight + "), bitstream off-corpus");
                    }
                } else {
                    idleIter = 0;
                }

                while (litLen > 0) {
                    if (w <= 1) {
                        if (w != 0) {
                            int context;
                            if (narrow) {
                                long sum3 = Math.Abs(2L * prev) + Math.Abs((long)aboveLeft) + Math.Abs((long)above);
                                context = GetBitLevel(unchecked((uint)(unchecked((int)sum3) / 4)));
                            } else {
                                long sum3 = Math.Abs((long)unchecked(prev * 2)) + Math.Abs((long)aboveLeft) + Math.Abs((long)above);
                                context = GetBitLevel(unchecked((uint)sum3) / 4);
                            }

                            int cur = ReadSymbol(contexts[context], ab, num);
                            if (cur != 0) {
                                int v = -vb.Get1();
                                cur = (cur ^ v) - v;
                                cur = unchecked(cur * qlevel);
                            }

                            Store(outp, outOffset++, cur);
                            --litLen;
                        }

                        if (--h == 0) {
                            return;
                        }

                        w = encWidth;
                        outOffset += yadj;
                        fromOffset = outOffset - pixelPitch;
                        above = Load(outp, fromOffset++);
                        aboveLeft = above;
                        prev = above;
                    } else {
                        int aboveRight = Load(outp, fromOffset);
                        int context = Context4(prev, aboveLeft, above, aboveRight);
                        int cur = ReadSymbol(contexts[context], ab, num);
                        if (cur != 0) {
                            int v = -vb.Get1();
                            cur = (cur ^ v) - v;
                            cur = unchecked(cur * qlevel);
                        }

                        Store(outp, outOffset, cur);
                        aboveLeft = above;
                        above = aboveRight;
                        prev = cur;
                        ++outOffset;
                        ++fromOffset;
                        --w;
                        --litLen;
                    }
                }

                while (zeroLen > 0) {
                    if (zeroLen >= w) {
                        zeroLen -= w;
                        fromOffset += w;
                        while (w-- > 0) {
                            Store(outp, outOffset++, 0);
                        }

                        if (--h == 0) {
                            return;
                        }

                        w = encWidth;
                        outOffset += yadj;
                        fromOffset = outOffset - pixelPitch;
                        above = Load(outp, fromOffset++);
                        aboveLeft = above;
                        prev = above;
                    } else {
                        w -= zeroLen;
                        fromOffset += zeroLen;
                        do {
                            Store(outp, outOffset++, 0);
                        } while (--zeroLen > 0);

                        prev = 0;
                        above = Load(outp, fromOffset - 1);
                        aboveLeft = Load(outp, fromOffset - 2);
                    }
                }
            }
        }
    }
}
