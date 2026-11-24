using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Data;
using Ragnarok.UI.Classic;

namespace Ragnarok.UI {
    /// <summary>
    /// The client's &lt;ITEML&gt; item links (iteml-spec §1, §2): base 62 over 0-9a-zA-Z, the payload = the wear-location
    /// mask in 5 characters, an equipment flag character, the item id, then '%' refine, '&amp;' view, '\'' grade, ')' each
    /// card (zero cards kept in place) and '+id,param-value' each random option, every value variable-length (parser
    /// 0x7e11a0 / build 0x7f4520, encoder 0x7fe020). Received text shows each decodable link as "&lt;name&gt;" with the name
    /// cut by pixels to half the chat box with "..." (0x7dade0); an undecodable one stays as its raw tags.
    /// </summary>
    public static class RoClassicItemLink {
        private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Open = "<ITEML>", Close = "</ITEML>";
        private const string Separators = "!#$%&'()*+,-/";   // 0x7e11c0 (the NUL is the 14th)

        /// <summary>A converted link: its label in the converted text and the item it stands for.</summary>
        public readonly struct Link {
            public Link(string label, RoItemInstance item, string name) {
                Label = label;
                Item = item;
                Name = name;
            }

            public string Label { get; }
            public RoItemInstance Item { get; }

            /// <summary>The full display name (the tooltip of a truncated label).</summary>
            public string Name { get; }

            public bool Truncated => Label != "<" + Name + ">";
        }

        // ---------------------------------------------------------------- base 62 (§1.1)

        /// <summary>0x7dc150: positional, a character outside the alphabet counts 0 and keeps its place.</summary>
        public static int Decode62(string s) {
            long v = 0;
            foreach (char c in s ?? string.Empty) {
                int d = Alphabet.IndexOf(c);
                v = v * 62 + (d < 0 ? 0 : d);
            }

            return unchecked((int) v);
        }

