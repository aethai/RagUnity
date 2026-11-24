using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The guild dialogs (RUNTIME-RULES-GUILD.md "invites-create-disband"): the helper 0xd4 while not in a guild, its tip
    /// 0xd6 (GuildTip.txt), UICreateGuildWnd 0xd5 (create) / 0xd7 (disband), the invitation 0x46 and the alliance offer 0x48.
    /// </summary>
    public sealed partial class RoClassicGuild {
        private const int HelpId = 0x153, CreateId = 0x1bc;
        private RoClassicWindow helper, tip, createForm, disbandForm, inviteWindow, allyWindow;
        private RoClassicEditField createName, disbandName;
        private uint inviteGuildId, allyAccountId;
        private readonly List<string> tipLines = new List<string>();
        private int tipFirst;
        private const int TipLineH = 18, TipRows = 175 / TipLineH;

        // ------------------------------------------------------------------ events

        /// <summary>The state's announcement (an event serial the window had not seen) as the receive handlers show it.</summary>
        private void OnGuildEvent(RoClassicGuildState state) {
            switch (state.EventKind) {
                case RoGuildUpdateKind.Left:
                case RoGuildUpdateKind.Expelled:
                    AnnounceEvent(state);
                    break;
                case RoGuildUpdateKind.Invite:
                    OpenInvite(state.EventCharId, state.EventName);
                    break;
                case RoGuildUpdateKind.InviteResult: {
                    // 0x0169 (0xc31b89): 0 already in a guild, 1 refused, 2 accepted (cyan), 3 full, 4 not online
                    int[] messages = { 0x17a, 0x17b, 0x17c, 0x17d, 0x82a };
                    string[] fallbacks = {
                        "He/She is already in a Guild.", "You have refused the guild invitation.", "You have accepted the guild invitation.",
                        "Your Guild is full.", "The character is not online or does not exist."
                    };
                    int v = state.EventValue;
                    if (v >= 0 && v < messages.Length) Chat(Msg(messages[v], fallbacks[v]), v == 2 ? "#00FFFF" : "#FF0000", RoChatLineTypes.Guild);
                    break;
                }
                case RoGuildUpdateKind.CreateResult: {
                    // 0x0167 (0xc31ad7): 0 → cyan 0x176 and the helper trio closes; 1..4 yellow
                    int v = state.EventValue;
                    if (v == 0) {
                        Chat(Msg(0x176, "Guild has been created."), "#00FFFF", RoChatLineTypes.Guild);
                        if (tip != null) tip.Visible = false;
                        if (createForm != null) createForm.Visible = false;
                        if (helper != null) helper.Visible = false;
                        break;
                    }

                    int[] messages = { 0, 0x177, 0x178, 0x195, 0xa7f };
                    string[] fallbacks = {
                        "", "You are already in a Guild.", "That Guild Name already exists.", "You need an Emperium to create a Guild.",
                        "Can't create a Guild in this area."
                    };
                    if (v > 0 && v < messages.Length) Chat(Msg(messages[v], fallbacks[v]), "#FFFF00", RoChatLineTypes.Guild);
                    break;
                }
                case RoGuildUpdateKind.DisbandResult: {
                    // 0x015e (0xc31dfb): 0 → the guild goes, 0x3b closes; yellow 0x190 / 0x191 / 0x192
                    int v = state.EventValue;
                    int[] messages = { 0x190, 0x191, 0x192 };
                    string[] fallbacks = {
                        "Guild was successfully disbanded.", "You have failed to disband the guild due to your incorrect SSN.",
                        "You have failed to disband the guild because there are guildsmen still present."
                    };
                    // 0xc31e3b: 0x191 is type 0, 0x190 / 0x192 type 4 (0xc31e83)
                    if (v >= 0 && v < messages.Length) Chat(Msg(messages[v], fallbacks[v]), "#FFFF00", v == 1 ? RoChatLineTypes.Public : RoChatLineTypes.Guild);
                    break;
                }
                case RoGuildUpdateKind.AllyOffer:
                    OpenAlly(state.EventCharId, state.EventName);
                    break;
                case RoGuildUpdateKind.AllyResult: {
                    // 0x0173 (0xc6b880): 0x18a..0x18e, 0x6b5, yellow but 2 (accepted) cyan
                    int[] messages = { 0x18a, 0x18b, 0x18c, 0x18d, 0x18e, 0x6b5 };
                    string[] fallbacks = {
                        "Your Guild is already in an Alliance with this Guild.", "You rejected the Alliance Request.", "You accepted the Alliance Request.",
                        "Other Guild has too many Alliances.", "You have too many Alliances.", "Guild alliance application is not possible.."
                    };
                    int v = state.EventValue;
                    if (v >= 0 && v < messages.Length) Chat(Msg(messages[v], fallbacks[v]), v == 2 ? "#00FFFF" : "#FFFF00", RoChatLineTypes.Guild);
                    break;
                }
                case RoGuildUpdateKind.HostileResult: {
                    // 0x0181 (0xc6ba10): 0 cyan 0x1ef, 1..3 red
                    int[] messages = { 0x1ef, 0x1f0, 0x1f1, 0x6b6 };
                    string[] fallbacks = {
                        "Antagonist has been set.", "Guild has too many Antagonists.", "Already set as an Antagonist",
                        "Guild hostility application is not possible."
                    };
                    int v = state.EventValue;
                    if (v >= 0 && v < messages.Length) Chat(Msg(messages[v], fallbacks[v]), v == 0 ? "#00FFFF" : "#FF0000", RoChatLineTypes.Guild);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 0xd4 / 0xd6

        /// <summary>
        /// Basic Info's guild button without a guild (DAT_014efb38 &lt; 1) toggles UIGuildHelperWnd 0xd4: first at
        /// ((W − 640) / 2 + 220, (H − 480) / 2 + 120), then its saved place (GUILDHELPERWNDINFO).
        /// </summary>
        private void ToggleHelper() {
            if (helper != null && helper.Visible) {
                helper.Visible = false;
                return;
            }

            if (helper == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIGuildHelperWnd").text);
                helper = new RoClassicWindow(system, layout) { Visible = false };
                helper.Moved += moved => {
                    PlayerPrefs.SetInt(Prefs + "helper.x", moved.Position.x);
                    PlayerPrefs.SetInt(Prefs + "helper.y", moved.Position.y);
                    // vf27 0x8c1a70: the create dialog follows at the helper's right edge + 2
                    if (createForm != null && createForm.Visible) createForm.MoveTo(moved.Position.x + moved.Width + 2, moved.Position.y);
                };
                helper.Clicked += child => {
                    switch (child.Id) {
                        case OkId:
                            if (tip != null) tip.Visible = false;
                            if (createForm != null) createForm.Visible = false;
                            helper.Visible = false;
                            break;
                        case HelpId:
                            ToggleTip();
                            break;
                        case CreateId:
                            if (createForm != null && createForm.Visible) createForm.Visible = false;
                            else {
                                OpenNameForm(false);
                                createForm.MoveTo(helper.Position.x + helper.Width + 2, helper.Position.y);
                            }

                            break;
                    }
                };
                helper.EscClose = () => {
                    helper.Visible = false;
                    return true;
                };
            }

            Vector2Int client = system.ClientSize;
            int x = PlayerPrefs.GetInt(Prefs + "helper.x", (client.x - 640) / 2 + 220), y = PlayerPrefs.GetInt(Prefs + "helper.y", (client.y - 480) / 2 + 120);
            if (x < 0 || y < 0 || x + helper.Width > client.x || y + helper.Height > client.y) (x, y) = ((client.x - 640) / 2 + 220, (client.y - 480) / 2 + 120);
            helper.MoveTo(x, y);
            helper.Visible = true;
            system.Windows.BringToFront(helper);
            helper.Invalidate();
        }

        /// <summary>UIGuildTipWnd 0xd6 (330 × 230 at (0, 0)): GuildTip.txt in its viewer (10, 22, 315, 175), 18 px lines.</summary>
        private void ToggleTip() {
            if (tip != null && tip.Visible) {
                tip.Visible = false;
                return;
            }

            if (tip == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + "UIGuildTipWnd").text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UITextViewer") child.Visible = false;
                }

                tip = new RoClassicWindow(system, layout) { Visible = false };
                tip.Clicked += child => {
                    if (child.Id == CloseId) tip.Visible = false;
                };
                tip.Wheeled += (w, local, delta) => {
                    tipFirst = Mathf.Clamp(tipFirst + (delta > 0 ? -1 : 1), 0, Math.Max(0, tipLines.Count - TipRows));
                    w.Invalidate();
                };
                tip.DrawDynamic += DrawTip;
                tip.EscClose = () => {
                    tip.Visible = false;
                    return true;
                };
            }

            LoadTip();
            tipFirst = 0;
            tip.MoveTo(0, 0);
            tip.Visible = true;
            system.Windows.BringToFront(tip);
            tip.Invalidate();
        }

        /// <summary>FUN_00a12c70: GuildTip.txt, '#' lines split the tips; every line goes to the viewer.</summary>
        private void LoadTip() {
            if (tipLines.Count > 0) return;
            RoAssetDatabase assets = Assets();
            string text = null;
            foreach (string path in new[] { "GuildTip.txt", "data/GuildTip.txt" }) {
                if (assets != null && assets.Exists(path)) {
                    text = assets.ReadText(path);
                    break;
                }
            }

            if (text == null) {
                string root = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "GuildTip.txt");
                if (System.IO.File.Exists(root)) text = System.IO.File.ReadAllText(root, Encoding.Default);
            }

            if (text == null) return;
            foreach (string raw in text.Replace("\r", string.Empty).Split('\n')) {
                if (raw.StartsWith("#", StringComparison.Ordinal)) continue;
                tipLines.Add(raw);
            }
        }

        private void DrawTip(RoClassicWindow w) {
            w.FillRect(10, 22, 315, 175, White);
            w.ClipTo(10, 22, 315, 175);
            for (int i = 0; i < TipRows && tipFirst + i < tipLines.Count; i++) w.TextOut(13, 26 + TipLineH * i, tipLines[tipFirst + i], 0, 12, Black);
            w.ResetClip();
            w.DrawScrollBar(325 - 13, 22, 175, Math.Max(0, tipLines.Count - TipRows), tipLines.Count, TipRows, tipFirst);
        }

        // ------------------------------------------------------------------ 0xd5 / 0xd7

        /// <summary>
        /// UICreateGuildWnd 0xd5 (create) / 0xd7 (disband), 150 × 100 at ((W − 640) / 2 + 200, (H − 480) / 2 + 120): the name
        /// edit (13, 42, 124, 20) #E6E6E6, 23 bytes, focused.
        /// </summary>
        private void OpenNameForm(bool disband) {
            ref RoClassicWindow form = ref disband ? ref disbandForm : ref createForm;
            if (form == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + (disband ? "UICreateGuildWnd_id215" : "UICreateGuildWnd")).text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrl") child.Visible = false;
                }

                var w = new RoClassicWindow(system, layout) { Visible = false };
                var edit = new RoClassicEditField { Rect = new RectInt(13, 42, 124, 20), Background = new Color32(0xe6, 0xe6, 0xe6, 255), MaxBytes = 23 };
                edit.Submitted += _ => SubmitNameForm(disband);
                w.AddEditField(edit);
                w.Clicked += child => {
                    if (child.Id == OkId) SubmitNameForm(disband);
                    else if (child.Id == CancelId) w.Visible = false;
                };
                w.EscClose = () => {
                    w.Visible = false;
                    return true;
                };
                if (disband) disbandName = edit;
                else createName = edit;
                form = w;
            }

            RoClassicEditField field = disband ? disbandName : createName;
            field.SetText(string.Empty);
            Vector2Int client = system.ClientSize;
            form.MoveTo((client.x - 640) / 2 + 200, (client.y - 480) / 2 + 120);
            form.Visible = true;
            system.Windows.BringToFront(form);
            field.Focus();
            form.Invalidate();
        }

        /// <summary>
        /// OK (vf37 0x8c2ab0): empty → 0x820; create → mode 0x70 (spaces 0x97b) → CZ_REQ_MAKE_GUILD, the form stays;
        /// disband → mode 0x6a (master only; another name 0x16e) → CZ_REQ_DISORGANIZE_GUILD, the form closes.
        /// </summary>
        private void SubmitNameForm(bool disband) {
            RoClassicWindow form = disband ? disbandForm : createForm;
            if (form == null || !form.Visible || session == null) return;
            string name = (disband ? disbandName : createName).Text;
            if (string.IsNullOrEmpty(name)) {
                RoClassicMessageBox.Show(Msg(0x820, "You must enter the name of your guild."), RoClassicMessageBox.TypeOk, true, _ => { });
                return;
            }

            if (!disband) {
                if (name.IndexOf(' ') >= 0) {
                    RoClassicMessageBox.Show(Msg(0x97b, "Guild Cannot use Space in the name."), RoClassicMessageBox.TypeOk, true, _ => { });
                    return;
                }

                _ = session.BeginLegacyGuildCreate(name);
                return;
            }

            form.Visible = false;
            if (!Master) return;
            if (!string.Equals(name, guild.Name, StringComparison.Ordinal)) {
                RoClassicMessageBox.Show(Msg(0x16e, "You have failed to disband the guild."), RoClassicMessageBox.TypeOk, true, _ => { });
                return;
            }

            _ = session.BeginLegacyGuildDisband(name);
        }

        // ------------------------------------------------------------------ 0x46 / 0x48

        /// <summary>
        /// UIJoinGuildAcceptWnd 0x46 (280 × 120 at ((W − 640) / 2 + 200, H · 200 / 480)): the guild name + 0x179 in the viewer
        /// (38-byte wrap); OK closes the helper trio and sends CZ_JOIN_GUILD 1, Cancel 0.
        /// </summary>
        private void OpenInvite(uint guildId, string guildName) {
            inviteGuildId = guildId;
            OpenAccept(ref inviteWindow, "UIJoinGuildAcceptWnd", guildName + Msg(0x179, " Guild has sent you an invitation. Would you like to join this Guild?"), accept => {
                if (accept) {
                    if (tip != null) tip.Visible = false;
                    if (createForm != null) createForm.Visible = false;
                    if (helper != null) helper.Visible = false;
                }

                _ = session?.BeginLegacyGuildInviteAnswer(inviteGuildId, accept);
            });
        }

        /// <summary>UIAllyGuildAcceptWnd 0x48 (0x81a3b0): "(" + the guild name + ")" + 0x189; OK / Cancel → CZ_ALLY_GUILD (inviter account, 1 / 0).</summary>
        private void OpenAlly(uint inviterAccountId, string guildName) {
            allyAccountId = inviterAccountId;
            OpenAccept(ref allyWindow, "UIAllyGuildAcceptWnd", "(" + guildName + ")" + Msg(0x189, " Guild is asking for an Alliance with them. Do you accept?"),
                accept => _ = session?.BeginLegacyGuildAllyAnswer(allyAccountId, accept));
        }

        private readonly Dictionary<RoClassicWindow, Action<bool>> answers = new Dictionary<RoClassicWindow, Action<bool>>();

        private void AnswerAccept(RoClassicWindow w, bool accept) {
            if (!w.Visible) return;
            w.Visible = false;
            if (answers.TryGetValue(w, out Action<bool> answer)) answer(accept);
        }

        private void OpenAccept(ref RoClassicWindow window, string layoutName, string text, Action<bool> answer) {
            if (window == null) {
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(Resources.Load<TextAsset>(Layouts + layoutName).text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class != "UITextViewer") continue;
                    child.Fields["text"] = "Message";
                    if (child.Props != null) child.Props["wrapBytes"] = 0x26;
                    child.Children.Clear();
                    // the oracle's construction placeholder ("deal") is replaced by Msg 0x49 / 0x4a
                    child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text);
                }

                var created = new RoClassicWindow(system, layout) { Visible = false };
                created.Clicked += child => {
                    if (child.Id == OkId || child.Id == CancelId) AnswerAccept(created, child.Id == OkId);
                };
                created.EscClose = () => {
                    AnswerAccept(created, false);
                    return true;
                };
                window = created;
            }

            RoClassicWindow w = window;
            answers[w] = answer;
            w.Set("Message", text);
            Vector2Int client = system.ClientSize;
            w.MoveTo((client.x - 640) / 2 + 200, client.y * 200 / 480);
            w.Visible = true;
            system.Windows.BringToFront(w);
            w.Invalidate();
        }
    }
}
