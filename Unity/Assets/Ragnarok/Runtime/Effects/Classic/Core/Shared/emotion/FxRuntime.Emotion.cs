// Synced from reference/port/src/RoClassicFx/Shared/emotion/FxRuntime.Emotion.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Package emotion: the emotion balloons of the original client (ZC_EMOTION 0x00c0 / 0x0bea). The packet handler sends
    /// actor msg 0xa0 (p1 = emotion pack, p2 = emotion id); CGameActor::SendMsg (0xbd8c08) makes a CMsgEffect of type 1
    /// through FUN_00bfba20 from the CCashEmotionMgr record of (pack, id) (Lua: CashEmotionList), attaches it to the actor
    /// and the world list; CMsgEffect::OnProcess plays the record's ACT action once (<see cref="MsgEffect.Emotion_d3a74e"/>).
    /// See packages/emotion/NOTES.md.
    /// </summary>
    public sealed partial class FxRuntime
    {
        /// <summary>One CCashEmotionMgr record (FUN_00576540): ACT action (+0x20) and the sprite / action names (+0x28 / +0x2c → FUN_00577480 / FUN_00576100).</summary>
        public readonly struct EmotionEntry
        {
            public readonly int Action;
            public readonly string Spr, Act;
            public EmotionEntry(int action, string spr, string act) { Action = action; Spr = spr; Act = act; }
        }

        /// <summary>
        /// The client's emotion records by (pack, id) — what c_InsertEmotionListTable / c_InsertEmotionVarientListTable /
        /// c_InsertEmotionGroup (FUN_00578130) build from Lua Files\CashEmotion\CashEmotionList at start-up. Defaults: the
        /// Custom data (<see cref="DefaultEmotions"/>); the host may replace or extend it (<see cref="SetEmotion"/>).
        /// Names are byte strings relative to data\sprite\ (CRenderObject::SetSprName / SetActName).
        /// </summary>
        public readonly Dictionary<long, EmotionEntry> Emotions = DefaultEmotions();

        /// <summary>
        /// FUN_00577b60(0) — the id of ET_CHAT_PROHIBIT in the client's emotion enum (CashEmotionList: 34). The emotion
        /// with this id goes into the actor's single slot +0x3a4 (<see cref="ActorEmotion_bd8c08"/>); its record's action
        /// 1000 makes a balloon that never ends by itself (removed by actor msg 0x76).
        /// </summary>
        public int EmotionChatProhibit = 34;

        static long EmotionKey(int pack, int id) => ((long)pack << 32) | (uint)id;

        /// <summary>Adds or replaces the record of (<paramref name="pack"/>, <paramref name="id"/>).</summary>
        public void SetEmotion(int pack, int id, int action, string spr, string act) => Emotions[EmotionKey(pack, id)] = new EmotionEntry(action, spr, act);

        /// <summary>FUN_00576540 (thiscall on CCashEmotionMgr [0x11a6124], RET 8) — the record of (pack, id); negative keys find nothing.</summary>
        public bool TryGetEmotion(int pack, int id, out EmotionEntry entry)
        {
            entry = default;
            if (pack < 0 || id < 0) return false;
            return Emotions.TryGetValue(EmotionKey(pack, id), out entry);
        }

        /// <summary>FUN_00578be0 (thiscall on CCashEmotionMgr, RET 4) — <paramref name="id"/> is ET_CHAT_PROHIBIT (FUN_00577b60(0)); negative ids are not.</summary>
        public bool IsChatProhibitEmotion_578be0(int id) => id >= 0 && id == EmotionChatProhibit;

        /// <summary>
        /// The Custom client's data (data\luafiles514\lua files\cashemotion\cashemotionlist.lub, main_emotion): pack 0
        /// (basic, emotion\basic.spr / .act, 98 actions) is the ZC_EMOTION set — note the id → action shifts (ET_BIGTHROB
        /// 14 → 4, ET_SWEAT 4 → 5, …, ET_CHAT_PROHIBIT 34 → 1000); packs 1..3 are the cash packs (emt_21,
        /// emt_2023chuseok, emt_2023thxgiving: 10 actions each) reached through ZC 0x0bea.
        /// </summary>
        public static Dictionary<long, EmotionEntry> DefaultEmotions()
        {
            var d = new Dictionary<long, EmotionEntry>();
            void Pack(int pack, string name, int[] pairs)
            {
                for (int i = 0; i < pairs.Length; i += 2) d[EmotionKey(pack, pairs[i])] = new EmotionEntry(pairs[i + 1], name + ".spr", name + ".act");
            }
            Pack(0, "emotion\\basic", new[]
            {
                0, 0,  1, 1,  2, 2,  3, 3,  14, 4,  4, 5,  5, 6,  6, 7,  7, 8,  8, 9,
                9, 10,  15, 15,  16, 16,  17, 17,  18, 18,  19, 19,  20, 20,  21, 21,  22, 22,  23, 23,
                24, 24,  25, 25,  26, 26,  27, 27,  28, 28,  29, 29,  30, 30,  31, 31,  32, 32,  33, 33,
                36, 35,  37, 36,  38, 37,  39, 38,  40, 39,  41, 40,  42, 41,  43, 42,  44, 43,  45, 44,
                46, 45,  47, 46,  52, 51,  53, 52,  54, 53,  55, 54,  56, 55,  57, 56,  11, 11,  10, 12,
                12, 13,  65, 64,  68, 67,  69, 68,  70, 69,  71, 70,  72, 71,  73, 72,  74, 73,  75, 74,
                76, 75,  77, 76,  78, 77,  79, 78,  80, 79,  13, 14,  34, 1000,  35, 34,  48, 47,  49, 48,
                50, 49,  51, 50,  58, 57,  59, 58,  60, 59,  61, 60,  62, 61,  63, 62,  64, 63,  66, 65,
                67, 66,  81, 86,  82, 87,  83, 88,  84, 89,  85, 90,  86, 91,  87, 92,  88, 93,  89, 94,
                90, 95,  91, 96,  92, 97,
            });
            Pack(1, "emotion\\emt_21", new[] { 29, 0, 27, 1, 38, 2, 52, 3, 1, 4, 93, 5, 94, 6, 95, 7, 96, 8, 97, 9 });
            Pack(2, "emotion\\emt_2023chuseok", new[] { 15, 0, 5, 1, 33, 2, 37, 3, 0, 4, 1, 5, 2, 6, 10, 7, 11, 8, 12, 9 });
            Pack(3, "emotion\\emt_2023thxgiving", new[] { 43, 0, 39, 1, 40, 2, 44, 3, 37, 4, 42, 5, 47, 6, 14, 7, 4, 8, 56, 9 });
            return d;
        }

        /// <summary>
        /// FUN_00bfba20 (thiscall on the mode, RET 0x10; position pointer, pack, id, AID) — the emotion balloon: the record
        /// of (pack, id) (none → 0, nothing allocated), its sprite / action names copied into two std::strings of the frame,
        /// a new CMsgEffect (nothrow new), pushed on the world list (world +0x8, when there is a world), msg 0x16 (AID,
        /// type 1), msg 0xe (position, names, the record's action), the strings freed (action name first). Returns the
        /// balloon, not yet attached (the caller sends msg 0x32).
        /// </summary>
        public uint MakeEmotion_bfba20(uint pos, int pack, int id, uint aid)
        {
            if (!TryGetEmotion(pack, id, out var e)) return 0;
            uint s1 = StdStringBuffer(e.Spr);
            uint s2 = StdStringBuffer(e.Act);
            var g = new MsgEffect(M, M.Alloc(MsgEffect.SizeOf));
            g.Construct(this);
            uint world = M.U32(Mode + 0xcc);
            if (world != 0)
            {
                ListPushBack(world + 0x8, g.A);
                MsgEffectObjects.Add(g.A);
            }
            g.SendMsg(this, 0, MsgEffect.MsgOwner, (int)aid, MsgEffect.TypeEmotion);
            g.Init_e(this, pos, e.Spr, e.Act, e.Action);
            StdStringFree(s2, e.Act.Length);
            StdStringFree(s1, e.Spr.Length);
            return g.A;
        }

        /// <summary>
        /// CGameActor::SendMsg msg 0xa0 (0x00bd8c08; p1 pack, p2 emotion id) — what ZC_EMOTION sends to the actor. Nothing
        /// without a game mode. An ordinary emotion always makes a new balloon (a second emotion does not replace the first:
        /// both play, each to its own end); ET_CHAT_PROHIBIT only when the actor's slot +0x3a4 is empty, and the balloon is
        /// kept there. The balloon goes into the actor's list +0x3b0 and is attached (msg 0x32). No record for (pack, id):
        /// no balloon, and the mode gets msg 0x21 (it clears its "emotion in progress" flag, mode +0x284).
        /// </summary>
        public void ActorEmotion_bd8c08(uint actor, int pack, int id)
        {
            if (Mode == 0) return;
            uint g;
            if (!IsChatProhibitEmotion_578be0(id))
            {
                g = MakeEmotion_bfba20(actor + 0x10, pack, id, M.U32(actor + 0x110));
                if (g == 0)
                {
                    GameModeSendMsg(0x21, 0, 0, 0, 0);
                    return;
                }
            }
            else
            {
                if (M.U32(actor + 0x3a4) != 0) return;
                g = MakeEmotion_bfba20(actor + 0x10, pack, id, M.U32(actor + 0x110));
                M.U32(actor + 0x3a4, g);
                if (g == 0)
                {
                    GameModeSendMsg(0x21, 0, 0, 0, 0);
                    return;
                }
            }
            ActorListPush_be8650(actor, g);
            new MsgEffect(M, g).SendMsg(this, actor, MsgEffect.MsgAttach);
        }

        /// <summary>
        /// CGameActor::SendMsg msg 0x76 (0x00bd8816) — the ET_CHAT_PROHIBIT balloon in slot +0x3a4, if any, is deleted now
        /// (CWorld::RemoveObject; its dtor's msg 0x30 clears the slot).
        /// </summary>
        public void ActorRemoveChatProhibit_bd8816(uint actor)
        {
            uint g = M.U32(actor + 0x3a4);
            if (g == 0) return;
            RemoveWorldObject_9fac00(g);
        }

        /// <summary>
        /// FUN_006c1700 (thiscall on CActRes, RET 4) — the ACT event name <paramref name="index"/> (vector of std::string
        /// at +0x120 / +0x124, 0x18 bytes each); out of range → "" (0xf432d8).
        /// </summary>
        public string ActEventName_6c1700(uint actRes, int index)
        {
            uint begin = M.U32(actRes + 0x120);
            int count = (int)(M.U32(actRes + 0x124) - begin) / 0x18;
            if ((uint)index >= (uint)count) return "";
            uint s = begin + (uint)index * 0x18;
            if (M.U32(s + 0x14) >= 0x10) s = M.U32(s);
            return ReadCString(s);
        }

        /// <summary>
        /// FUN_00bd4a60 (thiscall on a CRenderObject) — the ACT frame sounds: every frame passed since the last call (+0x44
        /// last frame, +0x40 last action; a new action restarts from frame 0; a wrap plays the rest of the action, then
        /// frames 0..current) whose motion has an event (+0x30 ≠ −1) plays the event name when it contains ".wav"
        /// (PlaySound3D relative to the player, y 0, 250 / 40 / 1.0). Then +0x44 = frame (+0x3c), +0x40 = action (+0x34).
        /// The name is copied into a std::string of the frame (heap buffer from 16 bytes, mirrored).
        /// </summary>
        public void ActFrameSounds_bd4a60(uint obj)
        {
            int last = M.I32(obj + 0x44);
            int cur = M.I32(obj + 0x3c);
            int lastAction = M.I32(obj + 0x40);
            if (last == cur)
            {
                if (lastAction == M.I32(obj + 0x34))
                {
                    M.I32(obj + 0x44, cur);
                    M.I32(obj + 0x40, M.I32(obj + 0x34));
                    return;
                }
                M.I32(obj + 0x44, 0);
                last = 0;
            }
            else if (lastAction != M.I32(obj + 0x34))
            {
                M.I32(obj + 0x44, 0);
                last = 0;
            }
            uint act = M.U32(obj + 0x108);
            if (last < cur)
            {
                for (int i = last + 1; i <= M.I32(obj + 0x3c); i++) FrameSound(obj, act, i, false);
            }
            else if (cur < last)
            {
                int n = ActMotionCount(act, M.U32(obj + 0x38));
                int i = M.I32(obj + 0x44);
                if (n <= i)
                {
                    i = n - 1;
                    M.I32(obj + 0x44, i);
                }
                for (i++; i < n; i++) FrameSound(obj, act, i, false);
                for (int j = 0; j <= M.I32(obj + 0x3c); j++) FrameSound(obj, act, j, true);
            }
            M.I32(obj + 0x44, M.I32(obj + 0x3c));
            M.I32(obj + 0x40, M.I32(obj + 0x34));
        }

        /// <summary>One frame of <see cref="ActFrameSounds_bd4a60"/>; <paramref name="inlineFind"/>: the third loop's inlined memchr search (same result as find(".wav")).</summary>
        void FrameSound(uint obj, uint act, int frame, bool inlineFind)
        {
            uint motion = ActGetMotion(act, M.U32(obj + 0x38), (uint)frame);
            int ev = M.I32(motion + 0x30);
            if (ev == -1) return;
            string name = ActEventName_6c1700(act, ev);
            uint buf = StdStringBuffer(name);
            bool wav = inlineFind ? name.Length >= 4 && name.IndexOf(".wav", StringComparison.Ordinal) >= 0
                                  : name.IndexOf(".wav", StringComparison.Ordinal) >= 0;
            if (wav) PlaySound3DRelative(name, M.F32(obj + 0x10), 0.0f, M.F32(obj + 0x18), 0xfa, 0x28, 1.0f, 0);
            StdStringFree(buf, name.Length);
        }
    }
}
