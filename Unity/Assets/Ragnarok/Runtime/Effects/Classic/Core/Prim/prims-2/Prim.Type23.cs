// Synced from reference/port/src/RoClassicFx/Prim/prims-2/Prim.Type23.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x23)]
        public FxCaseResult Case23(FxRuntime rt)
        {
            Process_ae5d40(rt);
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x23)]
        public void Render23(FxRuntime rt, uint viewMatrix) => Render_b00750(rt, viewMatrix, 1.0f, 100, 100, 0xff);

        /// <summary>
        /// Camera angles shared by the multi-slot prims (inlined in fn_ae5d40 / fn_ae7540 / fn_ae7a40 and the
        /// type 0x5e case): f_3ec = 360 − (short)cvtt(view +0x30), f_4a0 = 450 − (short)cvtt(view +0x2c)
        /// wrapped to &lt; 360 (16-bit arithmetic).
        /// </summary>
        void CameraAngles_prims2(FxRuntime rt)
        {
            uint view = M.U32(rt.Mode + 0xd0);
            f_3ec = (short)(0x168 - (short)FxOps.Cvtt(M.F32(view + 0x30)));
            short a = (short)(0x1c2 - (short)FxOps.Cvtt(M.F32(view + 0x2c)));
            f_4a0 = a;
            if (a >= 0x168) f_4a0 = (short)(a - 0x168);
        }

        /// <summary>
        /// CEffectPrim::fn_ae5d40 (0xae5d40) — OnProcess for prim type 0x23 (4 slots × 4 rising sprites):
        /// camera angles, follow the master, per sub-sprite life counter / alpha (+25 while young, −12 after 10
        /// ticks, respawn with random angle / texture / height when faded), height offset from the owner.
        /// </summary>
        public bool Process_ae5d40(FxRuntime rt)
        {
            CameraAngles_prims2(rt);
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                M.I32(s + 0x3f4, M.I32(s + 0x3f4) + 1);
                for (int j = 0; j < 0x10; j += 4)
                {
                    uint e = s + (uint)j * 4;
                    float life = M.F32(e + 0x3fc) + 1.0f;
                    float alpha = M.F32(e + 0x400);
                    M.F32(e + 0x3fc, life);
                    if (10.0f > life)
                    {
                        M.F32(e + 0x400, alpha + 25.0f);
                        continue;
                    }
                    alpha = alpha - 12.0f;
                    M.F32(e + 0x400, alpha);
                    if (!(12.0f > alpha)) continue;
                    M.F32(e + 0x3fc, 0f);
                    M.F32(e + 0x400, 0f);
                    int r = rt.Rand.Next() % 0x168;
                    M.F32(e + 0x408, (float)(r < 0 ? 0 : r));
                    r = rt.Rand.Next() % 5;
                    M.F32(e + 0x404, (float)(r < 0 ? 0 : r));
                    int h = rt.Rand.Next() % 8;
                    float height = (float)(-3 - (h < 0 ? 0 : h));
                    uint dst = j == 0 ? 0x488u : j == 4 ? 0x470u : j == 8 ? 0x494u : 0x47cu;
                    M.F32(s + dst, height);
                }
                uint owner = M.U32(master + 0x138);
                if (owner != 0) M.F32(s + 0x44c, rt.ActorHeightOffset_bd19d0(owner));
            }
            return true;
        }

        /// <summary>
        /// Whether a camera-facing sub-sprite of fn_b00750 / fn_affd30 swaps its corners (P0↔P1, P2↔P3):
        /// by its angle relative to the camera, inverted unless the texture index is 0 or 2.
        /// </summary>
        static bool Flip_b00750(float tex, int rel)
        {
            bool byAngle = (0x45 < rel || rel < -0x4f) && (uint)(rel + 0x10e) < 0x21d;
            return tex == 0.0f || tex == 2.0f ? byAngle : !byAngle;
        }

        /// <summary>
        /// CEffectPrim::fn_b00750 (0xb00750) — Render for prim type 0x23: for each active slot, a diamond of
        /// size scale·f_468 rotated to face the camera (FUN_00b0d9c0), drawn for four sub-sprites at
        /// (cos, sin)·4 of their angle, height offset, alpha and texture, via fn_b0a900 (mode 2).
        /// Slot byte +0x6c = 1 switches the colour to (0xff, 0x14, 0x14) for this and later slots.
        /// </summary>
        public void Render_b00750(FxRuntime rt, uint viewMatrix, float scale, int r, int g, int b)
        {
            int count = 0;
            uint[] cosOff = SubSpriteCosOff, sinOff = SubSpriteSinOff, hOff = SubSpriteHeightOff;
            System.Span<float> p = stackalloc float[12];
            System.Span<float> q = stackalloc float[12];
            for (int slot = 0; slot < 4; slot++)
            {
                uint s = A + (uint)slot * 0xb4;
                if (M.U8(s + 0x3e8) != 1) continue;
                if (M.U8(s + 0x454) == 1)
                {
                    r = (r & ~0xff) | 0xff;
                    g = (g & ~0xff) | 0x14;
                    b = (b & ~0xff) | 0x14;
                }
                float size = scale * M.F32(s + 0x468);
                for (int j = 0; j < 4; j++)
                {
                    int ia = FxOps.Cvtt(M.F32(s + 0x408 + (uint)j * 0x10));
                    M.F32(s + cosOff[j], FxMath.CosTableAt(ia) * 4.0f);
                    M.F32(s + sinOff[j], FxMath.SinTableAt(ia) * 4.0f);
                }
                Diamond45(p, size);
                Rotate4_b0d9c0(p, f_3ec, f_4a0);
                for (int j = 0; j < 4; j++)
                {
                    uint e = s + (uint)j * 0x10;
                    float tex = M.F32(e + 0x404);
                    int rel = FxOps.Cvtt(M.F32(e + 0x408) - (float)(int)f_3ec);
                    bool flip = Flip_b00750(tex, rel);
                    float c = M.F32(s + cosOff[j]), sn = M.F32(s + sinOff[j]), h = M.F32(s + hOff[j]);
                    float off = M.F32(s + 0x44c);
                    q.Clear();
                    for (int k = 0; k < 4; k++)
                    {
                        int src = flip ? (k ^ 1) : k;
                        q[k * 3] = p[src * 3] + c;
                        q[k * 3 + 1] = (p[src * 3 + 1] + h) - off;
                        q[k * 3 + 2] = p[src * 3 + 2] + sn;
                    }
                    Quad_b0a900(rt, ref count, (short)FxOps.Cvtt(M.F32(e + 0x400)),
                        q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], q[8], q[9], q[10], q[11],
                        viewMatrix, 0, (sbyte)(byte)FxOps.Cvtt(tex), 0, (byte)r, (byte)g, (byte)b,
                        0f, 1.0f, 0f, 1.0f, 2);
                }
            }
        }
    }
}
