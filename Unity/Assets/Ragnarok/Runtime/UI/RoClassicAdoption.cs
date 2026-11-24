using System;
using System.Text;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Adoption (player-menu-rest-spec §3). The menu's "Send an adoption request to %s" (0xd286b0: my base level ≥ 70, not a
    /// baby job, WE_CALLPARTNER 336 owned = married, the target in my party list with base level ≤ 50) sends CZ_REQ_JOIN_BABY
    /// 0x01f9 {target AID}; ZC_BABYMSG 0x0216 prints its three errors in #FF9B9B. The child gets ZC_REQ_BABY 0x01f6 →
    /// UIBabyAcceptWnd 0x69 "(Name) wishes to adopt you. Do you accept?", OK → UIBabyAcceptWnd2 0x6e (the transcend
    /// warning 0xb95), whose OK / Cancel and 0x69's Cancel answer CZ_JOIN_BABY 0x01f7 {inviter, partner, 1 | 0}.
    /// </summary>
    public sealed class RoClassicAdoption {
        private const string Layouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const int OkId = 0xb4, CancelId = 0xb5;
        private static RoClassicAdoption instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoClassicWindow offer, warning;
        private uint inviter, partner;   // mode + 0x28c / + 0x290

        public static bool Handles(ushort id) => id == 0x01f6 || id == 0x0216;

        /// <summary>0xd286b0 (its two checks on the AID instead of the job never fire and are left out).</summary>
        public static bool CanOffer(RoLoginShellSession session, RoOnlineActorSnapshot target, string name) {
            RoPlayerStatusSnapshot me = session.BasicInfoSnapshot().Status;
            if (me == null || me.BaseLevel < 70 || IsBabyJob(me.JobId)) return false;
            bool married = false;
            foreach (RoGameplaySkillInfo skill in session.SkillSnapshot().Skills ?? Array.Empty<RoGameplaySkillInfo>()) {
                if (skill.SkillId == 336) married = true;
            }

            if (!married || target.BaseLevel > 50) return false;
            foreach (RoPartyMemberRuntimeSnapshot member in session.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (member.Name == name) return true;
            }

            return false;
        }

        /// <summary>0xd1dd80: the baby jobs.</summary>
        private static bool IsBabyJob(int job) =>
            (job >= 4023 && job <= 4045) || (job >= 4096 && job <= 4112) || (job >= 4158 && job <= 4182) || job == 4191 ||
            (job >= 4193 && job <= 4196) || (job >= 4205 && job <= 4210) || job == 4220 || (job >= 4222 && job <= 4238) ||
            job == 4241 || job == 4242 || job == 4244 || job == 4247 || job == 4248;

        /// <summary>Action 0x24 → mode Msg 0xb3: CZ_REQ_JOIN_BABY 0x01f9.</summary>
        public static void Request(RoLoginShellSession session, uint targetAid) =>
            _ = session?.BeginClassicPacket(0x01f9, BitConverter.GetBytes(targetAid), "an adoption request");

        public static void Handle(byte[] b) {
            if (b == null || b.Length < 2) return;
            ushort id = BitConverter.ToUInt16(b, 0);
            if (id == 0x0216 && b.Length >= 6) {
                // 0xc439b0: 0 / 1 / 2, other values nothing
                int result = BitConverter.ToInt32(b, 2);
                string text = result == 0 ? RoClassicUiHost.MsgStr(0x383, "You cannot adopt more than 1 child.")
                    : result == 1 ? RoClassicUiHost.MsgStr(0x384, "You must be at least character level 70 in order to adopt someone.")
                    : result == 2 ? RoClassicUiHost.MsgStr(0x394, "You cannot adopt a married person.")
                    : null;
                if (text != null) RoClassicChat.Instance?.AddLine(text, "#FF9B9B");
                return;
            }

            if (id != 0x01f6 || b.Length < 34) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) instance = new RoClassicAdoption { system = system };
            RoClassicAdoption a = instance;
            a.inviter = BitConverter.ToUInt32(b, 2);
            a.partner = BitConverter.ToUInt32(b, 6);
            int end = 10;
            while (end < 34 && b[end] != 0) end++;
            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            string name = Encoding.GetEncoding(1252).GetString(b, 10, end - 10);
            // 0xc5fc20 → MakeWindow 0x69 (a second offer reuses the open box)
            if (a.warning != null) a.warning.Visible = false;
            a.Show(ref a.offer, "(" + name + ")" + RoClassicUiHost.MsgStr(0x31a, " wishes to adopt you. Do you accept?"), a.OnOffer);
        }

        private void OnOffer(bool accept) {
            if (!accept) {
                Answer(false);
                return;
            }

            // OK → 0x69 closes, MakeWindow 0x6e with the warning
            Show(ref warning, RoClassicUiHost.MsgStr(0xb95,
                "When adopted, character will not able to transcend, maximum stats will be limited, MaxHP and MaxSP will be reduced.\\nAre you sure you want to continue?").Replace("\\n", "\n"), Answer);
        }

        /// <summary>mode Msg 0xad (0xc1aa86): CZ_JOIN_BABY 0x01f7 {inviter, partner, answer}.</summary>
        private void Answer(bool accept) {
            var body = new byte[12];
            BitConverter.GetBytes(inviter).CopyTo(body, 0);
            BitConverter.GetBytes(partner).CopyTo(body, 4);
            BitConverter.GetBytes(accept ? 1 : 0).CopyTo(body, 8);
            _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x01f7, body, "an adoption answer");
        }

        private readonly System.Collections.Generic.Dictionary<RoClassicWindow, Action<bool>> answers =
            new System.Collections.Generic.Dictionary<RoClassicWindow, Action<bool>>();

        private readonly System.Collections.Generic.Dictionary<RoClassicWindow, string> texts =
            new System.Collections.Generic.Dictionary<RoClassicWindow, string>();

        private static RoClassicChildLayout Viewer(RoClassicWindow w) => w.Layout.Children.Find(c => c.Class == "UITextViewer");

        /// <summary>The viewer's wheel: one line, within the lines beyond its 3.</summary>
        private void Scroll(RoClassicWindow w, int delta) {
            RoClassicChildLayout viewer = Viewer(w);
            if (viewer?.Props == null || !texts.TryGetValue(w, out string text)) return;
            var lines = new System.Collections.Generic.List<(int, int)>();
            RoClassicWindow.WrapBytes(text, 0x26, lines);
            int scroll = Mathf.Clamp(((int?) viewer.Props["scroll"] ?? 0) + delta, 0, Math.Max(0, lines.Count - viewer.H / 18));
            viewer.Props["scroll"] = scroll;
            w.Invalidate();
        }

        private void Close(RoClassicWindow w, bool accept) {
            if (!w.Visible) return;
            w.Visible = false;
            if (answers.TryGetValue(w, out Action<bool> answer)) answer(accept);
        }

        /// <summary>The UIExchangeAcceptWnd-family box (280 × 120, win_msgbox, OK (189, 96), Cancel (234, 96), viewer (10, 22) 260 × 65).</summary>
        private void Show(ref RoClassicWindow window, string text, Action<bool> answer) {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIAllyGuildAcceptWnd").text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class != "UITextViewer") continue;
                    child.Fields["text"] = "Message";
                    if (child.Props != null) child.Props["wrapBytes"] = 0x26;
                    // its vertical scroll bar stays: the transcend warning (5 lines) is longer than the 3 the view shows;
                    // the capture holds no bitmaps for it (nothing scrolled then), so it gets UIScrollBar's own
                    child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                    foreach (RoClassicChildLayout bar in child.Children) {
                        if (bar.Class != "UIScrollBar" || bar.Props == null) continue;
                        var bitmaps = new Newtonsoft.Json.Linq.JObject();
                        foreach ((string role, string file) in new[] {
                                     ("scroll0up", "scroll0up"), ("scroll0mid", "scroll0mid"), ("scroll0down", "scroll0down"),
                                     ("up", "scroll0bar_up"), ("mid", "scroll0bar_mid"), ("down", "scroll0bar_down")
                                 }) {
                            bitmaps[role] = new Newtonsoft.Json.Linq.JArray("유저인터페이스\\" + file + ".bmp");
                        }

                        bar.Props["bitmaps"] = bitmaps;
                    }
                }

                var created = new RoClassicWindow(system, layout) { Visible = false };
                created.Wheeled += (w, local, delta) => Scroll(w, delta > 0 ? -1 : 1);
                created.Clicked += child => {
                    if (child.Id == OkId || child.Id == CancelId) Close(created, child.Id == OkId);
                };
                created.EscClose = () => {
                    Close(created, false);
                    return true;
                };
                window = created;
            }

            RoClassicWindow w = window;
            answers[w] = answer;
            texts[w] = text;
            w.Set("Message", text);
            Viewer(w).Props["scroll"] = 0;
            Vector2Int client = system.ClientSize;
            w.MoveTo((client.x - 640) / 2 + 200, client.y * 200 / 480);
            w.Visible = true;
            system.Windows.BringToFront(w);
            w.Invalidate();
        }

        /// <summary>!adopt offer name | err n | ok | cancel | state for the sweep (a local 0x01f6 / 0x0216).</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            if (t.Length >= 2 && t[0] == "offer") {
                var p = new byte[34];
                BitConverter.GetBytes((ushort) 0x01f6).CopyTo(p, 0);
                BitConverter.GetBytes(2000010u).CopyTo(p, 2);
                BitConverter.GetBytes(2000001u).CopyTo(p, 6);
                byte[] name = Encoding.ASCII.GetBytes(t[1]);
                Array.Copy(name, 0, p, 10, Math.Min(23, name.Length));
                Handle(p);
            } else if (t.Length >= 2 && t[0] == "err") {
                var p = new byte[6];
                BitConverter.GetBytes((ushort) 0x0216).CopyTo(p, 0);
                BitConverter.GetBytes(int.Parse(t[1])).CopyTo(p, 2);
                Handle(p);
            } else if (t[0] == "wheel" && instance?.warning != null) {
                instance.Scroll(instance.warning, -int.Parse(t[1]));
            } else if ((t[0] == "ok" || t[0] == "cancel") && instance != null) {
                RoClassicWindow w = instance.warning != null && instance.warning.Visible ? instance.warning : instance.offer;
                if (w != null && w.Visible) instance.Close(w, t[0] == "ok");
            }

            RoClassicAdoption a = instance;
            return a == null ? "none" : "offer=" + (a.offer != null && a.offer.Visible) + " warning=" + (a.warning != null && a.warning.Visible) +
                " inviter=" + a.inviter + " partner=" + a.partner;
        }
    }
}
