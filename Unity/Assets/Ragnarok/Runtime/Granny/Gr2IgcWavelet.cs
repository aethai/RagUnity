// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/igc-idwt.js (clean-room port of RAD's reversible-integer lifting wavelet, granny2.dll @ 0x10009700).
// See LICENSE-granny-ro-js.txt.

namespace Ragnarok.Granny {
    /// <summary>
    /// Inverse DWT of one IGC plane pass: rows (lifting kernel, or Haar below 16 wide) into the temp plane, columns
    /// back into the plane, interleaved in strips of 8 rows. All pitches and offsets are in S16 elements.
    /// Arithmetic follows the JS exactly: terms are exact integers, the round-to-S16 step takes its sign from the
    /// low 32 bits, stores wrap to S16, out-of-range reads give 0 and out-of-range writes are dropped.
    /// Not thread-safe (scratch rings); one instance per decode.
    /// </summary>
    internal sealed class Gr2IgcWavelet {
        const int SmallestDwtRow = 16;
        const int SmallestDwtCol = 16;
        const int FlipSize = 8;

        readonly int[] rowLp = new int[8];
        readonly int[] rowHp = new int[8];
        readonly int[] colLp = new int[(16 + 4) * 4];
        readonly int[] colHp = new int[(16 + 5) * 4];
        readonly int[] remLp = new int[4];
        readonly int[] remHp = new int[5];

        public void Inverse2D(short[] output, int pitch, int width, int height, byte[] rowMask, short[] temp) {
            bool rowDwt = width >= SmallestDwtRow;
            bool colDwt = height >= SmallestDwtCol;
            int ry = height <= FlipSize + 4 + 4 ? height : FlipSize + 4;
            int rh = height - ry;
            int cy = 0;
            int ch = height;

            Rows(rowDwt, temp, pitch, output, pitch, width, rowMask, 0, ry);
            do {
                int next = ch <= FlipSize + 4 ? ch : FlipSize;
                if (colDwt) {
                    Columns(output, pitch, temp, pitch, width, height, cy, next);
                } else {
                    HaarColumns(output, pitch, temp, pitch, width, cy, next);
                }

                cy += next;
                ch -= next;
                if (rh != 0) {
                    next = rh <= FlipSize + 4 ? rh : FlipSize;
                    Rows(rowDwt, temp, pitch, output, pitch, width, rowMask, ry, next);
                    ry += next;
                    rh -= next;
                }
            } while (ch != 0);
        }

        void Rows(bool dwt, short[] dest, int destPitch, short[] src, int srcPitch, int width, byte[] rowMask, int startY, int subHeight) {
            if (dwt) {
                DwtRows(dest, destPitch, src, srcPitch, width, rowMask, startY, subHeight);
            } else {
                HaarRows(dest, destPitch, src, srcPitch, width, rowMask, startY, subHeight);
            }
        }

        static int Rd(short[] a, long i) {
            return i >= 0 && i < a.Length ? a[i] : 0;
        }

        static void Wr(short[] a, long i, int value) {
            if (i >= 0 && i < a.Length) {
                a[i] = unchecked((short)value);
            }
        }

        /// <summary>(x + (32767 ^ sign)) / 65536 truncated: signed round-half-away-from-zero to S16.</summary>
        static int RoundS16(long x) {
            int sign = unchecked((int)x) >> 31;
            return unchecked((int)((x + (32767 ^ sign)) / 65536));
        }

        static long Even(long lm1, long l0, long l1, long hm2, long hm1, long h0, long h1) {
            return l0 * 51674 - (lm1 + l1) * 2667 - (hm2 + h1) * 1563 + (hm1 + h0) * 24733;
        }

        static long Odd(long lm1, long l0, long l1, long l2, long hm2, long hm1, long h0, long h1, long h2) {
            return (l0 + l1) * 27400 - (lm1 + l2) * 4230 - h0 * 55882 - (hm2 + h2) * 2479 + (hm1 + h1) * 7250;
        }

        static long EvenLow(long lm1, long l0, long l1) {
            return l0 * 51674 - (lm1 + l1) * 2667;
        }

        static long OddLow(long lm1, long l0, long l1, long l2) {
            return (l0 + l1) * 27400 - (lm1 + l2) * 4230;
        }

