using System;
using System.Collections.Generic;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The input row's popups (chat-popups-spec): "Select receiver" 0xdd lists the whisper names oldest first plus an empty
    /// row (112 × min((n + 1)·16, 80) at (sx + 1, sy + H − 23 − h); none → "No Whisper List." in red), "Select receiving
    /// group" 0xea the five groups (112 × 48 at (sx + w − 114, sy + H − 71), always with the scroll bar). Both are window 0x1a
    /// (UIComboBox: white, sysboxs corners, the highlight #709FED following the mouse, black text, release picks, wheel 3
    /// rows, Up / Down / Enter while open, any press outside closes it). The group (DAT_01552dc8: 0 all, 1 party, 2 guild,
    /// 3 clan, 4 allies; 0 at start, not saved) sets the chat edit's colour and routes plain lines (0xc07a00 case 0).
    /// </summary>
    public sealed partial class RoClassicChat {
        private RoClassicWindow classicPopup;
        private readonly List<string> classicPopupItems = new List<string>();
        private int classicPopupMode, classicPopupSel, classicPopupFirst;
        private Color32 classicGroupColor = new Color32(0, 0, 0, 255);
        private const int PopupRowH = 16;

        private bool ClassicPopupOpen => classicPopup != null && classicPopup.Visible;
        private int PopupVisibleRows => classicPopup.Height / PopupRowH;

        /// <summary>SendMsg(6, 0xdd) @898e7a.</summary>
        private void OpenClassicReceiverPopup() {
            CloseClassicPopup();
            int n = model.WhisperNames.Count;
            if (n == 0) {
                AddLine(RoClassicUiHost.MsgStr(0xc0, "No Whisper List."), "#FF0000", RoChatChannel.System);
                return;
            }

            classicPopupItems.Clear();
            classicPopupItems.AddRange(model.WhisperNames);
            classicPopupItems.Add(string.Empty);
            int h = Math.Min((n + 1) * PopupRowH, 80);
            OpenClassicPopup(0, Mathf.Clamp(model.WhisperIndex, 0, n), h, classicChat.Position.x + 1, classicChat.Position.y + classicChat.Height - 23 - h);
        }

        /// <summary>SendMsg(6, 0xea) @898cde.</summary>
        private void OpenClassicGroupPopup() {
            CloseClassicPopup();
            classicPopupItems.Clear();
            classicPopupItems.Add(RoClassicUiHost.MsgStr(0x55, "Send to All"));
            classicPopupItems.Add(RoClassicUiHost.MsgStr(0x56, "Send to Party"));
            classicPopupItems.Add(RoClassicUiHost.MsgStr(0x1b5, "Send to Guild"));
            classicPopupItems.Add(RoClassicUiHost.MsgStr(0x939, "Send to Clan"));
            classicPopupItems.Add(RoClassicUiHost.MsgStr(0xfc0, "Sent to Allies"));
            OpenClassicPopup(1, model.SendGroup, 48, classicChat.Position.x + classicChat.Width - 114, classicChat.Position.y + classicChat.Height - 71);
        }

        private void OpenClassicPopup(int mode, int selected, int height, int x, int y) {
            if (classicPopup == null) {
                classicPopup = new RoClassicWindow(RoClassicUiSystem.Instance, new RoClassicWindowLayout { Class = "UIComboBoxWnd", WindowId = 0x1a, Width = 112, Height = 80 }) {
                    Visible = false, Draggable = false, ClickableAt = _ => true,
                };
                classicPopup.DrawDynamic += DrawClassicPopup;
                classicPopup.Released += (w, p) => {
                    int row = classicPopupFirst + p.y / PopupRowH;
                    if (p.x < 0 || p.x >= (Scrollable ? w.Width - 16 : w.Width) || row < 0 || row >= classicPopupItems.Count) return;
                    classicPopupSel = row;
                    PickClassicPopup();
                };
                classicPopup.Wheeled += (w, p, delta) => {
                    if (!Scrollable) return;
                    classicPopupFirst = Mathf.Clamp(classicPopupFirst + (delta < 0 ? 3 : -3), 0, classicPopupItems.Count - PopupVisibleRows);
                    w.Invalidate();
                };
            }

            classicPopupMode = mode;
            classicPopupSel = selected;
            classicPopup.Layout.Height = height;
            classicPopup.SetHeight(height);
            // the current choice in view as the bottom row
            classicPopupFirst = Mathf.Clamp(selected - PopupVisibleRows + 1, 0, Math.Max(0, classicPopupItems.Count - PopupVisibleRows));
            classicPopup.MoveTo(x, y);
            classicPopup.Visible = true;
            RoClassicUiSystem.Instance.Windows.BringToFront(classicPopup);
            classicPopup.Invalidate();
        }

        private bool Scrollable => classicPopupItems.Count > PopupVisibleRows;

        private void CloseClassicPopup() {
            if (classicPopup != null) classicPopup.Visible = false;
        }

        private void DisposeClassicPopup() {
            classicPopup?.Destroy();
            classicPopup = null;
        }

        /// <summary>UIComboBox::OnDraw 0x7eab20.</summary>
        private void DrawClassicPopup(RoClassicWindow w) {
            const string basic = "유저인터페이스\\basic_interface\\";
            int width = w.Width, height = w.Height;
            w.FillRect(0, 0, width, height, new Color32(255, 255, 255, 255));
            w.DrawBitmapRaw(0, 0, basic + "sysboxs_lu.bmp");
            w.DrawBitmapRaw(0, height - 7, basic + "sysboxs_ld.bmp");
            w.DrawBitmapRaw(width - 7, 0, basic + "sysboxs_ru.bmp");
            w.DrawBitmapRaw(width - 7, height - 7, basic + "sysboxs_rd.bmp");
            int last = Math.Min(classicPopupItems.Count, PopupVisibleRows + classicPopupFirst);
            for (int i = classicPopupFirst, r = 0; i < last; i++, r++) {
                if (i == classicPopupSel) w.FillRect(3, PopupRowH * r + 1, width - 6, 14, new Color32(0x70, 0x9f, 0xed, 255));
                w.TextOut(3, PopupRowH * r + 2, classicPopupItems[i], 0, 12, ClassicChatBlack);
            }

            if (!Scrollable) return;
            w.FillRect(width - 16, 0, 16, height, new Color32(255, 255, 255, 255));
            w.DrawScrollBar(width - 13, 0, height, classicPopupItems.Count - PopupVisibleRows, classicPopupItems.Count, PopupVisibleRows, classicPopupFirst);
        }

        /// <summary>Per frame: the highlight follows the mouse, a left press outside closes the popup.</summary>
        private void TickClassicPopup() {
            if (!ClassicPopupOpen) return;
            RoClassicUiSystem ui = RoClassicUiSystem.Instance;
            if (ui == null) return;
            if (ui.Windows.HoverWindow == classicPopup) {
                Vector2Int p = ui.Windows.Mouse - classicPopup.Position;
                int row = classicPopupFirst + p.y / PopupRowH;
                if (row >= 0 && row < classicPopupItems.Count && row != classicPopupSel && p.x < (Scrollable ? classicPopup.Width - 16 : classicPopup.Width)) {
                    classicPopupSel = row;
                    classicPopup.Invalidate();
                }
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && ui.Windows.HoverWindow != classicPopup) CloseClassicPopup();
#endif
        }

        /// <summary>Up (+1) / Down (−1) while the popup is open: the highlight moves and stays in view.</summary>
        private bool ClassicPopupKey(int direction) {
            if (!ClassicPopupOpen) return false;
            classicPopupSel = Mathf.Clamp(classicPopupSel - direction, 0, classicPopupItems.Count - 1);
            if (classicPopupSel < classicPopupFirst) classicPopupFirst = classicPopupSel;
            if (classicPopupSel >= classicPopupFirst + PopupVisibleRows) classicPopupFirst = classicPopupSel - PopupVisibleRows + 1;
            classicPopup.Invalidate();
            return true;
        }

        /// <summary>The owner's msg 0x27 then DeleteWindow(0x1a).</summary>
        private void PickClassicPopup() {
            int i = classicPopupSel;
            CloseClassicPopup();
            if (classicPopupMode == 0) {
                // @8984bd: the name (or "" for the last row), its history index, the focus back in the chat edit
                int n = model.WhisperNames.Count;
                if (i < 0 || i > n) return;
                classicWhisperEdit.SetText(i == n ? string.Empty : model.WhisperNames[i]);
                model.WhisperIndex = i;
                classicMessageEdit.Focus();
                classicChat.Invalidate();
                return;
            }

            // @898457: the group and the chat edit's colour (clan keeps the previous one)
            model.SendGroup = i;
            switch (i) {
                case 0: classicGroupColor = new Color32(0, 0, 0, 255); break;
                case 1: classicGroupColor = new Color32(128, 0, 128, 255); break;
                case 2: classicGroupColor = new Color32(0, 128, 128, 255); break;
                case 4: classicGroupColor = new Color32(148, 0, 211, 255); break;
            }

            classicChat.Invalidate();
        }

        /// <summary>
        /// 0xc07a00 case 0 for a plain line: the prefixes, Ctrl / Alt / CapsLock held and the receiving group decide the
        /// channel (RoChatModel.TryResolveGroupMessage). False when the line sends nothing ("%" alone).
        /// </summary>
        private bool ClassicGroupMessage(string message, out RoChatModel.RoQueuedChatMessage queued) {
            bool ctrl = false, alt = false, capsLock = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null) {
                ctrl = kb.ctrlKey.isPressed;
                alt = kb.altKey.isPressed;
                capsLock = kb.capsLockKey.isPressed;
            }
#endif
            bool inParty = session != null && session.PartySnapshot().IsInParty;
            bool inGuild = session != null && session.GuildSnapshot().Guild.HasGuild;
            return RoChatModel.TryResolveGroupMessage(message, model.SendGroup, ctrl, alt, capsLock, inParty, inGuild, RoClassicClan.InClan, out queued);
        }

        /// <summary>Sweep: popup receiver|group|pick n|key d|state.</summary>
        internal string SweepClassicPopup(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            switch (t[0]) {
                case "receiver": OpenClassicReceiverPopup(); break;
                case "group": OpenClassicGroupPopup(); break;
                case "pick":
                    if (ClassicPopupOpen && t.Length > 1) {
                        classicPopupSel = int.Parse(t[1]);
                        PickClassicPopup();
                    }

                    break;
                case "key": ClassicPopupKey(t.Length > 1 ? int.Parse(t[1]) : 1); break;
            }

            return "popup=" + (ClassicPopupOpen ? (classicPopupMode == 0 ? "receiver" : "group") + " sel=" + classicPopupSel + " first=" + classicPopupFirst + " items=[" + string.Join(",", classicPopupItems) + "]" : "closed") +
                " group=" + model.SendGroup + " whisper='" + classicWhisperEdit.Text + "'";
        }
    }
}
