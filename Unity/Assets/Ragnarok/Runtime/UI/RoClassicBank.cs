using System;
using System.Globalization;
using Ragnarok.Gameplay;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The bank, UIBank_NewWnd 0x113 (bank-attendance-achieve-spec §1): made only by the server's ZC_BANKING_CHECK 0x09a6
    /// (reason 0 toggles it, 2 → red MsgStr 0xbd7, else 0x996); the menu icon 0x1c9 and Ctrl+B send CZ_REQ_BANKING_CHECK
    /// 0x09ab (Ctrl+B closes an open bank). 280×153 centred: "Zeny Storage" title, bank\bg_bank.bmp at y 17, "In Bank:" /
    /// "On Hand:" at (30, 29 / 46) with the amounts ("1,234 z", height 11) at the label width + 43, a red status line
    /// centred at y 112; the amount edit (10 bytes) 99×17 at (78, 90); Deposit / Withdraw, ±1 and +100,000 / +1,000,000 /
    /// +10,000,000 / Max buttons. The checks of ParseAmount 0x8079d0 and SendMsg 0x81a590 run before 0x09a7 / 0x09a9 go
    /// out; the acks 0x09a8 / 0x09aa refresh it or print their reason in red.
    /// </summary>
    public sealed class RoClassicBank {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private const string Bank = Ui + "bank\\";
        private const int W = 280, H = 153;
        private const int CloseId = 0xc5, DepositId = 0x1ca, WithdrawId = 0x1cb, PlusOneId = 0x113, MinusOneId = 0x114;
        private const int Plus100kId = 0x184, Plus1mId = 0x183, Plus10mId = 0x182, MaxId = 0x181;
        private const long MaxZeny = 0x7fffffff;
        private const string Red = "#FF0000";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 StatusRed = new Color32(255, 0, 0, 255);
        private static readonly Color32 EditInner = new Color32(247, 247, 247, 255);
        private static readonly Color32 EditBorder = new Color32(198, 198, 198, 255);

        private static RoClassicBank instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private RoClassicWindow window;
        private RoClassicEditField edit;
        private string status = string.Empty;
        private int seenSequence = -1;
        private long bank;
        private long zeny;

        /// <summary>Per frame from the chat window's classic ticks: the server's bank answers.</summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow) {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null || loginSession == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicBank { system = system };
            RoClassicBank b = instance;
            b.session = loginSession;
            b.chat = chatWindow;
            RoBankSnapshot snap = loginSession.BankSnapshot() ?? RoBankSnapshot.Empty;
            if (b.seenSequence < 0) {
                b.seenSequence = snap.EventSequence;   // nothing from before the classic bank existed
                return;
            }

            if (snap.EventSequence != b.seenSequence) {
                b.seenSequence = snap.EventSequence;
                b.OnServer(snap);
            }

            long hand = snap.OnHandZeny;
            if (b.window != null && b.window.Visible && hand != b.zeny) {
                b.zeny = hand;
                b.window.Invalidate();
            }
        }

        /// <summary>Whether the bank window is up (MakeWindow 0x155, the expanded barter shop, closes it).</summary>
        internal static bool IsOpen => instance?.window != null && instance.window.Visible;

        /// <summary>DeleteWindow(0x113) from another window (the expanded barter shop).</summary>
        internal static void Hide() {
            if (IsOpen) instance.window.Visible = false;
        }

        /// <summary>The menu icon 0x1c9 / Ctrl+B: sends 0x09ab, or closes an open bank.</summary>
        public static void Toggle(RoLoginShellSession loginSession) {
            RoClassicBank b = instance;
            if (b?.window != null && b.window.Visible) {
                b.window.Visible = false;
                return;
            }

            _ = loginSession?.BeginLegacyCheckBank();
        }

        private void OnServer(RoBankSnapshot snap) {
            short reason = snap.LastReason;
            switch (snap.LastOperation) {
                case RoBankOperation.Check:
                    // 0xc3be00: 0 sets the balance and toggles the window
                    if (reason == 0) {
                        bank = snap.BankZeny;
                        zeny = snap.OnHandZeny;
                        if (window != null && window.Visible) window.Visible = false;
                        else Open();
                    } else {
                        Say(reason == 2 ? RoClassicUiHost.MsgStr(0xbd7, "A work is in progress, please try again.")
                            : RoClassicUiHost.MsgStr(0x996, "The bank is not available. Please try again in a few minutes."));
                    }

                    break;
                case RoBankOperation.Deposit:
                    // 0xc3bbe0
                    if (reason == 0) Refresh(snap);
                    else Say(reason == 2 ? RoClassicUiHost.MsgStr(0x998, "You don't have enough zeny")
                        : reason == 3 ? RoClassicUiHost.MsgStr(0x9a1, "You cannot deposit more zeny.")
                        : reason == 4 ? RoClassicUiHost.MsgStr(0x9b9, "You cannot use the bank now.")
                        : RoClassicUiHost.MsgStr(0x996, "The bank is not available. Please try again in a few minutes."));
                    break;
                case RoBankOperation.Withdraw:
                    // 0xc3bcf0
                    if (reason == 0) Refresh(snap);
                    else Say(reason == 1 ? RoClassicUiHost.MsgStr(0x997, "Bank Balance is not enough.")
                        : reason == 3 ? RoClassicUiHost.MsgStr(0x9b9, "You cannot use the bank now.")
                        : RoClassicUiHost.MsgStr(0x996, "The bank is not available. Please try again in a few minutes."));
                    break;
            }
        }

        private void Refresh(RoBankSnapshot snap) {
            bank = snap.BankZeny;
            zeny = snap.OnHandZeny;
            window?.Invalidate();
        }

        private void Say(string text) {
            RoClassicChat target = chat != null ? chat : RoClassicChat.Instance;
            target?.AddLine(text, Red);
        }

        private static RoClassicChildLayout Button(int id, string name, int x, int y, int w, int h, int tipId, string tip) {
            var b = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = name, X = x, Y = y, W = w, H = h };
            b.States["normal"] = Bank + name + "_out.bmp";
            b.States["hover"] = Bank + name + "_over.bmp";
            b.States["pressed"] = Bank + name + "_press.bmp";
            b.Tooltip = tipId > 0 ? RoClassicUiHost.MsgStr(tipId, tip) : tip;
            return b;
        }

        /// <summary>MakeWindow 0x113 (OnCreate 0x80d540): centred, the edit focused, the status empty.</summary>
        private void Open() {
            if (window == null) {
                var layout = new RoClassicWindowLayout { Class = "UIBank_NewWnd", WindowId = 0x113, Width = W, Height = H };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = CloseId, Path = "close", X = 266, Y = 3, W = 11, H = 11, Tooltip = "Exit" };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = Basic + "sys_close_on.bmp";
                close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                layout.Children.Add(Button(DepositId, "btn_deposit", 209, 77, 42, 20, 0xad6, "Deposit"));
                layout.Children.Add(Button(WithdrawId, "btn_withdraw", 209, 102, 42, 20, 0xad7, "Withdraw"));
                layout.Children.Add(Button(PlusOneId, "btn_upper", 177, 89, 18, 10, 0xad8, "+ 1z"));
                layout.Children.Add(Button(MinusOneId, "btn_buttom", 177, 99, 18, 9, 0xad9, "- 1z"));
                layout.Children.Add(Button(Plus100kId, "btn_10mil", 189, 132, 46, 16, 0, "+ 100,000"));
                layout.Children.Add(Button(Plus1mId, "btn_100mil", 141, 132, 46, 16, 0, "+ 1,000,000"));
                layout.Children.Add(Button(Plus10mId, "btn_1000mil", 93, 132, 46, 16, 0, "+ 10,000,000"));
                layout.Children.Add(Button(MaxId, "btn_max", 45, 132, 46, 16, 0xada, "Max"));
                window = new RoClassicWindow(system, layout) { Visible = false };
                window.DrawDynamic += Draw;
                window.Clicked += OnClick;
                window.EscClose = () => {
                    window.Visible = false;
                    return true;
                };
                edit = new RoClassicEditField { Rect = new RectInt(79, 91, 97, 15), MaxBytes = 10, Background = EditInner };
                window.AddEditField(edit);
            }

            status = string.Empty;
            edit.SetText(string.Empty);
            Vector2Int c = system.ClientSize;
            window.MoveTo((int) (c.x * 0.5 - 140), (int) (c.y * 0.5 - 77));
            window.Visible = true;
            system.Windows.BringToFront(window);
            edit.Focus();
            window.Invalidate();
        }

        /// <summary>OnDraw 0x816f10.</summary>
        private void Draw(RoClassicWindow w) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < W - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(W - 12, 0, Basic + "titlebar_right.bmp");
            w.DrawBitmap(3, 3, Basic + "sys_base_off.bmp");
            string title = RoClassicUiHost.MsgStr(0xad3, "Zeny Storage");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            w.DrawBitmapRaw(0, 17, Bank + "bg_bank.bmp");
            string inBank = RoClassicUiHost.MsgStr(0xad4, "In Bank:");
            w.TextOut(30, 29, inBank, 0, 12, Black);
            int lw = w.FontFor(0, 12).Measure(inBank);
            w.TextOut(lw + 43, 29, Commas(bank) + " z", 0, 11, Black);
            w.TextOut(30, 46, RoClassicUiHost.MsgStr(0xad5, "On Hand:"), 0, 12, Black);
            w.TextOut(lw + 43, 46, Commas(zeny) + " z", 0, 11, Black);
            // the edit's frame: inner #F7F7F7 under a 1-px #C6C6C6 border, the text 3 px down
            w.FillRect(78, 90, 99, 17, EditBorder);
            w.FillRect(79, 91, 97, 15, EditInner);
            if (status.Length > 0) {
                int tw = w.FontFor(0, 12).Measure(status);
                w.TextOut((int) (W * 0.5 - tw * 0.5), 112, status, 0, 11, StatusRed);
            }
        }

        private static string Commas(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);

        /// <summary>SendMsg 0x81a590, msg 6.</summary>
        private void OnClick(RoClassicChildLayout child) {
            switch (child.Id) {
                case CloseId:
                    window.Visible = false;
                    return;
                case MaxId:
                    edit.SetText(RoClassicUiHost.MsgStr(0xada, "Max"));
                    edit.Focus();
                    return;
                case PlusOneId: Add(1); return;
                case MinusOneId: Add(-1); return;
                case Plus100kId: Add(100000); return;
                case Plus1mId: Add(1000000); return;
                case Plus10mId: Add(10000000); return;
                case DepositId: Transfer(true); return;
                case WithdrawId: Transfer(false); return;
            }
        }

        /// <summary>0x8077d0: the edit's number (0 unless all digits) plus delta, clamped to 0..2^31 − 1.</summary>
        private void Add(long delta) {
            string t = edit.Text;
            long v = delta + (t.Length > 0 && AllDigits(t) && long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out long n) ? n : 0);
            v = Math.Max(0, Math.Min(MaxZeny, v));
            edit.SetText(v.ToString(CultureInfo.InvariantCulture));
            edit.Focus();
            window.Invalidate();
        }

        private static bool AllDigits(string t) {
            foreach (char c in t) {
                if (c < '0' || c > '9') return false;
            }

            return true;
        }

        /// <summary>Deposit / Withdraw: ParseAmount 0x8079d0 (the edit cleared), then the balance checks and 0x09a7 / 0x09a9.</summary>
        private void Transfer(bool deposit) {
            long amount = ParseAmount(deposit);
            window.Invalidate();
            if (amount == 0) return;
            if (deposit) {
                if (amount > zeny) Fail(0x998, "You don't have enough zeny", 0xae1, "Insufficient Zeny");
                else if (bank + amount <= MaxZeny) _ = session?.BeginLegacyDepositBankZeny((int) amount);
                else Fail(0xae3, "Exceeded max zeny", 0xadf, "Exceeded max input");
            } else {
                if (amount > bank) Fail(0x997, "Bank Balance is not enough.", 0xae2, "Insufficient funds");
                else if (zeny + amount < 0x80000000L) _ = session?.BeginLegacyWithdrawBankZeny((int) amount);
                else Fail(0x99b, "You cannot hold more than 2,147,483,647 Zeny", 0xae3, "Exceeded max zeny");
            }
        }

        private long ParseAmount(bool deposit) {
            string t = edit.Text;
            edit.SetText(string.Empty);
            if (t == RoClassicUiHost.MsgStr(0xada, "Max")) {
                long excess = Math.Max(0, zeny + bank - MaxZeny);
                if (deposit) {
                    if (bank >= MaxZeny) return Fail(0xae3, "Exceeded max zeny", 0xadf, "Exceeded max input");
                    if (zeny == 0) return Fail(0xad2, "You don't have any zeny.", 0xadc, "Insufficient Zeny");
                    long amt = zeny - excess;
                    return amt < 0 ? Fail(0x9a1, "You cannot deposit more zeny.", 0xadc, "Insufficient Zeny") : amt;
                }

                if (bank == 0) return Fail(0xad2, "You don't have any zeny.", 0xadc, "Insufficient Zeny");
                long w = bank - excess;
                return zeny + w > MaxZeny ? MaxZeny - zeny : w;
            }

            if (t.Length == 0) return Fail(0xadb, "There is no Input value", 0xadd, "Input Required");
            if (!AllDigits(t)) return Fail(0x9b8, "You can enter only numbers.", 0xade, "Numbers only");
            long v = long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : long.MaxValue;
            if (v > MaxZeny) return Fail(0xad0, "Enter zeny below 2,147,483,647.", 0xadf, "Exceeded max input");
            return v >= 1 ? v : Fail(0xad1, "Please enter at least 1 zeny.", 0xae0, "Below Minimum Input");
        }

        /// <summary>A red chat line and the window's status line; 0 as the amount.</summary>
        private long Fail(int chatId, string chatText, int statusId, string statusText) {
            Say(RoClassicUiHost.MsgStr(chatId, chatText));
            status = RoClassicUiHost.MsgStr(statusId, statusText);
            window.Invalidate();
            return 0;
        }

        /// <summary>!bank open|check|amount n|press id|state for the sweep.</summary>
        internal static string Sweep(string args) {
            RoClassicBank b = instance;
            string[] t = (args ?? string.Empty).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length >= 1 && t[0] == "check") Toggle(b?.session);
            else if (b != null && t.Length >= 2 && t[0] == "amount" && b.edit != null) b.edit.SetText(t[1]);
            else if (b?.window != null && t.Length >= 2 && t[0] == "press") {
                int id = int.Parse(t[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b.OnClick(new RoClassicChildLayout { Id = id });
            }

            if (b == null) return "no bank";
            return (b.window != null && b.window.Visible ? "open" : "closed") + " bank " + b.bank + " zeny " + b.zeny + " edit '" + (b.edit?.Text ?? string.Empty) + "' status '" + b.status + "'";
        }
    }
}
