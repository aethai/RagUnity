// Synced from reference/port/src/RoClassicFx/Shared/effects-7/Effects7Shared.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Non-ASCII resource names used by the effects-7 handlers: the client's CP949 bytes mapped 1:1 to chars
    /// (Latin-1), exactly as the engine passes them to GetTexture / SetSprName. <see cref="Table"/> lists the
    /// game.exe address of each (verified byte-for-byte by the tests).
    /// </summary>
    public static class Effects7Strings
    {
        const string Efx = "ÀÌÆÑÆ®\\";   // 이팩트\ (sprite folder)
        const string FireArrow = "effect\\ºÒÈ­»ì";   // effect\불화살

        public const string SprParticle4 = Efx + "particle4.spr";
        public const string ActParticle4 = Efx + "particle4.act";

        /// <summary>Fire Bolt shard textures effect\불화살1..6.tga (0xffa81c + 0x14·i).</summary>
        public static readonly string[] FireArrowTga =
        {
            FireArrow + "1.tga", FireArrow + "2.tga", FireArrow + "3.tga",
            FireArrow + "4.tga", FireArrow + "5.tga", FireArrow + "6.tga",
        };

        public static readonly (uint Address, string Value)[] Table =
        {
            (0x00ffa894, SprParticle4), (0x00ffa8ac, ActParticle4),
            (0x00ffa81c, FireArrowTga[0]), (0x00ffa830, FireArrowTga[1]), (0x00ffa844, FireArrowTga[2]),
            (0x00ffa858, FireArrowTga[3]), (0x00ffa86c, FireArrowTga[4]), (0x00ffa880, FireArrowTga[5]),
        };
    }

    public static partial class FxMath
    {
        /// <summary>
        /// FUN_00bce500 (0xbce500, stdcall (x1, z1, x2, z2), ret 0x10) — heading from (x1, z1) to (x2, z2) in degrees:
        /// atan2(x2 - x1, -(z2 - z1)) through the CRT _CIatan2 (double, rounded to float), / 3.141592 * 180, minus 360
        /// when ≥ 360 (COMISS; JB: unordered keeps it), plus 360 when &lt; 0. Same arithmetic as
        /// <see cref="FxRuntime.FaceTowards"/> without the zero-vector early out; runs natively in the oracle
        /// (its _CIatan2 is serviced by FPATAN).
        /// </summary>
        public static float HeadingTo_bce500(float x1, float z1, float x2, float z2)
        {
            float dx = x2 - x1;
            float dz = -(z2 - z1);
            float a = (float)Math.Atan2((double)dx, (double)dz);
            a = a / 3.141592f * 180.0f;
            if (a >= 360.0f) a -= 360.0f;
            if (0f > a) a += 360.0f;
            return a;
        }
    }

    public readonly partial struct RagEffect
    {
        /// <summary>
        /// vf18 CRagEffect::SetDuration (0x5d6f20): +0x17c = ticks. This is how the client gives the cast effects
        /// their cast time: ZC_USESKILL_ACK → FUN_00c7c180 sends the caster msg 0x55 (EF 54..59 / 12 …, p2 =
        /// delaytime / 16, C truncation) and FUN_00c7f900 sends the target msg 0x56 (EF 60 lock-on, same p2);
        /// CAbleToMakeEffect::SendMsg cases 3/4 (0xbd817b / 0xbd809f) launch the effect (kept at actor +0x14c /
        /// +0x150) and immediately send it msg 0x50 → CRagEffect::vf16 (0xb5e650) case 5 → vf18(p2). So the duration
        /// replaces the Init default after Init and before the first OnProcess; a repeated msg 0x55/0x56 while the
        /// effect is alive only re-sends msg 0x50 (the new value applies from then on; prims already launched keep
        /// the duration they copied at tick 0). Hosts: call this right after <see cref="FxWorld.Launch"/>.
        /// </summary>
        public void SetDuration_5d6f20(int ticks) => f_17c = ticks;

        /// <summary>
        /// vf16 msg 0x18 (0xb5e650 case 1): m_master = 0 — how the client cancels a kept effect (cast interrupted /
        /// finished, msg 0x57 remove lock-on). The next OnProcess returns dead; prims die at their next OnProcess.
        /// </summary>
        public void DetachMaster_msg18() => M.U32(A + 0x138, 0);
    }

    public sealed partial class FxWorld
    {
        /// <summary>
        /// The client's cast-time conversion for msgs 0x55/0x56: (delay + ((delay &gt;&gt; 31) &amp; 15)) &gt;&gt; 4, i.e. delay / 16
        /// truncated toward zero (FUN_00c7c180 / FUN_00c7f900).
        /// </summary>
        public static int CastTicks(int delayMs) => delayMs / 16;

        /// <summary>
        /// Launch + msg 0x50 as the client does for cast effects (EF 54..59 on the caster, EF 60 on the target):
        /// <see cref="Launch"/> then <see cref="RagEffect.SetDuration_5d6f20"/>(<see cref="CastTicks"/>(delayMs)).
        /// </summary>
        public RagEffect? LaunchWithCastTime(int efId, uint owner, int delayMs)
        {
            RagEffect? e = Launch(efId, owner);
            if (e.HasValue) e.Value.SetDuration_5d6f20(CastTicks(delayMs));
            return e;
        }
    }
}
