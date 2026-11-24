// Synced from reference/port/src/RoClassicFx/Shared/mapfx/CSpriteParticle.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// View of a new-system sprite particle (C++ class CSpriteParticle : CParticle, 0x2ec bytes, vtable 0xfe1d28), pool 1
    /// of CEffectMemoryMgr (VCSpriteParticle pool, vtable 0xff6544: vf01 0xaa9740 = pop + reset FUN_00a68ba0, or
    /// operator new(0x2ec) + ctor FUN_00a689b0(0, 0, 0, −1); vf02 0xaa9e40 = deque push_back, the same as pool 0's).
    /// A CParticle whose quad is drawn as the clips of an ACT frame (FUN_00a68d20) instead of one textured quad.
    /// Fields past the CParticle: +0x2bc/+0x2c0/+0x2c4 and +0x2c8 free for the effect classes (CSmokeEffect: local
    /// offset and spin step), +0x2cc frame delay in ticks (truncated to an int64; CSmokeEffect keeps its rise speed
    /// there), +0x2d4 CSprRes, +0x2d8 CActRes, +0x2dc action, +0x2e0 motion (frame), +0x2e4 end mode (0 loop, 1 die —
    /// life +0xcc = −1, 2 hold the last frame, 3 no animation), +0x2e8 AID of an actor whose pulse scale applies.
    /// +0x2d0 is never written (operator new memory).
    /// </summary>
    public readonly partial struct CSpriteParticle
    {
        public readonly FxMemory M;
        public readonly uint A;

        public CSpriteParticle(FxMemory m, uint a) { M = m; A = a; }

        public const uint VtCSpriteParticle = 0x00fe1d28;
        public const uint Size = 0x2ec;

        /// <summary>CSpriteParticle::CSpriteParticle (FUN_00a689b0; x, y, z, flags): CParticle ctor, vtable, +0x2bc..+0x2c4 = 0, CParticle reset FUN_00a677e0, then the sprite fields.</summary>
        public static void Construct_a689b0(FxMemory M, uint p, float x, float y, float z, int flags)
        {
            CParticle.Construct_a67480(M, p, x, y, z, flags);
            M.U32(p, VtCSpriteParticle);
            M.U32(p + 0x2bc, 0);
            M.U32(p + 0x2c0, 0);
            M.U32(p + 0x2c4, 0);
            new CParticle(M, p).Reset_a677e0();
            ClearSpriteFields(M, p);
        }

        /// <summary>FUN_00a68ba0 — the reset a pooled sprite particle gets: CParticle reset FUN_00a677e0, then the sprite fields (the resources are dropped without a Release).</summary>
        public void Reset_a68ba0()
        {
            new CParticle(M, A).Reset_a677e0();
            ClearSpriteFields(M, A);
        }

        static void ClearSpriteFields(FxMemory M, uint p)
        {
            M.U32(p + 0x2d4, 0);
            M.U32(p + 0x2d8, 0);
            M.U32(p + 0x2cc, 0);
            M.U32(p + 0x2c8, 0);
            M.U32(p + 0x2bc, 0);
            M.U32(p + 0x2c0, 0);
            M.U32(p + 0x2c4, 0);
            M.U32(p + 0x2dc, 0);
            M.U32(p + 0x2e0, 0);
            M.U32(p + 0x2e4, 0);
            M.U32(p + 0x2e8, 0);
        }

        /// <summary>FUN_00a69a10 — sprite resource +0x2d4: the old one released, CResMgr::Get(name), AddRef when found (as CRenderObject::SetSprName).</summary>
        public void SetSpr_a69a10(FxRuntime rt, string name)
        {
            uint old = M.U32(A + 0x2d4);
            if (old != 0) rt.ResRelease(old);
            rt.GetResMgr();
            uint res = rt.ResGet(name);
            M.U32(A + 0x2d4, res);
            if (res != 0) rt.ResAddRef(res);
        }

        /// <summary>FUN_00a699d0 — action resource +0x2d8: the old one released, CResMgr::Get(name), AddRef unconditionally (a missing .act faults).</summary>
        public void SetAct_a699d0(FxRuntime rt, string name)
        {
            uint old = M.U32(A + 0x2d8);
            if (old != 0) rt.ResRelease(old);
            rt.GetResMgr();
            uint res = rt.ResGet(name);
            M.U32(A + 0x2d8, res);
            rt.ResAddRef(res);
        }

        /// <summary>
        /// FUN_00a68d20 (thiscall, one byte argument, RET 4) — draws the particle as the clips of its ACT frame.
        /// <paramref name="animate"/> (the argument == 1) with an ACT and end mode ≠ 3 first advances the frame: delay
        /// d = int64(+0x2cc) (FUN_00d48e30); frame +1 when d == 0 or (age ≥ d and age % d == 0, signed 64-bit); then at
        /// frame ≥ the action's motion count: mode 0 → 0, mode 1 → life +0xcc = −1.0, mode 2 → count − 1.
        /// Drawing (SPR and ACT set): rhw of the position (ProjectVertex2 0x53b1a0), scale = ScreenScale(rhw)
        /// (× FUN_00bd0b10 of the +0x2e8 actor), motion (+0x2dc, +0x2e0); every clip with an image: its texture
        /// (SprImg +8, else the renderer's sprite-texture cache FUN_0054d9d0 / FUN_0054d230), the particle's unit quad
        /// with its colour / alpha and the UVs (bit 0 of the clip flags mirrors u) copied into a pooled RP quad; vertex
        /// colour from the clip's rgb and alpha·clipAlpha/255 when +0xec has 0x200; corners through Scale(kx·w/2,
        /// ky·h/2) × RotZ(clip angle) × Translation((x + w/2)·kx, (y + h/2)·ky, 0) — kx = scale·zoomX / (rhw·xFactor),
        /// ky likewise — then through Scale(+0x20) × RotZ(+0xc4) × RotX(camera latitude) × RotY(camera longitude) ×
        /// Translation(position) × matrix +0x1c8, projected (0x53b370); rhw bias clip index · 1e-5 + +0x2b4 (when not 0:
        /// rhw += bias, z = FUN_0053b030(rhw)); blend 5 / (flags == 5 ? 2 : 6); AddRP quad with the flags +0x1c.
        /// </summary>
        public void Render_a68d20(FxRuntime rt, bool animate)
        {
            uint actRes = M.U32(A + 0x2d8);
            if (animate && actRes != 0 && M.I32(A + 0x2e4) != 3)
            {
                long count = rt.ActMotionCount(actRes, M.U32(A + 0x2dc));
                long delay = Ftoll_d48e30(M.F32(A + 0x2cc));
                bool step;
                if (delay == 0) step = true;
                else
                {
                    long age = M.I32(A + 0xe8);
                    step = age >= delay && age % delay == 0;                  // _allrem (FUN_00d49bf0)
                }
                if (step) M.I32(A + 0x2e0, unchecked(M.I32(A + 0x2e0) + 1));
                int mode = M.I32(A + 0x2e4);
                bool atEnd = (long)M.I32(A + 0x2e0) >= count;                   // signed 64-bit compare
                if (mode == 0) { if (atEnd) M.I32(A + 0x2e0, 0); }
                else if (mode == 1) { if (atEnd) M.U32(A + 0xcc, 0xbf800000); }
                else if (mode == 2) { if (atEnd) M.I32(A + 0x2e0, unchecked((int)count - 1)); }
            }

            uint spr = M.U32(A + 0x2d4);
            if (spr == 0) return;
            uint act = M.U32(A + 0x2d8);
            if (act == 0) return;
            uint view = M.U32(rt.Mode + 0xd0) + 0x98;
            var r = rt.Renderer;
            r.ProjectVertex2(M, view, M.F32(A + 0x24), M.F32(A + 0x28), M.F32(A + 0x2c), out _, out _, out float rhw);
            float scale = r.ScreenScale(rhw);
            uint actor = rt.ActorByAid_a64480(M.U32(A + 0x2e8));
            if (actor != 0) rt.ActorScale_bd0b10(actor, ref scale);
            uint motion = rt.ActGetMotion(act, M.U32(A + 0x2dc), M.U32(A + 0x2e0));

            Span<float> s = stackalloc float[12];
            Span<float> t = stackalloc float[12];
            Span<float> a = stackalloc float[12];
            Span<float> b = stackalloc float[12];
            Span<float> w = stackalloc float[12];
            Span<float> q = stackalloc float[12];                               // the four corners after the clip matrix
            for (int i = 0; i < M.I32(motion + 0x2c); i++)
            {
                uint clip = rt.MotionGetClip(motion, (uint)i);
                int index = M.I32(clip + 8);
                if (index == -1) continue;
                uint img = M.U32(M.U32(spr + (M.U32(clip + 0x20) + 0x6c) * 0xc) + (uint)index * 4);

                float u0 = 0f, v0 = 0f, u1 = 0f, v1 = 0f;
                uint tex = M.U32(img + 8);
                if (tex == 0)
                {
                    tex = rt.SprTextureFind_54d9d0(img, spr + 0x110, out var info);
                    if (tex == 0) tex = rt.SprTextureCreate_54d230(img, spr + 0x110, out info);
                    u0 = info.U0; v0 = info.V0; u1 = info.U1; v1 = info.V1;
                }
                else
                {
                    u1 = (float)(double)M.U32(tex + 0x14) / (float)(double)M.U32(tex + 0xc);
                    v1 = (float)(double)M.U32(tex + 0x18) / (float)(double)M.U32(tex + 0x10);
                }
                if ((M.U8(clip + 0xc) & 1) != 0) SetUv_a67a60(A + 0x1f8, v1, v0, u1, u0);
                else SetUv_a67a60(A + 0x1f8, v1, v0, u0, u1);

                uint quad = r.AcquireQuad();
                uint verts = M.U32(quad);
                M.Copy(verts, A + 0x1f8, 0x80);
                M.U32(quad + 8, tex);
                if ((M.U32(A + 0xec) & 0x200) != 0)
                {
                    float k = 0.003921569f;                                          // [0xfe1d2c]
                    float f = (float)M.I32(A + 0x44) * k;
                    f *= (float)M.U8(clip + 0x13) * k;
                    f *= 255.0f;
                    uint ab = (uint)(FxOps.Cvtt(f) & 0xff) << 8;
                    uint color = ((((uint)M.U8(clip + 0x10) | ab) << 8 | M.U8(clip + 0x11)) << 8) | M.U8(clip + 0x12);
                    for (uint k4 = 0; k4 < 4; k4++) M.U32(verts + 0x10 + k4 * 0x20, color);
                }

                float zx = scale * M.F32(clip + 0x14);
                float zy = scale * M.F32(clip + 0x18);
                float hw = (float)M.I16(img) * 0.5f;
                float hh = (float)M.I16(img + 2) * 0.5f;
                float ky = zy / (rhw * r.YFactor);
                float kx = zx / (rhw * r.XFactor);
                Mat43.Scale(kx * hw, ky * hh, 1.0f, s);                          // FUN_00532b50
                Mat43.RotationZ(((float)M.I32(clip + 0x1c) / 180.0f) * 3.141592f, t);   // FUN_00533450
                Mat43.Mul(s, t, a);
                Mat43.Translation(((float)M.I32(clip) + hw) * kx, ((float)M.I32(clip + 4) + hh) * ky, 0f, s);   // FUN_005330d0
                Mat43.Mul(a, s, b);
                for (int k = 0; k < 4; k++)
                {
                    uint v = verts + (uint)k * 0x20;
                    float x = M.F32(v), y = M.F32(v + 4), z = M.F32(v + 8);
                    float px = y * b[3] + x * b[0]; px += z * b[6]; px += b[9];
                    float py = y * b[4] + x * b[1]; py += z * b[7]; py += b[10];
                    float pz = y * b[5] + x * b[2]; pz += z * b[8]; pz += b[11];
                    q[k * 3] = px; q[k * 3 + 1] = py; q[k * 3 + 2] = pz;
                }

                float size = M.F32(A + 0x20);
                Mat43.Scale(size, size, 1.0f, s);
                Mat43.RotationZ((M.F32(A + 0xc4) / 180.0f) * 3.141592f, t);
                uint camera = M.U32(rt.Mode + 0xd0);
                Mat43.RotateX(t, (M.F32(camera + 0x2c) / 180.0f) * 3.141592f);   // FUN_00530f80
                Mat43.RotateY(t, (M.F32(camera + 0x30) / 180.0f) * 3.141592f);   // FUN_00531280
                Mat43.Mul(s, t, a);
                Mat43.Translation(M.F32(A + 0x24), M.F32(A + 0x28), M.F32(A + 0x2c), s);
                Mat43.Mul(a, s, b);
                Mat43.Load(M, A + 0x1c8, t);
                Mat43.Mul(b, t, w);
                for (int k = 0; k < 4; k++)
                {
                    float qx = q[k * 3], qy = q[k * 3 + 1], qz = q[k * 3 + 2];
                    float px = w[3] * qy + w[0] * qx; px += w[6] * qz; px += w[9];
                    float py = w[4] * qy + w[1] * qx; py += w[7] * qz; py += w[10];
                    float pz = w[5] * qy + w[2] * qx; pz += w[8] * qz; pz += w[11];
                    r.ProjectVertex(M, view, px, py, pz, out float sx, out float sy, out float sz, out float vr);
                    uint v = verts + (uint)k * 0x20;
                    M.F32(v, sx);
                    M.F32(v + 4, sy);
                    M.F32(v + 8, sz);
                    M.F32(v + 0xc, vr);
                }
                float bias = (float)i * 1e-05f + M.F32(A + 0x2b4);
                if (!(bias == 0f))                                                   // UCOMISS 0, bias; equal (ordered) skips
                {
                    for (uint k = 0; k < 4; k++)
                    {
                        uint v = verts + k * 0x20;
                        float vr = bias + M.F32(v + 0xc);
                        M.F32(v + 0xc, vr);
                        M.F32(v + 8, FxRenderer.DepthFromRhw_53b030(vr));
                    }
                }
                M.I32(quad + 0x18, 5);
                M.I32(quad + 0x1c, M.I32(A + 0x1c) != 5 ? 6 : 2);
                r.AddQuad(quad, M.I32(A + 0x1c));
            }
        }

        /// <summary>
        /// FUN_00a67a60 (thiscall on the particle, RET 0x14) — the four corners at <paramref name="verts"/>: colour
        /// ((alpha &lt;&lt; 8 | r) &lt;&lt; 8 | g) &lt;&lt; 8 | b (int16 channels, sign-extended), v = <paramref name="vTop"/> on corners 0 / 1
        /// and <paramref name="vBottom"/> on 2 / 3, u = <paramref name="uLeft"/> on 0 / 2 and <paramref name="uRight"/> on 1 / 3.
        /// </summary>
        void SetUv_a67a60(uint verts, float vTop, float vBottom, float uLeft, float uRight)
        {
            int cr = M.I16(A + 0x3c), cg = M.I16(A + 0x3e), cb = M.I16(A + 0x40);
            uint color = (uint)((((M.I32(A + 0x44) << 8) | cr) << 8 | cg) << 8 | cb);
            M.U32(verts + 0x10, color);
            M.U32(verts + 0x30, color);
            M.U32(verts + 0x50, color);
            M.U32(verts + 0x70, color);
            M.F32(verts + 0x1c, vTop);
            M.F32(verts + 0x3c, vTop);
            M.F32(verts + 0x18, uLeft);
            M.F32(verts + 0x38, uRight);
            M.F32(verts + 0x58, uLeft);
            M.F32(verts + 0x5c, vBottom);
            M.F32(verts + 0x78, uRight);
            M.F32(verts + 0x7c, vBottom);
        }

        /// <summary>
        /// FUN_00d48e30 (float in XMM0 → int64 in EDX:EAX, truncation): CVTTSS2SI below 2^31 in magnitude, an exact
        /// shift above. NaN and |x| ≥ 2^63 → 0x8000000000000000 *(inferred: the integer-indefinite value of the AVX-512
        /// path; the SSE tail could not be disassembled)*.
        /// </summary>
        public static long Ftoll_d48e30(float v)
        {
            if (float.IsNaN(v) || v >= 9.223372036854775807e18f || v < -9.223372036854775807e18f) return long.MinValue;
            return (long)v;
        }
    }

    /// <summary>The 0x28-byte texture-cache entry FUN_0054d9d0 / FUN_0054d230 copy to the caller (only the UVs are read).</summary>
    public struct FxSprTextureInfo
    {
        /// <summary>+0 SprImg*, +4 palette, +8 timestamp (0 here), +0xc/+0x10 u0/v0, +0x14/+0x18 u1/v1, +0x1c/+0x20 atlas x/y (0 here), +0x24 surface (the texture object here).</summary>
        public uint Image, Palette, Stamp;
        public float U0, V0, U1, V1;
        public uint X, Y, Surface;

        public byte[] ToBytes()
        {
            var b = new byte[0x28];
            void Put(int o, uint v) => BitConverter.GetBytes(v).CopyTo(b, o);
            void PutF(int o, float v) => BitConverter.GetBytes(v).CopyTo(b, o);
            Put(0, Image); Put(4, Palette); Put(8, Stamp);
            PutF(0xc, U0); PutF(0x10, V0); PutF(0x14, U1); PutF(0x18, V1);
            Put(0x1c, X); Put(0x20, Y); Put(0x24, Surface);
            return b;
        }
    }

    public sealed partial class FxRuntime
    {
        /// <summary>
        /// FUN_00aaa700 — a CSpriteParticle from pool 1 (FUN_00aa9830(1) → VCSpriteParticle pool vf01 0xaa9740: pop the
        /// front of the free deque and reset it (FUN_00a68ba0), or operator new(0x2ec) + ctor FUN_00a689b0(0, 0, 0, −1)),
        /// then pool index 1 at +0x2b0, the position (float[3] at <paramref name="pos"/>) and the RP flags.
        /// </summary>
        public uint AllocSpriteParticle_aaa700(uint pos, int flags)
        {
            float x = M.F32(pos), y = M.F32(pos + 4), z = M.F32(pos + 8);
            uint p = PoolAlloc(1);
            M.U32(p + 0x2b0, 1);
            M.F32(p + 0x24, x);
            M.F32(p + 0x28, y);
            M.I32(p + 0x1c, flags);
            M.F32(p + 0x2c, z);
            return p;
        }

        // ---- the renderer's sprite-image texture cache (renderer +0xc0) ------------------------------

        /// <summary>
        /// Host texture handle for a sprite image (SprImg* in FxMemory, ARGB1555 palette address = CSprRes + 0x110); the
        /// image's pixels are <see cref="GetSpriteModel"/>(palette − 0x110) → the image whose header address is
        /// <c>img</c>. Default: none (handle 0).
        /// </summary>
        public Func<uint, uint, uint> SprTextureHandleImpl;

        /// <summary>Sprite-image texture object: +0 host handle, +4 SprImg*, +8 palette, +0xc/+0x10 surface w/h, +0x14/+0x18 image w/h (= surface), +0x1c magic.</summary>
        public const uint SprTextureSize = 0x20;
        const uint SprTextureMagic = 0x54525053; // "SPRT"

        readonly Dictionary<ulong, uint> sprTextures = new Dictionary<ulong, uint>();

        /// <summary>
        /// FUN_0054d9d0 (thiscall on the renderer's sprite-texture cache at renderer +0xc0, RET 0xc) shim — the texture
        /// already made for (<paramref name="img"/>, <paramref name="palette"/>), with its entry copied to
        /// <paramref name="info"/>; 0 (nothing written) when there is none. The client packs SPR images into shared
        /// 256×256 surfaces (atlas UVs, LRU by timeGetTime); the port gives each image its own texture object in
        /// FxMemory with UVs 0..1 (the host draws the image itself). Shared verbatim with the oracle.
        /// </summary>
        public uint SprTextureFind_54d9d0(uint img, uint palette, out FxSprTextureInfo info)
        {
            info = default;
            ulong key = (ulong)img << 32 | palette;
            if (!sprTextures.TryGetValue(key, out uint t)) return 0;
            if (t + SprTextureSize > M.BumpPointer || !M.Contains(t, (int)SprTextureSize) || M.U32(t + 0x1c) != SprTextureMagic
                || M.U32(t + 4) != img || M.U32(t + 8) != palette)
            {
                sprTextures.Remove(key);                                       // the heap was rewound past it
                return 0;
            }
            info = SprTextureInfo(t);
            return t;
        }

        /// <summary>FUN_0054d230 (thiscall on renderer +0xc0, RET 0xc) shim — makes the texture object for the image (see <see cref="SprTextureFind_54d9d0"/>).</summary>
        public uint SprTextureCreate_54d230(uint img, uint palette, out FxSprTextureInfo info)
        {
            uint t = M.Alloc(SprTextureSize);
            int w = M.I16(img), h = M.I16(img + 2);
            M.U32(t, SprTextureHandleImpl?.Invoke(img, palette) ?? 0u);
            M.U32(t + 4, img);
            M.U32(t + 8, palette);
            M.I32(t + 0xc, w); M.I32(t + 0x10, h);
            M.I32(t + 0x14, w); M.I32(t + 0x18, h);
            M.U32(t + 0x1c, SprTextureMagic);
            sprTextures[(ulong)img << 32 | palette] = t;
            info = SprTextureInfo(t);
            return t;
        }

        FxSprTextureInfo SprTextureInfo(uint t) => new FxSprTextureInfo
        {
            Image = M.U32(t + 4), Palette = M.U32(t + 8), U0 = 0f, V0 = 0f, U1 = 1.0f, V1 = 1.0f, Surface = t,
        };
    }
}
