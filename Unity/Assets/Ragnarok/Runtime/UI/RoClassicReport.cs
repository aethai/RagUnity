using System;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// "Report a User" (player-menu-rest-spec §7): the menu's red entry makes CUIMacroReport 0x2715 (ctor 0x990970), 231 × 189
    /// centred: the title bar with "Report", MacroReport\bg_macroreport.bmp at y 17, the radio buttons "Illegal programs"
    /// (type 0) and "Bug &amp; exploits" (1), a 215 × 58 multi-line edit (100 characters, placeholder "Enter report content."),
    /// the red note "Abuse may be punished." and Report / Cancel. Report (0x990bd0) sends 0x0be2 {my AID, target AID,
    /// target name[24], u16 type, text[101]} (137 bytes, no checks, no answer) and closes. The server registers 0x0be2 only
    /// from packet version 20240619.
    /// </summary>
    public sealed class RoClassicReport {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const int W = 231, H = 189;
        private const int CloseId = 0xc5, ReportId = 0x1d0, CancelId = 0xb5;
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Placeholder = new Color32(0x80, 0x80, 0x80, 255);
        private static readonly Color32 EditBack = new Color32(0xEE, 0xF3, 0xFF, 255);
        private static readonly Color32 EditBorder = new Color32(0xBD, 0xC5, 0xD6, 255);
        private static readonly Color32 Note = new Color32(0xF7, 0x35, 0x2D, 255);

        private static RoClassicReport instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicWindow window;
        private RoClassicEditField edit;
        private uint targetAid;   // + 0xe0 (mode + 0x2e8)
        private string targetName = string.Empty;   // + 0xe8 (mode + 0x2f4)
        private int type;   // + 0xe4

        /// <summary>MakeWindow 0x2715 for the menu's target; vf16 0x990dc0 takes the target and resets the type to 0.</summary>
        public static void Open(RoLoginShellSession loginSession, uint aid, string name) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicReport { system = system };
            RoClassicReport r = instance;
            r.session = loginSession;
            r.targetAid = aid;
            r.targetName = name ?? string.Empty;
            r.type = 0;
            r.Show();
        }

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h, string normal, string over, string press) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            b.States["normal"] = Basic + normal;
            b.States["hover"] = Basic + over;
            b.States["pressed"] = Basic + press;
            return b;
        }

        private void Show() {
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "CUIMacroReport", WindowId = 0x2715, Width = W, Height = H };
                layout.Children.Add(Button(CloseId, "close", 217, 3, 11, 11, "sys_close_off.bmp", "sys_close_on.bmp", "sys_close_on.bmp"));
                layout.Children.Add(Button(ReportId, "report", 55, 165, 58, 20, "btn_report_normal.bmp", "btn_report_over.bmp", "btn_report_press.bmp"));
                layout.Children.Add(Button(CancelId, "cancel", 117, 165, 58, 20, "btn_cancel2_normal.bmp", "btn_cancel2_over.bmp", "btn_cancel2_press.bmp"));
                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += child => {
                    if (child.Id == ReportId) Send();
                    else if (child.Id == CloseId || child.Id == CancelId) window.Visible = false;
                };
                window.Pressed += (w, p) => {
                    // the radio buttons 12 × 12 at (9, 25) / (9, 43) with their 165-px labels (0x990e20)
                    for (int i = 0; i < 2; i++) {
                        if (p.x >= 9 && p.x < 28 + 165 && p.y >= 25 + 18 * i && p.y < 37 + 18 * i && type != i) {
                            type = i;
                            w.Invalidate();
                        }
                    }
                };
                window.EscClose = () => {
                    window.Visible = false;
                    return true;
                };
                edit = new RoClassicEditField {
                    Rect = new RectInt(10, 82, 211, 54), MaxBytes = 100, Multiline = true, Background = EditBack, LineHeight = 14, ScrollBarWidth = 0
                };
                edit.Changed += _ => window.Invalidate();
                window.AddEditField(edit);
            }

            edit.SetText(string.Empty);
            Vector2Int c = system.ClientSize;
            window.MoveTo((c.x - W) / 2, (c.y - H) / 2);
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.Focus();
            window.Invalidate();
        }

        /// <summary>OnCreate 0x98e960's controls.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            w.TextOut(18, 3, "Report", 0, 12, White);
            w.DrawBitmapRaw(0, 17, Ui + "MacroReport\\bg_macroreport.bmp");
            string[] labels = { "Illegal programs", "Bug & exploits" };
            for (int i = 0; i < 2; i++) {
                w.DrawBitmapRaw(9, 25 + 18 * i, Ui + (type == i ? "radiobtn_on.bmp" : "radiobtn_off.bmp"));
                w.TextOut(28, 25 + 18 * i, labels[i], 0, 12, Black);
            }

            w.FillRect(8, 80, 215, 58, EditBorder);
            w.FillRect(9, 81, 213, 56, EditBack);
            if (edit.Text.Length == 0) w.TextOut(12, 84, "Enter report content.", 0, 12, Placeholder);
            w.TextOut(9, 142, "Abuse may be punished.", 0, 12, Note);
        }

        /// <summary>0x990bd0: 0x0be2 then DeleteWindow(0x2715).</summary>
        private void Send() {
            _ = session?.BeginClassicPacket(0x0be2, Body(), "a user report");
            window.Visible = false;
        }

        private byte[] Body() {
            var body = new byte[135];
            BitConverter.GetBytes(session?.LocalActorId ?? 0).CopyTo(body, 0);
            BitConverter.GetBytes(targetAid).CopyTo(body, 4);
            byte[] name = RoLoginShellSession.TextBytes(targetName);
            Array.Copy(name, 0, body, 8, Math.Min(23, name.Length));
            BitConverter.GetBytes((ushort) type).CopyTo(body, 32);
            byte[] text = RoLoginShellSession.TextBytes(edit.Text);
            Array.Copy(text, 0, body, 34, Math.Min(100, text.Length));
            return body;
        }

        /// <summary>!report open aid name | type n | text t | bytes | state for the sweep (bytes shows the packet without sending it).</summary>
        internal static string Sweep(RoLoginShellSession loginSession, string args) {
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            RoClassicReport r = instance;
            switch (t[0]) {
                case "open":
                    string[] a = t.Length > 1 ? t[1].Split(new[] { ' ' }, 2) : new string[0];
                    Open(loginSession, a.Length > 0 ? uint.Parse(a[0]) : 0, a.Length > 1 ? a[1] : string.Empty);
                    r = instance;
                    break;
                case "type":
                    if (r != null) {
                        r.type = int.Parse(t[1]);
                        r.window.Invalidate();
                    }

                    break;
                case "text":
                    r?.edit.SetText(t.Length > 1 ? t[1] : string.Empty);
                    r?.window.Invalidate();
                    break;
                case "bytes":
                    return r == null ? "none" : "0be2 " + BitConverter.ToString(r.Body()).Replace("-", string.Empty);
            }

            return r == null ? "none" : "open=" + r.window.Visible + " aid=" + r.targetAid + " name='" + r.targetName + "' type=" + r.type + " text='" + r.edit.Text + "'";
        }
    }
}
