// Synced from reference/port/src/RoClassicFx/Rag/RagEffect.Str.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Text;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEZeffectRes (STR) image in FxMemory, laid out like vf05 0x6c7380 builds it:
    /// res+0x110 maxKey, +0x114 fps, +0x118 layer count, layers from +0x120 (0x380 each):
    /// +0 texture count, +0x8.. texture cache, +0x1c0 110 name pointers, +0x378 key count,
    /// +0x37c key array (0x7c bytes per key, raw file layout).
    /// </summary>
    public static class FxStrResource
    {
        public const uint ResSize = 0x120;
        public const uint LayerSize = 0x380;
        public const uint KeySize = 0x7c;

        public static uint Load(FxMemory m, byte[] file)
        {
            if (file.Length < 0x28 || file[0] != 'S' || file[1] != 'T' || file[2] != 'R' || file[3] != 'M')
                throw new FormatException("not an STR file");
            int p = 4;
            int Int() { int v = BitConverter.ToInt32(file, p); p += 4; return v; }
            int version = Int();
            if (version != 0x94) throw new FormatException($"STR version 0x{version:x} unsupported");
            int fps = Int(), maxKey = Int(), layers = Int();
            p += 0x10;
            uint res = m.Alloc(ResSize + (uint)Math.Max(layers, 0) * LayerSize);
            m.I32(res + 0x114, fps);
            m.I32(res + 0x110, maxKey);
            m.I32(res + 0x118, layers);
            uint defaultName = CString(m, "1.bmp");
            for (int l = 0; l < layers; l++)
            {
                uint layer = res + 0x120 + (uint)l * LayerSize;
                int texCount = Int();
                m.I32(layer, texCount);
                int t = 0;
                for (; t < texCount; t++)
                {
                    uint name = m.Alloc(0x80);
                    for (int i = 0; i < 0x80; i++) m.U8(name + (uint)i, file[p + i]);
                    p += 0x80;
                    if (t < 0x6e) m.U32(layer + 0x1c0 + (uint)t * 4, name);
                }
                for (; t < 0x6e; t++) m.U32(layer + 0x1c0 + (uint)t * 4, defaultName);
                int keys = Int();
                m.I32(layer + 0x378, keys);
                if (keys > 0)
                {
                    uint arr = m.Alloc((uint)keys * KeySize);
                    for (int i = 0; i < keys * (int)KeySize; i++) m.U8(arr + (uint)i, file[p + i]);
                    p += keys * (int)KeySize;
                    m.U32(layer + 0x37c, arr);
                }
            }
            return res;
        }

        static uint CString(FxMemory m, string s)
        {
            var b = Encoding.ASCII.GetBytes(s);
            uint a = m.Alloc((uint)b.Length + 1);
            for (int i = 0; i < b.Length; i++) m.U8(a + (uint)i, b[i]);
            return a;
        }
    }

    public readonly partial struct RagEffect
    {
        /// <summary>COMISS a,b + JC: taken when a &lt; b or unordered.</summary>
        static bool Jc(float a, float b) => a < b || float.IsNaN(a) || float.IsNaN(b);

        /// <summary>Tail of CRagEffect::Init (0xb422b0) once the STR resource is resolved.</summary>
        public void AttachStr(uint res)
        {
            M.U32(A + 0x7ec, res);
            if (res == 0) return;
            M.U32(A + 0x7f0, res + 0x110);
            int layers = M.I32(res + 0x118);
            M.I32(A + 0x119f8, layers);
            if (M.I32(res + 0x498) == 0) M.I32(A + 0x119f8, layers - 1);
        }

        /// <summary>CRagEffect::fn_b43cb0 (0xb43cb0) — restart the STR (repeat).</summary>
        public void StrRestart()
        {
            uint str = M.U32(A + 0x7f0);
            M.I32(A + 0x119f4, 0);
            m_stateCnt = 0;
            int layers = M.I32(str + 8);
            M.I32(A + 0x119f8, layers);
            if (M.I32(str + 0x388) == 0) M.I32(A + 0x119f8, layers - 1);
            for (int i = 0; i < M.I32(M.U32(A + 0x7f0) + 8); i++) M.I32(A + 0x11a7c + (uint)i * 4, 0);
        }

        /// <summary>
        /// CRagEffect::fn_b5c890 (0xb5c890) — advance every STR layer by one tick. Key type 0 copies
        /// absolute values; type 1 adds per-tick deltas; animation type drives the texture frame.
        /// Translated from the assembly (stale XMM3 in anim types 3/4 is reproduced deliberately).
        /// Returns false when the STR (including repeats) has finished.
        /// </summary>
        public bool StrPlay(FxRuntime rt, float dx, float dy, float dz)
        {
            if (M.U32(A + 0x7ec) == 0) return false;
            if (M.I32(A + 0x119f8) <= M.I32(A + 0x119f4))
            {
                int repeat = M.I32(A + 0x11c7c) + 1;
                M.I32(A + 0x11c7c, repeat);
                if (M.I32(A + 0x11c80) < repeat) return false;
                StrRestart();
            }

            uint masterPos = M.U32(A + 0x138) + 0x10;
            uint view = M.U32(rt.Mode + 0xd0);
            rt.ProjectTl(view + 0x98, masterPos, out float sx, out float sy, out float sz);
            f_154 = sx + dx;
            f_158 = sy + dy;
            f_15c = sz + dz;
            M.I32(A + 0x160, dx == 0f && dy == -80.0f && dz == 0f ? 0 : 1);

            uint str = M.U32(A + 0x7f0);
            if (M.I32(str + 8) <= 0) return true;

            uint visible = A + 0x119fc;
            float xmm3 = BitConverter.Int32BitsToSingle((int)visible);   // stale register: address bits
            const float xmm4 = 0f;
            uint layerOff = 0;
            uint state = A + 0x7f4;
            uint keyIndexAddr = A + 0x11a7c;
            for (int layerIndex = 0; ; )
            {
                M.U8(visible, 0);
                uint layer = layerOff + 0x10 + str;
                bool skip = layer == 0 || M.I32(layer + 0x378) == 0 || M.I32(keyIndexAddr) == -1;
                if (!skip)
                {
                    int keyIndex = M.I32(keyIndexAddr);
                    uint key = (uint)(keyIndex * (int)FxStrResource.KeySize) + M.U32(layerOff + str + 0x38c);
                    int elapsed = m_stateCnt - M.I32(key);
                    if (elapsed < 0) skip = true;
                    else
                    {
                        if (elapsed != 0 && M.I32(key + 0x7c) == m_stateCnt)
                        {
                            key += 0x7c;
                            int next = M.I32(keyIndexAddr) + 1;
                            M.I32(keyIndexAddr, next);
                            if (next >= M.I32(layerOff + str + 0x388) - 1)
                            {
                                M.I32(keyIndexAddr, -1);
                                M.I32(A + 0x119f4, M.I32(A + 0x119f4) + 1);
                            }
                        }
                        if (M.I32(key + 4) == 0)
                        {
                            M.Copy(state, key + 8, 0x1d * 4);
                            int idx = M.I32(keyIndexAddr);
                            if (idx != -1 && M.I32(key + 0x7c) == M.I32(key))
                            {
                                M.I32(keyIndexAddr, idx + 1);
                                M.U8(visible, 1);
                                str = M.U32(A + 0x7f0);
                                goto Next;
                            }
                        }
                        else
                        {
                            if (FxMemory.CanSpanF32(state) && FxMemory.CanSpanF32(key))
                            {
                                // the same adds on spans over the layer state (0x1d floats) and the key (0x1f floats):
                                // one range check each instead of one per float; operand order kept
                                Span<float> st = M.F32Span(state, 0x1d);
                                Span<float> ky = M.F32Span(key, 0x1f);
                                for (int k = 0; k < 0x12; k++) st[k] = st[k] + ky[2 + k];   // pos(2) + uv(8) + xy(8)
                                for (int k = 0x14; k < 0x18; k++) st[k + 2] = ky[0x18 + (k - 0x14)] + st[k + 2];
                                st[0x15] = ky[0x17] + st[0x15];
                            }
                            else
                            {
                                for (uint k = 0; k < 0x12; k++)   // pos(2) + uv(8) + xy(8)
                                    M.F32(state + k * 4, M.F32(state + k * 4) + M.F32(key + 8 + k * 4));
                                // (the first two adds are "state + key" and "key + state"; float + is commutative)
                                for (uint k = 0x14; k < 0x18; k++)
                                    M.F32(state + k * 4 + 8, M.F32(key + 0x18 * 4 + (k - 0x14) * 4) + M.F32(state + k * 4 + 8));
                                M.F32(state + 0x13 * 4 + 8, M.F32(key + 0x17 * 4) + M.F32(state + 0x13 * 4 + 8));
                            }
                            uint frameAddr = state + 0x10 * 4 + 8;
                            float delay = M.F32(key + 0x58);
                            switch (M.I32(key + 0x54))
                            {
                                case 1:
                                    M.F32(frameAddr, M.F32(key + 0x50) + M.F32(frameAddr));
                                    M.U8(visible, 1);
                                    str = M.U32(A + 0x7f0);
                                    goto Next;
                                case 2:
                                {
                                    float f = delay + M.F32(frameAddr);
                                    M.F32(frameAddr, f);
                                    float count = (float)M.I32(layer);
                                    if (!Jc(f, count)) M.F32(frameAddr, count - 1.0f);
                                    break;
                                }
                                case 3:
                                {
                                    float f = delay + M.F32(frameAddr);
                                    M.F32(frameAddr, f);
                                    float count = (float)M.I32(layer);
                                    if (!Jc(f, count))
                                    {
                                        xmm3 = (float)FxOps.Cvtt(f / count);
                                        M.F32(frameAddr, f - count * xmm3);
                                    }
                                    break;
                                }
                                case 4:
                                {
                                    float f = M.F32(frameAddr) - delay;
                                    M.F32(frameAddr, f);
                                    if (xmm4 > f)
                                    {
                                        float count = (float)M.I32(layer);
                                        f -= count * xmm3;
                                        xmm3 = (float)FxOps.Cvtt(f / count);
                                        if (xmm4 > f) f += count;
                                        M.F32(frameAddr, f);
                                    }
                                    break;
                                }
                                case 5:
                                {
                                    int last = M.I32(layer) - 1;
                                    int pos = FxOps.Cvtt((float)elapsed * delay + M.F32(frameAddr));
                                    int q = pos / last;
                                    int frame = (q & 1) != 0 ? (q + 1) * last - pos : pos - q * last;
                                    M.F32(frameAddr, (float)frame);
                                    M.U8(visible, 1);
                                    str = M.U32(A + 0x7f0);
                                    goto Next;
                                }
                            }
                        }
                        M.U8(visible, 1);
                        str = M.U32(A + 0x7f0);
                    }
                }
            Next:
                visible++;
                keyIndexAddr += 4;
                layerOff += FxStrResource.LayerSize;
                state += 0x74;
                layerIndex++;
                if (layerIndex >= M.I32(str + 8)) break;
            }
            return true;
        }
    }
}
