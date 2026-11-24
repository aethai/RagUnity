using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// /automsg (/am, CGameMode::SendMsg 0x2a case 0x97) and its window UIAutoMessageWnd 0x74 (ctor 0x8241b0, docs/audits/
    /// chat-options-2026-10-04.md): the command flips [0x01552ff0]; turned on it makes the window, 280 × 48 at
    /// (scaledX(185), scaledY(180)) (MakeWindow 0x9ca0c1), with the sysbox frame (0x809d50), MsgStr 0x3b0 "Please enter Away
    /// Message." at (15, 6), an edit at (15, 22) W − 77 wide holding the away message ([0x01552ff4], 100 bytes, inner
    /// #E8E8E8) and OK (btn_ok, id 0xb4, the default button) at (W − 55, H − 32). OK / Enter keeps the text, prints MsgStr
    /// 0x3ae in #FFFF64 and closes the window. While the flag is on every whisper that does not start with MsgStr 0x3ad
    /// "[Automated Message]" is answered with that text and the away message (0xc3ccb9).
    /// </summary>
    internal static class RoClassicAutoMessage {
        private const string Ui = "유저인터페이스\\";
        private const int OkId = 0xb4;
        private const int W = 280;
        private const int H = 48;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static RoClassicWindow window;
        private static RoClassicEditField edit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            window = null;
            edit = null;
            Enabled = false;
            Message = string.Empty;
        }

        /// <summary>[0x01552ff0]: away replies on.</summary>
        public static bool Enabled { get; private set; }

        /// <summary>[0x01552ff4]: the away message (kept for this run of the game).</summary>
        public static string Message { get; private set; } = string.Empty;

        /// <summary>The /am case: flips the flag; on → the window opens (true), off → the caller prints MsgStr 0x3af (false).</summary>
        public static bool Toggle() {
            Enabled = !Enabled;
            if (!Enabled) {
                Hide();
                return false;
            }

            Show();
            return true;
        }

        /// <summary>The reply to a whisper while away (null when there is none).</summary>
        public static string ReplyTo(string received) =>
            RoChatOptionCommands.TryBuildAwayReply(Enabled, Message, received, RoClassicUiHost.MsgStr(0x3ad, "[Automated Message]"), out string reply)
                ? reply
                : null;

        private static void Show() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UIAutoMessageWnd", WindowId = 0x74, Width = W, Height = H };
                var ok = new RoClassicChildLayout { Class = "UIBitmapButton", Id = OkId, X = W - 55, Y = H - 32, W = 42, H = 20 };
                ok.States["normal"] = Ui + "btn_ok.bmp";
                ok.States["hover"] = Ui + "btn_ok_a.bmp";
                ok.States["pressed"] = Ui + "btn_ok_b.bmp";
                layout.Children.Add(ok);
                window = new RoClassicWindow(system, layout) { Visible = false, FrameDragArea = local => local.y < 17 };
                edit = new RoClassicEditField { Rect = new RectInt(15, 22, W - 77, 16), MaxBytes = 100, Background = new Color32(0xe8, 0xe8, 0xe8, 255) };
                window.AddEditField(edit);
                edit.Submitted += _ => Confirm();
                window.Clicked += child => {
                    if (child.Id == OkId) Confirm();
                };
                // OnDraw 0x84c4c0: the frame box 0x809d50(0, 0, 0, 0), then the prompt
                window.DrawDynamic += w => {
                    w.DrawSysBox(Ui + "sysbox_", 0, 0, w.Width, w.Height, false);
                    w.TextOut(15, 6, RoClassicUiHost.MsgStr(0x3b0, "Please enter Away Message."), 0, 12, Black);
                };
            }

            Vector2Int client = system.ClientSize;
            window.MoveTo((client.x - 640) / 2 + 185, client.y * 180 / 480);   // (scaledX(0xb9), scaledY(0xb4))
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.SetText(Message);
            edit.Focus();
            window.Invalidate();
        }

        /// <summary>vf37 0x8572e0 msg 6 / 0xb4: the edit's text is the away message, MsgStr 0x3ae, DeleteWindow(0x74).</summary>
        private static void Confirm() {
            Message = edit != null ? edit.Text : string.Empty;
            RoClassicChat.Instance?.AddLine(RoClassicUiHost.MsgStr(0x3ae, "Send an automated message while you are away."),
                RoChatSlashCommands.OptionColor, Ragnarok.Network.RoChatChannel.System);
            Hide();
        }

        private static void Hide() {
            if (window == null || !window.Visible) return;
            if (edit.HasFocus) RoClassicTextInput.Focus(null);
            window.Visible = false;
        }
    }
}
