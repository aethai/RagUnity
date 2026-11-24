using System;
using System.Globalization;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The amount window UIItemDropCntWnd (id 15, oracle layout; ui/RUNTIME-RULES-ITEMS.md, RUNTIME-RULES-STORE-SHOP.md
    /// §3f): one window shared by every mode (drop, put into / take from storage, shop carts). The item's name at
    /// (15, 6), an edit field at (15, 22) holding the amount, OK / Enter hands the parsed number to the caller, Esc
    /// closes it. At (W/2 − 120, H·5/12).
    /// </summary>
    internal static class RoClassicCountWindow {
        private const string LayoutPath = "Ragnarok/Generated/OriginalClient/ui/UIItemDropCntWnd";
        private const int OkId = 180;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static RoClassicWindow window;
        private static RoClassicEditField edit;
        private static string label;
        private static Action<long> confirm;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            window = null;
            edit = null;
            label = null;
            confirm = null;
        }

        public static bool Visible => window != null && window.Visible && confirm != null;

        public static string Text => edit != null ? edit.Text : string.Empty;

        /// <summary>Opens the window for <paramref name="itemName"/> with <paramref name="amount"/> typed in.</summary>
        public static void Show(string itemName, long amount, Action<long> onConfirm) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (window == null) {
                var asset = Resources.Load<TextAsset>(LayoutPath);
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrl") child.Visible = false; // the native edit field draws instead
                }

                window = new RoClassicWindow(system, layout) { Draggable = false };
                edit = new RoClassicEditField { Rect = new RectInt(15, 22, 80, 16), Background = new Color32(0xe8, 0xe8, 0xe8, 255), HandlesEscape = true };
                window.AddEditField(edit);
                edit.Submitted += _ => Confirm();
                edit.Cancelled += _ => Hide();
                window.Clicked += child => {
                    if (child.Id == OkId) Confirm();
                };
                window.DrawDynamic += w => {
                    if (label == null) return;
                    w.ClipTo(15, 6, 100, 15);   // the UIStaticText label (+0x98)
                    w.TextOut(15, 6, label, 0, 12, Black);
                    w.ResetClip();
                };
            }

            label = itemName ?? string.Empty;
            confirm = onConfirm;
            Vector2Int client = system.ClientSize;
            window.MoveTo(client.x / 2 - 120, client.y * 5 / 12);   // x = W/2 − 120; y = H·5/12
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.SetText(amount.ToString(CultureInfo.InvariantCulture));
            edit.Focus();
            window.Invalidate();
        }

        /// <summary>OK / Enter (msg 6, 0xb4): the typed number goes to the caller, which applies its mode's limits.</summary>
        public static void Confirm() {
            Action<long> action = confirm;
            string text = edit != null ? edit.Text.Trim() : string.Empty;
            Hide();
            if (action != null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n)) action(n);
        }

        public static void Hide() {
            confirm = null;
            label = null;
            if (window == null || !window.Visible) return;
            if (edit.HasFocus) RoClassicTextInput.Focus(null);
            window.Visible = false;
        }

        /// <summary>Diagnostics: types <paramref name="text"/> into the field.</summary>
        public static void SetText(string text) => edit?.SetText(text ?? string.Empty);
    }
}
