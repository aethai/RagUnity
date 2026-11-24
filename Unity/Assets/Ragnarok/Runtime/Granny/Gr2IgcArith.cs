// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/igc-arith.js (clean-room port of the granny2.dll IGC arithmetic coder). See LICENSE-granny-ro-js.txt.

using System;

namespace Ragnarok.Granny {
    /// <summary>
    /// IGC range decoder (granny2.dll ArithBits, 31-bit precision): high / low / target, input bits pulled LSB-first
    /// from LE u32 words and bit-reversed per byte / nibble. Reads past the end of the stream yield zero bits.
    /// </summary>
    internal sealed class Gr2IgcArithBits {
        static readonly byte[] Br4 = { 0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15 };
        static readonly byte[] Br3 = { 0, 4, 2, 6, 1, 5, 3, 7 };
        static readonly byte[] Br8 = BuildBr8();

        readonly byte[] buf;
        long ptr;
        uint accum;
        int bitsLeft;
        uint high;
        uint low;
        uint target;

        /// <summary>fcn.10006650: first LE dword, top bit into the accumulator, low 31 bits reversed into target.</summary>
        public Gr2IgcArithBits(byte[] buf, long offset) {
            this.buf = buf;
            uint dword = Gr2IgcDecoder.ReadU32(buf, offset);
            uint lo31 = dword & 0x7FFFFFFF;
            uint t = Br4[(lo31 >> 4) & 0xF];
            t |= (uint)Br4[lo31 & 0xF] << 4;
            t = (t << 4) | Br4[(lo31 >> 8) & 0xF];
            t = (t << 4) | Br4[(lo31 >> 12) & 0xF];
            t = (t << 4) | Br4[(lo31 >> 16) & 0xF];
            t = (t << 4) | Br4[(lo31 >> 20) & 0xF];
            t = (t << 4) | Br4[(lo31 >> 24) & 0xF];
            t = (t << 3) | Br3[(lo31 >> 28) & 7];
            ptr = offset + 4;
            accum = (dword >> 31) & 1;
            bitsLeft = 1;
            high = 0x7FFFFFFF;
            low = 0;
            target = t;
        }

        static byte[] BuildBr8() {
            var table = new byte[256];
            for (int i = 0; i < 256; i++) {
                table[i] = (byte)((Br4[i & 0xF] << 4) | Br4[(i >> 4) & 0xF]);
            }

            return table;
        }

        /// <summary>Pulls n (1, 4 or 8) bits, already bit-reversed like the DLL's lookup tables.</summary>
        uint PullBits(int n) {
            uint raw;
            if (bitsLeft < n) {
                uint refill = Gr2IgcDecoder.ReadU32(buf, ptr);
                uint eax = bitsLeft == 0 ? refill : (refill << bitsLeft) | accum;
                accum = refill >> (n - bitsLeft);
                bitsLeft = bitsLeft + 32 - n;
                ptr += 4;
                raw = eax & ((1u << n) - 1);
            } else {
                raw = accum & ((1u << n) - 1);
                accum >>= n;
                bitsLeft -= n;
            }

            return n == 8 ? Br8[raw] : n == 4 ? Br4[raw] : raw;
        }

