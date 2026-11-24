using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Audio;
using Ragnarok.Data;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The battleground queue (pvp-bg-spec B, CEntryQueueMgr) and the battlefield chat mode (C2):
    /// <list type="bullet">
    /// <item>0x8d8 result 1 toggles the Standby window 0xd1, plays se_btg_request.wav, "[arena]" + MsgStr 0x833 in
    /// #FF6347 and queues; other results a message box (0x834..0x83d);</item>
    /// <item>0x8d9 fills 0xd1 ("Battlefield name:%s", "Persons required:%d", "Your position:%d"), opening it if needed;</item>
    /// <item>0x8db: 1 / 0xe close 0xd1 and dequeue with a box (0x83e / 0x841), 3 / 0xb only a box; always "[arena]" +
    /// MsgStr 0x842 in red;</item>
    /// <item>0x8df toggles the request window 0xd2 (se_btg_ready.wav and "[arena]" + 0x843 when it opens), 20 s to
    /// answer (0x856), OK → 0x8e0 result 1 and "Waiting for the opponents." in #FFFF64, Cancel / timeout → result 2;</item>
    /// <item>0x8e1 result 1 plays se_btg_forward.wav; 0x8de 3 / 0xb a box; 0x90e dequeues; 0x8dc is ignored.</item>
    /// </list>
    /// The arenas come from entryqueuelist (BattleFieldName, DisplayBattleFieldName, AllianceNum, EnemyNum). The list window
    /// 0x9d is not ported: Custom drops its menu icon 0x174. "/battlechat" toggles sending ordinary lines as 0x2db.
    /// </summary>
    public sealed class RoClassicEntryQueue : IRoClassicUiProducer {
        private const string Ui = "유저인터페이스\\";
        private const string Basic = Ui + "basic_interface\\";
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private sealed class Arena {
            public string Name, Display;
            public int Alliance, Enemy;
        }

        private static RoClassicEntryQueue instance;
        private static bool battleChat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            instance = null;
            battleChat = false;
        }

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private RoClassicChat chat;
        private readonly List<RoZcEntryQueue> drained = new List<RoZcEntryQueue>();
        private List<Arena> arenas;
        private RoClassicWindow standby, request;
        private readonly string[] standbyLines = new string[3];
        private string bgName = string.Empty, lobbyName = string.Empty;
        private Arena arena;
        private bool queued;
        private float requestOpened;
        private int requestShown = -1;

        /// <summary>The session the raw queue was last drained from (for answers sent from its handlers).</summary>
        internal static RoLoginShellSession CurrentSession => instance?.session;

        /// <summary>
        /// ZC_ITEM_PICKUP_PARTY 0x0b67 {AID @2, item id @6, …} (0xc8ef30): a party member other than me picked an item up →
        /// MsgStr 0x4fc "%s has acquired %s." with the member's name and the item's name, #00FFFF, chat type 9 (party).
        /// </summary>
        private static void PartyPickup(byte[] b) {
            RoLoginShellSession session = CurrentSession;
            if (b == null || b.Length < 10 || session == null) return;
            uint aid = BitConverter.ToUInt32(b, 2);
            int itemId = BitConverter.ToInt32(b, 6);
            if (aid == session.LocalCharacterIds().AccountId) return;
            string member = null;
            foreach (RoPartyMemberRuntimeSnapshot m in session.PartySnapshot().Members ?? Array.Empty<RoPartyMemberRuntimeSnapshot>()) {
                if (m.AccountId == aid) member = m.Name;
            }

            if (member == null) return;
            Ragnarok.Data.RoItemMetadataTable items = RoClassicUiHost.GameAssets()?.GameData?.Items;
            string item = items != null ? items.GetItemOrFallback(itemId).GetDisplayName(true) : itemId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string format = RoClassicUiHost.MsgStr(0x4fc, "%s has acquired %s.");
            int at = format.IndexOf("%s", StringComparison.Ordinal);
            if (at >= 0) format = format.Substring(0, at) + member + format.Substring(at + 2);
            at = format.IndexOf("%s", StringComparison.Ordinal);
            if (at >= 0) format = format.Substring(0, at) + item + format.Substring(at + 2);
            // 0xc8ef30: type 9 "Party member's obtained item", COLORREF 0x00FFFF00 (no mgr msg 3)
            RoClassicChat.Instance?.AddTypedLine(format, RoChatLineTypes.Cyan, RoChatLineTypes.PartyItem);
        }

        /// <summary>Per frame from the chat window: the queue packets received since the last frame.</summary>
        public static void Tick(RoLoginShellSession session, RoClassicChat chat) {
            if (session == null) return;
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return;
            if (instance == null || instance.system != system) {
                instance = new RoClassicEntryQueue { system = system };
                system.Register(instance);
            }

            instance.session = session;
            instance.chat = chat;
            instance.drained.Clear();
            session.DrainEntryQueue(instance.drained);
            foreach (RoZcEntryQueue packet in instance.drained) instance.Handle(packet);
            RoClassicRanking.Tick();
        }

        // ---------------------------------------------------------------- the battlefield chat (C2)

        /// <summary>"/battlechat" (slash id 0xb8): the mode toggles, MsgStr 0x555 / 0x556 in the chat.</summary>
        public static bool TryCommand(string message, RoClassicChat chat) {
            if (!string.Equals(message?.Trim(), "/battlechat", StringComparison.OrdinalIgnoreCase)) return false;
            battleChat = !battleChat;
            chat?.AddLine(battleChat
                ? RoClassicUiHost.MsgStr(0x555, "Battlefield Chat has been activated.")
                : RoClassicUiHost.MsgStr(0x556, "Battlefield Chat has been deactivated."), "#FFFF00");
            return true;
        }

        /// <summary>The mode on: an ordinary line goes out as CZ_BATTLEFIELD_CHAT "Name : text" (the server echoes it as 0x2dc).</summary>
        public static bool TryBattleChat(string message, RoLoginShellSession session) {
            if (!battleChat || session == null || string.IsNullOrEmpty(message) || message[0] == '/') return false;
            _ = session.BeginBattlefieldChat((session.SelectedCharacterName ?? string.Empty) + " : " + message);
            return true;
        }

        // ---------------------------------------------------------------- packets (B2)

        private void Handle(RoZcEntryQueue packet) {
            byte[] b = packet.Bytes;
            switch (packet.PacketId) {
                case RoPacketIds.ZcAckEntryQueueApply: {
                    int result = Byte(b, 2);
                    if (result != 1) {
                        Box(ApplyResult(result));
                        break;
                    }

                    ToggleStandby();
                    bgName = Text(b, 3, 24);
                    arena = FindArena(bgName);
                    RoAudioEvents.PlayUiSound("se_btg_request.wav");
                    Chat("[" + (arena?.Display ?? string.Empty) + "]" + RoClassicUiHost.MsgStr(0x833, "You are now in the battlefield queue."), "#FF6347");
                    queued = true;
                    break;
                }
                case RoPacketIds.ZcNotifyEntryQueueApply: {
                    queued = true;
                    if (standby == null || !standby.Visible) ToggleStandby();
                    string name = Text(b, 2, 24);
                    arena ??= FindArena(name);
                    if (arena != null) {
                        standbyLines[0] = Format(RoClassicUiHost.MsgStr(0x858, "Battlefield name:%s"), name);
                        standbyLines[1] = Format(RoClassicUiHost.MsgStr(0x859, "Persons required:%d"), (arena.Alliance + arena.Enemy).ToString(CultureInfo.InvariantCulture));
                        standbyLines[2] = Format(RoClassicUiHost.MsgStr(0x85a, "Your position:%d"), Int(b, 26).ToString(CultureInfo.InvariantCulture));
                        standby.Invalidate();
                    }

                    break;
                }
                case RoPacketIds.ZcAckEntryQueueCancel: {
                    int result = Byte(b, 2);
                    if (result == 1 || result == 0xe) {
                        Box(result == 1
                            ? RoClassicUiHost.MsgStr(0x83e, "You have left the battlefield queue.")
                            : RoClassicUiHost.MsgStr(0x841, "The selected arena is unavailable; your application has been cancelled"));
                        if (standby != null) standby.Visible = false;
                        queued = false;
                    } else if (result == 3) {
                        Box(RoClassicUiHost.MsgStr(0x83f, "Wrong battlefield name."));
                    } else if (result == 0xb) {
                        Box(RoClassicUiHost.MsgStr(0x840, "You are not in the battlefield queue list"));
                    }

                    Chat("[" + (arena?.Display ?? string.Empty) + "]" + RoClassicUiHost.MsgStr(0x842, "You have left the queue"), "#FF0000");
                    arena = null;
                    break;
                }
                case RoPacketIds.ZcReplyAckEntryQueueAdmission: {
                    int result = Byte(b, 2);
                    if (result == 3) Box(RoClassicUiHost.MsgStr(0x83f, "Wrong battlefield name."));
                    else if (result == 0xb) Box(RoClassicUiHost.MsgStr(0x840, "You are not in the battlefield queue list"));
                    break;
                }
                case RoPacketIds.ZcNotifyLobbyAdmission:
                    bgName = Text(b, 2, 24);
                    lobbyName = Text(b, 26, 24);
                    arena ??= FindArena(bgName);
                    if (request != null && request.Visible) {
                        request.Visible = false;
                        break;
                    }

                    OpenRequest();
                    RoAudioEvents.PlayUiSound("se_btg_ready.wav");
                    Chat("[" + (arena?.Display ?? string.Empty) + "]" + RoClassicUiHost.MsgStr(0x843, "Are you sure you want to join a battleground?"), "#FF6347");
                    break;
                case RoPacketIds.ZcReplyAckLobbyAdmission:
                    if (Byte(b, 2) == 1) RoAudioEvents.PlayUiSound("se_btg_forward.wav");
                    break;
                case RoPacketIds.ZcEntryQueueReset:
                    arena = null;
                    queued = false;
                    break;
                case RoPacketIds.ZcAllAchievementList:
                case RoPacketIds.ZcAchievementUpdate:
                case 0x0a26:
                case 0x0a2f:
                    // and the achievements' (and the titles')
                    RoClassicAchievements.Handle(b);
                    break;
                case RoPacketIds.ZcBroadcast:
                case RoPacketIds.ZcBroadcast2:
                case 0x07fd:
                case 0x0bba:
                case 0x0ada:
                    // and the server broadcasts' boxes
                    RoClassicBroadcast.Handle(b);
                    break;
                case 0x0b67:
                    PartyPickup(b);
                    break;
                case RoClassicSystemLines.ZcNotifyExp2:
                case RoClassicSystemLines.ZcNotifyExp:
                case RoClassicSystemLines.ZcEquipItemDamaged:
                case RoClassicSystemLines.ZcBroadcast4:
                case RoClassicSystemLines.ZcGospelInfo:
                case RoClassicRanking.ZcAckRanking:
                case RoClassicRanking.ZcAckRankingOld:
                case RoClassicRanking.ZcUpdateRankingPoint:
                    // the typed chat lines (exp 18, damaged equipment 13, the server's typed line) and the rankings
                    RoClassicSystemLines.HandleRaw(b);
                    break;
                case 0x00d1:
                case 0x00d2:
                case 0x00d4:
                case 0x0ae1:
                    // the chat block answers (/ex, /in, the menu's Block Chat)
                    RoClassicChatBlock.Handle(b);
                    break;
                case RoChatSlashCommands.ZcUserCount:
                case RoChatSlashCommands.ZcAckRememberWarpPoint:
                    // the answers of /who and /memo
                    RoClassicChat.HandleCommandReply(b);
                    break;
                case 0x02cb:
                case 0x02cc:
                case 0x02cd:
                case 0x02ce:
                    // the memorial dungeon window
                    RoClassicMemorial.Handle(b);
                    break;
                case 0x098a:
                case 0x0988:
                case 0x0989:
                    // the clan window
                    RoClassicClan.Handle(b);
                    break;
                case 0x0294:
                    // readbook: the book reader
                    RoClassicBook.HandleReadBook(b);
                    break;
                case 0x018c:
                    // Sense / Monster Observation
                    RoClassicMonsterInfo.Handle(b);
                    break;
                case 0x0253:
                    // Star Gladiator's Feel confirm
                    RoClassicPrompts.HandleStarPlace(b);
                    break;
                case 0x020e:
                    // Star Gladiator's places / monsters, the Taekwon / taming / collector missions
                    RoClassicPrompts.HandleStarSkill(b);
                    break;
                case 0x0ae7:
                case 0x0afa:
                    // the Adventurer Agency's join request (to the leader) and its result
                    RoClassicAgency.Handle(b);
                    break;
                case 0x0b36:
                case 0x0b47:
                case 0x01b4:
                    // the guild emblems' versions and images
                    RoClassicGuildEmblems.Handle(b);
                    break;
                case 0x0b2d:
                case 0x0b2e:
                    // the guild castle list's check / move answers
                    RoClassicGuild.HandleAgit(b);
                    break;
                case 0x01f6:
                case 0x0216:
                    // the adoption offer and its errors
                    RoClassicAdoption.Handle(b);
                    break;
                case RoPacketIds.ZcUiOpen:
                case RoPacketIds.ZcAttendanceResponse:
                    // and the attendance check's (0x0ae2 type 7, 0x0af0)
                    RoClassicAttendance.Handle(b);
                    break;
                case RoPacketIds.ZcPrivateAirshipResponse:
                    // the same raw queue carries the private airship's answer to the navigation
                    RoClassicNavigation.HandleAirshipResponse(b);
                    break;
            }
        }

        private static string ApplyResult(int result) {
            switch (result) {
                case 2: return RoClassicUiHost.MsgStr(0x834, "Queuing has finished.");
                case 3: return RoClassicUiHost.MsgStr(0x835, "Invalid name of the battlefield.");
                case 4: return RoClassicUiHost.MsgStr(0x836, "Invalid type of application.");
                case 5: return RoClassicUiHost.MsgStr(0x837, "People count exceeded.");
                case 6: return RoClassicUiHost.MsgStr(0x838, "Your level doesn't fit this battlefield rules.");
                case 7: return RoClassicUiHost.MsgStr(0x839, "Duplicate application.");
                case 8: return RoClassicUiHost.MsgStr(0x83a, "After reconnecting, please re-apply.");
                case 9: return RoClassicUiHost.MsgStr(0x83c, "Only party leader / guild master can apply.");
                case 10: return RoClassicUiHost.MsgStr(0x83b, "Your class can't participate in this battlefield.");
                case 15: return RoClassicUiHost.MsgStr(0x83d, "You can't apply while your team member is already on a battlefield.");
                default: return null;
            }
        }

        private static int Byte(byte[] b, int at) => at < b.Length ? b[at] : 0;

        private static int Int(byte[] b, int at) => at + 4 <= b.Length ? BitConverter.ToInt32(b, at) : 0;

        private static string Text(byte[] b, int at, int length) {
            if (at >= b.Length) return string.Empty;
            int n = Math.Min(length, b.Length - at), end = 0;
            while (end < n && b[at + end] != 0) end++;
            var raw = new byte[end];
            Array.Copy(b, at, raw, 0, end);
            return Ragnarok.Assets.RoTextEncoding.Decode(raw);
        }

        private static string Format(string pattern, string value) {
            int at = pattern.IndexOf('%');
            return at < 0 || at + 1 >= pattern.Length ? pattern + value : pattern.Substring(0, at) + value + pattern.Substring(at + 2);
        }

        private static byte[] Name24(string text) {
            var name = new byte[24];
            byte[] raw = RoLoginShellSession.TextBytes(text ?? string.Empty);
            Array.Copy(raw, name, Math.Min(23, raw.Length));
            return name;
        }

        private void Box(string text) {
            if (!string.IsNullOrEmpty(text)) RoClassicMessageBox.Show(text, RoClassicMessageBox.TypeOk, true, _ => { }, true, "Message");
        }

        private void Chat(string text, string color) => chat?.AddLine(text, color);

        /// <summary>EntryQueueList (luafiles514\lua files\entryqueue\entryqueuelist.lub): name, display name, team sizes.</summary>
        private Arena FindArena(string name) {
            if (arenas == null) {
                arenas = new List<Arena>();
                try {
                    Ragnarok.Assets.RoAssetDatabase assets = RoClassicUiHost.GameAssets();
                    byte[] bytes = assets?.ReadBytes("data/luafiles514/lua files/entryqueue/entryqueuelist.lub");
                    if (bytes != null && bytes.Length > 0 && bytes[0] != 0x1b) {
                        var context = RoWorldMapLuaParser.ParseTextContext("entryqueuelist.lub", Ragnarok.Assets.RoTextEncoding.Decode(bytes));
                        LuaValue list = context.GetGlobal("EntryQueueList");
                        if (list != null && list.Kind == LuaValueKind.Table) {
                            foreach (KeyValuePair<string, LuaValue> field in list.Table.Fields) AddArena(field.Value);
                            foreach (LuaValue row in list.Table.ArrayValues) AddArena(row);
                        }
                    }
                } catch (Exception e) {
                    Debug.LogWarning("Entry queue list: " + e.Message);
                }
            }

            foreach (Arena a in arenas) {
                if (string.Equals(a.Name, name, StringComparison.Ordinal)) return a;
            }

            return null;
        }

        private void AddArena(LuaValue row) {
            if (row == null || row.Kind != LuaValueKind.Table) return;
            string Field(string key) {
                foreach (KeyValuePair<string, LuaValue> f in row.Table.Fields) {
                    if (string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)) return f.Value.ToDisplayString();
                }

                return string.Empty;
            }

            int.TryParse(Field("AllianceNum"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int alliance);
            int.TryParse(Field("EnemyNum"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int enemy);
            string name = Field("BattleFieldName");
            if (name.Length > 0) arenas.Add(new Arena { Name = name, Display = Field("DisplayBattleFieldName"), Alliance = alliance, Enemy = enemy });
        }

        // ---------------------------------------------------------------- windows (B3, B4)

        private static RoClassicChildLayout Button(int id, string path, int x, int y, int w, int h, string stem) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = w, H = h };
            button.States["normal"] = stem + ".bmp";
            button.States["hover"] = stem + "_a.bmp";
            button.States["pressed"] = stem + "_b.bmp";
            return button;
        }

        private Vector2Int DefaultPosition() {
            Vector2Int c = system.ClientSize;
            return new Vector2Int((c.x - 640) / 2 + 185, (c.y - 480) / 2 + 180);
        }

        /// <summary>A style-1 title with its caption black at (18, 3) over white at (19, 4), the body under it.</summary>
        private static void DrawFrame(RoClassicWindow w, string title) {
            w.DrawBitmapRaw(0, 0, Basic + "titlebar_left.bmp");
            for (int x = 12; x < w.Width - 12; x += 12) w.DrawBitmapRaw(x, 0, Basic + "titlebar_mid.bmp");
            w.DrawBitmapRaw(w.Width - 12, 0, Basic + "titlebar_right.bmp");
            w.TextOut(19, 4, title, 0, 12, White);
            w.TextOut(18, 3, title, 0, 12, Black);
            w.FillRect(0, 17, w.Width, w.Height - 17, White);
            w.RoundOutline(0, 17, w.Width, w.Height - 17, new Color32(0xc8, 0xc8, 0xc8, 255));
        }

        /// <summary>0xd1 toggled (MakeWindow / DeleteWindow): 280×120; Cancel waiting (0) sends 0x8da and stays, Close (1) only closes.</summary>
        private void ToggleStandby() {
            if (standby != null && standby.Visible) {
                standby.Visible = false;
                return;
            }

            if (standby == null) {
                var layout = new RoClassicWindowLayout { Class = "UIEntryQueueStandByWnd", WindowId = 0xd1, Width = 280, Height = 120 };
                var baseButton = new RoClassicChildLayout { Class = "UIBitmapButton", Id = 0x248, Path = "base", X = 3, Y = 3, W = 11, H = 11 };
                baseButton.States["normal"] = Basic + "sys_base_off.bmp";
                baseButton.States["hover"] = Basic + "sys_base_on.bmp";
                baseButton.States["pressed"] = Basic + "sys_base_on.bmp";
                layout.Children.Add(baseButton);
                layout.Children.Add(Button(0, "cancel", 159, 96, 68, 20, Ui + "cancel_waiting"));
                layout.Children.Add(Button(1, "close", 234, 96, 42, 20, Ui + "btn_close"));
                standby = new RoClassicWindow(system, layout) { Visible = false };
                standby.DrawDynamic += w => {
                    DrawFrame(w, RoClassicUiHost.MsgStr(0x857, "Standby position"));
                    w.FillRect(10, 22, 260, 65, White);
                    for (int i = 0; i < standbyLines.Length; i++) {
                        if (!string.IsNullOrEmpty(standbyLines[i])) w.TextOut(13, 26 + 18 * i, standbyLines[i], 0, 12, Black);
                    }
                };
                standby.Clicked += child => {
                    if (child.Id == 0 && arena != null) _ = session?.BeginEntryQueuePacket(0x08da, Name24(arena.Name));
                    else if (child.Id == 1) standby.Visible = false;
                };
            }

            Array.Clear(standbyLines, 0, standbyLines.Length);
            Vector2Int p = DefaultPosition();
            standby.MoveTo(p.x, p.y);
            standby.Visible = true;
            system.Windows.BringToFront(standby);
            standby.Invalidate();
        }

        /// <summary>0xd2: 300×250, the arena's lines, OK (0) / Cancel (1), "Accept standby time:%d seconds" counting down from 20.</summary>
        private void OpenRequest() {
            if (request == null) {
                var layout = new RoClassicWindowLayout { Class = "UIEntryQueueRequestWnd", WindowId = 0xd2, Width = 300, Height = 250 };
                var close = new RoClassicChildLayout { Class = "UIBitmapButton", Id = 0xc5, Path = "x", X = 286, Y = 3, W = 11, H = 11 };
                close.States["normal"] = Basic + "sys_close_off.bmp";
                close.States["hover"] = Basic + "sys_close_on.bmp";
                close.States["pressed"] = Basic + "sys_close_on.bmp";
                layout.Children.Add(close);
                layout.Children.Add(Button(0, "ok", 209, 226, 42, 20, Ui + "btn_ok"));
                layout.Children.Add(Button(1, "cancel", 254, 226, 42, 20, Ui + "btn_cancel"));
                request = new RoClassicWindow(system, layout) { Visible = false };
                request.DrawDynamic += DrawRequest;
                request.Clicked += child => {
                    if (child.Id == 0) Answer(true);
                    else if (child.Id == 1) Answer(false);
                    // the X (0xc5) is not handled by vf37 0xacc550: nothing happens
                };
            }

            requestOpened = Time.unscaledTime;
            requestShown = -1;
            Vector2Int p = DefaultPosition();
            request.MoveTo(p.x, p.y);
            request.Visible = true;
            system.Windows.BringToFront(request);
            request.Invalidate();
        }

        private int SecondsLeft => 20 - (int) (Time.unscaledTime - requestOpened) % 60;

        private void DrawRequest(RoClassicWindow w) {
            DrawFrame(w, RoClassicUiHost.MsgStr(0x855, "Battlefield position request"));
            w.FillRect(10, 22, 280, 200, White);
            string first = arena != null ? Format(RoClassicUiHost.MsgStr(0x84e, "%s battle begins."), arena.Display) : string.Empty;
            string[] lines = {
                first, RoClassicUiHost.MsgStr(0x84f, "Do you want to enter the arena?"), string.Empty, RoClassicUiHost.MsgStr(0x850, "[Note]"),
                RoClassicUiHost.MsgStr(0x851, "When the battle is finished your character will"),
                RoClassicUiHost.MsgStr(0x852, "be returned to the current spot or (if it's not"), RoClassicUiHost.MsgStr(0x853, "possible) to the save point."),
            };
            for (int i = 0; i < lines.Length; i++) w.TextOut(13, 26 + 18 * i, lines[i], 0, 12, Black);
            w.TextOut(10, 232, Format(RoClassicUiHost.MsgStr(0x856, "Accept standby time:%d seconds"), Math.Max(0, SecondsLeft).ToString(CultureInfo.InvariantCulture)), 0, 12, Black);
        }

        /// <summary>OK: 0x8e0 result 1, both windows close, "Waiting for the opponents."; Cancel / timeout: result 2, dequeued.</summary>
        private void Answer(bool accept) {
            if (arena != null) {
                var body = new byte[49];
                body[0] = (byte) (accept ? 1 : 2);
                Array.Copy(Name24(arena.Name), 0, body, 1, 24);
                Array.Copy(Name24(lobbyName), 0, body, 25, 24);
                _ = session?.BeginEntryQueuePacket(0x08e0, body);
            }

            if (standby != null) standby.Visible = false;
            if (request != null) request.Visible = false;
            if (accept) Chat(RoClassicUiHost.MsgStr(0x854, "Waiting for the opponents."), "#FFFF64");
            else queued = false;
        }

        /// <summary>The countdown: a redraw per second, the automatic decline at 0.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            if (request == null || !request.Visible) return;
            int left = SecondsLeft;
            if (left != requestShown) {
                requestShown = left;
                request.Invalidate();
            }

            if (left < 1) Answer(false);
        }

        // ---------------------------------------------------------------- diagnostics (RoSkillSweepRunner "!bg …")

        /// <summary>inject hex (a whole received packet) | chat on/off | state.</summary>
        internal static string Sweep(string command) {
            RoClassicEntryQueue q = instance;
            if (q == null) return "no queue";
            string[] a = (command ?? string.Empty).Trim().Split(new[] { ' ' }, 2);
            if (a[0] == "inject" && a.Length > 1) {
                string hex = a[1].Replace(" ", string.Empty);
                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                q.Handle(new RoZcEntryQueue((ushort) (bytes[0] | bytes[1] << 8), bytes));
            }

            var s = new StringBuilder();
            s.Append("queued=").Append(q.queued).Append(" bg='").Append(q.bgName).Append("' lobby='").Append(q.lobbyName)
                .Append("' arena=").Append(q.arena != null ? q.arena.Display + " " + q.arena.Alliance + "v" + q.arena.Enemy : "-")
                .Append(" standby=").Append(q.standby != null && q.standby.Visible).Append(" [").Append(string.Join("|", q.standbyLines)).Append(']')
                .Append(" request=").Append(q.request != null && q.request.Visible ? q.SecondsLeft.ToString(CultureInfo.InvariantCulture) + "s" : "closed")
                .Append(" battlechat=").Append(battleChat);
            return s.ToString();
        }
    }
}
