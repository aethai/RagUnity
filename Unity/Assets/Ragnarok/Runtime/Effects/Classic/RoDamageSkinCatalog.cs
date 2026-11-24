using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Ragnarok.Assets;

namespace Ragnarok.Effects.Classic {
    /// <summary>One damage skin row of the client's Lua table (DSLName[id], damageskininfo.lub).</summary>
    public readonly struct RoDamageSkin {
        public RoDamageSkin(int id, string baseNumber, string baseMessage, string baseBlue, string displayName, string iconName, int millionUnit) {
            Id = id;
            BaseNumber = baseNumber;
            BaseMessage = baseMessage;
            BaseBlue = baseBlue;
            DisplayName = displayName;
            IconName = iconName;
            MillionUnit = millionUnit;
        }

        public int Id { get; }

        /// <summary>The digit sprite base name under data\sprite (e.g. 이팩트\숫자): BaseNumber.</summary>
        public string BaseNumber { get; }

        /// <summary>MISS / critical / LUCKY sprite base name: BaseMsg.</summary>
        public string BaseMessage { get; }

        /// <summary>The Taekwon kick burst sprite base name: BaseBlue.</summary>
        public string BaseBlue { get; }

        /// <summary>The equipment window's cell label: DSDisplayName.</summary>
        public string DisplayName { get; }

        /// <summary>The cell icon under data\texture (+ ".bmp"): DSIconName.</summary>
        public string IconName { get; }

        /// <summary>DSMillionUnit: 2 lays values of 100 000 000 and more out for a wide 'M' glyph.</summary>
        public int MillionUnit { get; }
    }

    /// <summary>
    /// The damage skins of the original client (package damageskin): DSList / DSLName from
    /// luafiles514\lua files\damageskin\damageskininfo.lub. The skin is a client-local option: no packet carries it,
    /// and every number this client draws (own, received, other players', monsters', heals, MISS / LUCKY / critical) uses
    /// the local choice. Ids run 0..N−1 (the client uses them as list indices). A compiled (bytecode) table falls back to
    /// the four rows the client ships with.
    /// </summary>
    public sealed class RoDamageSkinCatalog {
        private const string InfoPath = @"data\luafiles514\lua files\damageskin\damageskininfo.lub";
        private const string ListPath = @"data\luafiles514\lua files\damageskin\damageskinlist.lub";

        private static readonly Regex EntryPattern = new Regex(
            @"\[\s*DSList\.(\w+)\s*\]\s*=\s*\{(.*?)\}",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex ListPattern = new Regex(@"(DT_\w+)\s*=\s*(\d+)", RegexOptions.CultureInvariant);
        private static readonly Regex FieldPattern = new Regex(@"(\w+)\s*=\s*(""(?:[^""\\]|\\.)*""|-?\d+)", RegexOptions.CultureInvariant);

        private readonly List<RoDamageSkin> skins;

        private RoDamageSkinCatalog(List<RoDamageSkin> skins) {
            this.skins = skins;
        }

        /// <summary>The four rows of the client's own table (DT_Default, DT_NewNumber, DT_Han, DT_Invi).</summary>
        public static RoDamageSkinCatalog Default { get; } = new RoDamageSkinCatalog(new List<RoDamageSkin> {
            new RoDamageSkin(0, "이팩트\\숫자", "이팩트\\msg", "이팩트\\bluemsg", "Normal", "유저인터페이스\\item\\기본", 1),
            new RoDamageSkin(1, "이팩트\\NewNumber", "이팩트\\NewNumber_MSG", "이팩트\\NewNumber_BMSG", "Colour", "유저인터페이스\\item\\NewNumber", 1),
            new RoDamageSkin(2, "이팩트\\Han", "이팩트\\Han_MSG", "이팩트\\Han_BMSG", "Words", "유저인터페이스\\item\\Han", 2),
            new RoDamageSkin(3, "이팩트\\NewNumberH", "이팩트\\NewNumberH_MSG", "이팩트\\NewNumberH_BMSG", "Empty", "유저인터페이스\\item\\NewNumberH", 1),
        });

        public int Count => skins.Count;

        public IReadOnlyList<RoDamageSkin> Skins => skins;

        /// <summary>The skin <paramref name="id"/>; an id out of range is 0, as the client clamps it at startup.</summary>
        public RoDamageSkin Get(int id) => skins[id >= 0 && id < skins.Count ? id : 0];

        public static RoDamageSkinCatalog Load(RoAssetDatabase assets) {
            byte[] info = assets?.ReadBytesQuiet(InfoPath);
            if (info == null || info.Length == 0 || info[0] == 0x1b) {
                return Default;
            }

            Dictionary<string, int> ids = ParseList(assets.ReadBytesQuiet(ListPath));
            RoDamageSkinCatalog parsed = Parse(RoTextEncoding.Decode(info), ids);
            return parsed ?? Default;
        }

        /// <summary>Parses the text form of damageskininfo.lub; null when it holds no usable row.</summary>
        public static RoDamageSkinCatalog Parse(string text, IReadOnlyDictionary<string, int> listIds = null) {
            if (string.IsNullOrEmpty(text)) {
                return null;
            }

            var byId = new SortedDictionary<int, RoDamageSkin>();
            foreach (Match entry in EntryPattern.Matches(text)) {
                string name = entry.Groups[1].Value;
                if (!TryResolveId(name, listIds, out int id)) {
                    continue;
                }

                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match field in FieldPattern.Matches(entry.Groups[2].Value)) {
                    string value = field.Groups[2].Value;
                    fields[field.Groups[1].Value] = value.StartsWith("\"", StringComparison.Ordinal)
                        ? Unescape(value.Substring(1, value.Length - 2))
                        : value;
                }

                if (!fields.TryGetValue("BaseNumber", out string number) || !fields.TryGetValue("BaseMsg", out string message)) {
                    continue;
                }

                fields.TryGetValue("BaseBlue", out string blue);
                fields.TryGetValue("DSDisplayName", out string display);
                fields.TryGetValue("DSIconName", out string icon);
                int million = fields.TryGetValue("DSMillionUnit", out string unit) &&
                    int.TryParse(unit, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedUnit)
                    ? parsedUnit
                    : 1;
                byId[id] = new RoDamageSkin(id, number, message, blue ?? string.Empty, display ?? name, icon ?? string.Empty, million);
            }

            // ids are list indices in the client: keep the leading run 0..N−1
            var skins = new List<RoDamageSkin>();
            while (byId.TryGetValue(skins.Count, out RoDamageSkin skin)) {
                skins.Add(skin);
            }

            return skins.Count > 0 ? new RoDamageSkinCatalog(skins) : null;
        }

        private static Dictionary<string, int> ParseList(byte[] bytes) {
            if (bytes == null || bytes.Length == 0 || bytes[0] == 0x1b) {
                return null;
            }

            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match match in ListPattern.Matches(RoTextEncoding.Decode(bytes) ?? string.Empty)) {
                ids[match.Groups[1].Value] = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            return ids;
        }

        private static bool TryResolveId(string name, IReadOnlyDictionary<string, int> listIds, out int id) {
            if (listIds != null && listIds.TryGetValue(name, out id)) {
                return true;
            }

            switch (name) {
                case "DT_Default": id = 0; return true;
                case "DT_NewNumber": id = 1; return true;
                case "DT_Han": id = 2; return true;
                case "DT_Invi": id = 3; return true;
                default: id = -1; return false;
            }
        }

        private static string Unescape(string value) => value.Replace("\\\\", "\\").Replace("\\\"", "\"");
    }
}
