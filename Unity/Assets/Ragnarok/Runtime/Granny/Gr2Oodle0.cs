// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyOodle0.js (itself a port of Rasetsuu/blendergranny, MIT). See LICENSE-granny-ro-js.txt.

using System;
using System.Buffers.Binary;

namespace Ragnarok.Granny {
    /// <summary>
    /// Oodle0 section decompressor: the 2002-era RAD LZ77 + adaptive arithmetic coder embedded in granny2.dll
    /// (not modern Oodle). A section is 3 x 12-byte LZ block headers followed by one bitstream; the 3 blocks emit
    /// [0, First16Bit), [First16Bit, First8Bit), [First8Bit, ExpandedSize) with their own models.
    /// </summary>
    internal static class Gr2Oodle0 {
        const int HeaderSize = 36;
        const int BlockCount = 3;
        const int OffsetSplitShift = 2;
        const int LowOffsetMask = (1 << OffsetSplitShift) - 1;
        const int MaxLens = 64;
        const uint Mask31 = 0x7FFFFFFF;
        static readonly int[] LongLengths = { MaxLens * 2, MaxLens * 3, MaxLens * 4, MaxLens * 8 };

        // Decompression-bomb caps (same values as the JS).
        const long MaxExpandedSize = 256L * 1024 * 1024;
        const long MaxExpandRatio = 1024;
        const int MaxAlphabet = 1 << 21;

        public static byte[] Decompress(Gr2Section section, ReadOnlySpan<byte> compressed) {
            if (section.ExpandedSize == 0) {
                return Array.Empty<byte>();
            }

            if (compressed.Length < HeaderSize) {
                throw new Gr2FormatException("oodle0 data too short for 3 block headers");
            }

            long expanded = section.ExpandedSize;
            if (expanded > MaxExpandedSize || expanded > compressed.Length * MaxExpandRatio) {
                throw new Gr2FormatException("oodle0 expanded_size " + expanded + " exceeds cap");
            }

            // Block stops come from the section header, clamped to [0, expanded].
            long first16 = Math.Min(section.First16Bit, expanded);
            long first8 = Math.Min(section.First8Bit, expanded);
            if (first8 < first16) {
                first8 = first16;
            }

            Span<long> stops = stackalloc long[] { 0, first16, first8, expanded };
            var bits = new ArithBits(compressed, HeaderSize);
            var output = new byte[expanded];
            long cursor = 0;
            for (int i = 0; i < BlockCount; i++) {
                if (stops[i + 1] <= stops[i]) {
                    continue;
                }

                int w = i * 12;
                uint maxOffsetAndByte = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(w));
                uint uniqOffsetAndByte = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(w + 4));
                uint uniqLens = BinaryPrimitives.ReadUInt32LittleEndian(compressed.Slice(w + 8));
                var state = new LzState(maxOffsetAndByte, uniqOffsetAndByte, uniqLens);
                cursor = DecodeBlock(state, ref bits, output, cursor, stops[i + 1]);
            }

            if (cursor != expanded) {
                throw new Gr2FormatException("oodle0 decompressed " + cursor + ", expected " + expanded);
            }

