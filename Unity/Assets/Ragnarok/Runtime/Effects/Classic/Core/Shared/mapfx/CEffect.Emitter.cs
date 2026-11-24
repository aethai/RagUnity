// Synced from reference/port/src/RoClassicFx/Shared/mapfx/CEffect.Emitter.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CEmitterEffect (EF 974, vtable 0xfe2708, 0x18c bytes; allocator VCEmitterEffect 0xfe1738 → ctor FUN_00a77740) —
    /// the map particle emitter of the new effect system (package mapfx; 19051 RSW records on 236 maps). The world
    /// builder launches one per RSW record (ownerless, at the record's position); the map's EffectTool script then
    /// configures the k-th one through <c>SetEmitterInfo</c> (FUN_00aa7640, <see cref="FxRuntime.SetEmitterInfo_aa7640"/>).
    ///
    /// Fields: +0xcc/+0xd0/+0xd4 spawn direction 1 (−2, 2, −2), +0xd8/+0xdc/+0xe0 spawn direction 2 (2, −1, 2) — the
    /// start velocity of each axis is random between the two; +0xe4/+0xe8/+0xec gravity (0.7, −2, 1); +0xf0/+0xf4/+0xf8
    /// emit radius (−1, 2, −1; the start offset is random in [−r, r]); +0xfc spawn accumulator; +0x100 time step 1/60;
    /// +0x104/+0x108 size range (3, 8); +0x10c/+0x110 life range in seconds (5, 10); +0x114/+0x118 emit rate range in
    /// particles per second (20, 100); +0x11c/+0x120 scale x / y (1, 1); +0x124/+0x128/+0x12c/+0x130 colour r, g, b, alpha
    /// (int, 255); +0x134 std::string texture ("smoke2.bmp"); +0x14c/+0x150 src / dest blend (5, 2; −1 = keep the
    /// particle's); +0x154 particle count (40); +0x158 speed factor (1.0, copied into every particle's +0xc0); byte +0x15c
    /// z-enable (1), byte +0x15d billboard off (0); +0x160/+0x164/+0x168 rotation angles in degrees (float, used when the
    /// billboard is off); byte +0x16c eternity (0); +0x170 std::vector&lt;CParticle*&gt; (all particles, made once by vf01);
    /// +0x17c std::map&lt;CParticle*, bool&gt; "particle in use" (node 0x18: key +0x10, value byte +0x14) with +0x180 its
    /// size and +0x184 the number in use; +0x188 lifetime in ticks (−1 = for ever). Byte +0xc1 (CEffect padding) = the
    /// local player is out of range (vf02).
    /// </summary>
    public readonly partial struct CEffect
    {
        public const uint VtCEmitterEffect = 0x00fe2708;
        public const uint VtAllocEmitter = 0x00fe1738;
        public const uint EmitterSize = 0x18c;
        static readonly int[] EmitterIds = { 974 };

        // ---- construction / destruction -----------------------------------------------------------

        /// <summary>CEmitterEffect::CEmitterEffect (FUN_00a77740): CEffect(0x3ce), the defaults above, the particle set (FUN_00a68140).</summary>
        static uint ConstructEmitter_a77740(FxRuntime rt, uint a)
        {
            var M = rt.M;
            ConstructBase_a75740(M, a, 0x3ce);
            M.U32(a, VtCEmitterEffect);
            M.U32(a + 0xcc, 0xc0000000); M.U32(a + 0xd0, 0x40000000); M.U32(a + 0xd4, 0xc0000000);
            M.U32(a + 0xd8, 0x40000000); M.U32(a + 0xdc, 0xbf800000); M.U32(a + 0xe0, 0x40000000);
            M.U32(a + 0xe4, 0x3f333333); M.U32(a + 0xe8, 0xc0000000); M.U32(a + 0xec, 0x3f800000);
            M.U32(a + 0xf0, 0xbf800000); M.U32(a + 0xf4, 0x40000000); M.U32(a + 0xf8, 0xbf800000);
            M.U32(a + 0xfc, 0);
            M.U32(a + 0x100, 0x3c888889);                                    // 1/60
            M.U32(a + 0x104, 0x40400000); M.U32(a + 0x108, 0x41000000);      // size 3 .. 8
            M.U32(a + 0x10c, 0x40a00000); M.U32(a + 0x110, 0x41200000);      // life 5 .. 10 s
            M.U32(a + 0x114, 0x41a00000); M.U32(a + 0x118, 0x42c80000);      // rate 20 .. 100 / s
            M.U32(a + 0x11c, 0x3f800000); M.U32(a + 0x120, 0x3f800000);
            M.I32(a + 0x124, 0xff); M.I32(a + 0x128, 0xff); M.I32(a + 0x12c, 0xff); M.I32(a + 0x130, 0xff);
            // std::string +0x134 = "smoke2.bmp": constructed empty (capacity 15), then assigned in place (memmove 10 bytes)
            M.U32(a + 0x134, 0);
            M.U32(a + 0x144, 0);
            M.U32(a + 0x148, 0xf);
            M.U8(a + 0x134, 0);
            const string Smoke = "smoke2.bmp";
            M.U32(a + 0x144, (uint)Smoke.Length);
            for (int i = 0; i < Smoke.Length; i++) M.U8(a + 0x134 + (uint)i, (byte)Smoke[i]);
            M.U8(a + 0x134 + (uint)Smoke.Length, 0);
            M.I32(a + 0x14c, 5);
            M.I32(a + 0x150, 2);
            M.I32(a + 0x154, 0x28);
            M.U32(a + 0x158, 0x3f800000);
            M.U16(a + 0x15c, 1);                                             // z-enable 1, billboard off 0
            M.U32(a + 0x160, 0); M.U32(a + 0x164, 0); M.U32(a + 0x168, 0);
            M.U8(a + 0x16c, 0);
            M.U32(a + 0x170, 0); M.U32(a + 0x174, 0); M.U32(a + 0x178, 0);
            // FUN_00a68140: std::map<CParticle*, bool> (head node 0x18) + in-use count
            M.U32(a + 0x17c, 0);
            M.U32(a + 0x180, 0);
            M.U32(a + 0x17c, rt.TreeNewHead(0x18));
            M.U32(a + 0x184, 0);
            M.I32(a + 0x188, -1);
            return a;
        }

        /// <summary>CEmitterEffect::vf25_a77a60 body: particle set (FUN_00a683b0), particle vector, texture string, CEffect.</summary>
        void EmitterDestruct_a77a60(FxRuntime rt)
        {
            M.U32(A, VtCEmitterEffect);
            EmitterSetDestroy_a683b0(rt);
            DestroyPtrVector_678360(rt, A + 0x170);
            rt.StrDestroy(A + 0x134);
            DestructBase_a75970(rt);
        }

        // ---- virtual methods ----------------------------------------------------------------------

        /// <summary>
        /// CEmitterEffect::vf01_a77ca0 — +0x154 particles (FUN_00aaa6c0 at the effect's position, RP flags 5): speed
        /// factor +0xc0 = +0x158, vf29 (particle init), added to the set as not in use, appended to the vector.
        /// </summary>
        void EmitterInit_a77ca0(FxRuntime rt)
        {
            for (int i = 0; i < M.I32(A + 0x154); i++)
            {
                uint p = rt.AllocParticle_aaa6c0(A + 8, 5);
                if (p == 0) continue;
                M.U32(p + 0xc0, M.U32(A + 0x158));
                EmitterVf29InitParticle(rt, p);
                EmitterSetAdd_a68970(rt, p);
                rt.PtrVectorPushBack(A + 0x170, p);
            }
        }

        /// <summary>
        /// CEmitterEffect::vf02_a788c0 — in range (FUN_00a76280: range +0x38 &gt; distance to the local player): +0x34 = 0,
        /// +0xc1 = 0. Out of range: +0xc1 = 1 (no new particles), and only when no particle is in use (+0x184 == 0)
        /// +0x34 = 1 (no process, no tick, no draw) — particles already flying finish their life out of range.
        /// </summary>
        void EmitterRangeCheck_a788c0(FxRuntime rt)
        {
            rt.PlayerPosition_a644c0(out float px, out float py, out float pz);
            float dy = py - M.F32(A + 0xc);
            float dx = px - M.F32(A + 8);
            float dz = pz - M.F32(A + 0x10);
            float sum = dy * dy + dx * dx;
            sum += dz * dz;
            float dist = FxRuntime.SqrtF(sum);
            if ((float)M.I32(A + 0x38) > dist)                              // COMISS range, dist; SETA
            {
                M.U8(A + 0x34, 0);
                M.U8(A + 0xc1, 0);
                return;
            }
            M.U8(A + 0xc1, 1);
            if (M.U32(A + 0x184) == 0) M.U8(A + 0x34, 1);
        }

        /// <summary>
        /// CEmitterEffect::vf03_a77f80 — in range: n = (int)rand(rate) · 1/60 particles this tick (the fraction carried
        /// in +0xfc, a whole one taken once it exceeds 1), each the first free particle of the set (address order,
        /// FUN_00a68590) re-initialised by vf29 (eternity: alpha = +0x130). Then vf30 on every particle.
        /// </summary>
        void EmitterProcess_a77f80(FxRuntime rt)
        {
            if (M.U8(A + 0xc1) == 0)
            {
                float rate = RandRange_a64570(rt, M.F32(A + 0x114), M.F32(A + 0x118));
                float f = (float)FxOps.Cvtt(rate) * M.F32(A + 0x100);
                int count = FxOps.Cvtt(f);
                float acc = (f - (float)count) + M.F32(A + 0xfc);
                M.F32(A + 0xfc, acc);
                if (acc > 1.0f)                                              // COMISS acc, 1.0; JBE
                {
                    int whole = FxOps.Cvtt(acc);
                    count = unchecked(count + whole);
                    acc -= (float)whole;
                    M.F32(A + 0xfc, acc);
                }
                if (count > 0)
                {
                    do
                    {
                        uint p = EmitterSetTakeFree_a68590();
                        if (p != 0)
                        {
                            EmitterVf29InitParticle(rt, p);
                            if (M.U8(A + 0x16c) == 1) M.I32(p + 0x44, M.I32(A + 0x130));
                        }
                    } while (--count != 0);
                }
            }
            for (uint pp = M.U32(A + 0x170); pp != M.U32(A + 0x174); pp += 4) EmitterVf30UpdateParticle(rt, M.U32(pp));
        }

        /// <summary>
        /// CEmitterEffect::vf04_a783c0 — every particle of the vector (in use or not) through FUN_00a67b30 with
        /// {billboard off +0x15d, v 0..1, u 0..1, 0, z-enable +0x15c}.
        /// </summary>
        void EmitterRender_a783c0(FxRuntime rt)
        {
            for (uint pp = M.U32(A + 0x170); pp != M.U32(A + 0x174); pp += 4)
            {
                uint p = M.U32(pp);
                if (p == 0) continue;
                new CParticle(M, p).RenderStrip_a67b30(rt, M.U8(A + 0x15d) != 0, 0f, 1.0f, 0f, 1.0f, M.U8(A + 0x15c) != 0);
            }
        }

        /// <summary>CEmitterEffect::vf05_a77c60 — every particle back to its pool, vector emptied, the set cleared (FUN_00a685f0).</summary>
        void EmitterRelease_a77c60(FxRuntime rt)
        {
            uint pp = M.U32(A + 0x170);
            for (; pp != M.U32(A + 0x174); pp += 4) rt.FreeParticle_aaa740(M.U32(pp));
            M.U32(A + 0x174, M.U32(A + 0x170));
            EmitterSetClear_a685f0(rt);
        }

        /// <summary>
        /// CEmitterEffect::vf06_a77f60 — dead once the lifetime +0x188 (when positive) is exceeded, otherwise
        /// FUN_00a762f0 (alive while the owner AID is 0 or still resolves). Map emitters (ownerless, −1) never end.
        /// </summary>
        bool EmitterIsAlive_a77f60(FxRuntime rt)
        {
            int life = M.I32(A + 0x188);
            if (life > 0 && M.I32(A + 0x28) > life) return false;
            return OwnerAlive_a762f0(rt);
        }

        /// <summary>
        /// CEmitterEffect::vf07_a77f00 — re-init (SetEmitterInfo MSG_SET_INIT): with eternity on and a zero rate range every
        /// particle is put in use (FUN_00a68730); then vf29 on every particle.
        /// </summary>
        public void EmitterReinit_a77f00(FxRuntime rt)
        {
            if (M.U8(A + 0x16c) == 1 && 0f == M.F32(A + 0x114) && 0f == M.F32(A + 0x118)) EmitterSetTakeAll_a68730();
            for (uint pp = M.U32(A + 0x170); pp != M.U32(A + 0x174); pp += 4) EmitterVf29InitParticle(rt, M.U32(pp));
        }

        /// <summary>vf29 (slot +0x74) of the emitter classes.</summary>
        void EmitterVf29InitParticle(FxRuntime rt, uint p)
        {
            if (Vtable == VtCAnimatedEmitterEffect) { AnimatedEmitterInitParticle_6777b0(rt, p); return; }   // package mapfx-emitters
            if (Vtable != VtCEmitterEffect) throw Unported(29);
            EmitterInitParticle_a77d30(rt, p);
        }

        /// <summary>vf30 (slot +0x78) of the emitter classes.</summary>
        void EmitterVf30UpdateParticle(FxRuntime rt, uint p)
        {
            if (Vtable == VtCAnimatedEmitterEffect) { AnimatedEmitterUpdateParticle_6777f0(rt, p); return; }   // package mapfx-emitters
            if (Vtable != VtCEmitterEffect) throw Unported(30);
            EmitterUpdateParticle_a78490(rt, p);
        }

        /// <summary>
        /// CEmitterEffect::vf29_a77d30 — (re)starts a particle at the effect's position: texture GetTexture(+0x134), life
        /// +0xcc = rand(+0x10c, +0x110) s, size +0x20 = rand(+0x104, +0x108), velocity +0x30..+0x38 = rand(dir1, dir2) per
        /// axis, offset +0xd0..+0xd8 = (rand(±r.x), rand(±r.y) − 10, rand(±r.z)) (x87, 0 + …), alpha 0, age 0, blend from
        /// +0x14c / +0x150 unless −1. rand() order: life, size, vx, vy, vz, ox, oy, oz (none when a range is a single value).
        /// </summary>
        void EmitterInitParticle_a77d30(FxRuntime rt, uint p)
        {
            M.U32(p + 0x24, M.U32(A + 8));
            M.U32(p + 0x28, M.U32(A + 0xc));
            M.U32(p + 0x2c, M.U32(A + 0x10));
            new CParticle(M, p).SetTexture_a67fb0(rt, rt.ReadCString(rt.StrData(A + 0x134)));
            M.F32(p + 0xcc, RandRange_a64570(rt, M.F32(A + 0x10c), M.F32(A + 0x110)));
            M.F32(p + 0x20, RandRange_a64570(rt, M.F32(A + 0x104), M.F32(A + 0x108)));
            M.F32(p + 0x30, RandRange_a64570(rt, M.F32(A + 0xcc), M.F32(A + 0xd8)));
            M.F32(p + 0x34, RandRange_a64570(rt, M.F32(A + 0xd0), M.F32(A + 0xdc)));
            M.F32(p + 0x38, RandRange_a64570(rt, M.F32(A + 0xd4), M.F32(A + 0xe0)));
            float rx = M.F32(A + 0xf0);
            float ox = 0f + RandRange_a64570(rt, -rx, rx);                       // FLDZ; FADDP (−0 → +0)
            float ry = M.F32(A + 0xf4);
            float oy = (float)(0.0 + ((double)RandRange_a64570(rt, -ry, ry) - 10.0));   // FSUB [10.0f] in x87 (53-bit), FLDZ, FADDP
            float rz = M.F32(A + 0xf8);
            float oz = 0f + RandRange_a64570(rt, -rz, rz);
            M.F32(p + 0xd0, ox);
            M.F32(p + 0xd4, oy);
            M.I32(p + 0x44, 0);
            M.F32(p + 0xd8, oz);
            M.I32(p + 0xe8, 0);
            int src = M.I32(A + 0x14c);
            if (src != -1) M.I32(p + 0x2a8, src);
            int dst = M.I32(A + 0x150);
            if (dst != -1) M.I32(p + 0x2ac, dst);
        }

        /// <summary>
        /// CEmitterEffect::vf30_a78490 — one tick of a particle. Position = effect position. A particle in use stays while
        /// its life is 0 (for ever), life · 60 &gt; age, or (eternity and in range); otherwise it is returned to the set
        /// (FUN_00a68640). A staying one: colour = (+0x124, +0x128, +0x12c); without eternity its alpha fades by
        /// 255 / 60 a tick — up (to 255) until the last 60 ticks of its life, then down (to 0); offset += velocity ·
        /// speed factor / 60, position = effect + offset; velocity += gravity / 60; matrix = Scale(size · scale) ×
        /// RotX(camera latitude) (camera-facing, turned by the renderer) or, billboard off, Scale × RotX × RotY × RotZ
        /// (+0x160..+0x168, whole degrees) × Translation(position). Age +1 for every particle, in use or not.
        /// </summary>
        void EmitterUpdateParticle_a78490(FxRuntime rt, uint p)
        {
            M.U32(p + 0x24, M.U32(A + 8));
            M.U32(p + 0x28, M.U32(A + 0xc));
            M.U32(p + 0x2c, M.U32(A + 0x10));
            if (EmitterSetInUse_a686e0(p) == 1)
            {
                float life = M.F32(p + 0xcc);
                int age = M.I32(p + 0xe8);
                bool stays;
                if (0f == life) stays = true;                                    // UCOMISS 0, life: equal (ordered)
                else if (life * 60.0f > (float)age) stays = true;                // COMISS; SETA
                else stays = M.U8(A + 0x16c) != 0 && M.U8(A + 0xc1) == 0;
                if (!stays) EmitterSetRelease_a68640(p);
                else
                {
                    new CParticle(M, p).SetColor_a67f70(M.U16(A + 0x124), M.U16(A + 0x128), M.U16(A + 0x12c));
                    if (M.U8(A + 0x16c) == 0)
                    {
                        float alpha = (float)M.I32(p + 0x44);
                        float step = M.F32(A + 0x100) * 255.0f;
                        float rem = M.F32(p + 0xcc) * 60.0f - (float)age;
                        int a;
                        if (60.0f > rem)                                         // COMISS 60, rem; JBE → fade in
                        {
                            int v = FxOps.Cvtt(alpha - step);
                            a = v < 0 ? 0 : v;
                        }
                        else
                        {
                            int v = FxOps.Cvtt(step + alpha);
                            a = v > 0xff ? 0xff : v;
                        }
                        M.I32(p + 0x44, a);
                    }
                    float s = M.F32(A + 0x100);
                    float k = M.F32(p + 0xc0);
                    float oy = M.F32(p + 0x34) * k * s + M.F32(p + 0xd4);
                    float oz = M.F32(p + 0x38) * k * s + M.F32(p + 0xd8);
                    float ox = M.F32(p + 0x30) * k * s + M.F32(p + 0xd0);
                    M.F32(p + 0xd4, oy);
                    M.F32(p + 0xd8, oz);
                    M.F32(p + 0xd0, ox);
                    M.F32(p + 0x28, oy + M.F32(p + 0x28));
                    M.F32(p + 0x2c, oz + M.F32(p + 0x2c));
                    M.F32(p + 0x24, ox + M.F32(p + 0x24));
                    s = M.F32(A + 0x100);
                    float gy = M.F32(A + 0xe8), gz = M.F32(A + 0xec);
                    M.F32(p + 0x30, s * M.F32(A + 0xe4) + M.F32(p + 0x30));
                    M.F32(p + 0x34, s * gy + M.F32(p + 0x34));
                    M.F32(p + 0x38, s * gz + M.F32(p + 0x38));
                    float size = M.F32(p + 0x20);
                    if (M.U8(A + 0x15d) == 1)
                    {
                        float sy = size * M.F32(A + 0x120);
                        float sx = size * M.F32(A + 0x11c);
                        uint m = p + 0x1c8;                                       // the client builds it on the stack and copies it here
                        FxMatrix.Identity(M, m);                                  // FUN_00532b50
                        M.F32(m, sx); M.F32(m + 0x10, sy); M.F32(m + 0x20, 1.0f);
                        FxMatrix.RotateX(M, m, FxMath.DegToRad(FxOps.Cvtt(M.F32(A + 0x160))));
                        FxMatrix.RotateY(M, m, FxMath.DegToRad(FxOps.Cvtt(M.F32(A + 0x164))));
                        FxMatrix.RotateZ(M, m, FxMath.DegToRad(FxOps.Cvtt(M.F32(A + 0x168))));
                        Translate_530cb0(M, m, M.F32(p + 0x24), M.F32(p + 0x28), M.F32(p + 0x2c));
                    }
                    else
                    {
                        Span<float> rot = stackalloc float[12];
                        Span<float> sc = stackalloc float[12];
                        Span<float> res = stackalloc float[12];
                        uint view = M.U32(rt.Mode + 0xd0);
                        Mat43.RotationX((M.F32(view + 0x2c) / 180.0f) * 3.141592f, rot);
                        Mat43.Scale(size * M.F32(A + 0x11c), size * M.F32(A + 0x120), 1.0f, sc);
                        Mat43.Mul(sc, rot, res);
                        Mat43.Store(M, p + 0x1c8, res);
                    }
                }
            }
            M.I32(p + 0xe8, M.I32(p + 0xe8) + 1);
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>
        /// FUN_00a64570 (cdecl, two floats, result in ST0) — a random value from lo to hi in steps of 0.1:
        /// lo == hi (ordered) → lo, no rand(); otherwise (rand() % (|−(int)(lo·10) − (int)(hi·−10)| + 1) + (int)(lo·10)) · 0.1.
        /// With lo &gt; hi the range runs from lo upwards by lo − hi (client quirk, kept).
        /// </summary>
        public static float RandRange_a64570(FxRuntime rt, float lo, float hi)
        {
            if (lo == hi) return lo;
            unchecked
            {
                int l = FxOps.Cvtt(lo * 10.0f);
                int r = rt.Rand.Next();
                int d = -l - FxOps.Cvtt(hi * -10.0f);
                int sign = d >> 31;
                int span = ((d ^ sign) - sign) + 1;
                return (float)(r % span + l) * 0.1f;
            }
        }

        /// <summary>
        /// FUN_00530cb0 (thiscall on a 4x3 matrix) — M = M × Translation(x, y, z), computed with the identity constant
        /// (0x1137438) as the rotation part: each element is (m[r,0]·I[c] + m[r,1]·I[3+c]) + m[r,2]·I[6+c], plus the
        /// position on row 3 (the ×0 / ×1 terms kept: signed zeros come out as the client's).
        /// </summary>
        static void Translate_530cb0(FxMemory M, uint m, float x, float y, float z)
        {
            Span<float> v = stackalloc float[12];
            for (int i = 0; i < 12; i++) v[i] = M.F32(m + (uint)i * 4);
            for (int r = 0; r < 4; r++)
            {
                float a0 = v[r * 3], a1 = v[r * 3 + 1], a2 = v[r * 3 + 2];
                for (int c = 0; c < 3; c++)
                {
                    float e = a0 * IdentityAt(c) + a1 * IdentityAt(3 + c);
                    e += a2 * IdentityAt(6 + c);
                    if (r == 3) e += c == 0 ? x : c == 1 ? y : z;
                    M.F32(m + (uint)(r * 3 + c) * 4, e);
                }
            }
        }

        static float IdentityAt(int i) => i == 0 || i == 4 || i == 8 ? 1.0f : 0.0f;

        // ---- the particle set (std::map<CParticle*, bool> at +0x17c, size +0x180, in use +0x184) -------------

        /// <summary>_Find_lower_bound (FUN_00a680f0) on the particle set: unsigned keys (particle addresses).</summary>
        void EmitterSetLowerBound(uint key, out uint parent, out bool left, out uint bound)
        {
            uint head = M.U32(A + 0x17c);
            uint node = M.U32(head + 4);
            parent = node;
            left = false;
            bound = head;
            while (M.U8(node + 0xd) == 0)
            {
                parent = node;
                if (M.U32(node + 0x10) >= key)
                {
                    left = true;
                    bound = node;
                    node = M.U32(node);
                }
                else
                {
                    left = false;
                    node = M.U32(node + 8);
                }
            }
        }

        /// <summary>The set's node for <paramref name="p"/> or 0.</summary>
        uint EmitterSetFind(uint p)
        {
            EmitterSetLowerBound(p, out _, out _, out uint bound);
            if (M.U8(bound + 0xd) != 0 || p < M.U32(bound + 0x10) || bound == M.U32(A + 0x17c)) return 0;
            return bound;
        }

        /// <summary>FUN_00a68970 — inserts <paramref name="p"/> as "not in use" when it is not in the set yet (operator[] FUN_00a68450).</summary>
        void EmitterSetAdd_a68970(FxRuntime rt, uint p)
        {
            if (EmitterSetFind(p) != 0) return;
            EmitterSetLowerBound(p, out uint parent, out bool left, out _);
            if (M.U32(A + 0x180) == 0xaaaaaaa) throw new InvalidOperationException("map/set too long");
            uint head = M.U32(A + 0x17c);
            uint node = M.Alloc(0x18);
            M.U32(node + 0x10, p);
            M.U8(node + 0x14, 0);
            M.U32(node, head);
            M.U32(node + 4, head);
            M.U32(node + 8, head);
            M.U16(node + 0xc, 0);
            rt.TreeInsertNode(A + 0x17c, parent, left, node);             // FUN_00a68760
            M.U8(node + 0x14, 0);
        }

        /// <summary>FUN_00a68590 — the first particle (address order) not in use, now marked in use; 0 when all are.</summary>
        uint EmitterSetTakeFree_a68590()
        {
            if (M.U32(A + 0x180) <= M.U32(A + 0x184)) return 0;
            uint head = M.U32(A + 0x17c);
            uint n = M.U32(head);
            if (n == head) return 0;
            for (; ; )
            {
                if (M.U8(n + 0x14) == 0) break;
                n = SetNext(n);
                if (n == head) break;
            }
            if (n == head) return 0;
            M.U8(n + 0x14, 1);
            M.U32(A + 0x184, M.U32(A + 0x184) + 1);
            return M.U32(n + 0x10);
        }

        /// <summary>FUN_00a686e0 — the in-use byte of <paramref name="p"/> (0 when it is not in the set).</summary>
        byte EmitterSetInUse_a686e0(uint p)
        {
            uint node = EmitterSetFind(p);
            return node == 0 ? (byte)0 : M.U8(node + 0x14);
        }

        /// <summary>FUN_00a68640 — a particle in use (byte 1) is released: count −1, byte 0.</summary>
        void EmitterSetRelease_a68640(uint p)
        {
            uint node = EmitterSetFind(p);
            if (node == 0 || M.U8(node + 0x14) != 1) return;
            M.U32(A + 0x184, M.U32(A + 0x184) - 1);
            M.U8(node + 0x14, 0);
        }

        /// <summary>FUN_00a68730 — every particle in use; count = size.</summary>
        void EmitterSetTakeAll_a68730()
        {
            uint head = M.U32(A + 0x17c);
            for (uint n = M.U32(head); n != head; n = SetNext(n)) M.U8(n + 0x14, 1);
            M.U32(A + 0x184, M.U32(A + 0x180));
        }

        /// <summary>FUN_00a685f0 — erases every node (FUN_00a68080 order: right subtree, node, then left), size and count 0.</summary>
        void EmitterSetClear_a685f0(FxRuntime rt)
        {
            uint head = M.U32(A + 0x17c);
            EmitterSetErase_a68080(M.U32(head + 4));
            M.U32(head + 4, head);
            M.U32(head, head);
            M.U32(head + 8, head);
            M.U32(A + 0x180, 0);
            M.U32(A + 0x184, 0);
        }

        /// <summary>FUN_00a683b0 — the set's destructor: clear (as FUN_00a685f0), then FUN_00a68240 frees the head node.</summary>
        void EmitterSetDestroy_a683b0(FxRuntime rt)
        {
            EmitterSetClear_a685f0(rt);
            uint head = M.U32(A + 0x17c);
            EmitterSetErase_a68080(M.U32(head + 4));
            M.Free(head);
        }

        /// <summary>FUN_00a68080 — erase of a subtree (operator delete of each 0x18 node).</summary>
        void EmitterSetErase_a68080(uint node)
        {
            while (M.U8(node + 0xd) == 0)
            {
                EmitterSetErase_a68080(M.U32(node + 8));
                uint left = M.U32(node);
                M.Free(node);
                node = left;
            }
        }

        /// <summary>Tree iterator ++ (FUN_00a68530; same as the manager trees').</summary>
        uint SetNext(uint node)
        {
            uint right = M.U32(node + 8);
            if (M.U8(right + 0xd) == 0)
            {
                uint n = right;
                for (uint l = M.U32(n); M.U8(l + 0xd) == 0; l = M.U32(n)) n = l;
                return n;
            }
            uint parent = M.U32(node + 4);
            while (M.U8(parent + 0xd) == 0 && node == M.U32(parent + 8))
            {
                node = parent;
                parent = M.U32(parent + 4);
            }
            return parent;
        }

        // ---- SetEmitterInfo message handlers ------------------------------------------------------

        /// <summary>FUN_00a78870 — speed factor: when it changes (NaN always does), +0x158 and every particle's +0xc0.</summary>
        void EmitterSetSpeed_a78870(float speed)
        {
            if (M.F32(A + 0x158) == speed) return;
            M.F32(A + 0x158, speed);
            for (uint pp = M.U32(A + 0x170); pp != M.U32(A + 0x174); pp += 4) M.U32(M.U32(pp) + 0xc0, M.U32(A + 0x158));
        }

        /// <summary>FUN_00a78840 — particle count: when it changes, vf05 (all particles back to the pool) and vf01 (a new set).</summary>
        void EmitterSetMaxCount_a78840(FxRuntime rt, int count)
        {
            if (M.I32(A + 0x154) == count) return;
            M.I32(A + 0x154, count);
            Vf05ReleaseParticles(rt);
            Vf01Init(rt);
        }

        /// <summary>FUN_00aa6fd0 — texture: the by-value std::string argument is assigned to +0x134 (FUN_004d9270), then destroyed.</summary>
        void EmitterSetTexture_aa6fd0(FxRuntime rt, string name)
        {
            uint tmp = rt.StrLocalAssign(name);                             // FUN_004ccc60 (the caller's by-value argument)
            rt.StrAssign(A + 0x134, name);
            rt.StrLocalDestroy(name, tmp);
        }
    }
}
