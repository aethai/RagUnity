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
    /// The separate chat windows (UISubChatHisWnd, vtable 0x00fa6208, ctor 0x871e00, made by 0x9c7cd0 into mgr+0x28 keyed
    /// by the tab's index among the separate tabs; docs/audits/chat-windows-2026-10-04.md): a tab pulled out of the main
    /// window's strip (UITabControl vf27 0x7f3510 → chat msg 0x75) or one the "+" button could not fit; each shows one tab's
    /// lines in its own UIChatHisBox, moves and sizes on its own, goes back into the strip by its stick button or by being
    /// dropped over the chat (msg 0x76), and its close button deletes the tab. Also here: the strip's tooltip for a cut
    /// name (vf28 0x7f3060), PageUp / PageDown (0x9d3830 → the active window's msg 3 / 4) and the send path's reserved-tag
    /// refusal.
    /// </summary>
    public sealed partial class RoClassicChat {
        private const int SubTabStripH = 16;     // +0xc8
        private const int SubBottom = 15;        // +0xd0
        private const int SubMinTabW = 0x19;
        private const int SubLangTabW = 0x6e;    // ctor: +0xcc for langtype 1
        private const int SubStickOnId = 0x161;
        private const int SubBoxX = 5;
        private const string ClassicUiRoot = "유저인터페이스\\";

        /// <summary>One UISubChatHisWnd: its window, history box, buttons, rename edit (UIEditCtrl2 +0x108) and tab width (+0xcc).</summary>
        private sealed class ClassicSubChat {
            public RoClassicWindow Window;
            public readonly ClassicChatBox Box = new ClassicChatBox();
            public RoClassicEditField Rename;
            public RoClassicChildLayout StickOn, Option, Close;
            public int Key;            // +0xd4
            public int TabWidth;       // +0xcc
            public string Name = string.Empty;   // +0xf0
            public bool Dirty = true;
            public int WrappedAt = -1;
        }

        private readonly List<ClassicSubChat> classicSubs = new List<ClassicSubChat>(4);
        private ClassicSubChat classicSubCaptured;   // the separate window that took the press (its LBtnUp checks the drop)

        // UITabControl +0xac / +0xb0: where the strip last sent msg 0x75, and whether a tab press holds the mouse
        private bool classicTabCaptured;
        private Vector2Int classicTabDragLast;
        private Vector2Int classicTabMouse;

        private int SubBoxWidth(ClassicSubChat s) => s.Window.Width - 8;
        private int SubBoxHeight(ClassicSubChat s) => s.Window.Height - SubBottom - SubTabStripH - 4;
        private int SubBoxY => SubTabStripH + 4;
        private int SubVisibleLines(ClassicSubChat s) => Math.Max(0, SubBoxHeight(s) / ClassicLineHeight);

        // ---- making and closing (0x9c7cd0, OnCreate 0x88b770, the delete in msg 6 / 0xc6) ----

        /// <summary>OnCreate's loop over the separate records (0x884330): one window per record at its kept rect.</summary>
        private void CreateClassicSubWindows() {
            for (int i = 0; i < model.SubTabCount; i++) {
                RectInt r = model.SubTabRect(i);
                CreateClassicSub(i, r.x, r.y, r.width, r.height);
            }
        }

        /// <summary>
        /// 0x9c7cd0 (key, x, y, w, h): x = y = −1 → ((key + 10)·10, (key + 5)·20); w 0 or wider than the screen → 300; h 0 or
        /// taller → 95; x past the screen → screen − w; y past it → 100. The window reads its record (name, tab width), is
        /// moved there (Move writes the record back) and shown.
        /// </summary>
        private ClassicSubChat CreateClassicSub(int key, int x, int y, int width, int height) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || classicChat == null) return null;
            Vector2Int screen = system.ClientSize;
            RectInt rect = RoChatModel.SubWindowRect(key, x, y, width, height, screen.x, screen.y);
            x = rect.x;
            y = rect.y;
            width = rect.width;
            height = rect.height;

            var s = new ClassicSubChat { Key = key };
            var layout = new RoClassicWindowLayout { Class = "UISubChatHisWnd", WindowId = 0x82, Width = width, Height = height };
            // AddChild order of OnCreate: stickon (+0xbc, 0x161), battle_option2 (+0xc0, 0xbd), wnd_mini (+0xc4, 0xc6)
            s.StickOn = ChatButton(SubStickOnId, "stickon", 14, 9, "stickon");
            s.Option = ChatButton(ClassicBattleOption2Id, "battle_option2", 9, 9, "battle_option2");
            s.Close = ChatButton(ClassicWndMiniId, "wnd_mini", 8, 8, "wnd_mini");
            layout.Children.Add(s.StickOn);
            layout.Children.Add(s.Option);
            layout.Children.Add(s.Close);

            s.Window = new RoClassicWindow(system, layout) { FrameDragArea = local => SubFrameDragArea(s, local) };
            s.Window.DrawUnderlay += w => DrawClassicSubBoxes(s, w);
            s.Window.DrawDynamic += w => DrawClassicSub(s, w);
            s.Window.Clicked += child => OnClassicSubClicked(s, child);
            s.Window.Pressed += (w, local) => OnClassicSubPressed(s, local);
            s.Window.DoubleClicked += (w, local) => OnClassicSubDoubleClicked(s, local);
            s.Window.Wheeled += (w, local, delta) => {
                if (InSubBox(s, local)) ScrollClassicSub(s, delta < 0 ? 3 : -3);
            };
            s.Window.ResizerModeAt = local =>
                local.x >= s.Window.Width - 15 && local.x < s.Window.Width - 1 && local.y >= s.Window.Height - 15 && local.y < s.Window.Height - 1 ? 7 : -1;
            s.Window.ResizeRequested += (w, mode, rw, rh) => {
                if (mode == 7) ResizeClassicSub(s, rw, rh);
            };
            s.Window.Moved += w => {
                // vf04 0x87e4d0: the record's XPos / YPos follow the window
                model.SetSubTabRect(s.Key, w.Position.x, w.Position.y, w.Width, w.Height);
                SaveClassicSubRect(s);
            };

            // UIEditCtrl2 Create(100, 19), parked until a double click on the tab
            s.Rename = new RoClassicEditField { Background = ClassicChatWhite, Visible = false, HandlesEscape = true, Rect = new RectInt(-100, -100, 100, 19) };
            s.Window.AddEditField(s.Rename);
            s.Rename.Submitted += _ => CommitClassicSubRename(s);
            // Esc reaches the edit's parent as msg 0 too (0x9d3830 case 0x1b → SendMsg(edit, 0, 1)), which commits the same way
            s.Rename.Cancelled += _ => CommitClassicSubRename(s);

            // the record (FUN_0086f3f0 on map 0x154e3e4): name, tab width = its width + 10, at least 25 (the ctor's 110 without one)
            s.Name = model.SubTabName(key);
            s.TabWidth = key < model.SubTabCount ? Math.Max(SubMinTabW, classicChat.FontFor(0, 12).Measure(s.Name) + 10) : SubLangTabW;
            s.Box.VisibleLines = Math.Max(1, (height - SubBottom - SubTabStripH - 4) / ClassicLineHeight);

            while (classicSubs.Count <= key) classicSubs.Add(null);
            classicSubs[key] = s;
            LayoutClassicSubButtons(s);
            s.Window.MoveTo(x, y);
            model.SetSubTabRect(key, x, y, width, height);
            SaveClassicSubRect(s);
            s.Window.Visible = true;   // vf14 SetShow(1)
            system.Windows.BringToFront(s.Window);
            s.Window.Invalidate();
            return s;
        }

        /// <summary>
        /// 0x894470: unlocked, wnd_mini, stickon and battle_option2 right-aligned from W − 2 with 2 px between them at y 4;
        /// locked, only battle_option2 at (W − 11, 4).
        /// </summary>
        private void LayoutClassicSubButtons(ClassicSubChat s) {
            RoClassicWindow w = s.Window;
            w.SetChildVisible(s.Close, classicUnlocked);
            w.SetChildVisible(s.StickOn, classicUnlocked);
            if (!classicUnlocked) {
                w.SetChildPosition(s.Option.Path, w.Width - s.Option.W - 2, 4);
                w.Invalidate();
                return;
            }

            int x = w.Width - s.Close.W - 2;
            w.SetChildPosition(s.Close.Path, x, 4);
            x -= 2 + s.StickOn.W;
            w.SetChildPosition(s.StickOn.Path, x, 4);
            w.SetChildPosition(s.Option.Path, x - s.Option.W - 2, 4);
            w.Invalidate();
        }

        private void DestroyClassicSub(ClassicSubChat s) {
            if (s == null || s.Window == null) return;
            if (RoClassicTextInput.Focused == s.Rename) RoClassicTextInput.Focus(null);
            if (classicSubCaptured == s) classicSubCaptured = null;
            s.Window.Destroy();
            s.Window = null;
        }

        /// <summary>The windows after a removed key move up one (the renumbering loops of msg 0x76 and the close button).</summary>
        private void RemoveClassicSubAt(int key) {
            if (key < 0 || key >= classicSubs.Count) return;
            DestroyClassicSub(classicSubs[key]);
            classicSubs.RemoveAt(key);
            for (int i = 0; i < classicSubs.Count; i++) {
                if (classicSubs[i] != null) classicSubs[i].Key = i;
            }
        }

        private void DisposeClassicSubs() {
            foreach (ClassicSubChat s in classicSubs) DestroyClassicSub(s);
            classicSubs.Clear();
            classicSubCaptured = null;
            classicTabCaptured = false;
        }

        // ---- drawing (vf43 0x877b20, OnDraw 0x8911a0, UIChatHisBox, UIResizer) ----

        /// <summary>vf43: the tab (1, 1, tw − 2, 16) and the body (1, 17, W − 2, H − 18), black at alpha 0x66, under the window.</summary>
        private static void DrawClassicSubBoxes(ClassicSubChat s, RoClassicWindow w) {
            w.FillRect(1, 1, s.TabWidth - 2, SubTabStripH, ClassicBoxActive);
            w.FillRect(1, SubTabStripH + 1, w.Width - 2, w.Height - SubTabStripH - 2, ClassicBoxActive);
        }

        /// <summary>
        /// OnDraw 0x8911a0: the name in white over a black shadow at (5, 4), the tab's and the body's outline (white left and top,
        /// #8E8E8E right and bottom, cut corners); then the history box at (5, 20) and the resizer's btn_resize.bmp at (W − 15, H − 15).
        /// </summary>
        private void DrawClassicSub(ClassicSubChat s, RoClassicWindow w) {
            int W = w.Width, H = w.Height, tw = s.TabWidth, th = SubTabStripH;
            // the shadow plain (0x9b47f0), the name with colour codes (0x9b52c0) from white; the light lines take the colour it ends in
            Color32 light = ClassicChatWhite;
            w.TextOut(5, 4, s.Name, 0, 12, ClassicChatBlack);
            w.TextOutColored(4, 3, s.Name, 0, s.Name.Length, 0, 12, ref light);
            w.FillRect(2, 0, tw - 5, 1, light);
            w.FillRect(1, 1, 1, 1, light);
            w.FillRect(0, 2, 1, H - 4, light);
            w.FillRect(tw, th, W - tw - 3, 1, light);
            w.FillRect(tw - 3, 0, 1, 1, ClassicFrameGrey);
            w.FillRect(tw - 2, 1, 1, 1, ClassicFrameGrey);
            w.FillRect(tw - 1, 2, 1, th - 1, ClassicFrameGrey);
            w.FillRect(W - 3, th, 1, 1, ClassicFrameGrey);
            w.FillRect(W - 2, th + 1, 1, 1, ClassicFrameGrey);
            w.FillRect(W - 1, th + 2, 1, H - th - 4, ClassicFrameGrey);
            w.FillRect(1, H - 2, 1, 1, ClassicFrameGrey);
            w.FillRect(W - 2, H - 2, 1, 1, ClassicFrameGrey);
            w.FillRect(2, H - 1, W - 4, 1, ClassicFrameGrey);

            DrawClassicSubHistory(s, w);
            w.DrawBitmapRaw(W - 15, H - 15, ClassicUiRoot + "btn_resize.bmp");
        }

        /// <summary>The box (UIChatHisBox 0x7ea930) at (5, 20), (W − 8) × (H − 35): shadowed lines 14 px apart, its UIScrollBar2.</summary>
        private void DrawClassicSubHistory(ClassicSubChat s, RoClassicWindow w) {
            ClassicChatBox box = s.Box;
            int bx = SubBoxX, by = SubBoxY, bw = SubBoxWidth(s), bh = SubBoxHeight(s), visible = SubVisibleLines(s);
            if (bw <= 0 || bh <= 0) return;
            int last = Math.Min(box.Lines.Count, visible + box.First);
            w.ClipTo(bx, by, bw, bh);
            for (int i = box.First, k = 0; i < last; i++, k++) {
                ClassicChatLine line = box.Lines[i];
                w.TextOut(bx + 3, by + ClassicLineHeight * k + 4, line.Text, 0, 12, ClassicChatBlack);
                w.TextOut(bx + 2, by + ClassicLineHeight * k + 3, line.Text, 0, 12, line.Color);
                if (line.ItemLinks != null) {
                    foreach ((RoClassicItemLink.Link l, int lx) in line.ItemLinks) w.TextOut(bx + 2 + lx, by + ClassicLineHeight * k + 3, l.Label, 0, 12, ClassicNaviLink);
                }

                if (line.NaviLabel != null) {
                    int at = line.Text.IndexOf(line.NaviLabel, StringComparison.Ordinal);
                    int lx = w.FontFor(0, 12).Measure(line.Text.Substring(0, at));
                    w.TextOut(bx + 2 + lx, by + ClassicLineHeight * k + 3, line.NaviLabel, 0, 12, ClassicNaviLink);
                }
            }

            int total = box.Lines.Count, range = total - visible;
            if (range > 0) {
                int sx = bx + bw - 13;
                w.ClipTo(sx, by, 12, bh);
                w.FillRect(sx, by + 11, 10, bh - 21, ClassicChatBlack);
                w.DrawBitmap(sx, by, ChatUiRoot + "dialscr_up.bmp");
                int thumb = Math.Max(8, (bh - 20) * visible / total);
                w.FillRect(sx, by + 10 + 1 + box.First * (bh - 20 - thumb) / range, 10, thumb - 1, ClassicThumb);
                w.DrawBitmap(sx, by + bh - 11, ChatUiRoot + "dialscr_down.bmp");
            }

            w.ResetClip();
        }

        // ---- lines (msg 0x25 0x89d819, the box's re-wrap 0x7fd0c0) ----

        /// <summary>
        /// Chat msg 0x25's second loop: every separate window whose tab takes the line gets it; the window's own msg 0x25 stores
        /// it and adds it pixel-wrapped at the box width − 16 (0x7d61c0, no byte wrap), a line with item links whole (vf57).
        /// </summary>
        private void AppendClassicSubRaw(RoChatModel.Entry raw) {
            for (int i = 0; i < classicSubs.Count; i++) {
                ClassicSubChat s = classicSubs[i];
                if (s == null || s.Window == null || s.Dirty || !model.AcceptsSub(s.Key, raw)) continue;
                AddClassicSubRaw(s, raw);
                s.Window.Invalidate();
            }
        }

        private void AddClassicSubRaw(ClassicSubChat s, RoChatModel.Entry raw) {
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            int boxW = SubBoxWidth(s);
            classicLinkCursor = 0;
            if (raw.Special) {
                // vf57 → 0x7fec70: pixel-wrapped at box − 16 when wider than box − 20, else one line
                if (raw.ItemLinks != null && font.Measure(raw.Text) > boxW - 20) PixelWrapAdd(s.Box, font, raw.Text, boxW - 16, raw);
                else AddClassicBoxLine(s.Box, raw.Text, raw);
                return;
            }

            PixelWrapAdd(s.Box, font, raw.Text, boxW - 16, raw);
        }

        /// <summary>The box's lines again from the kept messages its tab takes (made, re-flagged, or wider: Resize 0x7f8230 → 0x7fd0c0).</summary>
        private void RebuildClassicSub(ClassicSubChat s) {
            s.Dirty = false;
            s.Box.Lines.Clear();
            s.Box.First = 0;
            s.Box.VisibleLines = Math.Max(1, SubVisibleLines(s));
            s.WrappedAt = SubBoxWidth(s);
            foreach (RoChatModel.Entry raw in model.Entries) {
                if (model.AcceptsSub(s.Key, raw)) AddClassicSubRaw(s, raw);
            }

            s.Box.First = Math.Max(0, s.Box.Lines.Count - SubVisibleLines(s));
            s.Window.Invalidate();
        }

        private void ScrollClassicSub(ClassicSubChat s, int lines) {
            int first = Mathf.Clamp(s.Box.First + lines, 0, Math.Max(0, s.Box.Lines.Count - SubVisibleLines(s)));
            if (first == s.Box.First) return;
            s.Box.First = first;
            s.Window.Invalidate();
        }

        // ---- mouse (LBtnDown 0x892080, DblClk 0x8917b0, LBtnUp 0x892490, the box) ----

        private bool InSubBox(ClassicSubChat s, Vector2Int local) =>
            local.x >= SubBoxX && local.x < SubBoxX + SubBoxWidth(s) && local.y >= SubBoxY && local.y < SubBoxY + SubBoxHeight(s);

        /// <summary>UIFrameWnd's drag only while the chat is unlocked (vf25 does nothing locked), from outside the box and the resizer.</summary>
        private bool SubFrameDragArea(ClassicSubChat s, Vector2Int local) =>
            classicUnlocked && !InSubBox(s, local) && s.Window.ResizerModeAt(local) < 0;

        /// <summary>
        /// vf25 0x892080 (unlocked): an open battle-message window follows this tab (SendMsg(6, 0xbd, key, 0, 2)), then the frame
        /// takes the press; the box takes its own (scroll bar, links).
        /// </summary>
        private void OnClassicSubPressed(ClassicSubChat s, Vector2Int local) {
            if (InSubBox(s, local)) {
                ClassicSubBoxPressed(s, local);
                return;
            }

            if (!classicUnlocked) return;
            RoClassicBattleMsgOption.FollowTab(this, s.Key, false);
            classicSubCaptured = s;
        }

        /// <summary>The box: an item / navigation link takes the press; else the scroll bar's arrows (a line) and track (a page).</summary>
        private void ClassicSubBoxPressed(ClassicSubChat s, Vector2Int local) {
            if (ClassicSubLinkAt(s, local, out RoClassicItemLink.Link link, out string naviPayload)) {
                if (naviPayload != null) RoClassicNavigation.SelectLocation(naviPayload);
                else RoClassicItemDescription.Toggle(link.Item, ResolveItemMetadata());
                return;
            }

            ClassicChatBox box = s.Box;
            int visible = SubVisibleLines(s), range = box.Lines.Count - visible, bh = SubBoxHeight(s);
            int bx = SubBoxX + SubBoxWidth(s) - 13, by = local.y - SubBoxY;
            if (range <= 0 || local.x < bx || local.x >= bx + 10) return;
            if (by < 10) {
                ScrollClassicSub(s, -1);
            } else if (by >= bh - 11) {
                ScrollClassicSub(s, 1);
            } else {
                int thumb = Math.Max(8, (bh - 20) * visible / box.Lines.Count);
                int top = 11 + box.First * (bh - 20 - thumb) / range;
                if (by < top) ScrollClassicSub(s, -(visible - 1));
                else if (by >= top + thumb) ScrollClassicSub(s, visible - 1);
            }
        }

        /// <summary>The item link button or the navigation label under a box point of a separate window.</summary>
        private bool ClassicSubLinkAt(ClassicSubChat s, Vector2Int local, out RoClassicItemLink.Link link, out string naviPayload) {
            link = default;
            naviPayload = null;
            int row = (local.y - SubBoxY) / ClassicLineHeight, index = s.Box.First + row;
            if (local.y < SubBoxY || index < 0 || index >= s.Box.Lines.Count || row >= SubVisibleLines(s)) return false;
            ClassicChatLine line = s.Box.Lines[index];
            RoClassicGdiFont font = classicChat.FontFor(0, 12);
            if (line.ItemLinks != null) {
                foreach ((RoClassicItemLink.Link l, int lx) in line.ItemLinks) {
                    int x0 = SubBoxX + 2 + lx - 1;
                    if (local.x >= x0 && local.x < x0 + font.Measure(l.Label) + 1) {
                        link = l;
                        return true;
                    }
                }
            }

            if (line.NaviLabel != null) {
                int at = line.Text.IndexOf(line.NaviLabel, StringComparison.Ordinal);
                int x0 = SubBoxX + 2 + font.Measure(line.Text.Substring(0, at)), x1 = x0 + font.Measure(line.NaviLabel) + 1;
                if (local.x >= x0 && local.x < x1) {
                    naviPayload = line.NaviPayload;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// vf26 0x8917b0 (unlocked, 0 &lt; x &lt; tab width, 0 &lt; y &lt; 16): the rename edit, emptied, (tw − 10) × 14 at (5, 2),
        /// focused. In the box, a line with a sender: msg 6 / 0x157 → 0x9bb6e0 (the whisper name).
        /// </summary>
        private void OnClassicSubDoubleClicked(ClassicSubChat s, Vector2Int local) {
            if (InSubBox(s, local)) {
                int index = s.Box.First + (local.y - SubBoxY) / ClassicLineHeight;
                if (index >= 0 && index < s.Box.Lines.Count && s.Box.Lines[index].Name.Length > 0) ClassicWhisperFromLine(s.Box.Lines[index].Name);
                return;
            }

            if (!classicUnlocked || local.x <= 0 || local.x >= s.TabWidth || local.y <= 0 || local.y >= SubTabStripH) return;
            s.Rename.SetText(string.Empty);
            s.Rename.Rect = new RectInt(5, 2, s.TabWidth - 10, SubTabStripH - 2);
            s.Rename.Visible = true;
            s.Rename.Focus();
            s.Window.Invalidate();
        }

        /// <summary>
        /// Msg 0 from the rename edit (0x89d569): a non-empty name with width + 10 &lt; W − 50 becomes the tab's (record and
        /// window, tab width = width + 10, at least 25); a name that wide shows MsgStr 0x629 in a box; the edit goes, the
        /// buttons are laid out again and an open battle-message window follows this tab.
        /// </summary>
        private void CommitClassicSubRename(ClassicSubChat s) {
            if (s.Window == null || !s.Rename.Visible) return;
            string name = s.Rename.Text;
            int textW = classicChat.FontFor(0, 12).Measure(name);
            if (name.Length > 0 && textW + 10 < s.Window.Width - 50 && model.RenameSubTab(s.Key, name)) {
                s.Name = name;
                s.TabWidth = Math.Max(SubMinTabW, textW + 10);
                SaveClassicTabs();
            }

            if (textW + 10 >= s.Window.Width - 50) {
                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x629, "Not very long to change the name of the specified tab."), RoClassicMessageBox.TypeOk, true, _ => { });
            }

            s.Rename.Visible = false;
            s.Rename.Rect = new RectInt(-100, -100, s.Rename.Rect.width, s.Rename.Rect.height);
            if (s.Rename.HasFocus) RoClassicTextInput.Focus(null);
            s.Rename.SetText(string.Empty);
            LayoutClassicSubButtons(s);
            RoClassicBattleMsgOption.FollowTab(this, s.Key, false);
        }

        /// <summary>Msg 6: battle_option2 (0xbd) opens / closes this tab's filter, the stick (0x161) puts it back, wnd_mini (0xc6) deletes it.</summary>
        private void OnClassicSubClicked(ClassicSubChat s, RoClassicChildLayout child) {
            switch (child.Id) {
                case ClassicBattleOption2Id:
                    RoClassicBattleMsgOption.Toggle(this, s.Key, false);
                    break;
                case SubStickOnId:
                    MergeClassicSub(s);
                    break;
                case ClassicWndMiniId:
                    CloseClassicSub(s);
                    break;
            }
        }

        /// <summary>
        /// 0x89da37: the record goes and the later ones move up, the window is deleted, an open battle-message window showing
        /// this tab closes (SendMsg(6, 0xbd, key, 0, 1)), the windows after it take the keys down.
        /// </summary>
        private void CloseClassicSub(ClassicSubChat s) {
            int key = s.Key;
            if (!model.RemoveSubTab(key)) return;
            RemoveClassicSubAt(key);
            RoClassicBattleMsgOption.CloseForTab(this, key, false);
            SaveClassicTabs();
        }

        /// <summary>
        /// Msg 0xe from the resizer (mode 7, unlocked): 280..512 × 74..384 in 32 px steps from the minimum; a new size lays the
        /// box (re-wrapped when its width changed), the resizer and the buttons out again and is kept in the record.
        /// </summary>
        private void ResizeClassicSub(ClassicSubChat s, int width, int height) {
            if (!classicUnlocked) return;
            Vector2Int size = RoChatModel.SnapSubWindowSize(width, height);
            if (size.x == s.Window.Width && size.y == s.Window.Height) return;
            ApplyClassicSubSize(s, size.x, size.y);
        }

        private void ApplyClassicSubSize(ClassicSubChat s, int w, int h) {
            bool wider = w != s.Window.Width;
            s.Window.SetWidth(w);
            s.Window.Layout.Height = h;
            s.Window.SetHeight(h);
            int visible = Math.Max(1, (h - SubBottom - SubTabStripH - 4) / ClassicLineHeight);
            if (wider) {
                s.Dirty = true;
            } else {
                s.Box.VisibleLines = visible;
                s.Box.First = Math.Max(0, s.Box.Lines.Count - visible);   // Resize: the last lines in view
            }

            LayoutClassicSubButtons(s);
            model.SetSubTabRect(s.Key, s.Window.Position.x, s.Window.Position.y, w, h);
            SaveClassicSubRect(s);
            s.Window.Invalidate();
        }

        /// <summary>
        /// vf31 0x892490 (unlocked): the window's lower left corner dropped inside the chat window (edges included) sends the
        /// chat msg 0x76.
        /// </summary>
        private void ClassicSubReleased(ClassicSubChat s) {
            if (s.Window == null || !classicUnlocked || classicChat == null || !classicChat.Visible) return;
            var window = new RectInt(s.Window.Position.x, s.Window.Position.y, s.Window.Width, s.Window.Height);
            var chat = new RectInt(classicChat.Position.x, classicChat.Position.y, classicChat.Width, classicChat.Height);
            if (RoChatModel.DropsOnChat(window, chat)) MergeClassicSub(s);
        }

        // ---- the main window's side: msg 0x75 / 0x76, "+" into a window ----

        /// <summary>
        /// UITabControl vf27 0x7f3510 while a tab press holds the mouse: on the first move (nothing kept at +0xac / +0xb0) or a
        /// move of more than 5 px left or up from the point kept, the chat gets msg 0x75 for the current tab; the point is
        /// kept either way (control-local, so a later pull must go 6 px past it).
        /// </summary>
        private void TickClassicTabCapture() {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            RoClassicUiSystem ui = RoClassicUiSystem.Instance;
            if (mouse == null || ui == null || !mouse.leftButton.isPressed) {
                classicTabCaptured = false;
                return;
            }

            Vector2Int m = ui.Windows.Mouse;
            if (m == classicTabMouse) return;
            classicTabMouse = m;
            Vector2Int local = m - classicChat.Position - new Vector2Int(3, 0);
            if (RoChatTabStrip.PullsOut(classicTabDragLast.x, classicTabDragLast.y, local.x, local.y)) DetachClassicTab(activeTabIndex);
            classicTabDragLast = local;
#else
            classicTabCaptured = false;
#endif
        }

        /// <summary>
        /// Chat msg 0x75 (0x897ac4, unlocked, more than one main tab): the current tab and its record go to a new separate
        /// window made at the cascade place (key + 10)·10, (key + 5)·20 in 300 × 95; the tab before it becomes current; the
        /// new window is put at the chat's place and takes the press as if grabbed where the tab was (x + tab width ·
        /// <paramref name="tab"/>), so it follows the mouse until the button goes up.
        /// </summary>
        private void DetachClassicTab(int tab) {
            if (!classicUnlocked || tabs.Count <= 1 || classicChat == null) return;
            EndClassicTabRename();
            int key = model.DetachActiveTab();
            if (key < 0) return;
            ClassicTabsChanged();
            int member = ClassicTabMemberWidth(classicChat);   // +0x100 = strip width / main tabs, after the removal
            ClassicSubChat s = CreateClassicSub(key, -1, -1, 0, 0);
            SaveClassicTabs();
            RoClassicBattleMsgOption.FollowTab(this, activeTabIndex);   // 0x875f50(1)
            ClassicLayoutHeader();
            classicChat.Invalidate();
            if (s == null) return;

            RoClassicUiSystem ui = RoClassicUiSystem.Instance;
            Vector2Int chatAt = classicChat.Position;
            s.Window.MoveTo(chatAt.x, chatAt.y);
            classicTabCaptured = false;   // the capture goes to the new window (0x9d70e0, then its LBtnDown)
            if (ui == null) return;
            ui.Windows.BeginDrag(s.Window, ui.Windows.Mouse - new Vector2Int(chatAt.x + member * tab, chatAt.y));
            RoClassicBattleMsgOption.FollowTab(this, s.Key, false);   // its vf25
            classicSubCaptured = s;
        }

        /// <summary>
        /// Chat msg 0x76 (0x897d83) from a separate window: unlocked and with room for one more tab of the current width in the
        /// strip (strip + tab width ≤ W − 90, else nothing happens); a name whose width · (main tabs + 1) passes W − 90 is
        /// refused — the chat pops open (mgr msg 3), MsgStr 0x6f0 in red, the window to the top; else the tab becomes the
        /// last main tab and the current one (AddTab), its window goes and the later ones take the keys down.
        /// </summary>
        private void MergeClassicSub(ClassicSubChat s) {
            if (!classicUnlocked || classicChat == null || s?.Window == null) return;
            int member = ClassicTabMemberWidth(classicChat);
            if (model.Strip.ControlWidth + member > ClassicChatWidth - 90) return;
            string name = model.SubTabName(s.Key);
            if (classicChat.FontFor(0, 12).Measure(name) * (tabs.Count + 1) > ClassicChatWidth - 90) {
                ClassicPopOpen();
                AddLine(RoClassicUiHost.MsgStr(0x6f0, "Failed to add because you have reached the limit."), "#ff0000", RoChatChannel.System);
                RoClassicUiSystem.Instance?.Windows.BringToFront(s.Window);
                return;
            }

            EndClassicTabRename();
            int key = s.Key;
            int index = model.MergeSubTab(key);
            if (index < 0) return;
            RemoveClassicSubAt(key);
            ClassicTabsChanged();
            SaveClassicTabs();
            RoClassicBattleMsgOption.FollowTab(this, index);   // 0x875f50(1)
            ClassicLayoutHeader();
            classicChat.Invalidate();
        }

        // ---- the per-frame part (from UpdateClassicChat) ----

        /// <summary>
        /// Per frame, cheap when idle: windows whose lines must be made again, the tab-drag capture, a separate window's
        /// release (its LBtnUp), PageUp / PageDown and the strip's tooltip.
        /// </summary>
        private void TickClassicSubs() {
            HookClassicEmotionMini();
            for (int i = 0; i < classicSubs.Count; i++) {
                ClassicSubChat s = classicSubs[i];
                if (s != null && s.Window != null && s.Dirty) RebuildClassicSub(s);
            }

            if (classicTabCaptured) TickClassicTabCapture();
#if ENABLE_INPUT_SYSTEM
            if (classicSubCaptured != null) {
                Mouse mouse = Mouse.current;
                if (mouse == null || !mouse.leftButton.isPressed) {
                    ClassicSubChat released = classicSubCaptured;
                    classicSubCaptured = null;
                    ClassicSubReleased(released);
                }
            }

            ProcessClassicPageKeys();
#endif
            UpdateClassicTabTooltip();
        }

        /// <summary>
        /// 0x9d3830 cases 0x21 / 0x22 (keys no hotkey took, an edit focused or not): the active window (mgr+0x4efe8) gets msg 3 /
        /// 4 — the chat pages its current box, a separate window its own, by the visible lines − 1 (box msg 9 / 10).
        /// </summary>
        private void ProcessClassicPageKeys() {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || RoClassicMessageBox.ModalOpen || RoClassicShortcutSettings.ShouldSuppressGameplayHotkeys) return;
            bool up = keyboard.pageUpKey.wasPressedThisFrame, down = keyboard.pageDownKey.wasPressedThisFrame;
            if (!up && !down) return;
            RoClassicWindow active = RoClassicUiSystem.Instance?.Windows.ActiveWindow;
            if (active == null) return;
            if (active == classicChat) {
                ClassicChatBox box = ClassicCurrentBox();
                if (box != null && box.Lines.Count > ClassicVisibleLines) ScrollClassicHistory(up ? -(ClassicVisibleLines - 1) : ClassicVisibleLines - 1);
                return;
            }

            foreach (ClassicSubChat s in classicSubs) {
                if (s == null || s.Window != active) continue;
                int visible = SubVisibleLines(s);
                if (s.Box.Lines.Count > visible) ScrollClassicSub(s, up ? -(visible - 1) : visible - 1);
                return;
            }
#endif
        }

        /// <summary>
        /// vf28 0x7f3060 (hover over the strip): a tab whose shown name was cut shows its full name in the tooltip at the tab's x,
        /// 20 px above the strip.
        /// </summary>
        private void UpdateClassicTabTooltip() {
            if (classicChat == null || !classicChat.Visible || classicState == 0 || RoClassicUiSystem.Instance == null) return;
            RoClassicWindowManager windows = RoClassicUiSystem.Instance.Windows;
            if (windows.HoverWindow != classicChat || windows.DragWindow != null) return;
            Vector2Int local = windows.Mouse - classicChat.Position;
            RoChatTabStrip strip = model.Strip;
            if (local.y < 0 || local.y >= RoChatTabStrip.Height || local.x < 3 || local.x >= 3 + strip.ControlWidth) return;
            int tab = strip.HitTest(local.x - 3);
            string full = strip.Tooltip(tab);
            if (full == null) return;
            RoClassicTooltip.Shared.Show(full, ClassicChatWhite, classicChat.Position.x + 3 + strip.TabX(tab), classicChat.Position.y - 20);
        }

        // ---- the battle-message option window's side ----

        internal RoChatModel.RoChatTab SnapshotSubTabForSettings(int sub) => model.SnapshotSubTab(sub);

        /// <summary>The option window's 24 flags for a separate window's tab: kept, and its box made again from the kept lines.</summary>
        internal void SetSubTabFlagsForSettings(int sub, IList<bool> flags) {
            if (!model.SetSubTabFlags(sub, flags)) return;
            if (sub >= 0 && sub < classicSubs.Count && classicSubs[sub] != null) classicSubs[sub].Dirty = true;
            SaveClassicTabs();
        }

        // ---- shared bits ----

        /// <summary>
        /// 0x9bb6e0 (msg 6 / 0x157 of the chat and of a separate window, a double click on a line's sender): the name joins the
        /// whisper names when new, becomes the list's current one and goes into the whisper edit (chat msg 0x27).
        /// </summary>
        private void ClassicWhisperFromLine(string name) {
            if (string.IsNullOrEmpty(name) || classicWhisperEdit == null) return;
            int index = -1;
            for (int i = 0; i < model.WhisperNames.Count; i++) {
                if (string.Equals(model.WhisperNames[i], name, StringComparison.Ordinal)) {
                    index = i;
                    break;
                }
            }

            if (index < 0) {
                model.AddWhisperName(name);
                index = model.WhisperNames.Count - 1;
            }

            model.WhisperIndex = index;
            classicWhisperEdit.SetText(name);
        }

        /// <summary>
        /// The send path's first test (0x899053): the edit's text holding a reserved link tag is refused — the edit emptied,
        /// MsgStr 0xbe2 in red, nothing kept in the history. (The English-only test after it, 0xd01840, passes every text in
        /// this build.)
        /// </summary>
        private bool RefuseReservedClassicLine(string typed) {
            if (!RoChatModel.ContainsReservedLinkTag(typed)) return false;
            classicMessageEdit.SetText(string.Empty);
            classicItemLinks.Clear();
            AddLine(RoClassicUiHost.MsgStr(0xbe2, "The message contains unrecognizable character"), "#ff0000", RoChatChannel.System);
            return true;
        }

        /// <summary>The kept rect of a separate window (".tab&lt;main + key&gt;.x/.y/.w/.h").</summary>
        private void SaveClassicSubRect(ClassicSubChat s) {
            if (s?.Window == null) return;
            string key = ClassicPrefsKey + "tab" + (tabs.Count + s.Key).ToString(System.Globalization.CultureInfo.InvariantCulture);
            PlayerPrefs.SetInt(key + ".x", s.Window.Position.x);
            PlayerPrefs.SetInt(key + ".y", s.Window.Position.y);
            PlayerPrefs.SetInt(key + ".w", s.Window.Width);
            PlayerPrefs.SetInt(key + ".h", s.Window.Height);
        }

        // ---- diagnostics (RoSkillSweepRunner "!chat sub ...") ----

        /// <summary>sub [detach | merge k | close k | move k x y | size k w h | rename k name | page ±1 | addtab] — then each window's state.</summary>
        internal string SweepClassicSubs(string args) {
            string[] a = (args ?? string.Empty).Trim().Split(new[] { ' ' }, 4);
            int Arg(int i) => i < a.Length ? int.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture) : 0;
            ClassicSubChat At(int k) => k >= 0 && k < classicSubs.Count ? classicSubs[k] : null;
            switch (a[0]) {
                case "detach":
                    DetachClassicTab(activeTabIndex);
                    break;
                case "merge":
                    if (At(Arg(1)) != null) MergeClassicSub(At(Arg(1)));
                    break;
                case "close":
                    if (At(Arg(1)) != null) CloseClassicSub(At(Arg(1)));
                    break;
                case "move":
                    At(Arg(1))?.Window.MoveTo(Arg(2), Arg(3));
                    break;
                case "drop":
                    if (At(Arg(1)) != null) ClassicSubReleased(At(Arg(1)));
                    break;
                case "size":
                    if (At(Arg(1)) != null) ResizeClassicSub(At(Arg(1)), Arg(2), Arg(3));
                    break;
                case "rename":
                    if (At(Arg(1)) != null && a.Length > 2) {
                        ClassicSubChat s = At(Arg(1));
                        OnClassicSubDoubleClicked(s, new Vector2Int(2, 2));
                        s.Rename.SetText(a.Length > 3 ? a[2] + " " + a[3] : a[2]);
                        CommitClassicSubRename(s);
                    }

                    break;
                case "addtab":
                    AddClassicTab();
                    break;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("main=").Append(tabs.Count).Append(" cur=").Append(activeTabIndex).Append(" strip=");
            for (int i = 0; i < model.Strip.Count; i++) sb.Append(i == 0 ? string.Empty : "|").Append(model.Strip.Shown(i)).Append('@').Append(model.Strip.TabWidth(i));
            sb.Append(" subs=").Append(model.SubTabCount);
            foreach (ClassicSubChat s in classicSubs) {
                if (s?.Window == null) continue;
                sb.Append(" [").Append(s.Key).Append(" '").Append(s.Name).Append("' tw=").Append(s.TabWidth).Append(" at ").Append(s.Window.Position)
                    .Append(' ').Append(s.Window.Width).Append('x').Append(s.Window.Height).Append(" lines=").Append(s.Box.Lines.Count).Append(']');
            }

            return sb.ToString();
        }
    }
}
