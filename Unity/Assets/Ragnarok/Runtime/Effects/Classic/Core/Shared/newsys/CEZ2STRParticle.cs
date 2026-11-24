// Synced from reference/port/src/RoClassicFx/Shared/newsys/CEZ2STRParticle.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// View of CEZ2STRParticle (0x3fe8 bytes, vtable 0xfe1cec) — the new system's STR player: a CParticle whose
    /// extension plays an STR resource (CEZeffectRes, the same object the old engine plays) as screen-space
    /// quads anchored at the particle's projected position. Pooled (CEffectMemoryMgr pool 2).
    /// Layout beyond CParticle: +0x2bc resource, +0x2c0 its animation clip (resource +0x110: +8 layer count,
    /// +0x10 layers of 0x380, key count at layer +0x378, keys (0x7c bytes) at +0x37c), +0x2c4 per-layer state
    /// (0x74 bytes = 29 floats each, up to 128 layers), +0x3cc4 finished layers, +0x3cc8 layers to finish,
    /// +0x3ccc per-layer visible bytes, +0x3d4c per-layer key index (-1 = done), +0x3f4c/+0x3f50/+0x3f54
    /// projected anchor, +0x3f58 frame, +0x3f5c loop, +0x3f60 resource name (std::string), +0x3f78 its
    /// directory (std::string), +0x3f90..+0x3fa0 stop frame / stopping / fade / repeats / start delay,
    /// +0x3fa4 rotation, +0x3fa8 scale, +0x3fac alpha scale, +0x3fb0..+0x3fbc UI anchoring, +0x3fc4/+0x3fc8
    /// owner-size flag / owner AID, +0x3fcc/+0x3fd0 screen offset, +0x3fd8 int64 height, +0x3fe0/+0x3fe1 UV flips.
    /// </summary>
    public readonly partial struct CEZ2STRParticle
    {
        public readonly FxMemory M;
        public readonly uint A;

        public CEZ2STRParticle(FxMemory m, uint a) { M = m; A = a; }

        public const uint VtCEZ2STRParticle = 0x00fe1cec;
        public const uint Size = 0x3fe8;
        const uint VtCEZeffectRes = 0x00f8327c;

        /// <summary>CEZ2STRParticle::CEZ2STRParticle (FUN_00a64cd0; x, y, z, flags).</summary>
        public static void Construct_a64cd0(FxRuntime rt, uint p, float x, float y, float z, int flags)
        {
            var M = rt.M;
            CParticle.Construct_a67480(M, p, x, y, z, flags);
            M.U32(p, VtCEZ2STRParticle);
            rt.StrInitEmpty(p + 0x3f60);
            rt.StrInitEmpty(p + 0x3f78);
            M.U32(p + 0x3fb8, 0);
            M.U32(p + 0x3fbc, 0);
            M.U32(p + 0x3fc0, 0);
            M.U16(p + 0x3fe2, 0);
            new CParticle(M, p).Reset_a677e0();
            ResetTail(M, p);
        }

        /// <summary>FUN_00a65410 — the reset a pooled STR particle gets when handed out again (the resource is not released).</summary>
        public void Reset_a65410()
        {
            new CParticle(M, A).Reset_a677e0();
            ResetTail(M, A);
        }

        static void ResetTail(FxMemory M, uint p)
        {
            M.U32(p + 0x2bc, 0);
            M.U32(p + 0x2c0, 0);
            M.U32(p + 0x3cc4, 0);
            M.U32(p + 0x3f58, 0);
            M.U8(p + 0x3f5c, 0);
            M.Fill(p + 0x3d4c, 0, 0x200);
            M.Fill(p + 0x2c4, 0, 0x3a00);
            M.U32(p + 0x3f90, 0);
            M.U8(p + 0x3f94, 0);
            M.U32(p + 0x3f98, 0);
            M.U32(p + 0x3f9c, 0);
            M.U32(p + 0x3fa0, 0);
            M.U32(p + 0x3fa4, 0);
            M.F32(p + 0x3fa8, 1.0f);
            M.F32(p + 0x3fac, 1.0f);
            M.I32(p + 0x3fb0, -1);
            M.U16(p + 0x3fb4, 0x100);
            M.U32(p + 0x3fb8, 0);
            M.U32(p + 0x3fbc, 0);
            M.U32(p + 0x3fc0, 0);
            M.U32(p + 0x3fcc, 0);
            M.U32(p + 0x3fd0, 0);
            M.U32(p + 0x3fd8, 0);
            M.U32(p + 0x3fdc, 0);
            M.U8(p + 0x3fc4, 0);
            M.U16(p + 0x3fe0, 0);
        }

        /// <summary>
        /// FUN_00a655c0 — load an STR: release the previous resource, CResMgr::Get(name) → +0x2bc, name → +0x3f60,
        /// its directory (_splitpath) → +0x3f78 (by move from a local std::string), then restart (FUN_00a65530).
        /// </summary>
        public void Load_a655c0(FxRuntime rt, string name)
        {
            uint old = M.U32(A + 0x2bc);
            if (old != 0) rt.ResRelease(old);
            rt.GetResMgr();
            M.U32(A + 0x2bc, rt.ResGet(name));
            rt.StrAssign(A + 0x3f60, name);
            string dir = FxRuntime.SplitPathDir(name);
            // local std::string (dir) moved into +0x3f78: the old value is destroyed, the 16-byte buffer copied
            uint local = rt.StrLocalAssign(dir);
            rt.StrDestroy(A + 0x3f78);
            if (dir.Length < 0x10)
            {
                for (uint i = 0; i < 0x10; i++) M.U8(A + 0x3f78 + i, i < dir.Length ? (byte)dir[(int)i] : (byte)0);
                M.U32(A + 0x3f88, (uint)dir.Length);
                M.U32(A + 0x3f8c, 0xf);
            }
            else
            {
                M.U32(A + 0x3f78, local);
                M.U32(A + 0x3f88, (uint)dir.Length);
                M.U32(A + 0x3f8c, (uint)dir.Length | 0xf);
            }
            Restart_a65530(rt);
        }

        /// <summary>
        /// FUN_00a65530 — (re)start the STR: finished layers and frame 0, AddRef, clip = resource +0x110, layers to
        /// finish = layer count (minus one when layer 0 has no keys), key indices 0, all layer states cleared.
        /// </summary>
        public void Restart_a65530(FxRuntime rt)
        {
            uint res = M.U32(A + 0x2bc);
            if (res == 0) return;
            M.U32(A + 0x3cc4, 0);
            M.U32(A + 0x3f58, 0);
            rt.ResAddRef(res);
            uint clip = M.U32(A + 0x2bc) + 0x110;
            M.U32(A + 0x2c0, clip);
            int layers = M.I32(clip + 8);
            M.I32(A + 0x3cc8, layers);
            if (M.I32(clip + 0x388) == 0) M.I32(A + 0x3cc8, layers - 1);
            for (int i = 0; i < M.I32(M.U32(A + 0x2c0) + 8); i++) M.U32(A + 0x3d4c + (uint)i * 4, 0);
            M.Fill(A + 0x2c4, 0, 0x3a00);
        }

        /// <summary>
        /// FUN_00a65840 (thiscall, dx, dy, dz; ret 0xc) — one STR frame: start delay, end handling (stop frame /
        /// repeats / loop: a finished, non-looping STR without repeats ends here: +0xcc = -1, false), anchor =
        /// projected position + (dx, dy, dz), then every layer advances its keys (type 0 absolute, else deltas; the
        /// texture frame by animation type 1..5) and is marked visible. Returns true while playing.
        /// </summary>
        public bool Process_a65840(FxRuntime rt, float dx, float dy, float dz)
        {
            if (M.U32(A + 0x2bc) == 0) return false;
            int delay = M.I32(A + 0x3fa0);
            if (delay > 0)
            {
                M.I32(A + 0x3fa0, delay - 0x10);
                return true;
            }
            int stop = M.I32(A + 0x3f98);
            bool stopping;
            if (stop != 0)
            {
                M.U8(A + 0x3f94, 1);
                stopping = true;
            }
            else stopping = M.U8(A + 0x3f94) == 1;
            if (stopping && M.I32(A + 0x3f58) - stop >= 0) return Dead();
            if (M.I32(A + 0x3cc4) >= M.I32(A + 0x3cc8))
            {
                if (M.U8(A + 0x3f5c) == 0)
                {
                    int frame = M.I32(A + 0x3f58);
                    if (frame - M.I32(A + 0x3f90) >= 0 && frame - stop >= 0)
                    {
                        int repeats = M.I32(A + 0x3f9c);
                        if (repeats <= 0) return Dead();
                        M.I32(A + 0x3f9c, repeats - 1);
                    }
                }
                Restart_a65530(rt);
            }
            uint view = M.U32(rt.Mode + 0xd0) + 0x98;
            rt.Renderer.ProjectVertex(M, view, M.F32(A + 0x24), M.F32(A + 0x28), M.F32(A + 0x2c), out float sx, out float sy, out float sz, out _);
            M.F32(A + 0x3f4c, sx + dx);
            M.F32(A + 0x3f50, sy + dy);
            M.F32(A + 0x3f54, sz + dz);
            int flags = M.I32(A + 0x1c);
            if (flags == 0x205 || flags == 0x225)
                throw new NotSupportedException("screen-anchored (UI) STR particles are not ported");

            uint clip = M.U32(A + 0x2c0);
            if (M.I32(clip + 8) > 0)
            {
                uint visible = A + 0x3ccc, keyIdx = A + 0x3d4c, state = A + 0x2c4, layerOff = 0;
                for (;;)
                {
                    M.U8(visible, 0);
                    uint res = M.U32(A + 0x2c0);
                    uint layer = layerOff + 0x10 + res;
                    if (layer != 0 && M.I32(layer + 0x378) != 0 && M.I32(keyIdx) != -1)
                        StepLayer(layer, res, layerOff, state, keyIdx, visible);
                    clip = M.U32(A + 0x2c0);
                    visible++;
                    keyIdx += 4;
                    layerOff += 0x380;
                    state += 0x74;
                    if ((int)(visible - (A + 0x3ccc)) >= M.I32(clip + 8)) break;
                }
            }
            M.I32(A + 0x3f58, M.I32(A + 0x3f58) + 1);
            if (M.U8(A + 0x3f94) == 1) M.I32(A + 0x3f98, M.I32(A + 0x3f98) - 0x10);
            return true;
        }

        bool Dead()
        {
            M.F32(A + 0xcc, -1.0f);
            return false;
        }

        void StepLayer(uint layer, uint res, uint layerOff, uint state, uint keyIdx, uint visible)
        {
            int keyCount = M.I32(layer + 0x378);
            int idx = M.I32(keyIdx);
            uint key = (uint)(idx * 0x7c) + M.U32(layerOff + res + 0x38c);
            int frame = M.I32(A + 0x3f58);
            int elapsed = frame - M.I32(key);
            if (elapsed < 0) return;
            bool advance;
            if (keyCount == 1) advance = M.I32(key) == frame;
            else if (idx + 1 >= keyCount) advance = M.I32(key) == frame;
            else advance = elapsed != 0 && M.I32(key + 0x7c) == frame;
            if (advance)
            {
                int next = idx + 1;
                M.I32(keyIdx, next);
                if (M.I32(layer + 0x378) - 1 > next) key += 0x7c;
                else
                {
                    M.I32(keyIdx, -1);
                    M.I32(A + 0x3cc4, M.I32(A + 0x3cc4) + 1);
                }
            }
            if (M.I32(key + 4) == 0)
            {
                M.Copy(state, key + 8, 0x1d * 4);
                int i2 = M.I32(keyIdx);
                if (i2 != -1)
                {
                    int count = M.I32(layer + 0x378);
                    if (count > 1 && i2 + 1 < count && M.I32(key + 0x7c) == M.I32(key)) M.I32(keyIdx, i2 + 1);
                }
                M.U8(visible, 1);
                return;
            }
            for (uint k = 0; k < 0x12; k++) M.F32(state + k * 4, M.F32(key + 8 + k * 4) + M.F32(state + k * 4));
            for (uint k = 0; k < 4; k++) M.F32(state + 0x58 + k * 4, M.F32(key + 0x60 + k * 4) + M.F32(state + 0x58 + k * 4));
            M.F32(state + 0x54, M.F32(key + 0x5c) + M.F32(state + 0x54));
            uint frameAddr = state + 0x48;
            float delay = M.F32(key + 0x58);
            switch (M.I32(key + 0x54))
            {
                case 1:
                    M.F32(frameAddr, M.F32(key + 0x50) + M.F32(frameAddr));
                    break;
                case 2:
                {
                    float f = delay + M.F32(frameAddr);
                    M.F32(frameAddr, f);
                    float count = (float)M.I32(layer);
                    if (!(f < count || float.IsNaN(f) || float.IsNaN(count))) M.F32(frameAddr, count - 1.0f);   // COMISS; JB
                    break;
                }
                case 3:
                {
                    float f = delay + M.F32(frameAddr);
                    M.F32(frameAddr, f);
                    float count = (float)M.I32(layer);
                    if (!(f < count || float.IsNaN(f) || float.IsNaN(count)))
                    {
                        float q = (float)FxOps.Cvtt(f / count);
                        q *= count;
                        M.F32(frameAddr, f - q);
                    }
                    break;
                }
                case 4:
                {
                    float f = M.F32(frameAddr) - delay;
                    M.F32(frameAddr, f);
                    if (0f > f)
                    {
                        float count = (float)M.I32(layer);
                        f -= count * 0f;
                        if (0f > f) f += count;
                        M.F32(frameAddr, f);
                    }
                    break;
                }
                case 5:
                {
                    int last = M.I32(layer) - 1;
                    float fp = (float)elapsed * delay;
                    fp += M.F32(frameAddr);
                    int pos = FxOps.Cvtt(fp);
                    int q = pos / last;
                    M.F32(frameAddr, (q & 1) != 0 ? (float)((q + 1) * last - pos) : (float)(pos - q * last));
                    break;
                }
            }
            M.U8(visible, 1);
        }

        /// <summary>FUN_00a660b0 — draws every visible layer but layer 0 (only while +0x3fb5 is set).</summary>
        public void Render_a660b0(FxRuntime rt, uint viewMatrix)
        {
            uint clip = M.U32(A + 0x2c0);
            if (clip == 0 || M.U8(A + 0x3fb5) == 0) return;
            uint state = A + 0x338;
            uint layerOff = 0x380;
            for (int i = 1; i < M.I32(clip + 8); i++, state += 0x74, layerOff += 0x380)
            {
                if (M.U8(A + 0x3ccc + (uint)i) != 0)
                {
                    RenderLayer_a66680(rt, clip + 0x10 + layerOff, state, viewMatrix, i);
                    clip = M.U32(A + 0x2c0);
                }
            }
        }

        /// <summary>
        /// FUN_00a66680 (thiscall; layer, state, matrix, index; ret 0x10) — one layer as a pooled RP quad
        /// (FUN_00521e00): corners / UVs from the state, rotated by angle / 2.8444 degrees, scaled by the screen
        /// scale (xfactor · rhw / 7) around the projected anchor, then by +0x3fa8 about the projected position,
        /// optionally turned by +0x3fa4 degrees, shifted by +0x3fcc/+0x3fd0 and the int64 height +0x3fd8; UVs
        /// scaled to the texture's image/surface ratio, colour with alpha · +0x3fac, rhw lowered by index · 1e-5,
        /// optional UV flips; flags +0x1c |= 0x20; submitted through the callback +0x2b8 (FUN_00a68060 → AddRP).
        /// </summary>
        void RenderLayer_a66680(FxRuntime rt, uint layer, uint p, uint viewMatrix, int index)
        {
            var r = rt.Renderer;
            uint quad = r.AcquireQuad();
            uint v = M.U32(quad);
            var mem = M;
            float P(int i) => mem.F32(p + (uint)i * 4);
            void V(int i, float value) => mem.F32(v + (uint)i * 4, value);

            if (P(0xb) > P(0xa))
            {
                M.U32(v, M.U32(p + 0xd * 4)); M.U32(v + 0x20, M.U32(p + 0xa * 4)); M.U32(v + 0x40, M.U32(p + 0xc * 4)); M.U32(v + 0x60, M.U32(p + 0xb * 4));
                V(0xe, P(2)); V(6, P(2));
                float u = P(4) + P(2);
                V(0x1e, u); V(0x16, u);
            }
            else
            {
                M.U32(v, M.U32(p + 0xc * 4)); M.U32(v + 0x20, M.U32(p + 0xb * 4)); M.U32(v + 0x40, M.U32(p + 0xd * 4)); M.U32(v + 0x60, M.U32(p + 0xa * 4));
                float u = P(4) + P(2);
                V(0xe, u); V(6, u);
                V(0x1e, P(2)); V(0x16, P(2));
            }
            if (P(0x10) > P(0xf))
            {
                M.U32(v + 4, M.U32(p + 0x11 * 4)); M.U32(v + 0x24, M.U32(p + 0xe * 4)); M.U32(v + 0x44, M.U32(p + 0x10 * 4)); M.U32(v + 0x64, M.U32(p + 0xf * 4));
                V(0x1f, P(3)); V(0xf, P(3));
                float t = P(3) + P(5);
                V(0x17, t); V(7, t);
            }
            else
            {
                M.U32(v + 4, M.U32(p + 0xe * 4)); M.U32(v + 0x24, M.U32(p + 0x11 * 4)); M.U32(v + 0x44, M.U32(p + 0xf * 4)); M.U32(v + 0x64, M.U32(p + 0x10 * 4));
                V(0x17, P(3)); V(7, P(3));
                float t = P(3) + P(5);
                V(0x1f, t); V(0xf, t);
            }

            r.ProjectVertex2(M, viewMatrix, M.F32(A + 0x24), M.F32(A + 0x28), M.F32(A + 0x2c), out float cx, out float cy, out float rhw);
            float rad = (float)((double)P(0x15) / 2.8444444444444) / 180.0f * 3.141592f;
            float sin = (float)Math.Sin(rad);
            float cos = (float)Math.Cos(rad);
            float scale = r.ScreenScale(rhw);
            if (M.U8(A + 0x3fc4) == 1 && rt.ActorByAid_a64480(M.U32(A + 0x3fc8)) != 0)
                throw new NotSupportedException("owner-size scaled STR particles (FUN_00bd0b10) are not ported");
            if (M.U8(A + 0x3fb4) == 1 && M.I32(A + 0x3fb0) == 0x169) scale = 1.0f;

            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                float x = M.F32(vx), y = M.F32(vx + 4);
                float rx = x * cos - y * sin;
                float rot = y * cos + x * sin;
                M.F32(vx, ((P(0) - 320.0f) + rx) * scale + M.F32(A + 0x3f4c));
                if (!rt.FlipScreenY)
                    M.F32(vx + 4, ((P(1) - 240.0f) + rot) * scale + M.F32(A + 0x3f50));
                else
                {
                    float anchor = M.F32(A + 0x3f50);
                    float d = cy - anchor;
                    M.F32(vx + 4, (anchor - ((P(1) - 240.0f) + rot) * scale) + (d + d));
                }
            }

            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                float s = M.F32(A + 0x3fa8);
                M.F32(vx, (M.F32(vx) - cx) * s + cx);
                M.F32(vx + 4, (M.F32(vx + 4) - cy) * s + cy);
            }

            if (M.F32(A + 0x3fa4) > 0f)
            {
                Span<float> m = stackalloc float[12];
                for (int k = 0; k < 4; k++)
                {
                    uint vx = v + (uint)k * 0x20;
                    float ddx = M.F32(vx) - cx, ddy = M.F32(vx + 4) - cy;
                    Mat43.RotationZ(M.F32(A + 0x3fa4) / 180.0f * 3.141592f, m);
                    float nx = m[0] * ddx + m[3] * ddy;
                    nx += m[6] * 0f;
                    nx += m[9];
                    nx += cx;
                    float ny = m[1] * ddx + m[4] * ddy;
                    ny += m[7] * 0f;
                    M.F32(vx, nx);
                    ny += m[10];
                    ny += cy;
                    M.F32(vx + 4, ny);
                }
            }

            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                M.F32(vx, M.F32(vx) + M.F32(A + 0x3fcc));
                M.F32(vx + 4, M.F32(A + 0x3fd0) + M.F32(vx + 4));
            }
            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                long h = (long)((ulong)M.U32(A + 0x3fdc) << 32 | M.U32(A + 0x3fd8));
                float hf = (float)(double)h;
                M.F32(vx + 4, M.F32(vx + 4) - hf);
            }

            int frameNo = FxOps.Cvtt(P(0x12));
            if (frameNo < 0) frameNo = 0;
            uint tex;
            if (M.U32(A + 0x3f88) == 0) tex = rt.GetLayerTexture(layer, frameNo);
            else
            {
                // FUN_006c7a60 with a by-value copy (FUN_004ccbd0) of the directory +0x3f78, destroyed by the callee (package mapfx-emitters)
                uint dirSize = M.U32(A + 0x3f88);
                uint arg = rt.StrLocalAssign(rt.StrData(A + 0x3f78), dirSize);
                tex = rt.GetLayerTextureInDir_6c7a60(layer, frameNo, rt.StrRead(A + 0x3f78));
                rt.StrLocalDestroy(dirSize, arg);
            }

            float depthStep = (float)index * 1e-5f;
            float ru = (float)(double)M.U32(tex + 0x14) / (float)(double)M.U32(tex + 0xc);
            float rv = (float)(double)M.U32(tex + 0x18) / (float)(double)M.U32(tex + 0x10);
            for (int k = 0; k < 4; k++)
            {
                uint vx = v + (uint)k * 0x20;
                M.F32(vx + 0x18, ru * M.F32(vx + 0x18));
                M.F32(vx + 0x1c, rv * M.F32(vx + 0x1c));
                float a = M.F32(A + 0x3fac) * P(0x19);
                int argb = FxOps.Cvtt(a);
                argb = (argb << 8) + (FxOps.Cvtt(P(0x16)) & 0xff);
                argb = (argb << 8) + (FxOps.Cvtt(P(0x17)) & 0xff);
                argb = (argb << 8) + (FxOps.Cvtt(P(0x18)) & 0xff);
                M.U32(vx + 0x14, 0xffffffff);
                M.U32(vx + 8, 0x38021cc8);
                M.I32(vx + 0x10, argb);
                M.F32(vx + 0xc, rhw - depthStep);
            }
            if (M.U8(A + 0x3fe0) == 1)
            {
                SwapUv(v, 0, 2);
                SwapUv(v, 1, 3);
            }
            if (M.U8(A + 0x3fe1) == 1)
            {
                SwapUv(v, 0, 1);
                SwapUv(v, 2, 3);
            }
            M.U32(quad + 8, tex);
            M.U32(quad + 0x18, M.U32(p + 0x1a * 4));
            M.U32(quad + 0x1c, M.U32(p + 0x1b * 4));
            M.U32(quad + 0x20, M.U32(p + 0x1c * 4));
            M.I32(A + 0x1c, M.I32(A + 0x1c) | 0x20);
            if (M.U32(A + 0x2b8) != 0x00a68060) throw new NotSupportedException($"particle submit callback 0x{M.U32(A + 0x2b8):x8} is not ported");
            r.AddQuad(quad, M.I32(A + 0x1c));
        }

        void SwapUv(uint v, int a, int b)
        {
            uint va = v + (uint)a * 0x20 + 0x18, vb = v + (uint)b * 0x20 + 0x18;
            uint u = M.U32(va), w = M.U32(va + 4);
            M.U32(va, M.U32(vb));
            M.U32(va + 4, M.U32(vb + 4));
            M.U32(vb, u);
            M.U32(vb + 4, w);
        }
    }

    public static partial class Mat43
    {
        /// <summary>FUN_00533450 — rotation about Z.</summary>
        public static float[] RotationZ(float rad)
        {
            var r = new float[12];
            RotationZ(rad, r);
            return r;
        }

        public static void RotationZ(float rad, Span<float> r)
        {
            SetIdentity(r);
            float s = (float)Math.Sin(rad), c = (float)Math.Cos(rad);
            r[0] = c; r[4] = c; r[3] = -s; r[1] = s;
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>The directory part _splitpath (ucrtbase) extracts: everything up to the last '\' or '/' after an optional drive.</summary>
        public static string SplitPathDir(string path)
        {
            int start = path.Length >= 2 && path[1] == ':' ? 2 : 0;
            int last = -1;
            for (int i = start; i < path.Length; i++) if (path[i] == '\\' || path[i] == '/') last = i;
            return last < 0 ? "" : path.Substring(start, last + 1 - start);
        }
    }
}
