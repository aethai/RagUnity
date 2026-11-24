using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The classic notifications (codex-ui-research RUNTIME-RULES-QUESTS-EMOTIONS-NOTIFICATIONS.md "emotions-notifications"):
    /// UINotifyLevelUpWnd 0x15 (43 × 43 at (W − 43, H − 43), opens Status), UINotifyJobLevelUpWnd 0x31 (at (0, H − 43), opens
    /// the skill list), UINotifyQuestWnd 0x8f (at (W − 43, H − 43), opens the quest list) and UINotifyItemObtainWnd 0x3a (a
    /// sysbox at ((W − 640) / 2 + 220, 75), the item's icon and " - %d obtained.", gone after 5 s). Level buttons come up
    /// when ZC_PAR_CHANGE raises the base / job level, the quest button with a new quest; ticked by the chat window.
    /// </summary>
    public sealed class RoClassicNotify : IRoClassicUiProducer {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string Ui = "유저인터페이스\\";
        private const int ButtonId = 0xbc;
        private const int Size = 43;
        private const int ObtainH = 32;
        private const int ObtainMs = 5000;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 Shadow = new Color32(0xe0, 0xe0, 0xe0, 255);

        private static RoClassicNotify instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoClassicWindow levelWin;
        private RoClassicWindow jobWin;
        private RoClassicWindow questWin;
        private RoClassicWindow obtainWin;
        private int seenBasicSequence = -1;
        private int seenQuestSequence = -1;
        private int baseLevel = -1;
        private int jobLevel = -1;
        private int seenQuestAdded = -1;

        // 0x3a
        private RoItemInstance obtainItem;
        private RoItemMetadataTable obtainMetadata = RoItemMetadataTable.Empty;
        private int obtainAmount;
        private float obtainStart;

        private static RoClassicNotify Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicNotify { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>Per frame from the chat window while the classic UI is on.</summary>
        public static void Tick(RoLoginShellSession session, RoClassicChat chat) {
            if (session == null) {
                instance?.HideAll();
                return;
            }

            RoClassicNotify n = Ensure();
            if (n == null) return;
            if (!ReferenceEquals(n.session, session)) {
                n.HideAll();
                n.session = session;
                n.seenBasicSequence = -1;
                n.seenQuestSequence = -1;
                n.baseLevel = -1;
                n.jobLevel = -1;
                n.seenQuestAdded = -1;
            }

            n.chat = chat;
            int basic = session.BasicInfoChangeSequence;
            if (basic != n.seenBasicSequence) {
                n.seenBasicSequence = basic;
                n.ObserveLevels(session.InventorySnapshot().Status);
            }

            int quests = session.QuestWindowChangeSequence;
            if (quests != n.seenQuestSequence) {
                n.seenQuestSequence = quests;
                n.ObserveQuests(session.QuestSnapshot());
            }
        }

        private void HideAll() {
            if (levelWin != null) levelWin.Visible = false;
            if (jobWin != null) jobWin.Visible = false;
            if (questWin != null) questWin.Visible = false;
            if (obtainWin != null) obtainWin.Visible = false;
        }

        /// <summary>
        /// 0xc5bfd0 (ZC_PAR_CHANGE): varID 0x0b makes 0x15, 0x37 makes 0x31. The client raises them on every such packet
        /// outside its loading gate; here a raise of the known level stands for it (the first values are the login's).
        /// </summary>
        private void ObserveLevels(RoPlayerStatusSnapshot status) {
            if (status == null || status.BaseLevel <= 0) return;
            if (baseLevel > 0 && status.BaseLevel > baseLevel) ShowButton(ref levelWin, "UINotifyLevelUpWnd", false);
            if (jobLevel > 0 && status.JobLevel > jobLevel) ShowButton(ref jobWin, "UINotifyJobLevelUpWnd", true);
            baseLevel = status.BaseLevel;
            jobLevel = status.JobLevel;
        }

        /// <summary>0xc43080 (ZC_ADD_QUEST_EX): a quest not held before makes 0x8f and posts MsgStr 0x55e (the login list does not).</summary>
        private void ObserveQuests(RoGameplayQuestSnapshot snapshot) {
            int added = snapshot?.AddedSerial ?? 0;
            bool fresh = seenQuestAdded >= 0 && added > seenQuestAdded;
            seenQuestAdded = added;
            if (!fresh) return;
            ShowButton(ref questWin, "UINotifyQuestWnd", false);
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            // 0xc43301: type 19 "Quest information", COLORREF 0x0000FFFF
            target?.AddTypedLine(RoClassicUiHost.MsgStr(0x55e, "You have received a new quest."), RoChatLineTypes.Yellow, RoChatLineTypes.Quest);
        }

        private void ShowButton(ref RoClassicWindow window, string layoutName, bool left) {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
                RoClassicWindow created = new RoClassicWindow(system, layout) { Visible = false, Draggable = false };
                created.Clicked += child => {
                    if (child.Id != ButtonId) return;
                    created.Visible = false;
                    OpenTarget(created);
                };
                window = created;
            }

            Vector2Int client = system.ClientSize;
            window.MoveTo(left ? 0 : client.x - Size, client.y - Size);
            window.Visible = true;
            system.Windows.BringToFront(window);
        }

        /// <summary>vf37 0x860560 / 0x860520 / 0x8605c0: Status 0x0b, the skill list 0x25, the quest list (CUI 0x2718).</summary>
        private void OpenTarget(RoClassicWindow window) {
            if (!RoClassicBasicInfo.IsBound) return;
            if (window == levelWin) {
                if (!RoClassicStatus.IsOpen) RoClassicStatus.Open(session);
            } else if (window == jobWin) {
                if (!RoClassicSkillTree.IsOpen) RoClassicSkillTree.Open(session);
            } else if (window == questWin) {
                if (!RoClassicQuest.IsOpen) RoClassicQuest.Toggle();
            }
        }

        // ================================================================ 0x3a: an item obtained

        /// <summary>The pickup notice (0x0a37 → 0xc51bf0: msg 0x4c, 0x24, 0x22); false while the classic UI is off.</summary>
        public static bool ShowItemObtained(RoItemInstance item, int amount, RoItemMetadataTable metadata) {
            if (item == null) return false;
            RoClassicNotify n = Ensure();
            if (n == null) return false;
            n.ShowObtain(item, amount, metadata ?? RoItemMetadataTable.Empty);
            return true;
        }

        /// <summary>FUN_00809d50: the sysbox for a width (the middles every 14 px from 14), white between the rows.</summary>
        public static void BuildObtainFrame(List<RoClassicOp> ops, int w) {
            ops.Clear();
            int n = (w - 28 + 13) / 14;
            for (int i = 0; i < n; i++) RoClassicFrames.Bitmap(ops, Ui + "sysbox_mu.bmp", 14 + 14 * i, 0);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_lu.bmp", 0, 0);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_ru.bmp", w - 14, 0);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_lm.bmp", 0, 14);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_rm.bmp", w - 14, 14);
            for (int i = 0; i < n; i++) RoClassicFrames.Bitmap(ops, Ui + "sysbox_md.bmp", 14 + 14 * i, 18);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_ld.bmp", 0, 18);
            RoClassicFrames.Bitmap(ops, Ui + "sysbox_rd.bmp", w - 14, 18);
            RoClassicFrames.Fill(ops, 14, 14, w - 28, 4, new Color32(255, 255, 255, 255));
        }

        /// <summary>vf37 0x860290 msg 0x24: the width is the measured "You got %s (%d)." + 54, plus 28 unless that is a multiple of 28.</summary>
        public static int ObtainWidth(int measured) {
            int w = measured + 54;
            if (w % 28 != 0) w += 28;
            return Math.Max(34, w);
        }

        private void ShowObtain(RoItemInstance item, int amount, RoItemMetadataTable metadata) {
            obtainItem = item;
            obtainAmount = amount;
            obtainMetadata = metadata;
            if (obtainWin == null) {
                var layout = new RoClassicWindowLayout { Class = "UINotifyItemObtainWnd", WindowId = 0x3a, Width = 180, Height = ObtainH };
                obtainWin = new RoClassicWindow(system, layout) { Visible = false, Draggable = false, ClickableAt = _ => false };
                obtainWin.DrawDynamic += DrawObtain;
            }

            string name = item.GetFullDisplayName(metadata);
            string message = RoClassicUiHost.MsgStr(0x99, "You got %s (%d).");
            message = Replace(Replace(message, "%s", name), "%d", amount.ToString(CultureInfo.InvariantCulture));
            int w = ObtainWidth(obtainWin.FontFor(0, 12).Measure(message));
            obtainWin.Layout.Width = w;
            BuildObtainFrame(obtainWin.Layout.Ops, w);
            Vector2Int client = system.ClientSize;
            obtainWin.MoveTo((client.x - 640) / 2 + 220, 75);
            obtainStart = Time.unscaledTime;
            obtainWin.Visible = true;
            system.Windows.BringToFront(obtainWin);
            obtainWin.Invalidate();
        }

        private static string Replace(string text, string token, string value) {
            int at = text.IndexOf(token, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + value + text.Substring(at + token.Length);
        }

        /// <summary>OnDraw 0x84f310: the icon at (13, 5), the name from (41, 10) and " - %d obtained." (MsgStr 0x2b8) after it, shadowed.</summary>
        private void DrawObtain(RoClassicWindow w) {
            if (obtainItem == null) return;
            RoClassicItemText.DrawIcon(w, 13, 5, obtainItem, obtainMetadata, 0);
            string name = obtainItem.GetFullDisplayName(obtainMetadata);
            string suffix = Replace(RoClassicUiHost.MsgStr(0x2b8, " - %d obtained."), "%d", obtainAmount.ToString(CultureInfo.InvariantCulture));
            RoClassicGdiFont font = w.FontFor(0, 12);
            int x = 41;
            w.TextOut(x + 1, 11, name, 0, 12, Shadow);
            w.TextOut(x, 10, name, 0, 12, Black);
            x += font.Measure(name);
            w.TextOut(x + 1, 11, suffix, 0, 12, Shadow);
            w.TextOut(x, 10, suffix, 0, 12, Black);
        }

        /// <summary>vf19 0x855730: 0x3a goes once 5000 ms have passed since its last message.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (obtainWin != null && obtainWin.Visible && (Time.unscaledTime - obtainStart) * 1000f > ObtainMs) obtainWin.Visible = false;
        }

        /// <summary>The sweep console: level | job | quest | item id n | state.</summary>
        public static string Sweep(string command, RoItemMetadataTable metadata) {
            RoClassicNotify n = Ensure();
            if (n == null) return "no classic ui";
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            switch (a[0]) {
                case "level":
                    n.ShowButton(ref n.levelWin, "UINotifyLevelUpWnd", false);
                    break;
                case "job":
                    n.ShowButton(ref n.jobWin, "UINotifyJobLevelUpWnd", true);
                    break;
                case "quest":
                    n.ShowButton(ref n.questWin, "UINotifyQuestWnd", false);
                    break;
                case "item":
                    n.ShowObtain(RoItemInstance.CreateRegular(0, Arg(1), Math.Max(1, Arg(2))), Math.Max(1, Arg(2)), metadata ?? RoItemMetadataTable.Empty);
                    break;
                case "click":
                    RoClassicWindow target = a.Length > 1 && a[1] == "job" ? n.jobWin : a.Length > 1 && a[1] == "quest" ? n.questWin : n.levelWin;
                    if (target != null && target.Visible) {
                        target.Visible = false;
                        n.OpenTarget(target);
                    }

                    break;
            }

            return "level=" + (n.levelWin != null && n.levelWin.Visible) + " job=" + (n.jobWin != null && n.jobWin.Visible) + " quest=" + (n.questWin != null && n.questWin.Visible) +
                   " obtain=" + (n.obtainWin != null && n.obtainWin.Visible ? n.obtainWin.Width + "px" : "-") + " levels=" + n.baseLevel + "/" + n.jobLevel + " quests added=" + n.seenQuestAdded;
        }
    }
}
