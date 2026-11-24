// Synced from reference/port/src/RoClassicFx/Prim/npcwarp/Prim.Type41.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Prim type 0x41 (EF 321, the map warp NPC portal; launched twice by CRagEffect::fn_b8fa90): four rings that
    /// shrink from their start radius to 0 while they tilt up from flat to vertical, fade in, and pop back out flat at
    /// radius 10. Processed inline in CEffectPrim::OnProcess (case 0x41, ends with break), drawn by the shared ring
    /// renderer fn_af0420 (CEffectPrim::Render 0xaf4ce0 groups 0x41 with 0x6b / 0x6c / 0x71 / 0x76 / 0x8b).
    /// Ring blocks (stride 0xb4 from +0x3e8): +0 enable, +2 alpha (short), +6 span, +8 band length, +0xc age,
    /// +0x10 start angle, +0x14..+0x67 21 segment lengths, +0x68..+0x7c 21 segment bytes, +0x7e tilt, +0x80 radius.
    /// </summary>
    public readonly partial struct Prim
    {
        [FxPrimProcess(0x41)]
        public FxCaseResult Case41(FxRuntime rt)
        {
            Process41_ad5a30();
            return FxCaseResult.Continue;
        }

        [FxPrimRender(0x41)]
        public void Render41(FxRuntime rt, uint viewMatrix) => Render_af0420(rt, viewMatrix);

        /// <summary>
        /// CEffectPrim::OnProcess case 0x41 (0xad5a30 main switch). Copies the master's position, then for each enabled
        /// block: age (+0xc) + 1; radius (+0x80) −= 0.05, and once it is ≤ 0 it restarts at 10 with alpha 0; tilt
        /// (+0x7e) = (short)(int)(90 − radius · 9); band length (+8) = radius; alpha (+2) + 1 up to 70 while
        /// age &lt; duration − 40, afterwards − 3 a tick (clamped at 0); every segment whose byte (+0x68 + j) is 0 gets
        /// length (+0x14 + 4j) = band length (0 if negative).
        /// </summary>
        void Process41_ad5a30()
        {
            uint master = m_master_ptr;
            M.U32(A + 0x10, M.U32(master + 0x10));
            M.U32(A + 0x14, M.U32(master + 0x14));
            M.U32(A + 0x18, M.U32(master + 0x18));
            for (uint k = 0; k < 4; k++)
            {
                uint b = A + 0x3e8 + k * 0xb4;
                if (M.U8(b) != 1) continue;
                int age = M.I32(b + 0xc) + 1;
                M.I32(b + 0xc, age);
                float r = M.F32(b + 0x80) - 0.05f;
                M.F32(b + 0x80, r);
                if (r <= 0.0f)
                {
                    M.F32(b + 0x80, 10.0f);
                    M.I16(b + 2, 0);
                    r = 10.0f;
                }
                M.I16(b + 0x7e, (short)FxOps.Cvtt(90.0f - r * 9.0f));
                M.F32(b + 8, r);
                short alpha = M.I16(b + 2);
                if (age < m_duration - 0x28)
                {
                    if (alpha < 0x46) M.I16(b + 2, (short)(alpha + 1));
                }
                else
                {
                    short faded = (short)(alpha - 3);
                    M.I16(b + 2, faded);
                    if (faded < 0) M.I16(b + 2, 0);
                }
                for (uint j = 0; j < 21; j++)
                {
                    if (M.U8(b + 0x68 + j) != 0) continue;
                    float len = M.F32(b + 8);
                    M.F32(b + 0x14 + j * 4, len);
                    if (len < 0.0f) M.F32(b + 0x14 + j * 4, 0.0f);
                }
            }
        }
    }
}