            return output;
        }

        // Length symbol 0 = literal; 1..MaxLens-4 = length symbol + 1; the last 4 map to LongLengths.
        // Distance = low + 1 + (high << OffsetSplitShift). A copy past the end of the output is dropped and caught
        // by the final length check (JS typed-array writes past the end are no-ops).
        static long DecodeBlock(LzState state, ref ArithBits bits, byte[] output, long cursor, long stop) {
            int length = output.Length;
            while (cursor < stop) {
                int lengthSymbol = ReadModelSymbol(state.Lengths[state.LastLength], ref bits, MaxLens + 1);
                state.LastLength = lengthSymbol;
                if (lengthSymbol != 0) {
                    int copyLength = lengthSymbol >= MaxLens - 3 ? LongLengths[lengthSymbol - (MaxLens - 3)] : lengthSymbol + 1;
                    int low = ReadModelSymbol(state.OffsetLow, ref bits, state.MaxOffsetLow);
                    int highScale = (int)(Math.Min(state.MaxOffsets, state.BytesDecompressed) >> OffsetSplitShift) + 1;
                    int high = ReadModelSymbol(state.OffsetHigh, ref bits, highScale);
                    long distance = low + 1 + ((long)high << OffsetSplitShift);
                    if (distance <= 0 || distance > cursor) {
                        throw new Gr2FormatException("oodle0 invalid copy distance " + distance);
                    }

                    // Byte-by-byte: an overlapping copy must see the bytes it just wrote.
                    long src = cursor - distance;
                    for (int k = 0; k < copyLength; k++) {
                        long dst = cursor + k;
                        if (dst < length) {
                            output[dst] = output[src + k];
                        }
                    }

                    cursor += copyLength;
                    state.BytesDecompressed += copyLength;
                } else {
                    int literal = ReadModelSymbol(state.Bytes, ref bits, state.MaxBytes);
                    if (literal < 0 || literal > 255) {
                        throw new Gr2FormatException("oodle0 invalid literal " + literal);
                    }

                    if (cursor < length) {
                        output[cursor] = (byte)literal;
                    }

                    cursor++;
                    state.BytesDecompressed += 1;
                }
            }

            return cursor;
        }

        static int ReadModelSymbol(ArithModel model, ref ArithBits bits, int escapeScale) {
            int value = model.Decompress(ref bits);
            if (value < 0) {
                int escaped = bits.GetValue(escapeScale);
                model.SetEscaped(-value - 1, escaped);
                return escaped;
            }

            return value;
        }

        /// <summary>Reads a LE u32, zero-padding past the end (the codec reads up to 3 bytes past its data).</summary>
        static uint U32Padded(ReadOnlySpan<byte> data, int offset) {
            if (offset >= 0 && offset + 4 <= data.Length) {
                return BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
            }

            uint v = 0;
            for (int i = 0; i < 4; i++) {
                int p = offset + i;
                if (p >= 0 && p < data.Length) {
                    v |= (uint)data[p] << (8 * i);
                }
            }

            return v;
        }

        static uint BitReverse(uint value, int nbits) {
            uint result = 0;
            for (int i = 0; i < nbits; i++) {
                result = (result << 1) | ((value >> i) & 1);
            }

            return result;
        }

        /// <summary>LSB-first bit reader over LE u32 words.</summary>
        ref struct VarBits {
            readonly ReadOnlySpan<byte> data;
            int cur;
            uint bits;
            int bitlen;

            public VarBits(ReadOnlySpan<byte> data, int offset) {
                this.data = data;
                cur = offset;
                bits = 0;
                bitlen = 0;
            }

            public uint Get(int nbits) {
                if (nbits == 0) {
                    return 0;
                }

                uint mask = (1u << nbits) - 1;
                if (bitlen >= nbits) {
                    uint value = bits & mask;
                    bits >>= nbits;
                    bitlen -= nbits;
                    return value;
                }

                uint word = U32Padded(data, cur);
                cur += 4;
                int need = nbits - bitlen;
                uint wordLow = word & ((1u << need) - 1);
                uint result = bits | (wordLow << bitlen);
                bits = word >> need;
                bitlen = bitlen + 32 - nbits;
                return result;
            }

            public uint Get1() {
                if (bitlen != 0) {
                    uint value = bits & 1;
                    bits >>= 1;
                    bitlen -= 1;
                    return value;
                }

                uint word = U32Padded(data, cur);
                cur += 4;
                bits = word >> 1;
                bitlen = 31;
                return word & 1;
            }
        }

        /// <summary>31-bit arithmetic decoder on top of <see cref="VarBits"/> (high / low / code masked to 31 bits).</summary>
        ref struct ArithBits {
            VarBits vbits;
            uint high;
            uint low;
            uint code;

            public ArithBits(ReadOnlySpan<byte> data, int offset) {
                vbits = new VarBits(data, offset);
                high = Mask31;
                low = 0;
                code = 0;
                code = BitReverse(vbits.Get(31), 31);
            }

            public long GetCount(int scale) {
                if (scale <= 0) {
                    return 0;
                }

                long width = (long)high - low + 1;
                return (long)Math.Floor((((double)code - low + 1) * scale - 1) / width);
            }

            public int GetValue(int scale) {
                long value = GetCount(scale);
                if (value >= scale) {
                    value = scale - 1;
                }

                Remove((int)value, 1, scale);
                return (int)value;
            }

            public void Remove(int start, int count, int scale) {
                if (scale <= 0) {
                    return;
                }

                uint h = high;
                uint l = low;
                uint c = code;
                long width = (long)h - l + 1;
                h = unchecked((uint)(l + FloorDiv(width * (start + count), scale) - 1));
                l = unchecked((uint)(l + FloorDiv(width * start, scale)));
                if (((h ^ l) & 0x40000000) == 0) {
                    while (((h ^ l) & 0x7F800000) == 0) {
                        l <<= 8;
                        h = (h << 8) | 0xFF;
                        uint b = vbits.Get(8);
                        c = (c << 8) | (BitReverse(b & 0xF, 4) << 4) | BitReverse(b >> 4, 4);
                    }

                    if (((h ^ l) & 0x78000000) == 0) {
                        l <<= 4;
                        h = (h << 4) | 0xF;
                        c = (c << 4) | BitReverse(vbits.Get(4), 4);
                    }

                    while (((h ^ l) & 0x40000000) == 0) {
                        l <<= 1;
                        h = (h << 1) | 1;
                        c = (c << 1) | vbits.Get1();
                    }
                }

                while ((l & 0x20000000) != 0 && (h & 0x20000000) == 0) {
                    c ^= 0x20000000;
                    l = (l & 0x1FFFFFFF) << 1;
                    h = (h << 1) | 0x40000001;
                    c = (c << 1) | vbits.Get1();
                }

                high = h & Mask31;
                low = l & Mask31;
                code = c & Mask31;
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
        /// Adaptive model: 16 cumulative bins over u16 counts, escape slot 0 introduces new symbols, counts are
        /// halved (and rare symbols dropped) when the total reaches 16384.
        /// </summary>
        sealed class ArithModel {
            readonly int uniqueValues;
            readonly int[] totals = new int[16];
            readonly int[] counts;
            readonly int[] values;
            int number;
            int binShift;
            int lastBinStart;

            public ArithModel(int uniqueValues) {
                if (uniqueValues > MaxAlphabet) {
                    throw new Gr2FormatException("oodle0 arith alphabet " + uniqueValues + " exceeds cap " + MaxAlphabet);
                }

                this.uniqueValues = uniqueValues;
                int count = (uniqueValues + 5) & ~3;
                counts = new int[count];
                values = new int[count];
                BestShift(uniqueValues + 1, out binShift, out lastBinStart);
                QuickIncrement(0, 3);
            }

            /// <summary>Returns the decoded symbol, or -(slot + 1) when an escape introduces a new symbol.</summary>
            public int Decompress(ref ArithBits bits) {
                if (totals[15] >= 16384) {
                    Rescale();
                }

                int scale = totals[15];
                long count = bits.GetCount(scale);
                int pos = FindPos(count, out int start);
                int oldCount = counts[pos];
                IncrementTotals(pos, 1);
                bits.Remove(start, oldCount, totals[15] - 1);
                counts[pos] = (counts[pos] + 1) & 0xFFFF;
                if (pos == 0) {
                    number += 1;
                    if (number >= counts.Length) {
                        throw new Gr2FormatException("oodle0 escape exceeded model capacity");
                    }

                    QuickIncrement(number, 2);
                    if (number == uniqueValues) {
                        DecrementCounts(0, counts[0]);
                    }

                    return -number - 1;
                }

                return values[pos];
            }

            public void SetEscaped(int slot, int value) {
                values[slot] = value & 0xFFFF;
            }

            int FindPos(long count, out int start) {
                int lo = 0;
                int hi = 16;
                while (lo < hi) {
                    int mid = (lo + hi) >> 1;
                    if (count < totals[mid]) {
                        hi = mid;
                    } else {
                        lo = mid + 1;
                    }
                }

                int bin = lo;
                int pos;
                int end;
                if (bin < 15) {
                    pos = bin << binShift;
                    end = pos + (1 << binShift);
                    if (end > counts.Length) {
                        end = counts.Length;
                    }
                } else {
                    pos = lastBinStart;
                    end = counts.Length;
                }

                long running = bin > 0 ? totals[bin - 1] : 0;
                while (pos < end) {
                    int entry = counts[pos];
                    if (count < running + entry) {
                        start = (int)running;
                        return pos;
                    }

                    running += entry;
                    pos++;
                }

                throw new Gr2FormatException("oodle0 model count " + count + " outside total " + running);
            }

            void QuickIncrement(int value, int delta) {
                IncrementTotals(value, delta);
                counts[value] = (counts[value] + delta) & 0xFFFF;
            }

            void IncrementTotals(int value, int delta) {
                if (value >= lastBinStart) {
                    totals[15] = (totals[15] + delta) & 0xFFFF;
                    return;
                }

                for (int i = value >> binShift; i < 16; i++) {
                    totals[i] = (totals[i] + delta) & 0xFFFF;
                }
            }

            void DecrementCounts(int value, int amount) {
                counts[value] = (counts[value] - amount) & 0xFFFF;
                if (value >= lastBinStart) {
                    totals[15] = (totals[15] - amount) & 0xFFFF;
                    return;
                }

                for (int i = value >> binShift; i < 16; i++) {
                    totals[i] = (totals[i] - amount) & 0xFFFF;
                }
            }

            void Rescale() {
                BestShift(number + 1, out binShift, out lastBinStart);
                Span<int> bins = stackalloc int[16];
                counts[0] >>= 1;
                bins[0 < lastBinStart ? 0 : 15] += counts[0];
                int maxCount = 0;
                int maxPos = 0;
                int index = 1;
                bool done = false;
                while (index <= number && !done) {
                    while (counts[index] <= 1) {
                        if (index < number) {
                            counts[index] = counts[number];
                            values[index] = values[number];
                            counts[number] = 0;
                            number -= 1;
                        } else {
                            counts[index] = 0;
                            number -= 1;
                            done = true;
                            break;
                        }
                    }

                    if (done) {
                        break;
                    }

                    counts[index] >>= 1;
                    if (counts[index] > maxCount) {
                        maxCount = counts[index];
                        maxPos = index;
                    }

                    int bucket = index < lastBinStart ? (index >> binShift) : 15;
                    bins[bucket] += counts[index];
                    index += 1;
                }

                if (maxCount != 0) {
                    int swapPos = number < lastBinStart ? (number >> binShift) << binShift : lastBinStart;
                    if (swapPos == 0) {
                        swapPos = 1;
                    }

                    if (maxPos != swapPos) {
                        int oldCount = counts[swapPos];
                        counts[swapPos] = counts[maxPos];
                        int swapBucket = swapPos < lastBinStart ? (swapPos >> binShift) : 15;
                        int maxBucket = maxPos < lastBinStart ? (maxPos >> binShift) : 15;
                        bins[swapBucket] += -oldCount + counts[swapPos];
                        bins[maxBucket] += oldCount - counts[swapPos];
                        counts[maxPos] = oldCount;
                        int tmp = values[swapPos];
                        values[swapPos] = values[maxPos];
                        values[maxPos] = tmp;
                    }
                }

                if (number != uniqueValues && counts[0] == 0) {
                    counts[0] = (counts[0] + 2) & 0xFFFF;
                    bins[0 < lastBinStart ? 0 : 15] += 2;
                }

                int running = 0;
                for (int i = 0; i < 16; i++) {
                    running += bins[i];
                    totals[i] = running & 0xFFFF;
                }
            }

            /// <summary>Bin layout minimising the leftover of a 16-bin split of <paramref name="value"/> slots.</summary>
            static void BestShift(int value, out int shift, out int lastStart) {
                if (value < 6) {
                    shift = 15;
                    lastStart = 0;
                    return;
                }

                long bestMax = 0xFFFFFFFF;
                int bestBin = 0;
                for (int index = 0; index < 16; index++) {
                    int size = 1 << index;
                    int bins = Math.Min((value + size - 1) / size, 16);
                    long last = value - (long)size * (bins - 1);
                    if (last < size) {
                        last = size;
                    }

                    if (last < bestMax) {
                        bestBin = index;
                        bestMax = last;
                    }

                    if (size > value) {
                        break;
                    }
                }

                shift = bestBin;
                lastStart = 15 * (1 << bestBin);
            }
        }

        sealed class LzState {
            public readonly int MaxBytes;
            public readonly long MaxOffsets;
            public readonly int MaxOffsetLow;
            public readonly ArithModel Bytes;
            public readonly ArithModel[] Lengths = new ArithModel[MaxLens + 1];
            public readonly ArithModel OffsetLow;
            public readonly ArithModel OffsetHigh;
            public long BytesDecompressed;
            public int LastLength;

            public LzState(uint maxOffsetAndByte, uint uniqOffsetAndByte, uint uniqLens) {
                MaxBytes = (int)(maxOffsetAndByte & 0x1FF);
                MaxOffsets = maxOffsetAndByte >> 9;
                MaxOffsetLow = (int)Math.Min(MaxOffsets, LowOffsetMask + 1);
                Bytes = new ArithModel((int)(uniqOffsetAndByte & 0x1FF));
                for (int i = 0; i <= MaxLens; i++) {
                    // 65 length contexts in 4 groups of 16; each group sized by one byte of uniqLens (MSB first).
                    int group = Math.Min(i / (MaxLens / 4), 3);
                    Lengths[i] = new ArithModel((int)((uniqLens >> ((3 - group) * 8)) & 0xFF));
                }

                OffsetLow = new ArithModel(MaxOffsetLow);
                OffsetHigh = new ArithModel((int)(uniqOffsetAndByte >> 9));
            }
        }
    }
}
