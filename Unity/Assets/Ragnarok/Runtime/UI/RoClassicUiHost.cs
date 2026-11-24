using Ragnarok.Assets;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Connects the classic UI core (assembly Ragnarok.ClassicUi, which references nothing of the game) to this game:
    /// client files come from the running map's GRF stack, the UI scale from the game settings (RoClassicUiScale).
    /// </summary>
    internal static class RoClassicUiHost {
        private static Maps.RoRuntimeMapController map;
        private static int mapLookupFrame = -1000;

        /// <summary>The pre-game screens' assets (the login shell's content, before any map runs).</summary>
        internal static RoAssetDatabase PreGameAssets { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() {
            map = null;
            mapLookupFrame = -1000;
            PreGameAssets = null;
            RoClassicUiImages.DefaultSourceProvider = ResolveGrfSource;
            RoClassicUiSystem.UiScaleProvider = ResolveUiScale;
            RoClassicUiSystem.WindowHotkeyText = WindowHotkeyText;
            RoClassicActorGauges.MonsterGaugesHidden = () => !RoClassicCommandFlags.MonsterHp || RoClassicCloseTabs.State == 2;
            // the anchors read the camera as it is now (it may have moved since the visibility checks of the actor ticks)
            RoClassicActorGauges.BeforeAnchors = Entities.RoClientDrawFrame.InvalidateCache;
            ClientSettings = null;
        }

        /// <summary>The shell's shortcut settings (the client's hotkey tables).</summary>
        internal static RoClientSettingsModel ClientSettings { get; set; }

        /// <summary>
        /// GetBeHaviorOfWindow: the window's toggle behaviour (BEHAVIOR_TO_WINDOWID in hotkey_v2.lub) as the interface shortcut
        /// that toggles it here, and that shortcut's key ("Alt + Q"), or empty when none is bound.
        /// </summary>
        private static string WindowHotkeyText(int windowId) {
            string action;
            switch (windowId) {
                case 0x0: action = "interface.basicinfo"; break;
                case 0x8: action = "interface.item"; break;
                case 0xa: action = "interface.equip"; break;
                case 0xb: action = "interface.info"; break;
                case 0x25: action = "interface.skill"; break;
                case 0x3b: action = "interface.guild"; break;
                case 0x45: action = "interface.party"; break;
                case 0x56: action = "interface.shortcuts"; break;
                case 0x57: action = "interface.emotions"; break;
                case 0x86:
                case 0x2718: action = "interface.quest"; break;
                case 0x8c: action = "interface.map"; break;
                case 0xc6: action = "interface.recording"; break;
                case 0xcb: action = "interface.navigation"; break;
                case 0xfb: action = "interface.bank"; break;
                case 0x107: action = "interface.mail"; break;
                case 0x10e: action = "interface.achievements"; break;
                default: return string.Empty;
            }

            RoClientSettingsModel settings = ClientSettings;
            if (settings == null || !settings.TryGetShortcutBinding(action, out RoShortcutBindingSnapshot binding) || !binding.Binding.IsAssigned) {
                return string.Empty;
            }

            return binding.Binding.ToDisplayString();
        }

        /// <summary>The game's GRF stack, once the running map has initialised it.</summary>
        private static System.Func<string, byte[]> ResolveGrfSource() {
            RoAssetDatabase assets = Assets();
            return assets != null ? assets.ReadBytes : null;
        }

        /// <summary>
        /// The running map's assets, else the pre-game screens'; the map controller is looked up at most every ten frames
        /// while there is none (MsgStr runs while windows draw).
        /// </summary>
        private static RoAssetDatabase Assets() {
            if (map == null && Time.frameCount - mapLookupFrame > 10) {
                mapLookupFrame = Time.frameCount;
                map = Object.FindFirstObjectByType<Maps.RoRuntimeMapController>();
            }

            RoAssetDatabase assets = map != null ? map.Content?.Assets : null;
            if (assets != null && assets.IsInitialized) return assets;
            return PreGameAssets != null && PreGameAssets.IsInitialized ? PreGameAssets : null;
        }

        /// <summary>The running map's assets (else the pre-game screens'), for classic windows that read client tables.</summary>
        internal static RoAssetDatabase GameAssets() => Assets();

        /// <summary>
        /// <c>MsgStr(n)</c>: line n (0-based) of data\msgstringtable.txt from the running map's assets, or
        /// <paramref name="fallback"/> before they are loaded.
        /// </summary>
        internal static string MsgStr(int index, string fallback) {
            RoAssetDatabase assets = Assets();
            string text = null;
            assets?.Tables?.MsgStringTable?.TryGetValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture), out text);
            return text ?? fallback;
        }

        private static RoAssetDatabase msgTagAssets;
        private static readonly System.Collections.Generic.Dictionary<string, string> msgTagCache =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);

        /// <summary>
        /// Server texts with message-table tags (RODEX system mails: "&lt;MSG&gt;3456,11&lt;/MSG&gt;"): each tag becomes
        /// MsgStr(id) with the comma-separated arguments put into its %d / %s in order. A tag stays as it is while the
        /// table is not loaded. Results are cached per text (the windows draw every frame).
        /// </summary>
        internal static string ResolveMsgTags(string value) {
            if (string.IsNullOrEmpty(value) || value.IndexOf("<MSG>", System.StringComparison.OrdinalIgnoreCase) < 0) return value ?? string.Empty;
            RoAssetDatabase assets = Assets();
            if (assets?.Tables?.MsgStringTable == null) return value;
            if (!ReferenceEquals(assets, msgTagAssets)) {
                msgTagAssets = assets;
                msgTagCache.Clear();
            }

            if (msgTagCache.TryGetValue(value, out string cached)) return cached;
            var s = new System.Text.StringBuilder(value.Length);
            int cursor = 0;
            while (cursor < value.Length) {
                int start = value.IndexOf("<MSG>", cursor, System.StringComparison.OrdinalIgnoreCase);
                int end = start < 0 ? -1 : value.IndexOf("</MSG>", start + 5, System.StringComparison.OrdinalIgnoreCase);
                if (start < 0 || end < 0) {
                    s.Append(value, cursor, value.Length - cursor);
                    break;
                }

                s.Append(value, cursor, start - cursor);
                string[] parts = value.Substring(start + 5, end - start - 5).Split(',');
                if (int.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id) &&
                    assets.Tables.MsgStringTable.TryGetValue(id.ToString(System.Globalization.CultureInfo.InvariantCulture), out string template) && template != null) {
                    s.Append(FormatMsgArgs(template, parts));
                } else {
                    s.Append(value, start, end + 6 - start);
                }

                cursor = end + 6;
            }

            string resolved = s.ToString();
            if (msgTagCache.Count > 256) msgTagCache.Clear();
            msgTagCache[value] = resolved;
            return resolved;
        }

        private static string FormatMsgArgs(string template, string[] parts) {
            if (parts.Length <= 1) return template;
            var s = new System.Text.StringBuilder(template.Length + 16);
            int arg = 1;
            for (int i = 0; i < template.Length; i++) {
                if (template[i] == '%' && i + 1 < template.Length && (template[i + 1] == 'd' || template[i + 1] == 's') && arg < parts.Length) {
                    s.Append(parts[arg++].Trim());
                    i++;
                } else {
                    s.Append(template[i]);
                }
            }

            return s.ToString();
        }

        private static RoAssetDatabase buyingListAssets;
        private static System.Collections.Generic.HashSet<int> buyingList;

        /// <summary>
        /// data\BuyingStoreItemList.txt (FUN_00cf10d0): the item IDs a buying store may list, '#'-terminated tokens read
        /// with atol, "//" comments. Both buying-store mirrors (0xaf, 0xb3) show only owned items in it. Null before the
        /// assets are loaded or without the file.
        /// </summary>
        internal static System.Collections.Generic.HashSet<int> BuyingStoreItemIds() {
            RoAssetDatabase assets = Assets();
            if (assets == null) return null;
            if (ReferenceEquals(assets, buyingListAssets)) return buyingList;
            buyingListAssets = assets;
            buyingList = null;
            const string path = "data/buyingstoreitemlist.txt";
            if (!assets.Exists(path)) return null;
            buyingList = RoClassicBuyingStore.ParseItemList(assets.ReadText(path));
            return buyingList;
        }

        /// <summary>mapnametable.txt's display name of a map ("prontera" → "Prontera City"), else the bare name.</summary>
        internal static string MapDisplayName(string mapName) {
            if (string.IsNullOrWhiteSpace(mapName)) return string.Empty;
            string bare = mapName.Trim();
            int slash = System.Math.Max(bare.LastIndexOf('/'), bare.LastIndexOf('\\'));
            if (slash >= 0) bare = bare.Substring(slash + 1);
            if (bare.EndsWith(".gat", System.StringComparison.OrdinalIgnoreCase) || bare.EndsWith(".rsw", System.StringComparison.OrdinalIgnoreCase)) {
                bare = bare.Substring(0, bare.Length - 4);
            }

            RoAssetTables tables = Assets()?.Tables;
            if (tables != null && (tables.TryGetMapTable(bare + ".rsw", out var table) || tables.TryGetMapTable(bare, out table)) &&
                !string.IsNullOrWhiteSpace(table.name)) {
                return table.name;
            }

            return bare;
        }

        /// <summary>The general UI scale from the game settings (RoClassicUiScale), fitted to the screen.</summary>
        private static float ResolveUiScale() => RoClassicPreGame.FitScale(RoClassicUiScale.EffectiveScale);
    }
}