        // wavelet.c iDWTrow: halfwidth low + halfwidth high S16s per row into a full row. lp is centred at +1,
        // hp at +2; a row flagged zero in rowMask drops the high-pass terms.
        void DwtRows(short[] dest, int destPitch, short[] src, int srcPitch, int width, byte[] rowMask, int startY, int subHeight) {
            int halfwidth = width >> 1;
            long outBase = (long)startY * destPitch;
            long linBase = (long)startY * srcPitch;
            long hinBase = (long)startY * srcPitch + halfwidth;
            int maskIdx = startY;
            int[] lp = rowLp;
            int[] hp = rowHp;

            for (int y = 0; y < subHeight; y++) {
                int next = 1;
                long xout = outBase;
                long xlin = linBase;
                long xhin = hinBase;
                long linEnd = linBase + halfwidth;

                lp[1] = Rd(src, xlin);
                lp[0] = lp[2] = Rd(src, xlin + 1);
                lp[3] = Rd(src, xlin + 2);
                lp[4] = Rd(src, xlin + 3);
                lp[5] = Rd(src, xlin + 4);
                lp[6] = Rd(src, xlin + 5);
                xlin += 6;

                hp[2] = Rd(src, xhin);
                hp[3] = Rd(src, xhin + 1);
                hp[4] = Rd(src, xhin + 2);
                hp[5] = Rd(src, xhin + 3);
                hp[6] = Rd(src, xhin + 4);
                hp[7] = Rd(src, xhin + 5);
                hp[0] = hp[3];
                hp[1] = hp[2];
                xhin += 6;

                bool nonZero = rowMask == null || (maskIdx < rowMask.Length && rowMask[maskIdx] != 0);
                int x = halfwidth < 8 ? 0 : (halfwidth - 8) / 4;
                int remainder = halfwidth < 8 ? halfwidth : (halfwidth & 3) + 8;

                if (nonZero) {
                    while (x-- > 0) {
                        for (int i = 0; i < 4; i++) {
                            long e = Even(lp[i], lp[i + 1], lp[i + 2], hp[i], hp[i + 1], hp[i + 2], hp[i + 3]);
                            long o = Odd(lp[i], lp[i + 1], lp[i + 2], lp[i + 3], hp[i], hp[i + 1], hp[i + 2], hp[i + 3], hp[i + 4]);
                            Wr(dest, xout + i * 2, RoundS16(e));
                            Wr(dest, xout + i * 2 + 1, RoundS16(o));
                        }

                        xout += 8;
                        lp[0] = lp[4];
                        lp[1] = lp[5];
                        lp[2] = lp[6];
                        lp[3] = Rd(src, xlin);
                        lp[4] = Rd(src, xlin + 1);
                        lp[5] = Rd(src, xlin + 2);
                        lp[6] = Rd(src, xlin + 3);
                        hp[0] = hp[4];
                        hp[1] = hp[5];
                        hp[2] = hp[6];
                        hp[3] = hp[7];
                        hp[4] = Rd(src, xhin);
                        hp[5] = Rd(src, xhin + 1);
                        hp[6] = Rd(src, xhin + 2);
                        hp[7] = Rd(src, xhin + 3);
                        xlin += 4;
                        xhin += 4;
                    }

                    while (remainder-- > 0) {
                        // End of the low band: mirror back.
                        if (xlin == linEnd) {
                            xlin -= next;
                            xhin -= next + next;
                            next = -next;
                        }

                        long e = Even(lp[0], lp[1], lp[2], hp[0], hp[1], hp[2], hp[3]);
                        long o = Odd(lp[0], lp[1], lp[2], lp[3], hp[0], hp[1], hp[2], hp[3], hp[4]);
                        Wr(dest, xout, RoundS16(e));
                        Wr(dest, xout + 1, RoundS16(o));
                        xout += 2;
                        for (int k = 0; k < 6; k++) {
                            lp[k] = lp[k + 1];
                        }

                        lp[6] = Rd(src, xlin);
                        for (int k = 0; k < 7; k++) {
                            hp[k] = hp[k + 1];
                        }

                        hp[7] = Rd(src, xhin);
                        xlin += next;
                        xhin += next;
                    }
                } else {
                    while (x-- > 0) {
                        for (int i = 0; i < 4; i++) {
                            Wr(dest, xout + i * 2, RoundS16(EvenLow(lp[i], lp[i + 1], lp[i + 2])));
                            Wr(dest, xout + i * 2 + 1, RoundS16(OddLow(lp[i], lp[i + 1], lp[i + 2], lp[i + 3])));
                        }

                        xout += 8;
                        lp[0] = lp[4];
                        lp[1] = lp[5];
                        lp[2] = lp[6];
                        lp[3] = Rd(src, xlin);
                        lp[4] = Rd(src, xlin + 1);
                        lp[5] = Rd(src, xlin + 2);
                        lp[6] = Rd(src, xlin + 3);
                        xlin += 4;
                    }

                    while (remainder-- > 0) {
                        if (xlin == linEnd) {
                            xlin -= 1;
                            next = -1;
                        }

                        Wr(dest, xout, RoundS16(EvenLow(lp[0], lp[1], lp[2])));
                        Wr(dest, xout + 1, RoundS16(OddLow(lp[0], lp[1], lp[2], lp[3])));
                        xout += 2;
                        for (int k = 0; k < 6; k++) {
                            lp[k] = lp[k + 1];
                        }

                        lp[6] = Rd(src, xlin);
                        xlin += next;
                    }
                }

                outBase += destPitch;
                linBase += srcPitch;
                hinBase += srcPitch;
                ++maskIdx;
            }
        }