        /// <summary>0x7dc260: at least two characters.</summary>
        public static string Encode62(int value) {
            uint v = unchecked((uint) value);
            var sb = new StringBuilder();
            do {
                sb.Insert(0, Alphabet[(int) (v % 62)]);
                v /= 62;
            } while (v > 61);

            sb.Insert(0, Alphabet[(int) v]);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- encoder (§1.4)

        /// <summary>0x7fe020: the link token a dropped item puts into the chat input.</summary>
        public static string Encode(RoItemInstance item, RoItemMetadataTable table) {
            if (item == null) return string.Empty;
            RoItemMetadata meta = item.ResolveMetadata(table);
            var sb = new StringBuilder(Open);
            string loc = Encode62((int) item.EquipLocationHint);
            sb.Append(loc.Length < 5 ? loc.PadLeft(5, '0') : loc);
            sb.Append(IsEquipment(item, meta) ? '1' : '0');
            sb.Append(Encode62(item.ItemId));
            if (item.RefineLevel != 0) sb.Append('%').Append(Encode62(item.RefineLevel));
            sb.Append('&').Append(Encode62(item.ViewIdHint != 0 ? item.ViewIdHint : meta?.ClassNumber ?? 0));
            sb.Append('\'').Append(Encode62(item.EnchantGrade));
            int n = meta != null ? meta.SlotCount : 0;
            bool anyCard = false;
            for (int i = 0; i < 4; i++) anyCard |= item.GetCardId(i) != 0;
            if (n == 0 || anyCard) n = 4;
            for (int i = 0; i < n; i++) sb.Append(')').Append(Encode62(i < 4 ? item.GetCardId(i) : 0));
            for (int o = 0; o < RoItemInstance.MaxRandomOptions; o++) {
                RoItemRandomOption opt = item.GetRandomOption(o);
                if (opt.IsEmpty || opt.Index == 0) continue;
                sb.Append('+').Append(Encode62(opt.Index)).Append(',').Append(Encode62(opt.Param)).Append('-').Append(Encode62(opt.Value));
            }

            return sb.Append(Close).ToString();
        }

        /// <summary>0x6606c0's item-type test: weapons, armour and shadow gear (else the location says so).</summary>
        private static bool IsEquipment(RoItemInstance item, RoItemMetadata meta) {
            if (meta != null && (meta.ItemType == RoItemType.Weapon || meta.ItemType == RoItemType.Armor || meta.ItemType == RoItemType.ShadowGear)) return true;
            return meta == null && item.EquipLocationHint != RoItemEquipLocation.None;
        }

        // ---------------------------------------------------------------- parser and build (§1.2, §1.3)

        /// <summary>0x7e11a0 + 0x7f4520: the item of a payload, or false (too short, unknown item).</summary>
        public static bool TryDecode(string payload, RoItemMetadataTable table, out RoItemInstance item) {
            item = null;
            if (payload == null || payload.Length < 5) return false;
            var f = new int[260];
            f[6] = -1;
            f[0] = Decode62(payload.Substring(0, 5));
            f[1] = payload.Length > 5 ? Decode62(payload.Substring(5, 1)) : 0;
            int cur = 2, nCards = 0;
            var acc = new StringBuilder();
            var opt = new List<int>();
            int nOpt = 0;
            string s = payload + "\0";
            for (int i = 6; i < s.Length; i++) {
                char c = s[i];
                if (c >= '0') {
                    acc.Append(c);
                    continue;
                }

                int k = c == '\0' ? 13 : Separators.IndexOf(c);
                if (k < 0) continue;
                if (k == 7) {
                    Flush();
                    nCards = ReadCards(f, s, ref i);
                } else if (k == 9) {
                    nOpt++;
                    Flush();
                    ReadOption(opt, s, ref i);
                } else {
                    Flush();
                }

                cur = k;
            }

            if (nOpt > 0 && 8 + nCards < f.Length) {
                f[8 + nCards] = nOpt;
                for (int j = 0; j < opt.Count && 9 + nCards + j < f.Length; j++) f[9 + nCards + j] = opt[j];
            }

            void Flush() {
                if (acc.Length > 0 && cur < f.Length) f[cur] = Decode62(acc.ToString());
                acc.Clear();
            }

            // build
            if (table == null || !table.TryGetItem(f[2], out RoItemMetadata meta)) return false;
            string name = meta.IdentifiedDisplayName ?? string.Empty;
            if (name.Length == 0 || name.IndexOf("unknown item", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            int count = f[6];
            var cards = new int[4];
            for (int j = 1; j <= count && j <= 4; j++) cards[j - 1] = f[6 + j];
            var options = new List<RoItemRandomOption>();
            if (count + 8 >= 0 && count + 8 < 260) {
                int optCount = f[count + 8];
                for (int o = 0; o < optCount && o < RoItemInstance.MaxRandomOptions && count + 11 + 3 * o < 260; o++) {
                    options.Add(new RoItemRandomOption((short) f[count + 9 + 3 * o], (short) f[count + 11 + 3 * o], (byte) f[count + 10 + 3 * o]));
                }
            }

            var location = unchecked((RoItemEquipLocation) f[0]);
            item = f[1] != 0
                ? RoItemInstance.CreateEquipment(-1, f[2], 0, true, location, RoItemEquipLocation.None, f[3], cards, viewIdHint: f[4], itemTypeRaw: 4,
                    enchantGrade: (byte) f[5], randomOptions: options)
                : RoItemInstance.CreateRegular(-1, f[2], 0, true, location, enchantGrade: (byte) f[5], randomOptions: options);
            return true;
        }

        /// <summary>0x7f4c20: cards after the first ')', each further ')' starts the next; another separator ends the list.</summary>
        private static int ReadCards(int[] f, string s, ref int i) {
            int count = 0;
            var acc = new StringBuilder();
            for (i = i + 1; i < s.Length; i++) {
                char c = s[i];
                if (c >= '0') {
                    acc.Append(c);
                    continue;
                }

                if (7 + count < f.Length) f[7 + count] = Decode62(acc.ToString());
                count++;
                acc.Clear();
                if (c != ')') {
                    i--;
                    break;
                }
            }

            f[6] = count;
            return count;
        }

        /// <summary>0x7f4860: an option "+id,param-value"; ',' and '-' close a value, another separator ends it.</summary>
        private static void ReadOption(List<int> opt, string s, ref int i) {
            var acc = new StringBuilder();
            for (i = i + 1; i < s.Length; i++) {
                char c = s[i];
                if (c >= '0') {
                    acc.Append(c);
                    continue;
                }

                opt.Add(Decode62(acc.ToString()));
                acc.Clear();
                if (c != ',' && c != '-') {
                    i--;
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- received text (§2.1)

        public static bool HasLinks(string text) =>
            text != null && text.IndexOf(Open, StringComparison.Ordinal) >= 0 && text.IndexOf(Close, StringComparison.Ordinal) >= 0;

        /// <summary>
        /// 0x7dade0: every decodable link → "&lt;name&gt;" (the name cut to <paramref name="maxWidth"/> with "..."), an
        /// undecodable one left raw; the links in order.
        /// </summary>
        public static string Convert(string text, RoItemMetadataTable table, RoClassicGdiFont font, int maxWidth, List<Link> links) {
            links?.Clear();
            if (text == null || text.IndexOf(Open, StringComparison.Ordinal) < 0) return text ?? string.Empty;
            var sb = new StringBuilder();
            int pos = 0;
            while (pos < text.Length) {
                int close = text.IndexOf(Close, pos, StringComparison.Ordinal);
                if (close < 0) break;
                int open = text.LastIndexOf(Open, close, close - pos + 1, StringComparison.Ordinal);
                if (open < 0) {
                    // a closing tag without an opening one stays raw
                    sb.Append(text, pos, close + Close.Length - pos);
                    pos = close + Close.Length;
                    continue;
                }

                sb.Append(text, pos, open - pos);
                string payload = text.Substring(open + Open.Length, close - open - Open.Length);
                if (TryDecode(payload, table, out RoItemInstance item)) {
                    string name = item.GetFullDisplayName(table) ?? string.Empty;
                    string label = "<" + Truncate(name, font, maxWidth) + ">";
                    sb.Append(label);
                    links?.Add(new Link(label, item, name));
                } else {
                    sb.Append(text, open, close + Close.Length - open);
                }

                pos = close + Close.Length;
            }

            if (pos < text.Length) sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        /// <summary>0x9b3c60: unchanged when it fits, else cut from the end until it fits with "..." appended.</summary>
        public static string Truncate(string s, RoClassicGdiFont font, int maxWidth) {
            if (font == null || font.Measure(s) <= maxWidth) return s;
            for (int n = s.Length - 1; n > 0; n--) {
                string cut = s.Substring(0, n) + "...";
                if (font.Measure(cut) <= maxWidth) return cut;
            }

            return "...";
        }
    }
}
