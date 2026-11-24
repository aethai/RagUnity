using System;
using System.Collections.Generic;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// How the client sends an emotion (docs/audits/chat-windows-2026-10-04.md §4.4): every emotion — the chat's talk types
    /// (mode msg 0x149 → 0x56e6a0), the Emoji List's double click and the mini window's Enter — runs
    /// CCashEmotionAction_Play::vf01 (0x56e6f0: ET_DICE1 + rand % 6, ET_YUT1 + rand % 7) and then CGameMode msg 0x1f
    /// (0xc15b0c): nothing while an emotion is in flight (mode +0x284) or within 2500 ms of the last one (+0x278); Basic
    /// Skill level &gt; 1 (a Summoner: SU_BASIC_SKILL level &gt; 0) else EMSG_EMOTION_USE_FAIL_SKILL_LEVEL in red; a pack that is
    /// neither the basic one nor owned → EMSG_EMOTION_EXPANTION_USE_FAIL_UNPURCHASED in red; no record → nothing; then the
    /// packet (0x0be9 {pack, emotion} on 20240619), the time kept and the flag set. Mode msg 0x21 clears the flag (the own
    /// emotion balloon ended, or ZC_EMOTION found no record — the effect engine's CMsgEffect sends it).
    /// </summary>
    public static class RoEmotionSender {
        public const int IntervalMs = 0x9c4;
        public const ushort BasicSkillId = 1;        // NV_BASIC
        public const ushort SummonerBasicSkillId = 0x139a;   // SU_BASIC_SKILL

        /// <summary>What mode msg 0x1f made of an emotion.</summary>
        public enum Outcome {
            Sent,
            InFlight,
            TooSoon,
            SkillLevel,
            Unpurchased,
            NoRecord,
        }

        private static bool inFlight;      // mode +0x284
        private static long lastSendMs;    // mode +0x278
        private static readonly System.Random Rng = new System.Random();
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetForMap();

        /// <summary>The packs the server reported owned (the client fills mgr +0x1a4 from a packet Custom never sends).</summary>
        public static readonly HashSet<int> OwnedPacks = new HashSet<int>();

        /// <summary>
        /// Whether the owned packs packet (handler 0xc46190) arrived: only then the client builds the mini window's word map
        /// (0x575880) and its Bought / Unbought lists. Custom's server has no such packet, so it stays false.
        /// </summary>
        public static bool OwnedListReceived { get; set; }

        /// <summary>The flag of mode +0x284 (the diagnostics' view).</summary>
        public static bool InFlight => inFlight;

        /// <summary>A new game mode (map entry): nothing in flight, no last time.</summary>
        public static void ResetForMap() {
            inFlight = false;
            lastSendMs = 0;
        }

        /// <summary>CGameMode msg 0x21 (0xc151c4): the emotion in flight is over.</summary>
        public static void EmotionDone() => inFlight = false;

        /// <summary>The 0x56e6f0 roll: ET_DICE1 + rand % 6, ET_YUT1 + rand % 7 (a negative remainder counts as 0).</summary>
        public static int Roll(int emotion, int dice1, int yut1, int random) {
            if (emotion == dice1) return emotion + Math.Max(0, random % 6);
            if (emotion == yut1) return emotion + Math.Max(0, random % 7);
            return emotion;
        }

        /// <summary>CGameMode msg 0x1f's checks in their order (pure: the caller sends and keeps the time on Sent).</summary>
        public static Outcome Check(bool flagSet, long nowMs, long lastMs, bool summoner, int basicSkillLevel, int summonerBasicSkillLevel,
            bool basicOrOwnedPack, bool recordExists) {
            if (flagSet) return Outcome.InFlight;
            if (nowMs <= lastMs + IntervalMs) return Outcome.TooSoon;
            if (summoner ? summonerBasicSkillLevel <= 0 : basicSkillLevel <= 1) return Outcome.SkillLevel;
            if (!basicOrOwnedPack) return Outcome.Unpurchased;
            return recordExists ? Outcome.Sent : Outcome.NoRecord;
        }

        /// <summary>FUN_00d2b820: the Summoner jobs (Doram) 4217..4221, 4308, 4315.</summary>
        public static bool IsSummonerJob(int job) => (job >= 0x1079 && job <= 0x107d) || job == 0x10d4 || job == 0x10db;

        /// <summary>CCashEmotionAction_Play::vf01 then mode msg 0x1f for (pack, emotion): the roll, the checks, the packet.</summary>
        public static Outcome Play(RoLoginShellSession session, int pack, int emotion) {
            if (session == null || pack < 0 || emotion < 0) return Outcome.NoRecord;
            RoCashEmotionData data = RoCashEmotionData.Shared;
            int dice = data != null ? data.EnumValue("ET_DICE1") : 58, yut = data != null ? data.EnumValue("ET_YUT1") : 81;
            emotion = Roll(emotion, dice, yut, Rng.Next(0, 32768));

            int basicLevel = -1, summonerLevel = -1;
            foreach (RoGameplaySkillInfo skill in session.SkillSnapshot().Skills ?? Array.Empty<RoGameplaySkillInfo>()) {
                if (skill.SkillId == BasicSkillId) basicLevel = skill.Level;
                if (skill.SkillId == SummonerBasicSkillId) summonerLevel = skill.Level;
            }

            RoPlayerStatusSnapshot status = session.BasicInfoSnapshot().Status;
            bool summoner = status != null && IsSummonerJob(status.JobId);
            int basicPack = data != null ? data.BasicPack : 0;
            bool recordExists = data == null ? pack == 0 : data.TryGet(pack, emotion, out _);
            long now = Clock.ElapsedMilliseconds + 100000;   // timeGetTime: never within 2.5 s of the start value 0
            Outcome outcome = Check(inFlight, now, lastSendMs, summoner, basicLevel, summonerLevel, pack == basicPack || OwnedPacks.Contains(pack),
                recordExists);
            switch (outcome) {
                case Outcome.SkillLevel:
                    RedLine(data, "EMSG_EMOTION_USE_FAIL_SKILL_LEVEL");
                    break;
                case Outcome.Unpurchased:
                    RedLine(data, "EMSG_EMOTION_EXPANTION_USE_FAIL_UNPURCHASED");
                    break;
                case Outcome.Sent:
                    lastSendMs = now;
                    inFlight = true;
                    _ = session.BeginEmotion((ushort) pack, (ushort) emotion);
                    break;
            }

            return outcome;
        }

        /// <summary>The chat's emotion talk types (mode msg 0x149, 0x56e6a0): the basic pack's emotion through <see cref="Play"/>.</summary>
        public static Outcome PlayBasic(RoLoginShellSession session, int emotion) {
            RoCashEmotionData data = RoCashEmotionData.Shared;
            return Play(session, data != null ? data.BasicPack : 0, emotion);
        }

        private static void RedLine(RoCashEmotionData data, string key) {
            string text = data != null ? data.Message(key, key) : key;
            RoClassicChat.Instance?.AddLine(text, "#ff0000", RoChatChannel.System);
        }
    }
}
