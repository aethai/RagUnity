namespace Ragnarok.World {
    /// <summary>
    /// The client's PvP rank and battleground score state, read by the classic UI overlays (rank / total in RankFont at
    /// the bottom right 0xbf4f80, the rank 1 timer 0xbf5210, the team score 0x538780). Written by
    /// <see cref="RoOnlineWorldController"/> from ZC_NOTIFY_MAPPROPERTY 0x199 / 0x1d6, ZC_MAPPROPERTY_R2 0x99b,
    /// ZC_NOTIFY_RANKING 0x19a (0xc58fb0) and ZC_BATTLEFIELD_NOTIFY_POINT 0x2de. Main thread only.
    /// </summary>
    public static class RoPvpState {
        /// <summary>CWorld +0x48: set by 0x199 type 1 or 5 and 0x1d6 type 5, 6 or 0x13; only a new map clears it.</summary>
        public static bool IsPk { get; private set; }

        /// <summary>[0x1552d88] COUNT_PK: bit 5 of 0x99b's flags, recomputed on every 0x99b.</summary>
        public static bool CountPk { get; private set; }

        /// <summary>The own player's rank (+0x48c); 0 until the first 0x19a, −1 when the server hides it.</summary>
        public static int Rank { get; private set; }

        /// <summary>The number of players ranked (+0x490).</summary>
        public static int Total { get; private set; }

        /// <summary>[0x154e448]: when the own rank became 1 (milliseconds, <see cref="Ragnarok.Network.RoMonotonicTime"/>); 0 when not rank 1.</summary>
        public static long Rank1SinceMs { get; private set; }

        /// <summary>[0x154ebc8] / [0x154ebcc]: the battleground team scores, −1 until 0x2de (and on every new map).</summary>
        public static int Lion { get; private set; } = -1;
        public static int Eagle { get; private set; } = -1;

        /// <summary>The PvP gate of the rank overlays (0xc020a0): the map is PK or counts PK.</summary>
        public static bool IsPvpMap => IsPk || CountPk;

        /// <summary>
        /// The siege flag (world + 0x4c, 0xd1e470): 0x99b's siege bit (0x04), the agit property (type 3) of 0x199, or the
        /// agit map types 8 / 0x12 of 0x1d6 (0xc33f39). It hides the castle teleport and shows the emblems over heads.
        /// </summary>
        public static bool IsSiege { get; private set; }

        /// <summary>
        /// The map forces /mineffect on: every map property packet first puts the effective flag back to the player's
        /// choice ([0x01552d7c] = [0x01552d80]), then 0x199 type 3 (0xc32f79) and 0x1d6's agit types (0xc33f78) force it,
        /// and so does the USE_SIMPLE_EFFECT bit (0x08) of the last 0x99b (property 0xf, 0xd1e000). /mineffect itself
        /// overrules it until the next packet (0xc0aead writes both flags).
        /// </summary>
        public static bool ForcesMinEffect { get; private set; }

        /// <summary>
        /// The battlefield flag (world + 0x50, 0xd1de40): 0x1d6 type 0x13 (0xc33f4f) or 0x99b's battlefield bit (0x80,
        /// property 0x13, 0xc3c140); only a new map clears it. UIEmblemWnd (0x7be420) draws nothing while it is set.
        /// </summary>
        public static bool IsBattlefield { get; private set; }

        /// <summary>
        /// CWorld +0x54: 0x199 type 2 (the event PvP zone, 0xc55e75); only a new map clears it. Like the PK flag it lets a
        /// plain click attack players outside the own party (0xc05710).
        /// </summary>
        public static bool IsEventPvp { get; private set; }

        /// <summary>0x99b bit 0 (map property 7, 0xd1dfa0): the attack cursor on every player outside the own party.</summary>
        public static bool PartyProperty { get; private set; }

        /// <summary>0x99b bit 1 (map property 8, 0xd1df90): the attack cursor on every player outside the own guild.</summary>
        public static bool GuildProperty { get; private set; }

        /// <summary>[0x1552d84] 0x99b bit 4 (map property 0x10, 0xd1dfd0), recomputed on every 0x99b: like the PK flag.</summary>
        public static bool NoLockOn { get; private set; }

        // the 0x99b flags of the map (0xbccda0 keeps them per property type; one map's are enough here)
        private static uint mapPropertyFlags;

        /// <summary>0xbf4f80: rank / total at the bottom right, while both are positive.</summary>
        public static bool ShowRankOverlay => IsPvpMap && Total > 0 && Rank > 0;

        /// <summary>0xbf5210: the time held at rank 1 at the top centre, with two or more players.</summary>
        public static bool ShowRank1Timer => IsPvpMap && Total >= 2 && Rank == 1;

        /// <summary>0x538780: the lion / eagle emblems and scores, once both scores are set.</summary>
        public static bool ShowScoreOverlay => Lion >= 0 && Eagle >= 0;

        /// <summary>A new CWorld (map change, 0x9f6f40): the PK flag and the scores start over.</summary>
        public static void BeginMap() {
            IsPk = false;
            IsEventPvp = false;
            IsSiege = false;
            IsBattlefield = false;
            Lion = -1;
            Eagle = -1;
        }

        /// <summary>0x199 (inline 0xc32f00), 0x1d6 (inline 0xc33ec0) and 0x99b (0xc3c140).</summary>
        public static void ApplyMapProperty(ushort packetId, short type, uint flags) {
            bool forced = false;
            switch (packetId) {
                case 0x0199:
                    if (type == 1 || type == 5) IsPk = true;
                    if (type == 2) IsEventPvp = true;
                    if (type == 3) {
                        IsSiege = true;
                        forced = true;
                    }

                    break;
                case 0x01d6:
                    // 0xc33efd: 5 / 6 PK; 0xb / 0xc nothing; the siege property, 8 or 0x12 siege (+ /mineffect); 0x13 PK
                    if (type == 5 || type == 6 || type == 0x13) IsPk = true;
                    if (type == 8 || type == 0x12 || (type != 0xb && type != 0xc && type != 5 && type != 6 && (mapPropertyFlags & 0x04) != 0)) {
                        IsSiege = true;
                        forced = true;
                    } else if (type == 0x13) {
                        IsBattlefield = true;
                    }

                    break;
                case 0x099b:
                    mapPropertyFlags = flags;
                    CountPk = (flags & 0x20) != 0;
                    PartyProperty = (flags & 0x01) != 0;
                    GuildProperty = (flags & 0x02) != 0;
                    NoLockOn = (flags & 0x10) != 0;
                    IsSiege = (flags & 0x04) != 0;
                    if ((flags & 0x80) != 0) IsBattlefield = true;
                    break;
            }

            ForcesMinEffect = forced || (mapPropertyFlags & 0x08) != 0;
        }

        /// <summary>/mineffect typed (0xc0aead): the player's choice holds until the next map property packet.</summary>
        public static void ClearForcedMinEffect() => ForcesMinEffect = false;

        /// <summary>
        /// The own part of 0x19a: the rank 1 clock (0xc5902e) and the new values. Returns the previous rank, which decides
        /// the chat line and the sound.
        /// </summary>
        public static int ApplyOwnRank(int rank, int total, long nowMs) {
            int old = Rank;
            if (rank == 1) {
                if (old != 1) Rank1SinceMs = nowMs;
            } else {
                Rank1SinceMs = 0;
            }

            Rank = rank;
            Total = total;
            return old;
        }

        /// <summary>0x2de.</summary>
        public static void ApplyBattlefieldScore(short lion, short eagle) {
            Lion = lion;
            Eagle = eagle;
        }

        /// <summary>Back to a fresh client (tests, logout).</summary>
        public static void Reset() {
            BeginMap();
            ForcesMinEffect = false;
            mapPropertyFlags = 0;
            CountPk = false;
            PartyProperty = false;
            GuildProperty = false;
            NoLockOn = false;
            Rank = 0;
            Total = 0;
            Rank1SinceMs = 0;
        }
    }
}
