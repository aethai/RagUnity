using Ragnarok.Data;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The card illustration UICardItemIllustWnd 0x2f (item-windows-spec §2): the description's view button 0xf6 makes it
    /// (300 × 417 at ((W − 640) / 2, 0) when new, not remembered) and sends msg 0x3e(id string, identified): a card with an
    /// illustration (num2cardillustnametable) replaces the picture and comes to the front, the same card again or one
    /// without an illustration closes it. OnDraw 0x84c5f0: the three-part title bar, cardBmp\&lt;name&gt;.bmp (300 × 400) at
    /// (0, 17) unkeyed, the item name at (10, 3) black on a white shadow; the close button at (286, 3). It drags from
    /// anywhere.
    /// </summary>
    public sealed class RoClassicCardIllust {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int W = 300, H = 417, CloseId = 0xc5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static RoClassicCardIllust instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow window;
        private RoItemInstance item;
        private RoItemMetadata metadata;
        private RoItemMetadataTable table;
        private string picture;

        public static void Toggle(RoItemInstance shown, RoItemMetadata meta, RoItemMetadataTable source) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || shown == null || meta == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicCardIllust { system = system };
            instance.Msg3e(shown, meta, source);
        }

        private void Msg3e(RoItemInstance shown, RoItemMetadata meta, RoItemMetadataTable source) {
            bool open = window != null && window.Visible;
            string name = meta.CardIllustrationResourceName;
            // a new card with an illustration: the picture swaps in place; the same card again or none: DeleteWindow(0x2f)
            if (string.IsNullOrWhiteSpace(name) || (open && item != null && item.ItemId == shown.ItemId)) {
                if (open) window.Visible = false;
                item = null;
                return;
            }

            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UICardItemIllustWnd", WindowId = 0x2f, Width = W, Height = H };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = W - 14, Y = 3, W = 11, H = 11 };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = _ => true };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == CloseId) Close();
                };
                window.EscClose = () => {
                    Close();
                    return true;
                };
            }

            item = shown;
            metadata = meta;
            table = source ?? RoItemMetadataTable.Empty;
            picture = Ui + "cardBmp\\" + name + ".bmp";
            if (!open) {
                // MakeWindow(0x2f) of a new window: (scaledX(0), scaledY(0))
                window.MoveTo((system.ClientSize.x - 640) / 2, 0);
                window.Visible = true;
            }

            system.Windows.BringToFront(window);
            system.Windows.PushEsc(window);
            window.Invalidate();
        }

        private void Close() {
            if (window != null) window.Visible = false;
            item = null;
        }

        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            if (picture != null) w.DrawBitmapRaw(0, 17, picture);
            if (item == null) return;
            // ItemName 0x82dc10 at (10, 3): black, a white shadow at (+1, +1), cut to W − 10 − x
            string name = item.GetFullDisplayName(metadata, table) ?? string.Empty;
            RoClassicGdiFont font = w.FontFor(0, 12);
            int width = W - 10 - 10;
            if (font.Measure(name) > width) {
                int cut = name.Length;
                while (cut > 0 && font.Measure(name.Substring(0, cut) + "...") > width) cut--;
                name = name.Substring(0, cut) + "...";
            }

            w.TextOut(11, 4, name, 0, 12, White);
            w.TextOut(10, 3, name, 0, 12, Black);
        }

        internal static string Describe() =>
            instance?.window == null || !instance.window.Visible ? "closed" : "card=" + instance.item?.ItemId + " picture=" + instance.picture + " at=" + instance.window.Position;
    }
}
