using System;
using System.Collections.Generic;
using System.IO;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The commands of 0xc07a00 that were silent until docs/audits/chat-options-2026-10-04.md: the head / body turns, /hi,
    /// /automsg, /savechat, /autosavechat, /eqopen and /font. The pure parts are in <see cref="RoChatOptionCommands"/>.
    /// </summary>
    public sealed partial class RoClassicChat {
        private static readonly RoChatOptionCommands.DoriDoriClock doriDori = new RoChatOptionCommands.DoriDoriClock();
        private static int pendingHiAcks;   // [0x01555ead]
        private static string autoSavePath;   // [0x01552e88] on: the log file (0x153a8a0)
        private static HashSet<uint> gmAids;  // [0x014ef1e4..e8]

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOptionStatics() {
            pendingHiAcks = 0;
            autoSavePath = null;
            gmAids = null;
        }

        private static Func<string> clientInfoXml = () => RoClassicUiHost.GameAssets()?.ReadText("data/clientinfo.xml");

        /// <summary>The source of clientinfo.xml for the GM list (0xa01160), read once; setting it reads the list again.</summary>
        public static Func<string> ClientInfoXml {
            get => clientInfoXml;
            set {
                clientInfoXml = value;
                gmAids = null;
            }
        }

        /// <summary>0xa010f0: the AID is in the client's GM list.</summary>
        public static bool IsGmAid(uint aid) {
            if (aid == 0) return false;
            if (gmAids == null) {
                string xml = null;
                try {
                    xml = ClientInfoXml?.Invoke();
                } catch (Exception e) {
                    Debug.LogWarning("clientinfo.xml: " + e.Message);
                }

                gmAids = new HashSet<uint>(RoChatOptionCommands.ParseClientInfoAdmins(xml));
            }

            return gmAids.Contains(aid);
        }

        /// <summary>
        /// /autosavechat (case 0x66, 0xc07c92): for an AID outside the GM list 0xd015a0(7) fails and the client answers like
        /// case 3 ("Invalid Command", yellow). A GM flips [0x01552e88]: on, the line counter (mode+0x5d0) restarts, the log
        /// is "Chat\Chat.txt" or the first free "Chat%03d.txt" and "Chatting Auto Save Start.." prints; off, "Chatting Auto
        /// Save END" (0xbf3210 closes it). Both lines COLORREF 0xffff. The client writes next to game.exe; here the data folder.
        /// </summary>
        private void ToggleAutoSaveChat() {
            if (!IsGmAid(session != null ? session.LocalActorId : 0)) {
                AddLine(Msg(0x5f, "Invalid Command"), RoChatSlashCommands.YellowColor, RoChatChannel.System);
                return;
            }

            if (autoSavePath != null) {
                autoSavePath = null;
                AddLine("Chatting Auto Save END", RoChatSlashCommands.YellowColor, RoChatChannel.System);
                return;
            }

            string directory = Path.Combine(Application.persistentDataPath, "Chat");
            try {
                Directory.CreateDirectory(directory);
            } catch (Exception e) {
                Debug.LogWarning("/autosavechat: " + e.Message);
            }

            autoSavePath = RoChatOptionCommands.AutoSaveChatPath(directory, File.Exists);
            AddLine("Chatting Auto Save Start..", RoChatSlashCommands.YellowColor, RoChatChannel.System);
        }

        /// <summary>An NPC / channel chat line (ZC_NPC_CHAT 0x2c1, 0xc5b340), which the chat adds as a system line.</summary>
        internal static void AutoSaveNpcChatLine(string text) {
            if (autoSavePath != null && !string.IsNullOrWhiteSpace(text)) AutoSaveChatLine(text.Trim(), RoChatChannel.Main);
        }

        /// <summary>0xc10e60: while the GM log runs, a chat line of the logged kinds is appended with CR LF (code page 1252).</summary>
        private static void AutoSaveChatLine(string text, RoChatChannel channel) {
            if (autoSavePath == null || !RoChatOptionCommands.IsAutoSavedChannel(channel)) return;
            try {
                Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
                File.AppendAllText(autoSavePath, text + "\r\n", System.Text.Encoding.GetEncoding(1252));
            } catch (Exception e) {
                Debug.LogWarning("/autosavechat: " + e.Message);
                autoSavePath = null;
            }
        }

        /// <summary>The talk types this file runs; false for the others.</summary>
        private bool RunOptionCommand(int type, string argument) {
            switch (type) {
                case RoChatSlashCommands.TypeDoriDori:
                case RoChatSlashCommands.TypeBingBing:
                case RoChatSlashCommands.TypeBangBang:
                    TurnHeadOrBody(type);
                    return true;
                case RoChatSlashCommands.TypeHi:
                    SendHi(argument);
                    return true;
                case RoChatSlashCommands.TypeAutoMsg:
                    // case 0x97: [0x01552ff0] flips, mgr msg 3; on → UIAutoMessageWnd 0x74, off → MsgStr 0x3af
                    ClassicPopOpen();
                    if (RoClassicAutoMessage.Toggle()) return true;
                    AddLine(Msg(0x3af, "Cancel automated away message."), RoChatSlashCommands.OptionColor, RoChatChannel.System);
                    return true;
                case RoChatSlashCommands.TypeSaveChat:
                    SaveChat();
                    return true;
                case RoChatSlashCommands.TypeAutoSaveChat:
                    ToggleAutoSaveChat();
                    return true;
                case RoChatSlashCommands.TypeEquipOpen:
                    // case 0xb7: mode msg 0xfd (CZ_CONFIG type 0) with the "show equip" option flipped
                    if (session != null) RoClassicEquipment.ToggleShowEquipConfig(session);
                    return true;
                case RoChatSlashCommands.TypeFont:
                    // case 0x5c: CGameMode+0x46c = (it == 0): under the character → over it, any other place → under it
                    RoClassicNameBalloon.OwnNamePlacement = RoClassicNameBalloon.OwnNamePlacement == 0 ? 1 : 0;
                    return true;
            }

            return false;
        }

        /// <summary>
        /// /doridori (case 0x6c): nothing under EFST_SUHIDE; the head goes to the other side and CZ_CHANGE_DIRECTION2 sends
        /// it with the body's direction; while sitting the clock may add CZ_DORIDORI. /bingbing / /bangbang (0x6d / 0x6e):
        /// the body one step on either way, the packet's head 0 (the own head stays as it was).
        /// </summary>
        private void TurnHeadOrBody(int type) {
            if (session == null) return;
            RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null || world.OwnActorId == 0 || !world.State.TryGetActor(world.OwnActorId, out RoOnlineActorSnapshot own)) return;
            byte direction = RoChatOptionCommands.PacketDirection(own.Direction);
            if (type == RoChatSlashCommands.TypeDoriDori) {
                if (HasOwnStatus(RoChatOptionCommands.StatusSuHide)) return;
                ushort head = RoChatOptionCommands.DoriDoriHead(own.HeadDirection);
                world.State.ApplyActorDirectionChange(new RoZcChangeDirection(world.OwnActorId, head, direction));
                _ = session.BeginClassicPacket(RoChatOptionCommands.CzChangeDirection2, RoChatOptionCommands.ChangeDirectionBody(head, direction), "a head turn");
                if (own.Motion == RoOnlineActorMotion.Sitting && doriDori.Record(Environment.TickCount & int.MaxValue)) {
                    _ = session.BeginClassicPacket(RoChatOptionCommands.CzDoriDori, Array.Empty<byte>(), "a doridori");
                }

                return;
            }

            byte turned = RoChatOptionCommands.TurnBody(direction, type == RoChatSlashCommands.TypeBingBing);
            world.State.ApplyActorDirectionChange(new RoZcChangeDirection(world.OwnActorId, own.HeadDirection, turned));
            _ = session.BeginClassicPacket(RoChatOptionCommands.CzChangeDirection2, RoChatOptionCommands.ChangeDirectionBody(0, turned), "a body turn");
        }

        private bool HasOwnStatus(int statusId) {
            foreach (RoStatusIconSnapshot status in session.StatusIconSnapshot()) {
                if (status.StatusId == statusId && status.Active) return true;
            }

            return false;
        }

        /// <summary>
        /// /hi (case 0x95 → 0xcf0430): "(Hi) text" (or "(Hi) *^_^*") whispered to every friend of the list, their answers
        /// kept quiet ([0x01555ead], at most 40), then "[ To Friends ] : (Hi) …" in COLORREF 0x1effff.
        /// </summary>
        private void SendHi(string argument) {
            if (session == null) return;
            string whisper = RoChatOptionCommands.HiWhisper(argument);
            foreach (RoFriendRuntimeSnapshot friend in session.FriendsSnapshot()) {
                if (string.IsNullOrEmpty(friend.Name)) continue;
                _ = session.BeginSendPrivateMessage(friend.Name, whisper);
                if (pendingHiAcks < RoChatOptionCommands.MaxPendingHiAcks) pendingHiAcks++;
            }

            AddLine(RoChatOptionCommands.HiLine(argument), RoChatOptionCommands.HiColor, RoChatChannel.System);
        }

        /// <summary>
        /// ZC_ACK_WHISPER (0xc29c80) while /hi whispers are out: a sent one (0) or one to a friend who is offline (1) prints
        /// nothing and counts one down; true when the answer is to stay quiet.
        /// </summary>
        public static bool TryConsumeHiAck(int result) {
            if (pendingHiAcks <= 0 || (result != 0 && result != 1)) return false;
            pendingHiAcks--;
            return true;
        }

        /// <summary>
        /// /savechat (case 0x33 → mgr msg 6 → 0x8a24c0): every tab's lines into "Chat\Chat_&lt;tab&gt;.txt" (the first free
        /// "_%03d" name), one line and a line feed each, in the client's code page; each file it writes says
        /// "&lt;path&gt; is Saved." in green after mgr msg 3. The client writes next to game.exe; here the player's data folder.
        /// </summary>
        private void SaveChat() {
            if (classicChat == null) return;
            if (classicBoxesDirty || classicBoxes.Count != tabs.Count) RebuildClassicBoxes();
            // the client shows the full Windows path ("<exe dir>\Chat\Chat_<tab>.txt is Saved.")
            string directory = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Chat"));
            try {
                Directory.CreateDirectory(directory);
            } catch (Exception e) {
                Debug.LogWarning("/savechat: " + e.Message);
                return;
            }

            Ragnarok.Assets.RoTextEncoding.EnsureCodePagesRegistered();
            System.Text.Encoding encoding = System.Text.Encoding.GetEncoding(1252);
            for (int t = 0; t < tabs.Count && t < classicBoxes.Count; t++) {
                string path = RoChatOptionCommands.SaveChatPath(directory, model.TabName(t), File.Exists);
                try {
                    using (var writer = new StreamWriter(path, false, encoding)) {
                        writer.NewLine = "\r\n";   // fopen "w": the line feed becomes CR LF
                        foreach (ClassicChatLine line in classicBoxes[t].Lines) writer.WriteLine(line.Text);
                    }
                } catch (Exception e) {
                    Debug.LogWarning("/savechat: " + e.Message);   // fopen failed: no line for this tab
                    continue;
                }

                ClassicPopOpen();
                AddLine(path + " is Saved.", RoChatOptionCommands.SavedColor, RoChatChannel.System);
            }
        }
    }
}
