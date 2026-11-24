namespace Ragnarok.Network {
    /// <summary>
    /// The held skill use of the original client (docs/audits/chat-options-2026-10-04.md §6): GC_ROLLINGCUTTER is the only
    /// skill mode msg 0x45 sends as CZ_USE_SKILL_START 0x0b10 instead of CZ_USE_SKILL2 0x0438 (0xc1b51c); the start stays
    /// "held" until a key goes up, another key goes down, a mouse use of another slot or the window losing the focus sends
    /// CZ_USE_SKILL_END 0x0b11. A mouse use of a slot holding it (/q1, /q2, a shortcut double click) ends its start at once.
    /// The counters are the session map at 0x0154dd00+0x79ec (key 2: held starts, key 4: one-shot uses) and the two keys
    /// at session+0x5da0 / +0x5da4 ([0x01553aa0] / [0x01553aa4]). Thread-safe: the start is counted where it is sent.
    /// </summary>
    public static class RoHeldSkillUse {
        /// <summary>GC_ROLLINGCUTTER (2036), the skill id 0xc1b51c, 0xc12f90, 0x876640 and the slot tests compare.</summary>
        public const int RollingCutter = 0x07f4;

        /// <summary>CZ_USE_SKILL_START 0x0b10 {u16 skill, u16 level, u32 target}: 10 bytes (client table and server alike).</summary>
        public const ushort CzUseSkillStart = 0x0b10;

        /// <summary>CZ_USE_SKILL_END 0x0b11 {u16 skill}: 4 bytes.</summary>
        public const ushort CzUseSkillEnd = 0x0b11;

        public const int StartLength = 10;
        public const int EndLength = 4;

        private static readonly object Sync = new object();
        private static int heldStarts;   // map key 2
        private static int oneShots;     // map key 4
        private static int heldKey;      // [0x01553aa0]: the key whose press started the held use (0: none)
        private static int lastKey;      // [0x01553aa4]: the last key pressed

        /// <summary>The skills mode msg 0x45 sends as a held use.</summary>
        public static bool IsHeldSkill(int skillId) => skillId == RollingCutter;

        /// <summary>A start is out and no end yet (map key 2 &gt; 0).</summary>
        public static bool IsHeld {
            get {
                lock (Sync) {
                    return heldStarts > 0;
                }
            }
        }

        /// <summary>0xc1b58f: {0x0b10, skill, level, target}.</summary>
        public static byte[] BuildStart(int skillLevel, int skillId, uint targetId) {
            var packet = new byte[StartLength];
            packet[0] = (byte) (CzUseSkillStart & 0xff);
            packet[1] = (byte) (CzUseSkillStart >> 8);
            packet[2] = (byte) skillId;
            packet[3] = (byte) (skillId >> 8);
            packet[4] = (byte) skillLevel;
            packet[5] = (byte) (skillLevel >> 8);
            packet[6] = (byte) targetId;
            packet[7] = (byte) (targetId >> 8);
            packet[8] = (byte) (targetId >> 16);
            packet[9] = (byte) (targetId >> 24);
            return packet;
        }

        /// <summary>0xd284e0 / 0xd44890 / 0xc1b5da: {0x0b11, 0x07f4} (the value 0x07f40b11 written at once).</summary>
        public static byte[] BuildEnd() => new byte[] { (byte) (CzUseSkillEnd & 0xff), CzUseSkillEnd >> 8, (byte) (RollingCutter & 0xff), RollingCutter >> 8 };

        /// <summary>The packet is a CZ_USE_SKILL_START.</summary>
        public static bool IsStart(byte[] packet) =>
            packet != null && packet.Length >= StartLength && packet[0] == (byte) (CzUseSkillStart & 0xff) && packet[1] == CzUseSkillStart >> 8;

        /// <summary>
        /// After a CZ_USE_SKILL_START went out (0xc1b5ca): with a one-shot use pending (key 4) it ends at once — true, send
        /// CZ_USE_SKILL_END now — and key 4 is cleared; else the start is held (key 2 + 1) by the last key pressed.
        /// </summary>
        public static bool OnStartSent() {
            lock (Sync) {
                if (oneShots != 0) {
                    oneShots = 0;
                    return true;
                }

                heldKey = lastKey;
                heldStarts++;
                return false;
            }
        }

        /// <summary>
        /// 0x9d60a0 for a key pressed (not an auto-repeat; WM_KEYDOWN / WM_SYSKEYDOWN with bit 30 clear): a key other
        /// than the one that started the held use ends it first (true: send CZ_USE_SKILL_END); the key becomes the last
        /// one. Runs before the key's own use.
        /// </summary>
        public static bool OnKeyDown(int key) {
            lock (Sync) {
                bool end = heldKey != key && TakeHeldLocked(true);
                lastKey = key;
                return end;
            }
        }

        /// <summary>
        /// 0xd284e0: a held start ends (true: send CZ_USE_SKILL_END) and the key is forgotten. The client runs it on every
        /// WM_KEYUP (0xd45bcb), before a mouse use of a slot without the skill and when the hotkey window opens (0x881260).
        /// </summary>
        public static bool Release() {
            lock (Sync) {
                return TakeHeldLocked(true);
            }
        }

        /// <summary>0xd44890 (WM_ACTIVATE, the window losing the focus): as <see cref="Release"/>, the key kept.</summary>
        public static bool ReleaseOnFocusLost() {
            lock (Sync) {
                return TakeHeldLocked(false);
            }
        }

        /// <summary>
        /// A mouse use of a slot (/q1 0xc07540, /q2 0xc0b640, UIShortCutWnd 0x891720 / UIQuikSlotWnd 0x891440 /
        /// UINewSkillListWnd 0x913e70 double clicks), before the slot's use: the held skill in it counts a one-shot use
        /// (key 4 + 1), anything else ends a held start (true: send CZ_USE_SKILL_END).
        /// </summary>
        public static bool OnMouseUse(int slotSkillId) {
            lock (Sync) {
                if (IsHeldSkill(slotSkillId)) {
                    oneShots++;
                    return false;
                }

                return TakeHeldLocked(true);
            }
        }

        /// <summary>Back to a fresh client (tests).</summary>
        public static void Reset() {
            lock (Sync) {
                heldStarts = 0;
                oneShots = 0;
                heldKey = 0;
                lastKey = 0;
            }
        }

        private static bool TakeHeldLocked(bool forgetKey) {
            if (heldStarts == 0) return false;
            if (forgetKey) heldKey = 0;
            heldStarts = 0;
            return true;
        }
    }
}
