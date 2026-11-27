using Ragnarok.Camera;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The slash-command flags that systems outside the chat read (docs/audits/chat-options-2026-10-04.md): read from their
    /// store once and again on every change (never per frame), and pushed into the assemblies that cannot see
    /// <see cref="RoChatSlashCommands"/> — the camera (/camera, /q1, /q2, /quake), the classic window manager (/window) and
    /// the name balloon (/showname). The rest is read here by the game's own code.
    /// </summary>
    public static class RoClassicCommandFlags {
        private static bool loaded;
        private static bool frameSkip = true;
        private static bool loginOut = true;
        private static bool shopping = true;
        private static bool tingOnly;
        private static bool monsterHp = true;
        private static bool minEffect;
        private static bool emblemFrame;
        private static bool skillRange;
        private static bool showName = true;
        private static bool multiHitSwing = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => loaded = false;

        /// <summary>/skip ([0x011842f8], 0xba4560): on, a frame runs every logic tick that elapsed; off, exactly one.</summary>
        public static bool FrameSkip {
            get {
                EnsureLoaded();
                return frameSkip;
            }
        }

        /// <summary>/loginout ([0x01552d50]): the friend (0xd06570) and guild member (mode msg 0x8c) log in / out lines.</summary>
        public static bool LoginOut {
            get {
                EnsureLoaded();
                return loginOut;
            }
        }

        /// <summary>/shopping ([0x01552d54]): a shop board opens on one left press and a right press closes the shop windows.</summary>
        public static bool Shopping {
            get {
                EnsureLoaded();
                return shopping;
            }
        }

        /// <summary>/tingonly (sound manager +0x14): the chat room entry sound (UIChatRoomWnd msg 0x2c), with /sound on.</summary>
        public static bool TingOnly {
            get {
                EnsureLoaded();
                return tingOnly;
            }
        }

        /// <summary>/monsterhp ([0x01553540], 0xbd43d0): the monster HP gauge is shown at all.</summary>
        public static bool MonsterHp {
            get {
                EnsureLoaded();
                return monsterHp;
            }
        }

        /// <summary>
        /// The effective /mineffect ([0x01552d7c]): the player's choice ([0x01552d80]) or a map that forces it
        /// (<see cref="RoPvpState.ForcesMinEffect"/>). The classic effect engine reads it (FxRuntime.MinEffect).
        /// </summary>
        public static bool MinEffect {
            get {
                EnsureLoaded();
                return minEffect || RoPvpState.ForcesMinEffect;
            }
        }

        /// <summary>/frame = Game Settings' "Emblem Border" ([0x015556bc]): the frame of the emblems on siege maps.</summary>
        public static bool EmblemFrame {
            get {
                EnsureLoaded();
                return emblemFrame;
            }
        }

        /// <summary>
        /// /showname ([0x01552da8] → [0x014ef125] / [0x014ef126]): on ("type 1"), a player's balloon is the guild position
        /// over the name in the bold font; off ("type 2"), "name (party)" and "guild [position]" in the regular font.
        /// </summary>
        public static bool ShowName {
            get {
                EnsureLoaded();
                return showName;
            }
        }

        /// <summary>Custom's /range ([0x0233a65c]): the skill range cells while a skill waits for its target.</summary>
        public static bool SkillRange {
            get {
                EnsureLoaded();
                return skillRange;
            }
        }

        /// <summary>
        /// RagUnity's /swing (on by default): the caster of a multi-hit skill the older clients drew with the attack motion
        /// (Sonic Blow, the Monk combos ...) swings again on every hit of the series. Off, the 2024 client's behaviour: one
        /// skill action for the packet's attackMT (FUN_00d15360), then standing through the hits.
        /// </summary>
        public static bool MultiHitSwing {
            get {
                EnsureLoaded();
                return multiHitSwing;
            }
        }

        /// <summary>Reads the flags the first time anything asks (RoClassicGameUi asks every frame: a bool test).</summary>
        public static void EnsureLoaded() {
            if (!loaded) Reload();
        }

        /// <summary>Reads every flag again and hands the new values to the systems that keep their own copy.</summary>
        public static void Reload() {
            loaded = true;
            frameSkip = Get("/skip");
            loginOut = Get("/loginout");
            shopping = Get("/shopping");
            tingOnly = Get("/tingonly");
            monsterHp = Get("/monsterhp");
            minEffect = Get("/mineffect");
            emblemFrame = Get("/frame");
            skillRange = Get("/range");
            showName = Get("/showname");
            multiHitSwing = Get("/swing");

            RoCameraController.FixedFollow = Get("/camera");
            RoCameraController.QuickSpellRightClick = Get("/q1");
            RoCameraController.QuickSpellWheel = Get("/q2");
            if (RoCameraController.UseQuickSpellSlot == null) RoCameraController.UseQuickSpellSlot = RoClassicHotkeyBar.UseQuickSpellSlot;
            RoCameraQuake.Enabled = Get("/quake");
            RoClassicWindowManager.SnapToWindows = Get("/window");
            RoClassicNameBalloon.Bold = showName;
            RoClassicMinimap.ApplyButtonsOption(Get("/minimap"));
        }

        private static bool Get(string name) => RoChatSlashCommands.GetOption(name, null);
    }
}