        // fcn.1000ddc0: byte, then nibble, then bit renormalisation, then the E3 underflow carry.
        void Renorm() {
            uint h = high;
            uint l = low;
            uint t = target;
            uint xh = l ^ h;
            if ((xh & 0x40000000) == 0) {
                if ((xh & 0x7F800000) == 0) {
                    do {
                        h = ((h << 8) | 0xFF) & 0x7FFFFFFF;
                        l = (l << 8) & 0x7FFFFFFF;
                        t = ((t << 8) | PullBits(8)) & 0x7FFFFFFF;
                        xh = l ^ h;
                    } while ((xh & 0x7F800000) == 0);
                }

                if ((xh & 0x78000000) == 0) {
                    h = ((h << 4) | 0xF) & 0x7FFFFFFF;
                    l = (l << 4) & 0x7FFFFFFF;
                    t = ((t << 4) | PullBits(4)) & 0x7FFFFFFF;
                    xh = l ^ h;
                }

                if ((xh & 0x40000000) == 0) {
                    do {
                        h = ((h << 1) | 1) & 0x7FFFFFFF;
                        l = (l << 1) & 0x7FFFFFFF;
                        t = ((t << 1) | PullBits(1)) & 0x7FFFFFFF;
                        xh = l ^ h;
                    } while ((xh & 0x40000000) == 0);
                }
            }

            if ((l & 0x20000000) != 0) {
                while ((h & 0x20000000) == 0) {
                    l = ((l & 0x1FFFFFFF) << 1) & 0x7FFFFFFF;
                    h = ((h << 1) | 0x40000001) & 0x7FFFFFFF;
                    t ^= 0x20000000;
                    t = ((t << 1) | PullBits(1)) & 0x7FFFFFFF;
                    if ((l & 0x20000000) == 0) {
                        break;
                    }
                }
            }

            high = h;
            low = l;
            target = t;
        }

        /// <summary>fcn.1000e6f0: offset = ((target - low + 1) * scale - 1) / (high - low + 1), clamped below scale.</summary>
        public int Get(int scale) {
            uint range = unchecked(high - low + 1);
            uint tNorm = unchecked(target - low + 1);
            long v = range == 0 ? 0 : ((long)tNorm * scale - 1) / range;
            int value = unchecked((int)v);
            return value >= scale ? scale - 1 : value;
        }

        /// <summary>fcn.1000ddc0: narrow [low, high] to [lo, lo + count) of scale, then renormalise.</summary>
        public void Remove(int lo, int count, int scale) {
            uint range = unchecked(high - low + 1);
            uint oldLow = low;
            if (scale == 0) {
                // JS: x / 0 is Infinity / NaN, both of which `>>> 0` turns into 0.
                high = 0;
                low = 0;
            } else {
                long num1 = FloorDiv((long)range * (lo + count), scale);
                long num2 = FloorDiv((long)range * lo, scale);
                high = unchecked((uint)(oldLow + num1 - 1));
                low = unchecked((uint)(oldLow + num2));
            }

            Renorm();
        }

        /// <summary>fcn.1000e020: Get followed by Remove(value, 1, scale).</summary>
        public int GetValue(int scale) {
            int value = Get(scale);
            Remove(value, 1, scale);
            return value;
        }

        static long FloorDiv(long a, long b) {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) {
                q--;
            }

