using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The three steps of F11 "Close Tabs" (Interface 16, hotkey 0x74 → UIWindowMgr 0x9d6530; docs/audits/chat-windows-2026-10-04.md
    /// §7), a state at mgr +0x50c and a list of remembered windows at mgr +0x510, both cleared at a map entry (0x9d2220).
    /// Each press first forgets the remembered windows that left the window list (+0x17c), then:
    /// <list type="bullet">
    /// <item>state 0 — Basic Info's msg (6, 0xda) (<see cref="RoClassicCloseTabs.BasicInfoDeletes"/>, the shortcut bar
    /// hidden, Basic Info collapsed); with nothing remembered every shown window is remembered; → 1.</item>
    /// <item>state 1 — every shown window that is not kept (0x9c71c0, <see cref="RoClassicCloseTabs.Keeps"/>) is deleted,
    /// the kept ones are remembered; any deleted → msg (6, 0xda) again, stay. None → every window but the broadcast
    /// boxes (mode +0x5f4 / +0x2b4) hidden, the minimap (0xe) deleted, the player's status icon effect (EF 164) removed; → 2.</item>
    /// <item>state 2 — every shown window but the broadcast boxes (+0x5f4 / +0x614 / +0x2b4) is hidden when remembered,
    /// else deleted; any → the shop boards (0xbd) hidden (0x9bc750(0)), msg (6, 0xda), stay. None → the remembered windows
    /// shown again, the shop boards shown per /showshop, the minimap made again when it is on (mode +0x254), the
    /// status icons back (0xb9a160); → 1.</item>
    /// </list>
    /// </summary>
    public sealed class RoCloseTabsCycle<T> where T : class {
        /// <summary>What a press does to the game (the manager's calls in 0x9d6530).</summary>
        public interface IHost {
            /// <summary>The window list (+0x17c): every window that exists, shown or hidden.</summary>
            void Windows(List<T> into);
            /// <summary>The window's id (+0x2c).</summary>
            int IdOf(T window);
            /// <summary>Its show flag (+0x28).</summary>
            bool IsShown(T window);
            /// <summary>Mode +0x5f4 / +0x2b4 (and +0x614 in state 2): never hidden nor deleted.</summary>
            bool IsException(T window, bool inStateTwo);
            /// <summary>Whether window 0x91 exists and is shown (0x9c71c0 keeps 0x91 only otherwise).</summary>
            bool Window91Shown();
            /// <summary>DeleteWindow(id) of this window.</summary>
            void Delete(T window);
            /// <summary>SetShow (vf14).</summary>
            void SetShow(T window, bool show);
            /// <summary>Basic Info's SendMsg(6, 0xda) when Basic Info exists (mgr +0x1dc).</summary>
            void BasicInfoMessage();
            /// <summary>DeleteWindow(0xe).</summary>
            void DeleteMinimap();
            /// <summary>MakeWindow(0xe) when mode +0x254 == 1.</summary>
            void MakeMinimapIfOn();
            /// <summary>The player's msg 0x6d id 0xa4: EF_STATUS_STATE (the status icons) off.</summary>
            void RemoveStatusIcons();
            /// <summary>0xb9a160: EF_STATUS_STATE launched again.</summary>
            void RelaunchStatusIcons();
            /// <summary>The windows 0xbd (shop boards): SetShow(0) (0x9bc750(0)), or SetShow(/showshop flag) when shown again.</summary>
            void ShopBoards(bool show);
        }

        private readonly List<T> remembered = new List<T>();
        private readonly List<T> list = new List<T>();

        /// <summary>mgr +0x50c.</summary>
        public int State { get; private set; }

        /// <summary>mgr +0x510.</summary>
        public IReadOnlyList<T> Remembered => remembered;

        /// <summary>0x9d2220 at a map entry: state 0, nothing remembered.</summary>
        public void Reset() {
            State = 0;
            remembered.Clear();
            list.Clear();
        }

        /// <summary>One F11.</summary>
        public void Press(IHost host) {
            Collect(host);
            for (int i = remembered.Count - 1; i >= 0; i--) {
                if (!list.Contains(remembered[i])) remembered.RemoveAt(i);
            }

            switch (State) {
                case 0:
                    host.BasicInfoMessage();
                    Collect(host);
                    if (remembered.Count == 0) {
                        foreach (T window in list) {
                            if (host.IsShown(window)) remembered.Add(window);
                        }
                    }

                    State = 1;
                    break;
                case 1:
                    PressInStateOne(host);
                    break;
                case 2:
                    PressInStateTwo(host);
                    break;
            }

            list.Clear();
        }

        private void PressInStateOne(IHost host) {
            bool deleted = false;
            foreach (T window in list) {
                if (!host.IsShown(window)) continue;
                int id = host.IdOf(window);
                if (!RoClassicCloseTabs.Keeps(id, id == 0x91 || host.Window91Shown())) {
                    host.Delete(window);
                    deleted = true;
                } else if (!remembered.Contains(window)) {
                    remembered.Add(window);
                }
            }

            if (deleted) {
                host.BasicInfoMessage();
                return;
            }

            foreach (T window in list) {
                if (!host.IsException(window, false)) host.SetShow(window, false);
            }

            host.ShopBoards(false);   // the boards are windows of the list in the client: the pass above hides them
            host.DeleteMinimap();
            host.RemoveStatusIcons();
            State = 2;
        }

        private void PressInStateTwo(IHost host) {
            bool any = false;
            foreach (T window in list) {
                if (host.IsException(window, true) || !host.IsShown(window)) continue;
                if (remembered.Contains(window)) host.SetShow(window, false);
                else host.Delete(window);
                any = true;
            }

            if (any) {
                host.ShopBoards(false);
                host.BasicInfoMessage();
                return;
            }

            foreach (T window in remembered) host.SetShow(window, true);
            host.ShopBoards(true);
            host.MakeMinimapIfOn();
            host.RelaunchStatusIcons();
            State = 1;
        }

        private void Collect(IHost host) {
            list.Clear();
            host.Windows(list);
        }
    }

    /// <summary>
    /// F11 "Close Tabs" over the classic windows (<see cref="RoCloseTabsCycle{T}"/>). A window is deleted through its own
    /// way out — its close button (0xc5) as if clicked, else what Esc does to it — so a window with a server side sends what
    /// a manual close sends; one with neither is hidden. Hiding is <see cref="RoClassicWindow.HiddenByCloseTabs"/>: the
    /// window stays in the list (remembered), its owner's refreshes keep it hidden, opening it again shows it. The broadcast
    /// boxes (mode +0x2b4 / +0x5f4) are layers here, not windows, so they stay as the client keeps them. The overhead
    /// things the client keeps as windows of the list (the shop and chat room boards, the map signboards) hide with the
    /// shop boards' step; the status icon strip (EF 164) hides and comes back with its own.
    /// </summary>
    public static class RoClassicCloseTabs {
        public const int CloseButtonId = 0xc5;
        public const int MinimapWindowId = 0xe;
        public const int ShortcutBarWindowId = 0x24;
        public const int ShopBoardWindowId = 0xbd;
        /// <summary>UIWindow's id when its maker sets none (ctor 0x9aaba0).</summary>
        public const int DefaultWindowId = 0x248;

        /// <summary>Basic Info's msg (6, 0xda) (vf37 0x8fae90): DeleteWindow of these ids, in this order.</summary>
        public static readonly int[] BasicInfoDeletes = { 0xb, 0xd, 8, 0x2718, 10, 0x45, 0x1b, 0x1e, 0x25, 0x26, 0x58, 0x56, 0x57, 0x28, 0x3b };

        private static readonly RoCloseTabsCycle<RoClassicWindow> Cycle = new RoCloseTabsCycle<RoClassicWindow>();
        private static readonly Host GameHost = new Host();
        private static readonly List<RoClassicWindow> Scratch = new List<RoClassicWindow>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            Cycle.Reset();
            OverheadHidden = false;
            StatusIconsHidden = false;
        }

        /// <summary>The shop / chat room boards and the map signboards are hidden (state 2).</summary>
        public static bool OverheadHidden { get; private set; }

        /// <summary>EF_STATUS_STATE is off: the status icon strip is hidden (state 2).</summary>
        public static bool StatusIconsHidden { get; private set; }

        /// <summary>mgr +0x50c.</summary>
        public static int State => Cycle.State;

        /// <summary>0x9c71c0: the windows the second step keeps (0x91 only while it is not shown).</summary>
        public static bool Keeps(int id, bool window91Shown) {
            if (id < 0x102) {
                switch (id) {
                    case 0: case 1: case 2: case 3: case 4: case 6: case 7: case 9: case 0xe: case 0xf: case 0x10: case 0x11:
                    case 0x12: case 0x14: case 0x15: case 0x16: case 0x17: case 0x18: case 0x1a: case 0x1c: case 0x1d: case 0x1f:
                    case 0x20: case 0x21: case 0x22: case 0x23: case 0x24: case 0x27: case 0x2a: case 0x2d: case 0x31: case 0x32:
                    case 0x38: case 0x3a: case 0x3c: case 0x3d: case 0x3e: case 0x3f: case 0x40: case 0x41: case 0x42: case 0x43:
                    case 0x46: case 0x49: case 0x4a: case 0x4e: case 0x4f: case 0x50: case 0x51: case 0x5a: case 0x5b: case 0x5e:
                    case 0x5f: case 0x63: case 0x64: case 0x65: case 0x68: case 0x69: case 0x6b: case 0x6d: case 0x6f: case 0x73:
                    case 0x74: case 0x7b: case 0x7c: case 0x7f: case 0x80: case 0x82: case 0x83: case 0x85: case 0x88: case 0x89:
                    case 0x8a: case 0x90: case 0x9a: case 0x9c: case 0x9e: case 0x9f: case 0xa0: case 0xa1: case 0xa4: case 0xaf:
                    case 0xb0: case 0xb1: case 0xb3: case 0xb4: case 0xb6: case 0xb7: case 0xba: case 0xbb: case 0xbd: case 0xbe:
                    case 0xc4: case 0xc6: case 0xc7: case 0xc8: case 0xcc: case 0xd8: case 0xd9: case 0xda: case 0xdb: case 0xdc:
                    case 0xdd: case 0xde: case 0xdf: case 0xe0: case 0xe1: case 0xe2: case 0xe3: case 0xfd: case 0x101:
                        return true;
                    case 0x91:
                        return !window91Shown;
                    default:
                        return false;
                }
            }

            switch (id) {
                case 0x102: case 0x106: case 0x10b: case 0x10c: case 0x10d: case 0x10f: case 0x115: case 0x116: case 0x11c:
                case 0x11d: case 0x11e: case 0x121: case 0x127: case 0x12d: case 0x133: case 0x145: case 0x14c: case 0x157:
                case 0x166: case 0x167: case 0x248: case 0x271b:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// A DeleteWindow id against a window's id: the same id, or a wrapper's windows — 0x45 (the Party / Friends entry
        /// of the menu) makes the messenger window 0x22, 0x3b (Guild) the guild tabs 0x3c..0x42.
        /// </summary>
        public static bool DeleteMatches(int deleteId, int windowId) =>
            deleteId == windowId || (deleteId == 0x45 && windowId == 0x22) || (deleteId == 0x3b && windowId >= 0x3c && windowId <= 0x42);

        /// <summary>The client's id of a classic window (its layout's; a shop board's is 0xbd, set by its ctor 0x7b2280).</summary>
        public static int OriginalId(RoClassicWindow window) {
            RoClassicWindowLayout layout = window.Layout;
            if (layout.Class == "UIMerchantShopTitle") return ShopBoardWindowId;
            return layout.WindowId >= 0 ? layout.WindowId : DefaultWindowId;
        }

        /// <summary>DeleteWindow through the window's own way out: its close button as if clicked, else its Esc, else hidden.</summary>
        public static void Close(RoClassicWindow window) {
            List<RoClassicChildLayout> children = window.Layout.Children;
            for (int i = 0; i < children.Count; i++) {
                RoClassicChildLayout child = children[i];
                if (child.Id != CloseButtonId || !child.Visible) continue;
                window.RaiseClicked(i);
                return;
            }

            if (window.EscClose != null) {
                window.EscClose();
                return;
            }

            window.Visible = false;
        }

        /// <summary>F11 (Interface 16 "Close Tabs"); nothing while a modal box runs its own loop.</summary>
        public static bool Press() {
            if (RoClassicUiSystem.Instance == null || RoClassicMessageBox.ModalOpen) return false;
            Cycle.Press(GameHost);
            return true;
        }

        /// <summary>
        /// A map entry (0x9d2220 deletes every window and clears the state and the list; the game windows are made again):
        /// what F11 hid shows again, state 0.
        /// </summary>
        public static void ResetForMap() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system != null) {
                foreach (RoClassicWindow window in system.Windows.Windows) {
                    if (window.HiddenByCloseTabs) window.CloseTabsSetShow(true);
                }
            }

            OverheadHidden = false;
            StatusIconsHidden = false;
            RoClassicMinimap.CloseTabsMake();
            Cycle.Reset();
        }

        /// <summary>Diagnostics: the state, the remembered windows and the hidden ones.</summary>
        public static string Describe() {
            var sb = new System.Text.StringBuilder();
            sb.Append("state=").Append(Cycle.State).Append(" overhead=").Append(OverheadHidden ? "hidden" : "shown")
                .Append(" statusIcons=").Append(StatusIconsHidden ? "hidden" : "shown").Append(" remembered=[");
            for (int i = 0; i < Cycle.Remembered.Count; i++) {
                if (i > 0) sb.Append(", ");
                RoClassicWindow w = Cycle.Remembered[i];
                sb.Append(w.Layout.Class).Append(" 0x").Append(OriginalId(w).ToString("x"));
            }

            sb.Append("] hidden=[");
            bool first = true;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system != null) {
                foreach (RoClassicWindow w in system.Windows.Windows) {
                    if (!w.HiddenByCloseTabs) continue;
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(w.Layout.Class);
                }
            }

            return sb.Append(']').ToString();
        }

        private sealed class Host : RoCloseTabsCycle<RoClassicWindow>.IHost {
            public void Windows(List<RoClassicWindow> into) {
                RoClassicUiSystem system = RoClassicUiSystem.Instance;
                if (system == null) return;
                foreach (RoClassicWindow window in system.Windows.Windows) {
                    if (window.Visible || window.HiddenByCloseTabs) into.Add(window);
                }
            }

            public int IdOf(RoClassicWindow window) => OriginalId(window);

            public bool IsShown(RoClassicWindow window) => window.Visible;

            // the broadcast boxes are RoClassicBroadcast's layers, not windows of the list
            public bool IsException(RoClassicWindow window, bool inStateTwo) => false;

            public bool Window91Shown() {
                RoClassicUiSystem system = RoClassicUiSystem.Instance;
                if (system == null) return false;
                foreach (RoClassicWindow window in system.Windows.Windows) {
                    if (OriginalId(window) == 0x91) return window.Visible;
                }

                return false;
            }

            public void Delete(RoClassicWindow window) => Close(window);

            public void SetShow(RoClassicWindow window, bool show) => window.CloseTabsSetShow(show);

            public void BasicInfoMessage() {
                RoClassicUiSystem system = RoClassicUiSystem.Instance;
                if (system == null || !RoClassicBasicInfo.IsBound) return;
                foreach (int id in BasicInfoDeletes) {
                    Scratch.Clear();
                    foreach (RoClassicWindow window in system.Windows.Windows) {
                        if (window.Visible && DeleteMatches(id, OriginalId(window))) Scratch.Add(window);
                    }

                    foreach (RoClassicWindow window in Scratch) Close(window);
                }

                // ShowWindow(0x24, 0) (0x9dacd0): the shortcut bar and its quick slots hidden, its shown flag (mgr +0x75c) off
                Scratch.Clear();
                foreach (RoClassicWindow window in system.Windows.Windows) {
                    if (window.Visible && OriginalId(window) == ShortcutBarWindowId) Scratch.Add(window);
                }

                foreach (RoClassicWindow window in Scratch) Close(window);
                Scratch.Clear();
                RoClassicBasicInfo.Collapse();   // 0x8f99c0(+0x25)
            }

            public void DeleteMinimap() => RoClassicMinimap.CloseTabsDelete();

            public void MakeMinimapIfOn() => RoClassicMinimap.CloseTabsMake();

            public void RemoveStatusIcons() => StatusIconsHidden = true;

            public void RelaunchStatusIcons() => StatusIconsHidden = false;

            public void ShopBoards(bool show) => OverheadHidden = !show;
        }
    }
}
