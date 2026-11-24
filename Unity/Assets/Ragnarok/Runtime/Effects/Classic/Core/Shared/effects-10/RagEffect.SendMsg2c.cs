// Synced from reference/port/src/RoClassicFx/Shared/effects-10/RagEffect.SendMsg2c.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// CRagEffect::SendMsg (vtable slot 16, 0xb5e650) message 0x2c — byte table 0xb5ea2e → jump-table case 3
        /// (0xb5e7d3). The client sends it right after LaunchEffect (status / option handlers, the Sight / Ruwach
        /// per-tick launcher, bolt skills). Parameters are the message's 64-bit par1 / par2 and the 32-bit par3:
        /// <list type="bullet">
        /// <item>par1 == 0xffff: the effect colour floats m_pos (+0x10/+0x14/+0x18, read by EF 166 EF_MAKEBLUR and
        /// fn_b458c0 as an ARGB tint) by the selector par2: 0x82 / 0xfa → (200, 200, 0) (quicken), 0x80 → (250, 150,
        /// 150) (overthrust), 0xa9 → (180, 180, 250) (energy coat), 0xf3 → (250, 250, 250) and duration f_17c = par3,
        /// 0x318 / 0x39d / 0x338 → (250, 250, 250), 0x422 → target f_180/f_184/f_188 = the three floats at address par3
        /// and duration 20; any other selector: nothing.</item>
        /// <item>par1 == 0xfffe: selector 0x80 → m_pos = (−1, −1, −1), 0x82 → (−2, −2, −2) (EF 166's "flash" modes);
        /// anything else: nothing.</item>
        /// <item>otherwise: count f_174 = (int)low dword of par1, capped at 50 (signed compare; negative counts
        /// stay).</item>
        /// </list>
        /// Every comparison checks the high dword too (0xffff with a non-zero high dword is a count → 50).
        /// </summary>
        public void SendMsg2c_b5e650(long par1, long par2, uint par3 = 0)
        {
            uint p1 = (uint)par1, p1hi = (uint)((ulong)par1 >> 32);
            uint p2 = (uint)par2, p2hi = (uint)((ulong)par2 >> 32);
            if (p1 == 0xffff && p1hi == 0)
            {
                if (p2hi != 0) return;
                switch (p2)
                {
                    case 0x82:
                    case 0xfa:
                        M.U32(A + 0x10, 0x43480000); M.U32(A + 0x14, 0x43480000); M.U32(A + 0x18, 0);
                        return;
                    case 0x80:
                        M.U32(A + 0x10, 0x437a0000); M.U32(A + 0x14, 0x43160000); M.U32(A + 0x18, 0x43160000);
                        return;
                    case 0xa9:
                        M.U32(A + 0x10, 0x43340000); M.U32(A + 0x14, 0x43340000); M.U32(A + 0x18, 0x437a0000);
                        return;
                    case 0xf3:
                        M.U32(A + 0x10, 0x437a0000); M.U32(A + 0x14, 0x437a0000); M.U32(A + 0x18, 0x437a0000);
                        M.U32(A + 0x17c, par3);
                        return;
                    case 0x318:
                    case 0x39d:
                    case 0x338:
                        M.U32(A + 0x10, 0x437a0000); M.U32(A + 0x14, 0x437a0000); M.U32(A + 0x18, 0x437a0000);
                        return;
                    case 0x422:
                        M.U32(A + 0x180, M.U32(par3));
                        M.U32(A + 0x184, M.U32(par3 + 4));
                        M.U32(A + 0x188, M.U32(par3 + 8));
                        M.I32(A + 0x17c, 0x14);
                        return;
                    default:
                        return;
                }
            }
            if (p1 == 0xfffe && p1hi == 0)
            {
                if (p2hi != 0) return;
                if (p2 == 0x80)
                {
                    M.U32(A + 0x10, 0xbf800000); M.U32(A + 0x14, 0xbf800000); M.U32(A + 0x18, 0xbf800000);
                }
                else if (p2 == 0x82)
                {
                    M.U32(A + 0x10, 0xc0000000); M.U32(A + 0x14, 0xc0000000); M.U32(A + 0x18, 0xc0000000);
                }
                return;
            }
            M.U32(A + 0x174, p1);
            if ((int)p1 > 0x32) M.I32(A + 0x174, 0x32);
        }

        /// <summary>
        /// Host API for the status table's <c>count</c> / <c>color</c> items (packages/status-effects/UNITY_FORMAT.md):
        /// msg 0x2c(<paramref name="par1"/>, <paramref name="par2"/>) with 32-bit parameters (high dwords 0) — call it
        /// right after <see cref="FxWorld.Launch"/>, before the first tick, as the client does. <c>count n</c> →
        /// SendMsg2c(n); <c>color</c> → SendMsg2c(0xffff, selector); [−1,−1,−1] / [−2,−2,−2] → SendMsg2c(0xfffe, 0x80 /
        /// 0x82).
        /// </summary>
        public void SendMsg2c(int par1, int par2 = 0) => SendMsg2c_b5e650((uint)par1, (uint)par2, 0);
    }
}
