namespace Ragnarok.Data {
    /// <summary>
    /// Which sprite folder the client draws a non-player actor (CNpc) from (RE: reference packages/monster-sprite/NOTES.md).
    /// <para>CNpc::vf21 0xcc2230 builds <c>{folder}\{name}.spr</c> / <c>.act</c> from the job id alone: <c>npc\</c> when
    /// FUN_00d01810 (45..999, 10001..19999), <c>인간족\몸통\</c> for the mercenaries 6017..6046, <c>homun\</c> for 6001..6052,
    /// <c>몬스터\</c> for every other id. The name is the job name vector (FUN_00ceac40: Lua ReqJobName = JobNameTable[id]) used
    /// as is, so a mercenary's <c>여\활용병</c> keeps its sex folder.</para>
    /// <para>There is no existence check and no fallback: CRenderObject::SetSprName / SetActName (0xbe6c80 / 0xbe3a70) load
    /// exactly that path, and a missing file fails in CSprRes::Load 0x6d9880 / CActRes::Load 0x6bdf90 ("Cannot find File").
    /// The "_mj" monsters (e.g. 20373 nightmare_terror_mj) ship both files; ids of 6000 and above are monsters, not homunculi.</para>
    /// </summary>
    public static class RoEntitySpriteRules {
        public const string MonsterFolder = "몬스터";                        // 몬스터
        public const string MercenaryFolder = "인간족/몸통";       // 인간족/몸통
        public const string NpcFolder = "npc";
        public const string HomunculusFolder = "homun";

        /// <summary>FUN_00d01810: the ids the client draws from <c>npc\</c>.</summary>
        public static bool IsNpcJob(int job) {
            return (job >= 45 && job <= 999) || (job >= 10001 && job <= 19999);
        }

        /// <summary>0xcc2230: 6017..6046 (job - 0x1781 &lt; 0x1e), drawn from <c>인간족\몸통\</c>.</summary>
        public static bool IsMercenaryJob(int job) {
            return job >= 6017 && job <= 6046;
        }

        /// <summary>0xcc2230: 6001..6052 (job - 0x1771 &lt; 0x34) once the mercenaries are taken out, drawn from <c>homun\</c>.</summary>
        public static bool IsHomunculusJob(int job) {
            return job >= 6001 && job <= 6052 && !IsMercenaryJob(job);
        }

        /// <summary>FUN_00ceac40: the player jobs (0..30, 4001..6000), named from the PC tables and drawn as CPc bodies.</summary>
        public static bool IsPlayerJob(int job) {
            return (job >= 0 && job <= 30) || (job >= 4001 && job <= 6000);
        }

        public static RoEntityDisplayKind Classify(int job) {
            if (job < 0 || IsPlayerJob(job)) {
                return RoEntityDisplayKind.Unknown;
            }

            if (IsNpcJob(job)) {
                return RoEntityDisplayKind.Npc;
            }

            if (IsMercenaryJob(job)) {
                return RoEntityDisplayKind.Mercenary;
            }

            return IsHomunculusJob(job) ? RoEntityDisplayKind.Homunculus : RoEntityDisplayKind.Monster;
        }

        public static string GetFolder(RoEntityDisplayKind kind) {
            switch (kind) {
                case RoEntityDisplayKind.Npc:
                    return NpcFolder;
                case RoEntityDisplayKind.Monster:
                    return MonsterFolder;
                case RoEntityDisplayKind.Homunculus:
                    return HomunculusFolder;
                case RoEntityDisplayKind.Mercenary:
                    return MercenaryFolder;
                default:
                    return null;
            }
        }
    }
}
