using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ragnarok.Data;

namespace Ragnarok.Gameplay {
    public readonly struct RoItemLink {
        private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly Regex ItemLinkPattern = new Regex(
            @"(?<local>\[item:[^\]]+\])|<(?<legacyTag>ITEMLINK|ITEM)>(?<legacyName>[\s\S]*?)<INFO>(?<legacyInfo>[\s\S]*?)<\/INFO><\/\k<legacyTag>>|<(?<payloadTag>ITEML|ITEM)>(?<payload>[\s\S]*?)<\/\k<payloadTag>>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FirstIntegerPattern = new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> GeneratedItemlDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);

        public RoItemLink(int itemId, string displayName, int refineLevel, int[] cardIds) {
            ItemId = itemId;
            DisplayName = displayName ?? string.Empty;
            RefineLevel = Math.Max(0, refineLevel);
            CardIds = CopyCardIds(cardIds);
            ItemTypeRaw = 3;
            IsIdentified = true;
            IsDamaged = false;
            EnchantGrade = 0;
            OptionCount = 0;
            Location = 0u;
            ViewIdHint = 0;
            RandomOptions = Array.Empty<RoItemRandomOption>();
        }

        public RoItemLink(
            int itemId,
            string displayName,
            int refineLevel,
            int[] cardIds,
            byte itemTypeRaw,
            bool isIdentified,
            bool isDamaged,
            byte enchantGrade,
            int optionCount,
            uint location,
            int viewIdHint,
            IReadOnlyList<RoItemRandomOption> randomOptions = null) {
            ItemId = itemId;
            DisplayName = displayName ?? string.Empty;
            RefineLevel = Math.Max(0, refineLevel);
            CardIds = CopyCardIds(cardIds);
            ItemTypeRaw = itemTypeRaw;
            IsIdentified = isIdentified;
            IsDamaged = isDamaged;
            EnchantGrade = enchantGrade;
            OptionCount = Math.Max(0, optionCount);
            Location = location;
            ViewIdHint = Math.Max(0, viewIdHint);
            RandomOptions = CopyRandomOptions(randomOptions);
        }

        public int ItemId { get; }
        public string DisplayName { get; }
        public int RefineLevel { get; }
        public int[] CardIds { get; }
        public byte ItemTypeRaw { get; }
        public bool IsIdentified { get; }
        public bool IsDamaged { get; }
        public byte EnchantGrade { get; }
        public int OptionCount { get; }
        public uint Location { get; }
        public int ViewIdHint { get; }
        public RoItemRandomOption[] RandomOptions { get; }

        public static RoItemLink FromItem(RoItemInstance item, RoItemMetadataTable metadataTable) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            return new RoItemLink(
                item.ItemId,
                item.GetFullDisplayName(metadataTable, includeRandomOptionCount: true),
                item.RefineLevel,
                item.CopyCardIds(),
                item.ItemTypeRaw,
                item.IsIdentified,
                item.IsDamaged,
                item.EnchantGrade,
                CountRandomOptions(item),
                unchecked((uint) item.EquipLocationHint),
                item.ViewIdHint,
                item.CopyRandomOptions());
        }

        public string ToChatToken() {
            var cards = new StringBuilder();
            for (int i = 0; i < CardIds.Length; i++) {
                if (i > 0) {
                    cards.Append(',');
                }

                cards.Append(CardIds[i].ToString(CultureInfo.InvariantCulture));
            }

            return string.Concat(
                "[item:",
                ItemId.ToString(CultureInfo.InvariantCulture),
                ":",
                RefineLevel.ToString(CultureInfo.InvariantCulture),
                ":",
                cards,
                ":",
                Uri.EscapeDataString(DisplayName),
                "]");
        }

        public string ToItemlToken() {
            if (ItemId <= 0) {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(EncodeBase62(Location).PadLeft(5, '0'));
            builder.Append(IsEquipmentLink ? '1' : '0');
            builder.Append(EncodeBase62(unchecked((uint) ItemId)));
            if (RefineLevel > 0) {
                builder.Append('%').Append(EncodeBase62(unchecked((uint) RefineLevel)).PadLeft(2, '0'));
            }

            builder.Append('&').Append(EncodeBase62(unchecked((uint) ViewIdHint)).PadLeft(2, '0'));
            if (EnchantGrade > 0) {
                builder.Append('\'').Append(EncodeBase62(EnchantGrade).PadLeft(2, '0'));
            }

            for (int i = 0; i < CardIds.Length; i++) {
                if (CardIds[i] > 0) {
                    builder.Append(')').Append(EncodeBase62(unchecked((uint) CardIds[i])).PadLeft(2, '0'));
                }
            }

            RoItemRandomOption[] options = RandomOptions ?? Array.Empty<RoItemRandomOption>();
            for (int i = 0; i < options.Length && i < RoItemInstance.MaxRandomOptions; i++) {
                RoItemRandomOption option = options[i];
                if (option.IsEmpty) {
                    continue;
                }

                builder.Append('+').Append(EncodeBase62(unchecked((uint) option.Index)).PadLeft(2, '0'));
                builder.Append(',').Append(EncodeBase62(option.Param).PadLeft(2, '0'));
                builder.Append('-').Append(EncodeBase62(unchecked((uint) Math.Max(0, (int) option.Value))).PadLeft(2, '0'));
            }

            string payload = builder.ToString();
            RememberGeneratedItemlDisplayName(payload, DisplayName);
            return "<ITEML>" + payload + "</ITEML>";
        }

        public bool IsEquipmentLink =>
            Location != 0u ||
            RefineLevel > 0 ||
            HasAnyCard(CardIds) ||
            ItemTypeRaw == 4 ||
            ItemTypeRaw == 5;

        public RoItemInstance ToItemInstance() {
            if (IsEquipmentLink) {
                return RoItemInstance.CreateEquipment(
                    ItemId,
                    ItemId,
                    count: 1,
                    isIdentified: IsIdentified,
                    equipLocationHint: unchecked((RoItemEquipLocation) Location),
                    refineLevel: RefineLevel,
                    cardIds: CardIds,
                    viewIdHint: ViewIdHint,
                    itemTypeRaw: ItemTypeRaw,
                    enchantGrade: EnchantGrade,
                    isDamaged: IsDamaged,
                    randomOptions: RandomOptions);
            }

            return RoItemInstance.CreateRegular(
                ItemId,
                ItemId,
                count: 1,
                isIdentified: IsIdentified,
                equipLocationHint: unchecked((RoItemEquipLocation) Location),
                itemTypeRaw: ItemTypeRaw,
                enchantGrade: EnchantGrade,
                isDamaged: IsDamaged,
                randomOptions: RandomOptions);
        }

        public static bool TryParse(string text, out RoItemLink link) {
            link = default;
            if (string.IsNullOrWhiteSpace(text) ||
                !text.StartsWith("[item:", StringComparison.Ordinal) ||
                !text.EndsWith("]", StringComparison.Ordinal)) {
                return false;
            }

            string payload = text.Substring(6, text.Length - 7);
            string[] parts = payload.Split(new[] { ':' }, 4);
            if (parts.Length != 4 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int refineLevel)) {
                return false;
            }

            string[] cardParts = parts[2].Split(',');
            var cards = new int[RoItemInstance.MaxCardSlots];
            for (int i = 0; i < cards.Length && i < cardParts.Length; i++) {
                int.TryParse(cardParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out cards[i]);
            }

            link = new RoItemLink(itemId, Uri.UnescapeDataString(parts[3]), refineLevel, cards);
            return true;
        }

        public static bool TryParseAny(string text, Func<int, string> itemNameResolver, out RoItemLink link) {
            link = default;
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            string source = NormalizeItemLinkText(text);
            if (TryParse(source, out link)) {
                return true;
            }

            Match match = ItemLinkPattern.Match(source);
            return match.Success && TryCreateItemRun(match, itemNameResolver, out RoItemLinkRun run) && run.IsItemLink && (link = run.Link).ItemId > 0;
        }

        public static IReadOnlyList<RoItemLinkRun> ParseRuns(string text, Func<int, string> itemNameResolver = null) {
            if (string.IsNullOrEmpty(text)) {
                return Array.Empty<RoItemLinkRun>();
            }

            string source = NormalizeItemLinkText(text);
            var runs = new List<RoItemLinkRun>();
            int position = 0;
            MatchCollection matches = ItemLinkPattern.Matches(source);
            for (int i = 0; i < matches.Count; i++) {
                Match match = matches[i];
                if (!match.Success) {
                    continue;
                }

                if (match.Index > position) {
                    runs.Add(new RoItemLinkRun(source.Substring(position, match.Index - position)));
                }

                if (TryCreateItemRun(match, itemNameResolver, out RoItemLinkRun itemRun)) {
                    runs.Add(itemRun);
                } else {
                    runs.Add(new RoItemLinkRun(ResolveUnparsedItemLinkText(match)));
                }

                position = match.Index + match.Length;
            }

            if (position < source.Length) {
                runs.Add(new RoItemLinkRun(source.Substring(position)));
            }

            return runs;
        }

        private static int[] CopyCardIds(int[] source) {
            var copy = new int[RoItemInstance.MaxCardSlots];
            if (source != null) {
                Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
            }

            return copy;
        }

        private static bool TryCreateItemRun(Match match, Func<int, string> itemNameResolver, out RoItemLinkRun run) {
            if (match.Groups["local"].Success && TryParse(match.Groups["local"].Value, out RoItemLink local)) {
                run = new RoItemLinkRun(WrapDisplayName(local.DisplayName), local);
                return true;
            }

            if (TryParseLegacyItemLink(match, itemNameResolver, out run)) {
                return true;
            }

            if (TryParseIteml(match, itemNameResolver, out run)) {
                return true;
            }

            run = default;
            return false;
        }

        private static bool TryParseLegacyItemLink(Match match, Func<int, string> itemNameResolver, out RoItemLinkRun run) {
            string itemName = match.Groups["legacyName"].Success ? match.Groups["legacyName"].Value : string.Empty;
            string itemIdText = match.Groups["legacyInfo"].Success ? match.Groups["legacyInfo"].Value : string.Empty;
            Match idMatch = FirstIntegerPattern.Match(itemIdText ?? string.Empty);
            if (!idMatch.Success ||
                !int.TryParse(idMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId) ||
                itemId <= 0) {
                run = default;
                return false;
            }

            string displayName = ResolveDisplayName(itemId, itemName, itemNameResolver);
            var link = new RoItemLink(itemId, displayName, 0, null, 3, true, false, 0, 0, 0u, 0);
            run = new RoItemLinkRun(WrapDisplayName(displayName), link);
            return true;
        }

        private static bool TryParseIteml(Match match, Func<int, string> itemNameResolver, out RoItemLinkRun run) {
            string payload = match.Groups["payload"].Success ? match.Groups["payload"].Value : string.Empty;
            if (!TryParseItemlPayload(payload, itemNameResolver, out RoItemLink link)) {
                run = default;
                return false;
            }

            run = new RoItemLinkRun(WrapDisplayName(link.DisplayName), link);
            return true;
        }

        private static bool TryParseItemlPayload(string payload, Func<int, string> itemNameResolver, out RoItemLink link) {
            link = default;
            payload = DecodeKnownHtmlEntities(payload ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(payload) || payload.Length < 7) {
                return false;
            }

            if (!TryDecodeBase62(payload.Substring(0, 5), out uint location)) {
                return false;
            }

            bool isEquipment = payload[5] == '1';
            int position = 6;
            int itemIdStart = position;
            while (position < payload.Length && !IsItemlSeparator(payload[position])) {
                position++;
            }

            if (position <= itemIdStart ||
                !TryDecodeBase62(payload.Substring(itemIdStart, position - itemIdStart), out uint itemIdValue) ||
                itemIdValue == 0u ||
                itemIdValue > int.MaxValue) {
                return false;
            }

            int itemId = unchecked((int) itemIdValue);
            int refineLevel = 0;
            int viewIdHint = 0;
            if (position < payload.Length && payload[position] == '%') {
                position++;
                if (!TryReadBase62Segment(payload, ref position, 2, out uint refineValue)) {
                    return false;
                }

                refineLevel = unchecked((int) Math.Min(refineValue, int.MaxValue));
            }

            if (position < payload.Length && payload[position] == '&') {
                position++;
                if (!TryReadBase62Segment(payload, ref position, 2, out uint viewValue)) {
                    return false;
                }

                viewIdHint = unchecked((int) Math.Min(viewValue, int.MaxValue));
            }

            byte grade = 0;
            ItemLinkTailParseResult tail = default;
            int modernPosition = position;
            if (modernPosition < payload.Length && payload[modernPosition] == '\'') {
                modernPosition++;
                if (TryReadBase62Segment(payload, ref modernPosition, 2, out uint gradeValue)) {
                    grade = unchecked((byte) Math.Min(gradeValue, byte.MaxValue));
                    TryParseItemlTail(payload, modernPosition, ')', '+', ',', '-', ref tail);
                }
            } else {
                TryParseItemlTail(payload, modernPosition, ')', '+', ',', '-', ref tail);
            }

            if (!tail.Success) {
                grade = 0;
                TryParseItemlTail(payload, position, '(', '*', '+', ',', ref tail);
            }

            if (!tail.Success) {
                grade = 0;
                TryParseItemlTail(payload, position, '\'', ')', '*', '+', ref tail);
            }

            int[] cards = {
                unchecked((int) tail.Card1),
                unchecked((int) tail.Card2),
                unchecked((int) tail.Card3),
                unchecked((int) tail.Card4)
            };
            string displayName = ResolveDisplayName(itemId, string.Empty, itemNameResolver);
            if (IsGeneratedItemFallback(displayName, itemId) &&
                TryResolveGeneratedItemlDisplayName(payload, out string generatedDisplayName)) {
                displayName = generatedDisplayName;
            }

            link = new RoItemLink(
                itemId,
                displayName,
                refineLevel,
                cards,
                isEquipment ? (byte) 5 : (byte) 3,
                true,
                false,
                grade,
                tail.Success ? tail.OptionCount : 0,
                location,
                viewIdHint,
                tail.Success ? tail.RandomOptions : null);
            return true;
        }

        private static string NormalizeItemLinkText(string text) {
            if (string.IsNullOrEmpty(text) || text.IndexOf('&') < 0) {
                return text ?? string.Empty;
            }

            string decoded = text;
            for (int i = 0; i < 3; i++) {
                string next = DecodeKnownHtmlEntities(decoded);
                if (string.Equals(next, decoded, StringComparison.Ordinal)) {
                    break;
                }

                decoded = next;
                if (ItemLinkPattern.IsMatch(decoded)) {
                    return decoded;
                }
            }

            return ItemLinkPattern.IsMatch(decoded) ? decoded : text;
        }

        private static string DecodeKnownHtmlEntities(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            return text
                .Replace("&lt;", "<")
                .Replace("&LT;", "<")
                .Replace("&#60;", "<")
                .Replace("&#x3c;", "<")
                .Replace("&#x3C;", "<")
                .Replace("&gt;", ">")
                .Replace("&GT;", ">")
                .Replace("&#62;", ">")
                .Replace("&#x3e;", ">")
                .Replace("&#x3E;", ">")
                .Replace("&amp;", "&")
                .Replace("&AMP;", "&")
                .Replace("&#38;", "&")
                .Replace("&#x26;", "&")
                .Replace("&apos;", "'")
                .Replace("&APOS;", "'")
                .Replace("&#39;", "'")
                .Replace("&#x27;", "'")
                .Replace("&quot;", "\"")
                .Replace("&QUOT;", "\"")
                .Replace("&#34;", "\"")
                .Replace("&#x22;", "\"");
        }

        private static string ResolveDisplayName(int itemId, string embeddedName, Func<int, string> itemNameResolver) {
            string trimmed = (embeddedName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(trimmed)) {
                return trimmed;
            }

            string resolved = itemNameResolver?.Invoke(itemId);
            if (!IsFallbackDisplayName(resolved)) {
                return resolved.Trim();
            }

            return itemId > 0 ? "Item " + itemId.ToString(CultureInfo.InvariantCulture) : "Unknown Item";
        }

        public static bool IsFallbackDisplayName(string displayName) {
            if (string.IsNullOrWhiteSpace(displayName)) {
                return true;
            }

            string trimmed = displayName.Trim();
            return string.Equals(trimmed, "Unknown Item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static string WrapDisplayName(string displayName) {
            string resolved = string.IsNullOrWhiteSpace(displayName) ? "Unknown Item" : displayName.Trim();
            return "<" + resolved + ">";
        }

        private static void RememberGeneratedItemlDisplayName(string payload, string displayName) {
            if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(displayName)) {
                return;
            }

            string trimmed = displayName.Trim();
            if (IsFallbackDisplayName(trimmed)) {
                return;
            }

            lock (GeneratedItemlDisplayNames) {
                if (GeneratedItemlDisplayNames.Count >= 128) {
                    GeneratedItemlDisplayNames.Clear();
                }

                GeneratedItemlDisplayNames[payload] = trimmed;
            }
        }

        private static bool TryResolveGeneratedItemlDisplayName(string payload, out string displayName) {
            if (string.IsNullOrWhiteSpace(payload)) {
                displayName = string.Empty;
                return false;
            }

            lock (GeneratedItemlDisplayNames) {
                return GeneratedItemlDisplayNames.TryGetValue(payload, out displayName);
            }
        }

        private static bool IsGeneratedItemFallback(string displayName, int itemId) {
            if (IsFallbackDisplayName(displayName)) {
                return true;
            }

            return itemId > 0 &&
                string.Equals(
                    displayName != null ? displayName.Trim() : string.Empty,
                    "Item " + itemId.ToString(CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveUnparsedItemLinkText(Match match) {
            if (match == null || !match.Success) {
                return string.Empty;
            }

            string embedded = match.Groups["legacyName"].Success ? match.Groups["legacyName"].Value.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(embedded)) {
                return embedded;
            }

            if (match.Groups["payload"].Success) {
                return "Item";
            }

            return string.Empty;
        }

        private static string EncodeBase62(uint value) {
            if (value == 0u) {
                return "0";
            }

            var builder = new StringBuilder();
            uint remaining = value;
            while (remaining > 0u) {
                builder.Insert(0, Base62Alphabet[unchecked((int) (remaining % 62u))]);
                remaining /= 62u;
            }

            return builder.ToString();
        }

        private static bool TryDecodeBase62(string text, out uint value) {
            value = 0u;
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            ulong accumulator = 0UL;
            for (int i = 0; i < text.Length; i++) {
                int digit = Base62Alphabet.IndexOf(text[i]);
                if (digit < 0) {
                    return false;
                }

                accumulator = (accumulator * 62UL) + unchecked((ulong) digit);
                if (accumulator > uint.MaxValue) {
                    return false;
                }
            }

            value = unchecked((uint) accumulator);
            return true;
        }

        private static bool TryReadBase62Segment(string text, ref int position, int length, out uint value) {
            value = 0u;
            if (position < 0 || length <= 0 || position + length > text.Length) {
                return false;
            }

            bool success = TryDecodeBase62(text.Substring(position, length), out value);
            if (success) {
                position += length;
            }

            return success;
        }

        private static bool IsItemlSeparator(char value) {
            return value == '%' ||
                value == '&' ||
                value == '\'' ||
                value == '(' ||
                value == ')' ||
                value == '*' ||
                value == '+' ||
                value == ',' ||
                value == '-';
        }

        private static void TryParseItemlTail(
            string payload,
            int position,
            char cardSeparator,
            char optionIdSeparator,
            char optionParamSeparator,
            char optionValueSeparator,
            ref ItemLinkTailParseResult result) {
            result = default;
            if (position < 0 || position > payload.Length) {
                return;
            }

            int current = position;
            uint[] cards = { 0u, 0u, 0u, 0u };
            int cardIndex = 0;
            while (cardIndex < cards.Length && current < payload.Length && payload[current] == cardSeparator) {
                current++;
                int cardStart = current;
                while (current < payload.Length &&
                    payload[current] != cardSeparator &&
                    payload[current] != optionIdSeparator &&
                    !IsItemlSeparator(payload[current])) {
                    current++;
                }

                string cardText = payload.Substring(cardStart, current - cardStart);
                if (cardText.Length != 0 &&
                    !string.Equals(cardText, "00", StringComparison.Ordinal) &&
                    !TryDecodeBase62(cardText, out cards[cardIndex])) {
                    return;
                }

                cardIndex++;
            }

            var randomOptions = new List<RoItemRandomOption>(RoItemInstance.MaxRandomOptions);
            while (current < payload.Length && payload[current] == optionIdSeparator && randomOptions.Count < RoItemInstance.MaxRandomOptions) {
                current++;
                if (!TryReadBase62Segment(payload, ref current, 2, out uint optionId) ||
                    current >= payload.Length ||
                    payload[current] != optionParamSeparator) {
                    return;
                }

                current++;
                if (!TryReadBase62Segment(payload, ref current, 2, out uint optionParam) ||
                    current >= payload.Length ||
                    payload[current] != optionValueSeparator) {
                    return;
                }

                current++;
                if (!TryReadBase62Segment(payload, ref current, 2, out uint optionValue)) {
                    return;
                }

                randomOptions.Add(new RoItemRandomOption(
                    unchecked((short) Math.Min(optionId, short.MaxValue)),
                    unchecked((short) Math.Min(optionValue, short.MaxValue)),
                    unchecked((byte) Math.Min(optionParam, byte.MaxValue))));
            }

            if (current != payload.Length) {
                return;
            }

            result = new ItemLinkTailParseResult(true, cards[0], cards[1], cards[2], cards[3], randomOptions);
        }

        private static int CountRandomOptions(RoItemInstance item) {
            if (item == null) {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < RoItemInstance.MaxRandomOptions; i++) {
                if (!item.GetRandomOption(i).IsEmpty) {
                    count++;
                }
            }

            return count;
        }

        private static bool HasAnyCard(IReadOnlyList<int> cards) {
            if (cards == null) {
                return false;
            }

            for (int i = 0; i < cards.Count; i++) {
                if (cards[i] > 0) {
                    return true;
                }
            }

            return false;
        }

        private static RoItemRandomOption[] CopyRandomOptions(IReadOnlyList<RoItemRandomOption> source) {
            if (source == null || source.Count == 0) {
                return Array.Empty<RoItemRandomOption>();
            }

            var copy = new RoItemRandomOption[Math.Min(source.Count, RoItemInstance.MaxRandomOptions)];
            for (int i = 0; i < copy.Length; i++) {
                copy[i] = source[i];
            }

            return copy;
        }

        private readonly struct ItemLinkTailParseResult {
            public ItemLinkTailParseResult(bool success, uint card1, uint card2, uint card3, uint card4, IReadOnlyList<RoItemRandomOption> randomOptions) {
                Success = success;
                Card1 = card1;
                Card2 = card2;
                Card3 = card3;
                Card4 = card4;
                RandomOptions = CopyRandomOptions(randomOptions);
                OptionCount = RandomOptions.Length;
            }

            public bool Success { get; }
            public uint Card1 { get; }
            public uint Card2 { get; }
            public uint Card3 { get; }
            public uint Card4 { get; }
            public int OptionCount { get; }
            public RoItemRandomOption[] RandomOptions { get; }
        }
    }

    public readonly struct RoItemLinkRun {
        public RoItemLinkRun(string text) {
            Text = text ?? string.Empty;
            IsItemLink = false;
            Link = default;
        }

        public RoItemLinkRun(string text, RoItemLink link) {
            Text = text ?? string.Empty;
            IsItemLink = link.ItemId > 0;
            Link = link;
        }

        public string Text { get; }
        public bool IsItemLink { get; }
        public RoItemLink Link { get; }
        public int ItemId => Link.ItemId;
        public byte ItemTypeRaw => Link.ItemTypeRaw;
        public bool IsIdentified => Link.IsIdentified;
        public bool IsDamaged => Link.IsDamaged;
        public int RefineLevel => Link.RefineLevel;
        public byte EnchantGrade => Link.EnchantGrade;
        public int OptionCount => Link.OptionCount;
        public uint Location => Link.Location;
        public int[] CardIds => Link.CardIds;
    }
}