            return q;
        }
    }

    /// <summary>
    /// granny2.dll adaptive model (Arith family, not the SDK radarith.c): 16 u16 cumulative counts maintained during
    /// a 4-level binary search, per-symbol single counts, escape slot 0, rescale at 0x4000.
    /// </summary>
    internal sealed class Gr2IgcArithModel {
        const int RescaleTrigger = 0x4000;
        public const int MaxAlphabet = 8192;

        readonly ushort[] cumCounts = new ushort[16];
        readonly ushort[] singleCounts;
        readonly ushort[] values;
        readonly int uniqueCount;
        int singlesLength;
        int bandBoundary;
        int shiftDepth;
        int bucketSize;

        /// <summary>fcn.1000e0c0 (Arith_open, decode side).</summary>
        public Gr2IgcArithModel(int uniqueValues) {
            if (uniqueValues > MaxAlphabet) {
                throw new Gr2FormatException("igc arith alphabet " + uniqueValues + " exceeds cap " + MaxAlphabet);
            }

            int countsSize = (uniqueValues + 5) & ~3;
            singleCounts = new ushort[countsSize];
            values = new ushort[countsSize];
            uniqueCount = uniqueValues;
            InitBands(uniqueValues + 1);
            Update(0, 0x30003);
        }

        // fcn.1000e130: shiftDepth minimising the leftover of a 16-bucket split.
        void InitBands(int uniqueValuesPlus1) {
            if (uniqueValuesPlus1 < 6) {
                bucketSize = 0;
                shiftDepth = 15;
                bandBoundary = 0;
                return;
            }

            int bestShift = 0;
            long bestLeftover = 0xFFFFFFFF;
            for (int cl = 0; cl < 16; cl++) {
                long size = 1L << cl;
                long buckets = (size + uniqueValuesPlus1 - 1) / size;
                if (buckets > 16) {
                    buckets = 16;
                }

                long leftover = (uint)(uniqueValuesPlus1 - (buckets - 1) * size);
                if (leftover < size) {
                    leftover = size;
                }

                if (leftover < bestLeftover) {
                    bestShift = cl;
                    bestLeftover = leftover;
                }

                if (size > uniqueValuesPlus1) {
                    break;
                }
            }

            bucketSize = (1 << bestShift) & 0xFFFF;
            shiftDepth = bestShift & 0xFF;
            bandBoundary = (15 * bucketSize) & 0xFFFF;
        }

        // fcn.1000e1c0: leaf path bumps cumCounts[15]; cached path adds magic as one dword per cumCounts pair
        // (carry from the low half into the high half included), falling through to the last pair.
        void Update(int idx, uint magic) {
            uint lo = magic & 0xFFFF;
            if (idx >= bandBoundary) {
                cumCounts[15] = (ushort)(cumCounts[15] + lo);
                if (idx < singleCounts.Length) {
                    singleCounts[idx] = (ushort)(singleCounts[idx] + lo);
                }

                return;
            }

            int d = idx >> shiftDepth;
            if ((d & 1) != 0) {
                cumCounts[d] = (ushort)(cumCounts[d] + lo);
                d++;
            }

            d >>= 1;
            if (d <= 7) {
                for (int k = d; k <= 7; k++) {
                    int j = k * 2;
                    uint dword = ((uint)cumCounts[j + 1] << 16) | cumCounts[j];
                    dword = unchecked(dword + magic);
                    cumCounts[j] = (ushort)dword;
                    cumCounts[j + 1] = (ushort)(dword >> 16);
                }
            }

            if (idx < singleCounts.Length) {
                singleCounts[idx] = (ushort)(singleCounts[idx] + lo);
            }
        }

        // fcn.1000e7f0: 4-level search over the OLD cumCounts, then cumCounts[bucket..15] += 1, then a linear scan.
        int Search(int offset, out int slotLo) {
            ushort[] cc = cumCounts;
            int bucket = 0;
            int lo = 0;
            if (offset >= cc[7]) {
                lo = cc[7];
                bucket = 8;
            }

            if (offset >= cc[bucket + 3]) {
                lo = cc[bucket + 3];
                bucket += 4;
            }

            if (offset >= cc[bucket + 1]) {
                lo = cc[bucket + 1];
                bucket += 2;
            }

            if (offset >= cc[bucket]) {
                lo = cc[bucket];
                bucket += 1;
            }

            for (int i = bucket; i < 16; i++) {
                cc[i] = (ushort)(cc[i] + 1);
            }

            int idx = bucket * bucketSize;
            int cumLo = lo;
            int end = Math.Min(singleCounts.Length, uniqueCount + 1);
            while (idx < end) {
                int next = (cumLo + singleCounts[idx]) & 0xFFFF;
                if (offset < next) {
                    slotLo = cumLo;
                    return idx;
                }

                cumLo = next;
                idx++;
            }

            slotLo = cumLo;
            return Math.Min(Math.Max(idx - 1, 0), Math.Max(uniqueCount, 0));
        }

        // fcn.1000ec30: halve counts, drop slots <= 1 by swapping with the last, move the max to the start of the
        // last bucket, then fcn.1000e690 (restore the escape weight, re-emit cumCounts).
        void Rescale() {
            InitBands(singlesLength + 1);
            Span<uint> scratch = stackalloc uint[16];
            scratch.Clear();
            singleCounts[0] = (ushort)(singleCounts[0] >> 1);
            scratch[0 >= bandBoundary ? 15 : 0] = singleCounts[0];

            int max = 0;
            int maxPos = 0;
            int i = 1;
            while (i <= singlesLength) {
                while (singleCounts[i] <= 1) {
                    if (i >= singlesLength) {
                        singleCounts[i] = 0;
                        singlesLength--;
                        break;
                    }

                    singleCounts[i] = singleCounts[singlesLength];
                    singleCounts[singlesLength] = 0;
                    values[i] = values[singlesLength];
                    singlesLength--;
                }

                if (i > singlesLength) {
                    break;
                }

                singleCounts[i] = (ushort)(singleCounts[i] >> 1);
                int sc = singleCounts[i];
                if (sc > max) {
                    max = sc;
                    maxPos = i;
                }

                int bucket = i >= bandBoundary ? 15 : (i >> shiftDepth);
                scratch[bucket] = unchecked(scratch[bucket] + (uint)sc);
                i++;
            }

            if (max > 0) {
                int target = singlesLength >= bandBoundary ? bandBoundary : (singlesLength >> shiftDepth) << shiftDepth;
                if (target == 0) {
                    target = 1;
                }

                if (maxPos != target) {
                    int oldTarget = singleCounts[target];
                    int atMax = singleCounts[maxPos];
                    int delta = atMax - oldTarget;
                    int bucketTarget = target >= bandBoundary ? 15 : (target >> shiftDepth);
                    int bucketMax = maxPos >= bandBoundary ? 15 : (maxPos >> shiftDepth);
                    scratch[bucketTarget] = unchecked(scratch[bucketTarget] + (uint)delta);
                    scratch[bucketMax] = unchecked(scratch[bucketMax] - (uint)delta);
                    singleCounts[target] = (ushort)atMax;
                    singleCounts[maxPos] = (ushort)oldTarget;
                    ushort value = values[target];
                    values[target] = values[maxPos];
                    values[maxPos] = value;
                }
            }

            if (singlesLength != uniqueCount && singleCounts[0] == 0) {
                singleCounts[0] = 2;
                int bucket0 = 0 >= bandBoundary ? 15 : 0;
                scratch[bucket0] = unchecked(scratch[bucket0] + 2);
            }

            uint cum = 0;
            for (int k = 0; k < 16; k++) {
                cum = unchecked(cum + scratch[k]) & 0xFFFF;
                cumCounts[k] = (ushort)cum;
            }
        }

        /// <summary>
        /// fcn.1000e6f0 (Arith_decompress): the decoded symbol, or -(slot + 1) for an escape; the caller then reads
        /// the new symbol and registers it with <see cref="SetDecompressed"/>.
        /// </summary>
        public int Decompress(Gr2IgcArithBits ab) {
            if (cumCounts[15] >= RescaleTrigger) {
                Rescale();
            }

            int total = cumCounts[15];
            int offset = ab.Get(total);
            int refined = Search(offset, out int slotLo);
            ab.Remove(slotLo, singleCounts[refined], total);
            singleCounts[refined] = (ushort)(singleCounts[refined] + 1);
            if (refined != 0) {
                return values[refined];
            }

            singlesLength = (singlesLength + 1) & 0xFFFF;
            Update(singlesLength, 0x20002);
            if (singlesLength == uniqueCount) {
                // Remove the escape weight: dword add of ((-sc0 - 1) << 16) | (-sc0 & 0xFFFF) to cumCounts[0..1].
                int sc0 = singleCounts[0];
                uint magic = ((uint)((-sc0 - 1) & 0xFFFF) << 16) | (uint)((-sc0) & 0xFFFF);
                Update(0, magic);
            }

            return -singlesLength - 1;
        }

        public void SetDecompressed(int escape, int value) {
            values[-escape - 1] = (ushort)value;
        }
    }
}
