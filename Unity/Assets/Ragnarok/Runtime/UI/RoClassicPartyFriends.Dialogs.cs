using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The classic party / friend dialogs (ui/RUNTIME-RULES-PARTY.md §2): UIPartySettingWnd 0x35 "Party Setup" (in a party,
    /// 130 × 245) and 0xcf "Create Party" (130 × 220), UIPartyInvitationToWnd 0xd0, UIFriendOptionWnd 0x44, and the two
    /// questions from the server, UIJoinPartyAcceptWnd 0x23 (a party invitation) and UIJoinFriendAcceptWnd 0x6d (a friend
    /// request). The questions open whether the Party / Friend window is open or not, as in the client: they follow the
    /// lists from <see cref="RoClassicPartyFriends.Tick"/>. The model's requests send the packets; each question is
    /// answered once.
    /// </summary>
    public sealed partial class RoClassicPartyFriends {
        private const string PdLayouts = "Ragnarok/Generated/OriginalClient/ui/";
        private const string PdUi = "유저인터페이스\\";
        private const int PdOkId = 0xb4;
        private const int PdCancelId = 0xb5;
        private const int PdCloseId = 0xc5;
        private const int PdSetupVariant = 70;   // 0x35 in a party: +0xc0
        private const int PdCreateVariant = 50;  // 0xcf
        private static readonly Color32 PdBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 PdWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 PdEditFill = new Color32(0xe6, 0xe6, 0xe6, 255);

        /// <summary>The Setup button (0xe6): Party Setup in the party tab (only in a party), Friend Setup in the friend tab.</summary>
        private void ClassicSetupButton() {
            if (MsPartyTab) {
                if (RoPartyFriendsModel.IsInParty(msParty)) pd.TogglePartySetting(PdSetupVariant, true);
            } else {
                pd.ToggleFriendOption();
            }
        }

        private void ClassicCreateButton() => pd.TogglePartySetting(PdCreateVariant, true);

        private void ClassicInviteButton() => pd.ToggleInvitation();

        /// <summary>The Party / Friend window closing takes Friend Setup with it (DeleteWindow 0x45).</summary>
        private void ClassicDialogsFollowMessengerHidden() => pd.CloseFriendOption();

        private static RoClassicWindowLayout PdLoad(string name) {
            var asset = Resources.Load<TextAsset>(PdLayouts + name);
            return asset != null ? RoClassicWindowLayout.Parse(asset.text) : null;
        }

        /// <summary>The dialogs, followed and ticked by the owner every frame.</summary>
        private sealed class PartyDialogs {
            private readonly RoClassicPartyFriends owner;

            // 0x35 / 0xcf
            private RoClassicWindow setting;
            private RoClassicEditField settingName;
            private int variant;
            private uint exp;
            private byte pickup, share;
            private uint seenExp = uint.MaxValue;
            private byte seenPickup = byte.MaxValue, seenShare = byte.MaxValue;
            private int settingOpenedFrame;
            private Func<bool> settingEnterClaim;

            // 0xd0
            private RoClassicWindow invitation;
            private RoClassicEditField invitationName;

            // 0x44
            private RoClassicWindow friendOption;
            private readonly bool[] friendFlags = new bool[3];
            private static readonly string[] FriendFlagKeys = { "OpenStranger", "OpenFriend", "Alarm" };

            // 0x23 / 0x6d
            private RoClassicWindow joinParty;
            private RoClassicWindow joinFriend;
            private RoFriendRuntimeSnapshot joinFriendRequest;

            public PartyDialogs(RoClassicPartyFriends owner) {
                this.owner = owner;
            }

            private RoPartyFriendsModel Model => owner.model;

            public bool IsPartySettingOpen(int v) => setting != null && variant == v;

            public bool IsFriendOptionOpen => friendOption != null;

            /// <summary>Per frame: 0x35 is in the Enter chain (mgr+0x30c), Enter = OK (the Create name field submits by itself).</summary>
            public void Tick() {
#if ENABLE_INPUT_SYSTEM
                if (setting == null || !setting.Visible || variant == PdCreateVariant) return;
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && settingOpenedFrame != Time.frameCount && !RoClassicMessageBox.ModalOpen &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) {
                    SettingOk();
                }
#endif
            }

            // ---- the server's side ----

            /// <summary>The session's lists moved: the questions and Party Setup follow them.</summary>
            public void Follow(RoPartyRuntimeSnapshot party, List<RoFriendRuntimeSnapshot> friends) {
                FollowParty(party);
                FollowFriendRequests(friends);
            }

            private void FollowParty(RoPartyRuntimeSnapshot party) {
                // ZC_PARTY_JOIN_REQ (0x02c6): 0x23 opens, or takes the newer invitation; the invitation gone closes it
                int invite = Model.FollowPartyInvite(party);
                if (invite > 0) {
                    string text = "(" + (party.PendingInvitationName ?? string.Empty) + ")" +
                        RoClassicUiHost.MsgStr(0x5e, " Party has sent you an invitation. Would you like to join?");
                    joinParty = ShowQuestion(joinParty, "UIJoinPartyAcceptWnd", text, JoinPartyAnswer);
                } else if (invite < 0) {
                    Close(ref joinParty);
                }

                // msg 0x3c (ZC_GROUPINFO_CHANGE): Party Setup re-reads the options
                uint e = party.ExpOption == 2 ? 0u : party.ExpOption;
                if (e != seenExp || party.ItemPickupRule != seenPickup || party.ItemDivisionRule != seenShare) {
                    seenExp = e;
                    seenPickup = party.ItemPickupRule;
                    seenShare = party.ItemDivisionRule;
                    if (setting != null && variant == PdSetupVariant) {
                        exp = e;
                        pickup = seenPickup;
                        share = seenShare;
                        setting.Invalidate();
                    }
                }

                // leaving the party closes Party Setup (0x35) and the invitation box (0xd0)
                if (!RoPartyFriendsModel.IsInParty(party)) {
                    if (variant == PdSetupVariant) CloseSetting();
                    Close(ref invitation);
                }
            }

            private void FollowFriendRequests(List<RoFriendRuntimeSnapshot> friends) {
                // ZC_REQ_ADD_FRIENDS (0x0207): 0x6d shows the newest request it has not answered
                if (Model.TryGetPendingFriendRequest(friends, out RoFriendRuntimeSnapshot f)) {
                    if (joinFriend == null || f.AccountId != joinFriendRequest.AccountId || f.CharacterId != joinFriendRequest.CharacterId) {
                        joinFriendRequest = f;
                        string text = string.Format(RoClassicUiHost.MsgStr(0x332, "(%s) wishes to be friends with you. Would you like to accept?")
                            .Replace("%s", "{0}"), f.Name ?? string.Empty);
                        joinFriend = ShowQuestion(joinFriend, "UIJoinFriendAcceptWnd", text, JoinFriendAnswer);
                    }
                } else if (joinFriend != null) {
                    Close(ref joinFriend);
                }
            }

            /// <summary>
            /// 0x23 / 0x6d (OnCreate 0x83c560): win_msgbox with the text viewer (wrap 38) and OK / Cancel, at
            /// (scaledX(200), scaledY(200)); a second question re-uses the open box. Esc and Enter do nothing.
            /// </summary>
            private RoClassicWindow ShowQuestion(RoClassicWindow window, string layoutName, string text, Action<bool> answer) {
                RoClassicUiSystem system = owner.system;
                if (window == null) {
                    RoClassicWindowLayout layout = PdLoad(layoutName);
                    if (layout == null) return null;
                    foreach (RoClassicChildLayout child in layout.Children) {
                        if (child.Class != "UITextViewer") continue;
                        child.Fields["text"] = "Message";
                        child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text); // the captured sample line
                    }

                    window = new RoClassicWindow(system, layout);
                    window.Clicked += child => {
                        if (child.Id == PdOkId) answer(true);
                        else if (child.Id == PdCancelId) answer(false);
                    };
                    Vector2Int client = system.ClientSize;
                    window.MoveTo((client.x - 640) / 2 + 200, 200 * client.y / 480);
                }

                window.Set("Message", text);
                window.Visible = true;
                system.Windows.BringToFront(window);
                window.Invalidate();
                return window;
            }

            /// <summary>OK → CZ_PARTY_JOIN_REQ_ACK {GRID, 1} and the Party / Friend window opens; Cancel → {GRID, 0}.</summary>
            private void JoinPartyAnswer(bool accept) {
                Close(ref joinParty);
                if (Model.OpenPartyInviteId == 0) return;
                _ = Model.AnswerPartyInvite(accept);
                if (accept) owner.ShowMessenger(RoPartyFriendsTab.Party);
            }

            /// <summary>OK → CZ_ACK_REQ_ADD_FRIENDS {AID, GID, 1} and the Friend tab opens; Cancel → {AID, GID, 0}.</summary>
            private void JoinFriendAnswer(bool accept) {
                RoFriendRuntimeSnapshot request = joinFriendRequest;
                Close(ref joinFriend);
                _ = Model.AnswerFriendRequest(request, accept);
                if (accept) owner.ShowMessenger(RoPartyFriendsTab.Friends);
                FollowFriendRequests(owner.msFriends); // the next request, if any, at once
            }

            // ---- 0x35 Party Setup / 0xcf Create Party ----

            public void TogglePartySetting(int newVariant, bool beside) {
                bool wasSame = setting != null && variant == newVariant;
                CloseSetting();
                Close(ref invitation);
                CloseFriendOption();
                if (wasSame) return;

                RoClassicUiSystem system = owner.system;
                RoClassicWindowLayout layout = PdLoad(newVariant == PdCreateVariant ? "UIPartySettingWnd.id207" : "UIPartySettingWnd");
                if (layout == null) return;
                variant = newVariant;
                int h = variant == PdCreateVariant ? 220 : variant == PdSetupVariant ? 245 : 180;
                layout.Width = 130;
                layout.Height = h;
                // OnDraw 0x84f740: the frame for any variant; the title and labels are drawn with the radios
                layout.Ops.Clear();
                RoClassicFrames.TitleBar(layout.Ops, 130, true);
                RoClassicFrames.Body(layout.Ops, 130, h);
                RoClassicFrames.ButtonBar(layout.Ops, 130, h, 2);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIRadioBtn" || child.Class == "UIEditCtrl") child.Visible = false;
                    else if (child.Id == PdOkId) { child.X = 130 - 92; child.Y = h - 24; }
                    else if (child.Id == PdCancelId) { child.X = 130 - 46; child.Y = h - 24; }
                }

                setting = new RoClassicWindow(system, layout);
                setting.DrawDynamic += DrawSetting;
                setting.Pressed += (w, local) => PressSetting(local);
                setting.Clicked += child => {
                    if (child.Id == PdOkId) SettingOk();
                    else if (child.Id == PdCancelId || child.Id == PdCloseId) CloseSetting();
                };
                setting.EscClose = () => {
                    CloseSetting();
                    return true;
                };

                RoPartyRuntimeSnapshot party = owner.msParty;
                exp = party.ExpOption == 2 ? 0u : party.ExpOption;
                pickup = party.ItemPickupRule;
                share = party.ItemDivisionRule;
                if (variant == PdCreateVariant) {
                    settingName = new RoClassicEditField { Rect = new RectInt(20, 38, 100, 20), Background = PdEditFill, MaxBytes = 23 };
                    setting.AddEditField(settingName);
                    settingName.Submitted += _ => SettingOk();
                    settingName.Focus();
                } else {
                    settingEnterClaim ??= () => setting != null && setting.Visible && variant != PdCreateVariant;
                    RoClassicTextInput.AddKeyboardClaim(settingEnterClaim);
                }

                Place(setting, beside, 120);
                settingOpenedFrame = Time.frameCount;
                system.Windows.PushEsc(setting);
            }

            private int Extra => variant;

            private void DrawSetting(RoClassicWindow w) {
                string title = variant == PdCreateVariant
                    ? RoClassicUiHost.MsgStr(0x806, "Create Party")
                    : RoClassicUiHost.MsgStr(0x123, "Party Setup");
                w.TextOut(19, 4, title, 0, 12, PdWhite);
                w.TextOut(18, 3, title, 0, 12, PdBlack);
                if (variant == PdSetupVariant) {
                    w.TextOut(10, 22, RoClassicUiHost.MsgStr(0x124, "How to share EXP"), 0, 12, PdBlack);
                    Radio(w, 42, exp == 0, RoClassicUiHost.MsgStr(0x11f, "Each Take "));
                    Radio(w, 62, exp == 1, RoClassicUiHost.MsgStr(0x120, "Even Share"));
                } else if (variant == PdCreateVariant) {
                    w.TextOut(10, 22, RoClassicUiHost.MsgStr(0x809, "Party Name:"), 0, 12, PdBlack);
                }

                w.TextOut(10, Extra + 17, RoClassicUiHost.MsgStr(0x125, "How to share Items"), 0, 12, PdBlack);
                Radio(w, Extra + 37, pickup == 0, RoClassicUiHost.MsgStr(0x121, "Each Take"));
                Radio(w, Extra + 57, pickup == 1, RoClassicUiHost.MsgStr(0x122, "Party Share"));
                w.TextOut(10, Extra + 82, RoClassicUiHost.MsgStr(0x2e2, "Item sharing type"), 0, 12, PdBlack);
                Radio(w, Extra + 102, share == 0, RoClassicUiHost.MsgStr(0x2e3, "Individual"));
                Radio(w, Extra + 122, share == 1, RoClassicUiHost.MsgStr(0x2e4, "Shared"));
            }

            // UIRadioBtn 160 × 12 at x 20: the bullet, the text at +13
            private static void Radio(RoClassicWindow w, int y, bool on, string text) {
                w.DrawBitmap(20, y, PdUi + (on ? "radiobtn_on.bmp" : "radiobtn_off.bmp"));
                w.TextOut(33, y, text, 0, 12, PdBlack);
            }

            /// <summary>A radio (msg 6 / 0xd3): not in a party or the leader sets the group; others get MsgStr 0x126.</summary>
            private void PressSetting(Vector2Int local) {
                if (local.x < 20 || local.x >= 180) return;
                int group = -1, index = 0;
                void Hit(int g, int y0) {
                    if (local.y >= y0 && local.y < y0 + 12) { group = g; index = 0; }
                    else if (local.y >= y0 + 20 && local.y < y0 + 32) { group = g; index = 1; }
                }

                if (variant == PdSetupVariant) Hit(0, 42);
                Hit(1, Extra + 37);
                Hit(2, Extra + 102);
                if (group < 0) return;

                RoPartyRuntimeSnapshot party = owner.msParty;
                bool inParty = RoPartyFriendsModel.IsInParty(party), leader = Model.IsLocalLeader(party);
                if (group == 0 ? !leader : inParty && !leader) {
                    // UIPartySettingWnd 0x8612f6: mgr msg 3, then type 10 in COLORREF 0x00FFFF64
                    owner.Chat(RoClassicUiHost.MsgStr(0x126, "Only the Party Leader can change this setting."), RoChatLineTypes.Notice, RoChatLineTypes.PartyStatus, popOpen: true);
                    return;
                }

                if (group == 0) exp = (uint) index;
                else if (group == 1) pickup = (byte) index;
                else share = (byte) index;
                setting.Invalidate();
            }

            /// <summary>
            /// OK (0xb4): in a party the leader sends CZ_GROUPINFO_CHANGE_V2 when something changed; Create checks the name
            /// ('#' → MsgStr 0xb17, empty → 0x819) and sends CZ_MAKE_GROUP2.
            /// </summary>
            private void SettingOk() {
                if (setting == null) return;
                RoPartyRuntimeSnapshot party = owner.msParty;
                if (variant == PdCreateVariant) {
                    string name = settingName != null ? settingName.Text : string.Empty;
                    if (name.IndexOf('#') >= 0) {
                        owner.Chat(RoClassicUiHost.MsgStr(0xb17, "You cannot use '#' in the name."), "#FF0000");
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(name)) {
                        owner.Chat(RoClassicUiHost.MsgStr(0x819, "You must enter the name of the party."), "#FF0000");
                        return;
                    }

                    byte p = pickup, s = share;
                    CloseSetting();
                    _ = Model.CreatePartyAsync(name, p, s);
                    return;
                }

                uint e = exp;
                byte pk = pickup, sh = share;
                CloseSetting();
                if (RoPartyFriendsModel.IsInParty(party) && Model.IsLocalLeader(party) &&
                    (e != (party.ExpOption == 2 ? 0u : party.ExpOption) || pk != party.ItemPickupRule || sh != party.ItemDivisionRule)) {
                    _ = Model.UpdatePartyOptionsAsync(e, pk, sh);
                }
            }

            private void CloseSetting() {
                if (setting == null) return;
                if (settingName != null && settingName.HasFocus) RoClassicTextInput.Focus(null);
                settingName = null;
                RoClassicTextInput.RemoveKeyboardClaim(settingEnterClaim);
                Close(ref setting);
                variant = 0;
            }

            // ---- 0xd0 Party Invitation ----

            public void ToggleInvitation() {
                if (invitation != null) {
                    Close(ref invitation);
                    return;
                }

                CloseSetting();
                CloseFriendOption();
                RoClassicUiSystem system = owner.system;
                RoClassicWindowLayout layout = PdLoad("UIPartyInvitationToWnd");
                if (layout == null) return;
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class == "UIEditCtrl") child.Visible = false;
                }

                invitation = new RoClassicWindow(system, layout);
                invitationName = new RoClassicEditField { Rect = new RectInt(20, 42, 120, 20), Background = PdEditFill, MaxBytes = 23 };
                invitation.AddEditField(invitationName);
                // Enter in the field goes to the default button 0x248: nothing
                invitation.Clicked += child => {
                    if (child.Id == PdOkId) InvitationOk();
                    else if (child.Id == PdCancelId) Close(ref invitation);
                };
                invitation.EscClose = () => {
                    Close(ref invitation);
                    return true;
                };
                Place(invitation, true, 120);
                invitationName.Focus();
                system.Windows.PushEsc(invitation);
            }

            /// <summary>
            /// OK: the field is cleared; empty → MsgStr 0x818; 24 bytes or more → box MsgStr 0x213; else CZ_PARTY_JOIN_REQ and,
            /// for the leader, "name has received an invitation…" (COLORREF 0xffff00). The window stays open.
            /// </summary>
            private void InvitationOk() {
                if (invitationName == null) return;
                string name = invitationName.Text;
                invitationName.SetText(string.Empty);
                if (string.IsNullOrEmpty(name)) {
                    owner.Chat(RoClassicUiHost.MsgStr(0x818, "You must enter a character name."), "#FF0000");
                    return;
                }

                if (Encoding.UTF8.GetByteCount(name) >= 24) {
                    RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0x213, "The name is too long."), RoClassicMessageBox.TypeOk, true, null);
                    return;
                }

                _ = Model.InviteAsync(name);
                if (Model.IsLocalLeader(owner.msParty)) {
                    owner.Chat(name + RoClassicUiHost.MsgStr(0x80b, " has received an invitation to join your party."), "#00FFFF");
                }
            }

            // ---- 0x44 Friend Setup ----

            public void ToggleFriendOption() {
                if (friendOption != null) {
                    CloseFriendOption();
                    return;
                }

                CloseSetting();
                Close(ref invitation);
                RoClassicUiSystem system = owner.system;
                RoClassicWindowLayout layout = PdLoad("UIFriendOptionWnd");
                if (layout == null) return;
                for (int i = 0; i < 3; i++) friendFlags[i] = Model.LoadFriendSetupPreference(FriendFlagKeys[i], true);
                friendOption = new RoClassicWindow(system, layout);
                SyncFriendCheckBoxes();
                friendOption.Pressed += (w, local) => PressFriendOption(local);
                friendOption.Clicked += child => {
                    if (child.Id == PdCloseId) CloseFriendOption();
                };
                friendOption.EscClose = () => {
                    CloseFriendOption();
                    return true;
                };
                Place(friendOption, true, 120);
                system.Windows.PushEsc(friendOption);
            }

            private void SyncFriendCheckBoxes() {
                int i = 0;
                foreach (RoClassicChildLayout child in friendOption.Layout.Children) {
                    if (child.Class != "UICheckBox") continue;
                    foreach (RoClassicOp op in child.Ops) {
                        if (op.Kind == RoClassicOpKind.Bitmap && op.Path != null) op.Path = PdUi + (friendFlags[i] ? "checkbox_1.bmp" : "checkbox_0.bmp");
                    }

                    i++;
                }

                friendOption.Invalidate();
            }

            /// <summary>msg 6 / 0xd1: a check box flips its option (client-side, no packet).</summary>
            private void PressFriendOption(Vector2Int local) {
                int i = 0;
                foreach (RoClassicChildLayout child in friendOption.Layout.Children) {
                    if (child.Class != "UICheckBox") continue;
                    if (local.x >= child.X && local.x < child.X + child.W && local.y >= child.Y && local.y < child.Y + child.H) {
                        friendFlags[i] = !friendFlags[i];
                        Model.SaveFriendSetup(friendFlags[0], friendFlags[1], friendFlags[2]);
                        SyncFriendCheckBoxes();
                        return;
                    }

                    i++;
                }
            }

            public void CloseFriendOption() => Close(ref friendOption);

            // ---- shared ----

            /// <summary>Attached to the Party / Friend window's right edge when it is open, else at (scaledX(200), scaledY(y)).</summary>
            private void Place(RoClassicWindow window, bool beside, int y) {
                RoClassicUiSystem system = owner.system;
                Vector2Int client = system.ClientSize;
                RoClassicWindow ms = owner.classicMs;
                Vector2Int p = beside && owner.MsShown
                    ? new Vector2Int(ms.Position.x + ms.Width, ms.Position.y)
                    : new Vector2Int((client.x - 640) / 2 + 200, y * client.y / 480);
                window.MoveTo(Mathf.Clamp(p.x, 0, Mathf.Max(0, client.x - window.Width)), Mathf.Clamp(p.y, 0, Mathf.Max(0, client.y - window.Height)));
                window.Visible = true;
                system.Windows.BringToFront(window);
                window.Invalidate();
            }

            private void Close(ref RoClassicWindow window) {
                if (window == null) return;
                owner.system?.Windows.RemoveEsc(window);
                window.Destroy();
                window = null;
            }

            public void CloseAll() {
                CloseSetting();
                Close(ref invitation);
                CloseFriendOption();
                Close(ref joinParty);
                Close(ref joinFriend);
            }

            // ---- diagnostics (!pf dlg ...) ----

            public string Sweep(string[] a) {
                int Arg(int i) => i < a.Length && int.TryParse(a[i], out int v) ? v : 0;
                switch (a.Length > 1 ? a[1] : "state") {
                    case "setup": owner.ClassicSetupButton(); break;
                    case "create": owner.ClassicCreateButton(); break;
                    case "invite": owner.ClassicInviteButton(); break;
                    case "press":
                        if (setting != null) PressSetting(new Vector2Int(Arg(2), Arg(3)));
                        else if (friendOption != null) PressFriendOption(new Vector2Int(Arg(2), Arg(3)));
                        break;
                    case "name":
                        (settingName ?? invitationName)?.SetText(a.Length > 2 ? string.Join(" ", a, 2, a.Length - 2) : string.Empty);
                        break;
                    case "ok":
                        if (setting != null) SettingOk();
                        else if (invitation != null) InvitationOk();
                        break;
                    case "cancel":
                        if (setting != null) CloseSetting();
                        else Close(ref invitation);
                        break;
                    case "injparty":
                        // injparty <party id> <party name>: ZC_PARTY_JOIN_REQ as if received
                        Model.Session?.InjectSocialEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcPartyInvite((uint) Arg(2),
                            a.Length > 3 ? string.Join(" ", a, 3, a.Length - 3) : "TestParty")));
                        break;
                    case "injfriend":
                        // injfriend <aid> <cid> <name>: ZC_REQ_ADD_FRIENDS as if received
                        Model.Session?.InjectSocialEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcFriendRequest((uint) Arg(2), (uint) Arg(3),
                            a.Length > 4 ? string.Join(" ", a, 4, a.Length - 4) : "Someone")));
                        break;
                    case "yes":
                    case "no":
                        if (joinParty != null) JoinPartyAnswer(a[1] == "yes");
                        else if (joinFriend != null) JoinFriendAnswer(a[1] == "yes");
                        break;
                }

                var s = new StringBuilder();
                s.Append("setting=").Append(setting == null ? "-" : variant + " " + setting.Width + "x" + setting.Height + " at " + setting.Position)
                    .Append(" exp=").Append(exp).Append(" pickup=").Append(pickup).Append(" share=").Append(share)
                    .Append(" invite=").Append(invitation == null ? "-" : invitation.Position.ToString())
                    .Append(" friendOpt=").Append(friendOption == null ? "-" : friendFlags[0] + "/" + friendFlags[1] + "/" + friendFlags[2])
                    .Append(" joinParty=").Append(joinParty == null ? "-" : Model.OpenPartyInviteId.ToString())
                    .Append(" joinFriend=").Append(joinFriend == null ? "-" : joinFriendRequest.Name);
                return s.ToString();
            }
        }
    }
}
