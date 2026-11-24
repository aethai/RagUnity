// Synced from reference/port/src/RoClassicFx/Shared/emotion/MsgEffect.Emotion.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CMsgEffect type 1 — the emotion balloon (package emotion; created by <see cref="FxRuntime.MakeEmotion_bfba20"/>).
    /// Msg 0x16 / 0x32 have no type-1 branch; msg 0xe starts it at the actor position lifted by 20 with the record's ACT
    /// action (action 1000 = ET_CHAT_PROHIBIT: action 10, +0x70 = 10000, red); msg 0x31 (actor gone) ends it.
    /// </summary>
    public readonly partial struct MsgEffect
    {
        public const int TypeEmotion = 1;

        /// <summary>
        /// FUN_00d3c860, case 0x00d3ca47 (types 1, 2, 8, 9, 10, 0x25, 0x26 — only 1 is ported): while attached, the
        /// balloon takes the actor's x / y / z and is lifted by 20 (by 10 with +0x70 = 10000, the chat-ban balloon). The
        /// render flags +0x124 are not touched. Detached: nothing.
        /// </summary>
        public void FollowEmotion_d3ca47()
        {
            uint a = M.U32(A + 0x15c);
            if (a == 0) return;
            M.U32(A + 0x10, M.U32(a + 0x10));
            M.U32(A + 0x14, M.U32(a + 0x14));
            M.U32(A + 0x18, M.U32(a + 0x18));
            float y = M.F32(A + 0x14);
            M.F32(A + 0x14, M.I32(A + 0x70) == 0x2710 ? y - 10.0f : y - 20.0f);
        }

        /// <summary>
        /// CMsgEffect::OnProcess case 0x00d3a74e for type 1 (after the follow and FUN_00bd4a60): ACT frame = (int)(t /
        /// delay) mod frames (t = 24 ms ticks since msg 0xe, delay +0x58 from the ACT); the chat-ban balloon (+0x70 =
        /// 10000) instead holds frame 15 (its motion's first clip gets +0xc = 0), tint (255, 250, 80, 80), and never ends;
        /// any other balloon dies after one pass of its action ((t / delay) / frames ≥ 1) — the local player's
        /// (+0x184 = player AID) then sends mode msg 0x21 (emotion done). Returns false when the balloon dies.
        /// </summary>
        public bool Emotion_d3a74e(FxRuntime rt)
        {
            uint act = rt.GetActRes(A);
            float t = Ticks_bd1c10(rt);
            int n = rt.ActMotionCount(act, M.U32(A + 0x38));
            if (n == 0) return true;
            float f = t / M.F32(A + 0x58);
            M.I32(A + 0x3c, FxOps.Cvtt(f) % n);
            if (M.I32(A + 0x70) == 0x2710)
            {
                M.I32(A + 0x3c, 0xf);
                uint motion = rt.ActGetMotion(act, M.U32(A + 0x38), 0xf);
                M.U32(M.U32(motion + 0x20) + 0xc, 0);
                rt.SetArgb(A, 0xff, 0xfa, 0x50, 0x50);
                return true;
            }
            f = f / (float)n;
            if (f < 1.0f || float.IsNaN(f)) return true;   // COMISS f, 1.0; JB alive (unordered too)
            if (M.U32(A + 0x184) == rt.PlayerAid) rt.GameModeSendMsg(0x21, 0, 0, 0, 0);
            return false;
        }
    }
}