        // wavelet.c iDWTcol: low rows and high rows interleaved in the source (pitch * 2 apart), 4 columns at a time
        // through a [20][4] / [21][4] ring, then single remainder columns.
        void Columns(short[] dest, int destPitch, short[] src, int srcPitch, int width, int height, int startY, int subHeight) {
            int halfheight = subHeight >> 1;
            long outBase = (long)startY * destPitch;
            long linBase = 0;
            long hinBase = srcPitch;
            long lendCol0 = (long)srcPitch * height;
            long ppitch2 = (long)srcPitch * 2;
            if (startY != 0) {
                linBase += (startY / 2 - 1) * ppitch2;
                hinBase += (startY / 2 - 2) * ppitch2;
            }

            long colsBase = 0;
            int groupCount = width / 4;
            int[] lp = colLp;
            int[] hp = colHp;
            for (int g = 0; g < groupCount; g++) {
                long next = ppitch2;
                long yout = outBase + colsBase;
                long ylin = linBase + colsBase;
                long yhin = hinBase + colsBase;
                long lend = lendCol0 + colsBase;

                if (startY != 0) {
                    for (int r = 0; r < 4; r++) {
                        LoadRow(lp, r, src, ylin);
                        ylin += next;
                    }

                    for (int r = 0; r < 5; r++) {
                        LoadRow(hp, r, src, yhin);
                        yhin += next;
                    }
                } else {
                    LoadRow(lp, 1, src, ylin);
                    ylin += next;
                    LoadRow(lp, 0, src, ylin);
                    LoadRow(lp, 2, src, ylin);
                    ylin += next;
                    LoadRow(lp, 3, src, ylin);
                    ylin += next;

                    LoadRow(hp, 1, src, yhin);
                    LoadRow(hp, 2, src, yhin);
                    yhin += next;
                    LoadRow(hp, 0, src, yhin);
                    LoadRow(hp, 3, src, yhin);
                    yhin += next;
                    LoadRow(hp, 4, src, yhin);
                    yhin += next;
                }

                int lpOff = 0;
                int hpOff = 0;
                for (int y = 0; y < halfheight; y++) {
                    if (ylin == lend) {
                        ylin -= next;
                        yhin -= next + next;
                        next = -next;
                    }

                    for (int k = 0; k < 4; k++) {
                        int l = lpOff * 4 + k;
                        int h = hpOff * 4 + k;
                        long e = Even(lp[l], lp[l + 4], lp[l + 8], hp[h], hp[h + 4], hp[h + 8], hp[h + 12]);
                        long o = Odd(lp[l], lp[l + 4], lp[l + 8], lp[l + 12], hp[h], hp[h + 4], hp[h + 8], hp[h + 12], hp[h + 16]);
                        Wr(dest, yout + k, RoundS16(e));
                        Wr(dest, yout + destPitch + k, RoundS16(o));
                    }

                    yout += destPitch + destPitch;
                    ++lpOff;
                    ++hpOff;

                    // Re-centre at the end of the ring. The JS reads one entry past each ring (0) here, which the
                    // C original reads from the adjacent array; with strips of at most 12 rows this never runs.
                    if (lpOff + 3 == 16 + 4) {
                        for (int k = 0; k < 4; k++) {
                            lp[k] = RingAt(lp, (lpOff + 1) * 4 + k);
                            lp[4 + k] = RingAt(lp, (lpOff + 2) * 4 + k);
                            lp[8 + k] = RingAt(lp, (lpOff + 3) * 4 + k);
                            hp[k] = RingAt(hp, (hpOff + 1) * 4 + k);
                            hp[4 + k] = RingAt(hp, (hpOff + 2) * 4 + k);
                            hp[8 + k] = RingAt(hp, (hpOff + 3) * 4 + k);
                            hp[12 + k] = RingAt(hp, (hpOff + 4) * 4 + k);
                        }

                        lpOff = 0;
                        hpOff = 0;
                    }

                    LoadRow(lp, lpOff + 3, src, ylin);
                    LoadRow(hp, hpOff + 4, src, yhin);
                    ylin += next;
                    yhin += next;
                }

                colsBase += 4;
            }

            int remCols = width & 3;
            for (int g = 0; g < remCols; g++) {
                long next = ppitch2;
                long yout = outBase + colsBase;
                long ylin = linBase + colsBase;
                long yhin = hinBase + colsBase;
                long lend = lendCol0 + colsBase;
                int[] l = remLp;
                int[] h = remHp;

                if (startY != 0) {
                    for (int r = 0; r < 4; r++) {
                        l[r] = Rd(src, ylin);
                        ylin += next;
                    }

                    for (int r = 0; r < 5; r++) {
                        h[r] = Rd(src, yhin);
                        yhin += next;
                    }
                } else {
                    l[1] = Rd(src, ylin);
                    ylin += next;
                    l[0] = l[2] = Rd(src, ylin);
                    ylin += next;
                    l[3] = Rd(src, ylin);
                    ylin += next;
                    h[1] = h[2] = Rd(src, yhin);
                    yhin += next;
                    h[0] = h[3] = Rd(src, yhin);
                    yhin += next;
                    h[4] = Rd(src, yhin);
                    yhin += next;
                }

                for (int y = 0; y < halfheight; y++) {
                    if (ylin == lend) {
                        ylin -= next;
                        yhin -= next + next;
                        next = -next;
                    }

                    Wr(dest, yout, RoundS16(Even(l[0], l[1], l[2], h[0], h[1], h[2], h[3])));
                    Wr(dest, yout + destPitch, RoundS16(Odd(l[0], l[1], l[2], l[3], h[0], h[1], h[2], h[3], h[4])));
                    yout += destPitch + destPitch;
                    l[0] = l[1];
                    l[1] = l[2];
                    l[2] = l[3];
                    l[3] = Rd(src, ylin);
                    h[0] = h[1];
                    h[1] = h[2];
                    h[2] = h[3];
                    h[3] = h[4];
                    h[4] = Rd(src, yhin);
                    ylin += next;
                    yhin += next;
                }

                colsBase += 1;
            }
        }

