using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The small party windows (codex-ui-research RUNTIME-RULES-PARTY-REMAINDER.md "friends-mini"): UIMiniPartyWnd 174 × 52
    /// with its UIMiniPartyInfoWnd child, one per detached member, stacked at the left edge in slots of 52 + 3 px from
    /// 0.3 · screen height (009db4d0). OnDraw 0x6bd3f0: img_partymini_bg(.tga, _mini, _maxmini for the widths 174 / 101 /
    /// 64) at (0, 0) three times; the child 0x6bab10: the job icon (renewalparty\icon_jobs_&lt;job&gt;[_die]) at (7, 8)
    /// for the leader or (7, 6), grey offline, the crown at (9, 0), the HP gauge 0.38·W × 5 at (35, 23) in the full mode,
    /// "Lv.%d %s(%s)" fitted to 0.6·W (name only, 0.532·W, in the 101 mode) bold at (35, 9), white or #AAAAAA offline.
    /// Left drag moves between slots (swapping an occupied one); right-click opens the member menu (0x6bbab0). They
    /// follow the party list whether the Party / Friend window is open or not.
    /// </summary>
    public sealed partial class RoClassicPartyFriends {
        private const int MiniW = 174, MiniH = 52, MiniPitch = MiniH + 3;
        private static readonly int[] MiniWidths = { 174, 101, 64 };
        private static readonly string[] MiniBackgrounds = { "img_partymini_bg.tga", "img_partymini_bg_mini.tga", "img_partymini_bg_maxmini.tga" };
        private static readonly Color32 MiniWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 MiniOfflineText = new Color32(0xaa, 0xaa, 0xaa, 255);

        private sealed class ClassicMini {
            public RoClassicWindow Window;
            public RoPartyMemberRuntimeSnapshot Member;
            public int Slot;
        }

        private readonly Dictionary<uint, ClassicMini> classicMinis = new Dictionary<uint, ClassicMini>();
        private readonly HashSet<uint> miniSeenScratch = new HashSet<uint>();
        private readonly List<uint> miniGoneScratch = new List<uint>(4);
        private int miniMode;
        private ClassicMini miniDragging;

        private int MiniTop() => (int) (system.ClientSize.y * 0.3f);

        /// <summary>006b49d0: a member not yet detached gets the first free slot.</summary>
        private void ClassicMiniAttach(RoPartyMemberRuntimeSnapshot member) {
            if (classicMinis.ContainsKey(member.AccountId)) return;
            var used = new HashSet<int>();
            foreach (ClassicMini m in classicMinis.Values) used.Add(m.Slot);
            int slot = 0;
            while (used.Contains(slot)) slot++;
            var layout = new RoClassicWindowLayout { Class = "UIMiniPartyWnd", Width = MiniWidths[miniMode], Height = MiniH };
            var mini = new ClassicMini { Member = member, Slot = slot };
            mini.Window = new RoClassicWindow(system, layout) { Draggable = false, ClickableAt = _ => true };
            mini.Window.DrawDynamic += w => DrawClassicMini(w, mini);
            mini.Window.Pressed += (w, local) => {
                if (!Locked) miniDragging = mini;
            };
            mini.Window.Released += (w, local) => ClassicMiniDrop(mini, w.Position.y + local.y);
            mini.Window.RightClicked += (w, local) => OpenClassicMiniMenu(mini);
            classicMinis[member.AccountId] = mini;
            PlaceClassicMini(mini);
            mini.Window.Visible = true;
        }

        private void PlaceClassicMini(ClassicMini mini) {
            mini.Window.MoveTo(0, MiniTop() + mini.Slot * MiniPitch);
            mini.Window.Invalidate();
        }

        /// <summary>006bd620: the slot under the release point; an occupied one swaps, a free one moves.</summary>
        private void ClassicMiniDrop(ClassicMini mini, int screenY) {
            if (miniDragging != mini) return;
            miniDragging = null;
            int slot = (screenY - MiniTop()) / MiniPitch;
            if (screenY < MiniTop() || slot < 0) return;
            foreach (ClassicMini other in classicMinis.Values) {
                if (other != mini && other.Slot == slot) {
                    other.Slot = mini.Slot;
                    PlaceClassicMini(other);
                    break;
                }
            }

            mini.Slot = slot;
            PlaceClassicMini(mini);
        }

        /// <summary>
        /// The Sort button (0x168): the small windows restacked from the top slot in the party list's order (the legacy
        /// window's sort lined its UGUI mini windows up the same way; the original's handler is not traced).
        /// </summary>
        private void SortClassicMinis() {
            int slot = 0;
            foreach (RoPartyMemberRuntimeSnapshot member in msParty.Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (!classicMinis.TryGetValue(member.AccountId, out ClassicMini mini)) continue;
                mini.Slot = slot++;
                PlaceClassicMini(mini);
            }
        }

        /// <summary>Party list rebuilds (009c6970) and HP relays (009db8c0): the detached members' data.</summary>
        private void RefreshClassicMinis(RoPartyRuntimeSnapshot party) {
            if (classicMinis.Count == 0 && model.DetachedMembers.Count == 0) return;
            miniSeenScratch.Clear();
            foreach (RoPartyMemberRuntimeSnapshot member in party.Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (!model.IsDetached(member.AccountId)) continue;
                miniSeenScratch.Add(member.AccountId);
                if (!classicMinis.TryGetValue(member.AccountId, out ClassicMini mini)) {
                    ClassicMiniAttach(member);
                    continue;
                }

                mini.Member = member;
                mini.Window.Invalidate();
            }

            // a member who left (or the party gone) takes the small window and the detached mark with it
            miniGoneScratch.Clear();
            foreach (uint id in classicMinis.Keys) {
                if (!miniSeenScratch.Contains(id)) miniGoneScratch.Add(id);
            }

            foreach (uint id in model.DetachedMembers) {
                if (!miniSeenScratch.Contains(id) && !miniGoneScratch.Contains(id)) miniGoneScratch.Add(id);
            }

            foreach (uint id in miniGoneScratch) {
                ClassicMiniRemove(id);
                model.RemoveDetached(id);
            }

            miniGoneScratch.Clear();
            miniSeenScratch.Clear();
        }

        /// <summary>009bca80: the member's small window goes.</summary>
        private void ClassicMiniRemove(uint accountId) {
            if (!classicMinis.TryGetValue(accountId, out ClassicMini mini)) return;
            mini.Window.Destroy();
            classicMinis.Remove(accountId);
            if (miniDragging == mini) miniDragging = null;
        }

        private void DestroyClassicMinis() {
            foreach (ClassicMini mini in classicMinis.Values) mini.Window.Destroy();
            classicMinis.Clear();
            miniDragging = null;
        }

        /// <summary>Msg 0xe / 7: the width picks the mode for every small window (> 101 full, > 64 name only, else icon only).</summary>
        private void SetClassicMiniMode(int mode) {
            miniMode = Mathf.Clamp(mode, 0, 2);
            foreach (ClassicMini mini in classicMinis.Values) {
                mini.Window.SetWidth(MiniWidths[miniMode]);
                mini.Window.Invalidate();
            }
        }

        private void DrawClassicMini(RoClassicWindow w, ClassicMini mini) {
            for (int i = 0; i < 3; i++) w.DrawBitmap(0, 0, MsParty + MiniBackgrounds[miniMode]);
            RoPartyMemberRuntimeSnapshot m = mini.Member;
            bool online = RoPartyFriendsModel.IsOnlinePartyMember(m), leader = RoPartyFriendsModel.IsPartyLeaderMember(msParty, m);
            string job = MsParty + "icon_jobs_" + m.ClassId.ToString(CultureInfo.InvariantCulture) + (m.IsDead ? "_die" : string.Empty) + ".bmp";
            w.DrawBitmap(7, leader ? 8 : 6, online ? job : RoClassicUiImages.Tinted(job, 1));
            if (leader) w.DrawBitmap(9, 0, MsParty + "ico_partyCrown.bmp");
            int width = w.Width;
            if (miniMode == 0 && online && m.MaxHp > 0) {
                int gw = (int) (0.38f * width);
                w.FillRect(35, 23, gw, 5, MsGaugeBorder);
                w.FillRect(36, 24, gw - 2, 3, MsGaugeEmpty);
                int pct = m.Hp * 100 / m.MaxHp, fill = gw * pct / 100 - 2;
                if (fill > 0) w.FillRect(36, 24, fill, 3, pct >= 25 ? MsGaugeHp : MsGaugeLow);
            }

            if (miniMode == 2) return;
            RoClassicGdiFont bold = w.FontFor(0, 12, true);
            string text = miniMode == 0
                ? "Lv." + m.BaseLevel.ToString(CultureInfo.InvariantCulture) + " " + m.Name + "(" + RoClassicUiHost.MapDisplayName(m.MapName) + ")"
                : m.Name;
            text = MsFit(text, bold, (int) (width * (miniMode == 0 ? 0.6f : 0.532f)));
            w.TextOutPlain(35, 9, text, bold, online ? MiniWhite : MiniOfflineText);
        }

        /// <summary>
        /// 0x6bbab0's menu: Send a mail 0xaf8, Open 1:1 Chat 0x168 (another member), Expel 0x61 and Assign Party Leader 0x5fc
        /// (the leader on another member), Leave party 0x60 (oneself), Remove small party window 0xc1e.
        /// </summary>
        private void OpenClassicMiniMenu(ClassicMini mini) {
            if (Locked) return;
            RoPartyMemberRuntimeSnapshot m = mini.Member;
            uint me = model.LocalAccountId;
            bool self = m.AccountId == me, leader = model.IsLocalLeader(msParty);
            var actions = new List<Action>();
            RoClassicMenu.Begin();
            void Item(int msgId, string fallback, Action action) {
                RoClassicMenu.Add(RoClassicUiHost.MsgStr(msgId, fallback));
                actions.Add(action);
            }

            Item(0xaf8, "Send a mail", () => OpenMail(m.Name));
            if (!self) Item(0x168, "Open 1:1 Chat", () => OpenWhisper(m.Name));
            if (!self && leader) {
                Item(0x61, "Expel from party", () => RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x16b, "Are you sure that you want to expel?"),
                    RoClassicMessageBox.TypeOkCancel, true, r => {
                        if (r == RoClassicMessageBox.IdYes) _ = model.ExpelPartyMemberAsync(m);
                    }));
                Item(0x5fc, "Assign Party Leader", () => RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x5fd, "Switch Party Leader?"),
                    RoClassicMessageBox.TypeOkCancel, true, r => {
                        if (r == RoClassicMessageBox.IdYes) _ = model.ChangePartyLeaderAsync(m);
                    }));
            }

            if (self) {
                Item(0x60, "Leave party", () => RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x165, "Are you sure that you want to leave?"),
                    RoClassicMessageBox.TypeOkCancel, true, r => {
                        if (r == RoClassicMessageBox.IdYes) _ = model.LeavePartyAsync();
                    }));
            }

            Item(0xc1e, "Remove small party window", () => RemoveDetachedMember(m.AccountId));
            Vector2Int mouse = system.Windows.Mouse;
            RoClassicMenu.Open(mouse.x + 1, mouse.y + 1, item => {
                if (item >= 0 && item < actions.Count) actions[item]();
            });
        }

        /// <summary>The sweep console: mini list | mini mode n | mini drop aid slot | mini menu aid | mini sort.</summary>
        private string SweepClassicMini(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            uint Aid(int i) => a.Length > i && uint.TryParse(a[i], out uint v) ? v : 0;
            switch (a[0]) {
                case "mode":
                    SetClassicMiniMode((int) Aid(1));
                    break;
                case "drop":
                    if (classicMinis.TryGetValue(Aid(1), out ClassicMini d)) {
                        miniDragging = d;
                        ClassicMiniDrop(d, MiniTop() + (int) Aid(2) * MiniPitch + 10);
                    }

                    break;
                case "menu":
                    if (classicMinis.TryGetValue(Aid(1), out ClassicMini m)) OpenClassicMiniMenu(m);
                    break;
                case "sort":
                    SortClassicMinis();
                    break;
            }

            var parts = new List<string>();
            foreach (KeyValuePair<uint, ClassicMini> p in classicMinis) parts.Add(p.Key + "@" + p.Value.Slot + ":" + p.Value.Member.Name);
            return "minis mode=" + miniMode + " " + string.Join(" ", parts);
        }
    }
}
