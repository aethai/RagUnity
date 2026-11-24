using System.Collections.Generic;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The round-icon menu docked under Basic Info: the original client's UIMenuIconWnd (window id 307), built from its
    /// OnCreate (0x7ad070 + Custom 0x233d770) and OnDraw (0x7ae200) as reverse-engineered in reference ui-static
    /// ui/RUNTIME-RULES.md §2 (the oracle cannot draw it: it is a render-target window). An icon toggles its window through
    /// the icon of the same action in <see cref="RoBasicInfoIconRegistry"/> (the one the Alt / Ctrl hotkeys use).
    /// </summary>
    public sealed partial class RoClassicBasicInfo {
        private const string MenuIconRoot = "유저인터페이스\\menu_icon\\";
        private const int MenuWidth = 220;
        private const int MenuHeight = 197;
        private const int MenuOpenButtonId = 0x1fa;
        private const int MenuCloseButtonId = 0xc5;

        /// <summary>
        /// The 15 icons left after the client's filter, in its order (n → x = 9 + 42·(n % 5), y = 23 + 43·(n / 5)):
        /// bitmap name, the client's icon id, and the registry icon that opens the same window (rec: none).
        /// </summary>
        private static readonly (string Name, int Id, string Action)[] ClassicMenuIcons = {
            ("status", 0xbc, "info"), ("equip", 0xbf, "equip"), ("item", 0xbe, "item"), ("skill", 0xc0, "skill"),
            ("party", 0xc3, "party"), ("guild", 0x171, "guild"), ("quest", 0x165, "quest"), ("map", 0xd7, "map"),
            ("navigation", 0x1aa, "navigation"), ("option", 0xbd, "option"), ("bank", 0x1c9, "bank"), ("rec", 0x18b, null),
            ("mail", 0x1d8, "mail"), ("achievement", 0x1d5, "achievment"), ("attendance", 0x218, "attendance"),
        };

        /// <summary>
        /// UIMenuIcon::vf27 0x7ae600 by icon id: the name's MsgStr and the window GetBeHaviorOfWindow asks about (−1: option,
        /// whose text is "%s (Esc)").
        /// </summary>
        private static readonly Dictionary<int, (int Name, int Window)> ClassicMenuTooltips = new Dictionary<int, (int, int)> {
            [0xbc] = (0x69, 0xb), [0xbf] = (0x68, 0xa), [0xbe] = (0x6a, 0x8), [0xc0] = (0x11b, 0x25), [0xc3] = (0x67, 0x45),
            [0x171] = (0xc3e, 0x3b), [0x165] = (0x525, 0x86), [0xd7] = (0xb2b, 0x8c), [0x1aa] = (0x931, 0xcb), [0xbd] = (0x109, -1),
            [0x1c9] = (0xc3f, 0xfb), [0x18b] = (0x92e, 0xc6), [0x1d8] = (0xc40, 0x107), [0x1d5] = (0xa56, 0x10e), [0x218] = (0xd91, 0x148),
        };

        /// <summary>The icon's balloon: its name, then " (" + the window's hotkey + ")" when one is bound.</summary>
        private static string ClassicMenuTooltip(RoClassicChildLayout child) {
            if (!ClassicMenuTooltips.TryGetValue(child.Id, out (int Name, int Window) tip)) return null;
            string name = RoClassicUiHost.MsgStr(tip.Name, string.Empty);
            if (tip.Window < 0) return name + " (Esc)";
            string hotkey = RoClassicUiSystem.WindowHotkeyText?.Invoke(tip.Window) ?? string.Empty;
            return hotkey.Length > 0 ? name + " (" + hotkey + ")" : name;
        }

        /// <summary>g_menuExpanded (DAT_01555604): outlives the window, collapsed when the client starts.</summary>
        private static bool classicMenuExpanded;

        private RoClassicWindow classicMenu;
        private RoClassicChildLayout menuBackground;
        private RoClassicChildLayout menuOpenStrip;
        private RoClassicChildLayout menuCloseStrip;
        private readonly List<RoClassicChildLayout> menuIconNodes = new List<RoClassicChildLayout>();
        private RoClassicChildLayout menuNewStrip;
        private bool lastAchievementBadge;

        /// <summary>The icons that have a "new" variant (status, item, skill, achievement, mail), by icon id.</summary>
        private readonly Dictionary<int, RoClassicChildLayout> menuNewIcons = new Dictionary<int, RoClassicChildLayout>();

        /// <summary>The alarm list (+0xa4): icons showing their "new" variant.</summary>
        private readonly HashSet<int> menuBadges = new HashSet<int>();

        /// <summary>The sweep console's !menu tips: every icon's balloon text.</summary>
        public static string DescribeClassicMenuTooltips() {
            var s = new System.Text.StringBuilder();
            foreach ((string name, int id, _) in ClassicMenuIcons) {
                string tip = ClassicMenuTooltip(new RoClassicChildLayout { Id = id });
                s.Append(s.Length > 0 ? " | " : string.Empty).Append(name).Append('=').Append(tip ?? "-");
            }

            return s.ToString();
        }

        /// <summary>Opens / collapses the classic menu like its strip buttons (diagnostics console).</summary>
        public static void SetClassicMenuExpanded(bool expanded) {
            classicMenuExpanded = expanded;
            if (instance?.classicMenu != null) instance.ApplyClassicMenuState();
        }

        /// <summary>
        /// A menu icon by its registry id (info, equip, item, skill, ...) as if clicked (diagnostics console); false when
        /// there is no such icon or Basic Info is not bound.
        /// </summary>
        public static bool ToggleMenuIcon(string id) {
            IRoBasicInfoIcon icon = RoBasicInfoIconRegistry.Find(id);
            if (icon == null || instance == null) return false;
            icon.Toggle(instance.BuildIconContext());
            return true;
        }

        /// <summary>The context a menu icon toggles with (the shell's hotkeys build the same one).</summary>
        internal static RoBasicInfoIconContext BuildMenuIconContext() {
            return instance != null ? instance.BuildIconContext() : new RoBasicInfoIconContext(null, null, null);
        }

        private RoBasicInfoIconContext BuildIconContext() {
            return new RoBasicInfoIconContext(session, worldController, iconHost);
        }

        private void EnsureClassicMenu(RoClassicUiSystem system) {
            if (classicMenu != null) return;
            classicMenu = new RoClassicWindow(system, BuildClassicMenuLayout()) { Draggable = false, ChildTooltip = ClassicMenuTooltip };
            classicMenu.Clicked += OnClassicMenuClicked;
            ApplyClassicMenuState();
        }

        private RoClassicWindowLayout BuildClassicMenuLayout() {
            var layout = new RoClassicWindowLayout { Class = "UIMenuIconWnd", WindowId = 307, Width = MenuWidth, Height = MenuHeight };
            // bg_menu.tga (overlay, black at alpha 128): drawn into the window's render target under the icons
            menuBackground = new RoClassicChildLayout { Class = "UIMenuIconBackground", X = 0, Y = 13, W = 220, H = 136 };
            menuBackground.Ops.Add(new RoClassicOp { Kind = RoClassicOpKind.Bitmap, Path = MenuIconRoot + "bg_menu.tga" });
            layout.Children.Add(menuBackground);
            menuIconNodes.Clear();
            menuNewIcons.Clear();
            for (int n = 0; n < ClassicMenuIcons.Length; n++) {
                (string name, int id, _) = ClassicMenuIcons[n];
                RoClassicChildLayout icon = Button(id, 9 + 42 * (n % 5), 23 + 43 * (n / 5), 32, 34,
                    MenuIconRoot + "bt_" + name + ".bmp", MenuIconRoot + "bt_" + name + ".bmp", MenuIconRoot + "bt_" + name + "_press.bmp");
                layout.Children.Add(icon);
                menuIconNodes.Add(icon);
                if (id == 0xbc || id == 0xbe || id == 0xc0 || id == 0x1d5 || id == 0x1d8) {
                    // its "new" variant, 35x40 at (x, y - 6), same id
                    RoClassicChildLayout fresh = Button(id, icon.X, icon.Y - 6, 35, 40, MenuIconRoot + "bt_" + name + "_new.bmp",
                        MenuIconRoot + "bt_" + name + "_new.bmp", MenuIconRoot + "bt_" + name + "_new_press.bmp");
                    layout.Children.Add(fresh);
                    menuNewIcons[id] = fresh;
                }
            }

            menuOpenStrip = Button(MenuOpenButtonId, 0, 4, 219, 9,
                MenuIconRoot + "bt_menu_normal.bmp", MenuIconRoot + "bt_menu_over.bmp", MenuIconRoot + "bt_menu_press.bmp");
            menuCloseStrip = Button(MenuCloseButtonId, 0, 4, 219, 9,
                MenuIconRoot + "bt_menu_close_normal.bmp", MenuIconRoot + "bt_menu_close_over.bmp", MenuIconRoot + "bt_menu_close_press.bmp");
            menuNewStrip = Button(MenuOpenButtonId, 0, 0, 219, 13,
                MenuIconRoot + "bt_menu_new_normal.bmp", MenuIconRoot + "bt_menu_new_over.bmp", MenuIconRoot + "bt_menu_new_press.bmp");
            layout.Children.Add(menuOpenStrip);
            layout.Children.Add(menuNewStrip);
            layout.Children.Add(menuCloseStrip);
            return layout;
        }

        private static RoClassicChildLayout Button(int id, int x, int y, int w, int h, string normal, string hover, string pressed) {
            var button = new RoClassicChildLayout { Class = "UIMenuIcon", Id = id, X = x, Y = y, W = w, H = h };
            button.States["normal"] = normal;
            button.States["hover"] = hover;
            button.States["pressed"] = pressed;
            return button;
        }

        /// <summary>
        /// OnDraw 0x7ae200: expanded = close strip, background and icons (an icon on the alarm list shows its "new"
        /// variant instead); collapsed = only the open strip (bt_menu_new while the alarm list is not empty), which is
        /// also the only part that takes the mouse (HitTest 0x7acf50).
        /// </summary>
        private void ApplyClassicMenuState() {
            bool open = classicMenuExpanded, alarm = menuBadges.Count > 0;
            classicMenu.SetChildVisible(menuOpenStrip, !open && !alarm);
            classicMenu.SetChildVisible(menuNewStrip, !open && alarm);
            classicMenu.SetChildVisible(menuCloseStrip, open);
            classicMenu.SetChildVisible(menuBackground, open);
            foreach (RoClassicChildLayout icon in menuIconNodes) {
                bool badge = menuBadges.Contains(icon.Id);
                classicMenu.SetChildVisible(icon, open && !badge);
                if (menuNewIcons.TryGetValue(icon.Id, out RoClassicChildLayout fresh)) classicMenu.SetChildVisible(fresh, open && badge);
            }

            classicMenu.HitArea = open ? (RectInt?) null : new RectInt(0, 0, 219, 13);
        }

        /// <summary>Between refreshes: the achievement badge is set and cleared outside the basic info sequence.</summary>
        private void TickMenuBadges() {
            if (RoClassicAchievements.HasNewBadge != lastAchievementBadge) UpdateClassicMenuBadges();
        }

        /// <summary>The alarm list from the icons' notifications (unspent points, unread Rodex mail, a new achievement).</summary>
        private void UpdateClassicMenuBadges() {
            lastAchievementBadge = RoClassicAchievements.HasNewBadge;
            if (classicMenu == null) return;
            RoBasicInfoIconContext ctx = BuildIconContext();
            bool changed = false;
            foreach ((_, int id, string action) in ClassicMenuIcons) {
                // only the five icons with a "new" variant ever get on the client's alarm list
                if (action == null || !menuNewIcons.ContainsKey(id)) continue;
                bool on = RoBasicInfoIconRegistry.Find(action) is IRoBasicInfoNotificationIcon icon && icon.HasNewNotification(ctx);
                changed |= on ? menuBadges.Add(id) : menuBadges.Remove(id);
            }

            if (changed) ApplyClassicMenuState();
        }

        /// <summary>UIMenuIconWnd::Move (0x7ad040) keeps y − 4; Basic Info re-docks it at (x, y + its height).</summary>
        private void DockClassicMenu() {
            if (classicMenu == null || classicWindow == null) return;
            classicMenu.MoveTo(classicWindow.Position.x, classicWindow.Position.y + classicWindow.Height - 4);
        }

        private void OnClassicMenuClicked(RoClassicChildLayout child) {
            if (child.Id == MenuOpenButtonId || child.Id == MenuCloseButtonId) {
                classicMenuExpanded = child.Id == MenuOpenButtonId;
                ApplyClassicMenuState();
                return;
            }

            foreach ((_, int id, string action) in ClassicMenuIcons) {
                if (id != child.Id) continue;
                IRoBasicInfoIcon icon = action != null ? RoBasicInfoIconRegistry.Find(action) : null;
                icon?.Toggle(BuildIconContext());
                return;
            }
        }
    }
}