        static void LoadRow(int[] ring, int entry, short[] src, long index) {
            int b = entry * 4;
            for (int k = 0; k < 4; k++) {
                ring[b + k] = Rd(src, index + k);
            }
        }

        static int RingAt(int[] ring, int index) {
            return index < ring.Length ? ring[index] : 0;
        }

        // wavelet.c iHarrrow (narrow bands). An out-of-range sample makes the JS compute NaN, which lands as 0.
        static void HaarRows(short[] dest, int destPitch, short[] src, int srcPitch, int width, byte[] rowMask, int startY, int subHeight) {
            int halfwidth = width >> 1;
            long outBase = (long)startY * destPitch;
            long linBase = (long)startY * srcPitch;
            long hinBase = (long)startY * srcPitch + halfwidth;
            int maskIdx = startY;
            for (int y = 0; y < subHeight; y++) {
                long xout = outBase;
                long xlin = linBase;
                long xhin = hinBase;
                bool nonZero = rowMask == null || (maskIdx < rowMask.Length && rowMask[maskIdx] != 0);
                for (int x = 0; x < halfwidth; x++) {
                    if (nonZero) {
                        HaarPair(src, xlin, xhin, out int e, out int o);
                        Wr(dest, xout, e);
                        Wr(dest, xout + 1, o);
                        xhin += 1;
                    } else {
                        int e = Rd(src, xlin);
                        Wr(dest, xout, e);
                        Wr(dest, xout + 1, e);
                    }

                    xout += 2;
                    xlin += 1;
                }

                outBase += destPitch;
                linBase += srcPitch;
                hinBase += srcPitch;
                ++maskIdx;
            }
        }

        // wavelet.c iHarrcol.
        static void HaarColumns(short[] dest, int destPitch, short[] src, int srcPitch, int width, int startY, int subHeight) {
            int halfheight = subHeight >> 1;
            long outBase = (long)startY * destPitch;
            long linBase = 0;
            long hinBase = srcPitch;
            long ppitch2 = (long)srcPitch * 2;
            if (startY != 0) {
                linBase += (startY / 2) * ppitch2;
                hinBase += (startY / 2) * ppitch2;
            }

            for (int x = 0; x < width; x++) {
                long yout = outBase + x;
                long ylin = linBase + x;
                long yhin = hinBase + x;
                for (int y = 0; y < halfheight; y++) {
                    HaarPair(src, ylin, yhin, out int e, out int o);
                    Wr(dest, yout, e);
                    Wr(dest, yout + destPitch, o);
                    yout += destPitch + destPitch;
                    ylin += ppitch2;
                    yhin += ppitch2;
                }
            }
        }

        static void HaarPair(short[] src, long lowIndex, long highIndex, out int e, out int o) {
            if (lowIndex < 0 || lowIndex >= src.Length || highIndex < 0 || highIndex >= src.Length) {
                e = 0;
                o = 0;
                return;
            }

            int lv = src[lowIndex];
            int hv = src[highIndex];
            e = lv * 2 + hv;
            o = lv * 2 - hv;
            e = (e + (1 ^ (e >> 31))) / 2;
            o = (o + (1 ^ (o >> 31))) / 2;
        }
    }
}
