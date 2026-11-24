// Synced from reference/port/src/RoClassicFx/Prim/Prim.Lifecycle.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct Prim
    {
        // field offsets the constructor zeroes (static: no array per constructed prim)
        static readonly uint[] CtorZero1 = { 0x2f4, 0x2f8, 0x354, 0x360 };
        static readonly uint[] CtorZero2 = { 0x2bc, 0x31c, 0x34c, 0x300, 0x2fc, 0x330, 0x334, 0x338, 0x358, 0x364, 0x328, 0x2ec,
                                             0x2c0, 0x320, 0x350, 0x2f0, 0x33c, 0x340, 0x344, 0x308, 0x304, 0x35c, 0x368 };
        static readonly uint[] CtorZero3 = { 0x2a4, 0x2a8, 0x2ac, 0x2b0, 0x2b4, 0x2b8 };
        static readonly uint[] CtorZero4 = { 0x370, 0x374, 0x378, 0x388, 0x38c, 0x390, 0x394, 0x398, 0x39c,
                                             0x3a0, 0x3a4, 0x3a8, 0x3ac, 0x3b0, 0x3b4, 0x30c, 0x310, 0x314 };

        /// <summary>CEffectPrim::CEffectPrim (0xad4bb0).</summary>
        /// <summary>The 0x200 RP faces' TL vertices: zero with specular 0xff000000 (FUN_005470f0), 32-bit stores.</summary>
        static unsafe void InitFaces(byte* faces)
        {
            for (int face = 0; face < 0x200; face++, faces += FxRenderer.FaceSize)
            {
                int* v = (int*)faces;
                for (int k = 0; k < 3 * FxRenderer.VertexSize / 4; k++) v[k] = 0;
                v[0x14 / 4] = v[0x34 / 4] = v[0x54 / 4] = unchecked((int)0xff000000u);
            }
        }

        public unsafe void Construct(FxRuntime rt)
        {
            rt.ConstructRenderObject(A);
            M.U32(A, FxRuntime.VtCEffectPrim);
            // runs of zeroed dwords / floats (+0.0f is all-zero bits) written in bulk; same bytes as the per-field stores
            M.Fill(A + 0x258, 0, 0x284 + 4 - 0x258);
            M.Fill(A + 0x370, 0, 0x3b4 + 4 - 0x370);
            M.Fill(A + 0x3b8, 0, 0x3e8 - 0x3b8);
            for (uint block = 0; block < 4; block++) M.Fill(A + 0x46c + block * 0xb4, 0, 12 * 4);
            for (uint trail = 0; trail < 0x32; trail++) M.Fill(A + 0x6b8 + trail * 0x8c, 0, 15 * 4);
            // the 0x200 RP faces: three TL vertices each zeroed with specular = 0xff000000 (FUN_005470f0); the face tail
            // (+0x60 texture, +0x64/+0x68 blend) is left as it is
            InitFaces(M.Pointer(A + 0x2210, 0x200 * FxRenderer.FaceSize));
            f_208 = 0; M.U32(A + 0x148, 0); f_1cc = 0; m_type = 0; f_1dc = 0; f_1c4 = 1;
            f_14c = 0; f_150 = 0; f_154 = 0; f_158 = 0; f_15c = 0; f_138 = 0; m_master_ptr = 0;
            f_1d4 = 0; f_1d8 = 0; f_1e0 = 0; f_348 = 0f;
            M.U32(A + 0x28c, 0); f_288 = 36.0f; f_4c = 0f; M.U32(A + 0x1f8, 0xb4); f_318 = 1.0f;
            foreach (uint off in CtorZero1) M.F32(A + off, 0f);
            f_36c = 100.0f;
            foreach (uint off in CtorZero2) M.F32(A + off, 0f);
            M.U32(A + 0x23c, 0xff); f_240 = 0xff; f_244 = 0xff;
            f_248 = 0; f_24c = 0; f_250 = 0; f_200 = -1;
            f_2c8 = 0.0001f; f_2cc = 0.0001f; f_29c = 0.0001f;
            foreach (uint off in CtorZero3) M.F32(A + off, 0f);
            f_2c4 = 0.0001f; f_2d0 = 0.0001f; f_2a0 = 0.0001f;
            f_1fc = 0; f_234 = 0; M.U32(A + 0x238, 0);
            foreach (uint off in CtorZero4) M.F32(A + off, 0f);
            m_duration = 100; f_1ec = -1; f_1f0 = -1; m_argb = -1;
            f_324 = 254.0f; f_32c = 1.0f; f_2d4 = 254.0f; f_2d8 = 0f;
            M.U8(A + 0x254, 1); f_1f4 = 1; f_1e8 = 4; f_38 = 0; f_3c = 0; f_204 = 1; f_144 = 0;
            f_2e4 = 0f; M.U32(A + 0x2dc, 0); f_2e8 = 1.0f; M.F32(A + 0x2e0, 1.0f);
            FxMatrix.Identity(M, A + 0x160);
            FxMatrix.Identity(M, A + 0x190);
            M.U8(A + 0x1c8, 0);
            M.U32(A + 0xfa10, 0); M.U32(A + 0xfa14, 0);
        }

        /// <summary>CEffectPrim::Init (0xad5830).</summary>
        public void Init(RagEffect master, int primType, float dx, float dy, float dz)
        {
            m_master = master;
            m_type = primType;
            f_388 = dx + f_388;
            f_38c = dy + f_38c;
            f_390 = dz + f_390;
            float x = master.m_pos_x; f_37c = x;
            float y = master.m_pos_y; f_380 = y;
            float z = master.m_pos_z; f_384 = z;
            m_pos_x = x; m_pos_y = y; m_pos_z = z;
            M.U32(A + 0x13c, 0);
        }

    }

    public readonly partial struct RagEffect
    {
        /// <summary>CRagEffect::LaunchEffectPrim (0xb43ec0).</summary>
        public Prim LaunchEffectPrim(FxRuntime rt, int primType, float dx, float dy, float dz)
        {
            var prim = new Prim(M, M.Alloc(Prim.SizeOf));
            prim.Construct(rt);
            prim.Init(this, primType, dx, dy, dz);
            rt.ListPushBack(A + 0x1a4, prim.A);
            rt.ListPushBack(M.U32(rt.Mode + 0xcc) + 0x8, prim.A);
            return prim;
        }
    }
}
