using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using ROIO.Models;

namespace Ragnarok.UI {
    public enum RoChatCommandArgumentKind {
        None,
        Item,
        Monster,
        Map,
        GoTown,
        PlayerName,
        FreeText
    }

    public enum RoChatCommandCatalogProfile {
        Player,
        SuperPlayer,
        All
    }

    public readonly struct RoChatCommandArgumentSpec {
        public RoChatCommandArgumentSpec(string hint, RoChatCommandArgumentKind kind) {
            Hint = hint ?? string.Empty;
            Kind = kind;
        }

        public string Hint { get; }
        public RoChatCommandArgumentKind Kind { get; }
    }

    public sealed class RoChatCommandDefinition {
        public RoChatCommandDefinition(string canonicalCommand, string description, string[] argumentHints, params string[] aliases) {
            Initialize(canonicalCommand, description, BuildArgumentSpecs(argumentHints, RoChatCommandArgumentKind.None), aliases);
        }

        public RoChatCommandDefinition(
            string canonicalCommand,
            string description,
            string[] argumentHints,
            RoChatCommandArgumentKind firstArgumentKind,
            params string[] aliases) {
            Initialize(canonicalCommand, description, BuildArgumentSpecs(argumentHints, firstArgumentKind), aliases);
        }

        public RoChatCommandDefinition(
            string canonicalCommand,
            string description,
            RoChatCommandArgumentSpec[] argumentSpecs,
            params string[] aliases) {
            Initialize(canonicalCommand, description, argumentSpecs, aliases);
        }

        private void Initialize(
            string canonicalCommand,
            string description,
            RoChatCommandArgumentSpec[] argumentSpecs,
            string[] aliases) {
            CanonicalCommand = NormalizeCommand(canonicalCommand);
            Description = description ?? string.Empty;
            ArgumentSpecs = argumentSpecs ?? Array.Empty<RoChatCommandArgumentSpec>();
            var hints = new List<string>(ArgumentSpecs.Length);
            for (int i = 0; i < ArgumentSpecs.Length; i++) {
                if (!string.IsNullOrWhiteSpace(ArgumentSpecs[i].Hint)) {
                    hints.Add(ArgumentSpecs[i].Hint);
                }
            }

            ArgumentHints = hints.ToArray();

            if (aliases == null || aliases.Length == 0) {
                Aliases = Array.Empty<string>();
            } else {
                var normalized = new List<string>(aliases.Length);
                for (int i = 0; i < aliases.Length; i++) {
                    string alias = NormalizeCommand(aliases[i]);
                    if (alias.Length > 0 && !string.Equals(alias, CanonicalCommand, StringComparison.OrdinalIgnoreCase)) {
                        normalized.Add(alias);
                    }
                }

                Aliases = normalized.ToArray();
            }
        }

        public string CanonicalCommand { get; private set; }
        public string Description { get; private set; }
        public string[] ArgumentHints { get; private set; }
        public RoChatCommandArgumentSpec[] ArgumentSpecs { get; private set; }
        public string[] Aliases { get; private set; }
        public RoChatCommandArgumentKind FirstArgumentKind => ArgumentSpecs.Length > 0
            ? ArgumentSpecs[0].Kind
            : RoChatCommandArgumentKind.None;

        public string Usage {
            get {
                if (ArgumentHints.Length == 0) {
                    return CanonicalCommand;
                }

                return CanonicalCommand + " " + string.Join(" ", ArgumentHints);
            }
        }

        internal static string NormalizeCommand(string command) {
            string value = (command ?? string.Empty).Trim();
            if (value.Length == 0) {
                return string.Empty;
            }

            return value[0] == '@' ? value : "@" + value;
        }

        private static RoChatCommandArgumentSpec[] BuildArgumentSpecs(string[] argumentHints, RoChatCommandArgumentKind firstArgumentKind) {
            if (argumentHints == null || argumentHints.Length == 0) {
                return Array.Empty<RoChatCommandArgumentSpec>();
            }

            var specs = new RoChatCommandArgumentSpec[argumentHints.Length];
            for (int i = 0; i < argumentHints.Length; i++) {
                specs[i] = new RoChatCommandArgumentSpec(
                    argumentHints[i],
                    i == 0 ? firstArgumentKind : RoChatCommandArgumentKind.None);
            }

            return specs;
        }
    }

    public readonly struct RoChatCommandSuggestion {
        public RoChatCommandSuggestion(RoChatCommandDefinition definition, string matchedCommand) {
            Definition = definition;
            MatchedCommand = matchedCommand ?? string.Empty;
        }

        public RoChatCommandDefinition Definition { get; }
        public string MatchedCommand { get; }
    }

    public readonly struct RoChatCommandArgumentSuggestion {
        public RoChatCommandArgumentSuggestion(string displayName, int value, string secondaryText = null) {
            DisplayName = displayName ?? string.Empty;
            Value = value;
            SecondaryText = secondaryText ?? string.Empty;
            ReplacementText = value.ToString(CultureInfo.InvariantCulture);
        }

        public RoChatCommandArgumentSuggestion(string displayName, string replacementText, string secondaryText = null) {
            DisplayName = displayName ?? string.Empty;
            ReplacementText = replacementText ?? string.Empty;
            SecondaryText = secondaryText ?? string.Empty;
            Value = int.TryParse(ReplacementText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        public string DisplayName { get; }
        public int Value { get; }
        public string SecondaryText { get; }
        public string ReplacementText { get; }
        public string DisplayText => string.IsNullOrWhiteSpace(SecondaryText)
            ? DisplayName
            : DisplayName + " (" + SecondaryText + ")";
    }

    public readonly struct RoChatCommandSuggestionState {
        public RoChatCommandSuggestionState(
            RoChatCommandSuggestion[] suggestions,
            int activeIndex,
            int replaceStart,
            int replaceLength,
            string argumentHint,
            RoChatCommandDefinition resolvedCommand,
            RoChatCommandArgumentSuggestion[] argumentSuggestions = null) {
            Suggestions = suggestions ?? Array.Empty<RoChatCommandSuggestion>();
            ArgumentSuggestions = argumentSuggestions ?? Array.Empty<RoChatCommandArgumentSuggestion>();
            int activeCount = Suggestions.Length > 0 ? Suggestions.Length : ArgumentSuggestions.Length;
            ActiveIndex = activeCount == 0 ? -1 : Math.Max(0, Math.Min(activeIndex, activeCount - 1));
            ReplaceStart = replaceStart;
            ReplaceLength = replaceLength;
            ArgumentHint = argumentHint ?? string.Empty;
            ResolvedCommand = resolvedCommand;
        }

        public static RoChatCommandSuggestionState Empty { get; } = new RoChatCommandSuggestionState(
            Array.Empty<RoChatCommandSuggestion>(),
            -1,
            0,
            0,
            string.Empty,
            null);

        public RoChatCommandSuggestion[] Suggestions { get; }
        public RoChatCommandArgumentSuggestion[] ArgumentSuggestions { get; }
        public int ActiveIndex { get; }
        public int ReplaceStart { get; }
        public int ReplaceLength { get; }
        public string ArgumentHint { get; }
        public RoChatCommandDefinition ResolvedCommand { get; }
        public bool HasSuggestions => Suggestions.Length > 0;
        public bool HasArgumentSuggestions => ArgumentSuggestions.Length > 0;
        public bool HasArgumentHint => !string.IsNullOrWhiteSpace(ArgumentHint);
        public bool IsVisible => HasSuggestions || HasArgumentSuggestions || HasArgumentHint;
        public RoChatCommandSuggestion ActiveSuggestion => HasSuggestions && ActiveIndex >= 0 ? Suggestions[ActiveIndex] : default;
        public RoChatCommandArgumentSuggestion ActiveArgumentSuggestion => HasArgumentSuggestions && ActiveIndex >= 0 ? ArgumentSuggestions[ActiveIndex] : default;
    }

    public sealed class RoChatCommandCatalog {
        private static readonly string[] CustomBaseCommands = {
            "accept", "accinfo", "addperm", "addwarp", "adjgroup", "agi", "agitend", "agitend2",
            "agitstart", "agitstart2", "alive", "allowks", "allskill", "allstats", "alootid",
            "alootignoreid", "auction", "autolootcard", "autolootrate", "autoloottype", "autotrade",
            "ban", "block", "blvl", "bodystyle", "breakguild", "broadcast", "broadcast_item_drop",
            "camerainfo", "cart", "cartlist", "cash", "cashmount", "cddebug", "changecharsex",
            "changedress", "changegm", "changeleader", "changelook", "changesex", "channel",
            "charban", "charcommands", "charunban", "claninfo", "cleanarea", "cleanmap", "clearcart",
            "cleargstorage", "clearstorage", "clearweather", "clone", "clouds", "clouds2", "commands",
            "costume", "cvcoff", "cvcon", "day", "delitem", "dex", "disguise", "disguiseall",
            "disguiseguild", "displayskill", "displaystatus", "divorce", "doom", "doommap", "dropall",
            "duel", "dye", "effect", "email", "enchantui", "evilclone", "exp", "fakename", "feelreset",
            "fireworks", "fog", "follow", "font", "fontcolor", "gat", "glvl", "go", "grade", "gradeui",
            "guild", "guildrecall", "guildspy", "guildstorage", "gvgoff", "gvgon", "haircolor",
            "hairstyle", "hatch", "hatereset", "heal", "help", "hide", "hidenpc", "hidepet",
            "homevolution", "homfriendly", "homhungry", "hominfo", "homlevel", "hommutate",
            "homshuffle", "homstats", "homtalk", "identify", "identifyall", "idsearch", "int",
            "invite", "item", "item2", "itembound", "itembound2", "iteminfo", "itemlist", "itemreform",
            "itemreset", "jail", "jailfor", "jailtime", "jlvl", "jobchange", "join", "joinclan",
            "jump", "jumpto", "kami", "kamib", "kamic", "kick", "kickall", "kill", "killable",
            "killer", "killmonster", "killmonster2", "lang", "lasttp", "leave", "leaveclan", "leaves",
            "lkami", "load", "loadnpc", "localbroadcast", "lostskill", "luk", "mail", "maintenance",
            "makeegg", "makehomun", "mapexit", "mapflag", "mapinfo", "marry", "me", "memo", "misceffect",
            "mobinfo", "mobsearch", "model", "monster", "monsterbig", "monsterignore", "monstersmall",
            "mount", "mute", "mutearea", "night", "noask", "nocosplay", "noks", "npcmove", "npctalk",
            "nuke", "option", "party", "partyoption", "partyrecall", "partyspy", "petfriendly",
            "pethungry", "petrename", "pettalk", "points", "produce", "pvpoff", "pvpon", "quest",
            "questskill", "raise", "raisemap", "rates", "recall", "recallall", "refine", "refineryui",
            "refresh", "refreshall", "reject", "reloadatcommand", "reloadbattleconf", "reloadclans",
            "reloadgradedb", "reloaditemdb", "reloadmobdb", "reloadnpc", "reloadpcdb", "reloadquestdb",
            "reloadscript", "reloadskilldb", "reloadstatusdb", "repairall", "request", "reset",
            "rmvperm", "sakura", "save", "searchstore", "send", "set", "setbattleflag", "setzone",
            "showdelay", "showexp", "showmobs", "shownpc", "showzeny", "size", "sizeall", "sizeguild",
            "skdebug", "skillid", "skilloff", "skillon", "skilltree", "skpoint", "skreset", "slaveclone",
            "snow", "soulball", "sound", "speed", "spiritball", "stats", "storage", "storagelist",
            "storeall", "stpoint", "str", "streset", "summon", "time", "tonpc", "trade", "unban",
            "unblock", "undisguise", "undisguiseall", "undisguiseguild", "unequipall", "unjail",
            "unloadnpc", "unloadnpcfile", "unmute", "uptime", "users", "useskill", "version", "vit",
            "warp", "where", "whereis", "who", "who2", "who3", "whobuy", "whodrops", "whogm", "whomap",
            "whomap2", "whomap3", "whosell", "zeny"
        };

        private static readonly RoChatCommandCatalog playerCatalog = new RoChatCommandCatalog(BuildPlayerDefinitions());
        private static readonly RoChatCommandCatalog superPlayerCatalog = new RoChatCommandCatalog(BuildSuperPlayerDefinitions());
        private static readonly RoChatCommandCatalog allCatalog = new RoChatCommandCatalog(BuildAllDefinitions());

        private readonly RoChatCommandDefinition[] definitions;

        public RoChatCommandCatalog(IEnumerable<RoChatCommandDefinition> commandDefinitions) {
            if (commandDefinitions == null) {
                definitions = Array.Empty<RoChatCommandDefinition>();
                return;
            }

            var list = new List<RoChatCommandDefinition>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RoChatCommandDefinition definition in commandDefinitions) {
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.CanonicalCommand) &&
                    seen.Add(definition.CanonicalCommand)) {
                    list.Add(definition);
                }
            }

            definitions = list.ToArray();
        }

        public static RoChatCommandCatalog Default => allCatalog;
        public static RoChatCommandCatalog Player => playerCatalog;
        public static RoChatCommandCatalog SuperPlayer => superPlayerCatalog;
        public static RoChatCommandCatalog All => allCatalog;
        public IReadOnlyList<RoChatCommandDefinition> Definitions => definitions;

        public static RoChatCommandCatalog ForProfile(RoChatCommandCatalogProfile profile) {
            switch (profile) {
                case RoChatCommandCatalogProfile.SuperPlayer:
                    return SuperPlayer;
                case RoChatCommandCatalogProfile.All:
                    return All;
                default:
                    return Player;
            }
        }

        public RoChatCommandSuggestion[] FindMatches(string rawToken, int maxResults) {
            string token = RoChatCommandDefinition.NormalizeCommand(rawToken);
            if (token.Length <= 1 || maxResults <= 0) {
                return Array.Empty<RoChatCommandSuggestion>();
            }

            var results = new List<RoChatCommandSuggestion>(Math.Min(maxResults, definitions.Length));
            for (int i = 0; i < definitions.Length && results.Count < maxResults; i++) {
                RoChatCommandDefinition definition = definitions[i];
                if (EqualsCommand(definition.CanonicalCommand, token)) {
                    TryAddMatch(results, maxResults, definition, definition.CanonicalCommand);
                }
            }

            for (int i = 0; i < definitions.Length && results.Count < maxResults; i++) {
                RoChatCommandDefinition definition = definitions[i];
                for (int aliasIndex = 0; aliasIndex < definition.Aliases.Length; aliasIndex++) {
                    string alias = definition.Aliases[aliasIndex];
                    if (EqualsCommand(alias, token) && TryAddMatch(results, maxResults, definition, alias)) {
                        break;
                    }
                }
            }

            for (int i = 0; i < definitions.Length && results.Count < maxResults; i++) {
                RoChatCommandDefinition definition = definitions[i];
                if (StartsWith(definition.CanonicalCommand, token)) {
                    TryAddMatch(results, maxResults, definition, definition.CanonicalCommand);
                }
            }

            for (int i = 0; i < definitions.Length && results.Count < maxResults; i++) {
                RoChatCommandDefinition definition = definitions[i];
                for (int aliasIndex = 0; aliasIndex < definition.Aliases.Length; aliasIndex++) {
                    string alias = definition.Aliases[aliasIndex];
                    if (StartsWith(alias, token) && TryAddMatch(results, maxResults, definition, alias)) {
                        break;
                    }
                }
            }

            return results.ToArray();
        }

        private static bool TryAddMatch(
            List<RoChatCommandSuggestion> results,
            int maxResults,
            RoChatCommandDefinition definition,
            string matchedCommand) {
            if (results.Count >= maxResults || definition == null) {
                return false;
            }

            for (int i = 0; i < results.Count; i++) {
                if (ReferenceEquals(results[i].Definition, definition)) {
                    return false;
                }
            }

            results.Add(new RoChatCommandSuggestion(definition, matchedCommand));
            return true;
        }

        public bool TryResolve(string rawToken, out RoChatCommandDefinition definition) {
            string token = RoChatCommandDefinition.NormalizeCommand(rawToken);
            for (int i = 0; i < definitions.Length; i++) {
                RoChatCommandDefinition candidate = definitions[i];
                if (EqualsCommand(candidate.CanonicalCommand, token)) {
                    definition = candidate;
                    return true;
                }

                for (int aliasIndex = 0; aliasIndex < candidate.Aliases.Length; aliasIndex++) {
                    if (EqualsCommand(candidate.Aliases[aliasIndex], token)) {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        private static bool StartsWith(string value, string prefix) {
            return value != null && prefix != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsCommand(string left, string right) {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<RoChatCommandDefinition> BuildPlayerDefinitions() {
            var definitions = new Dictionary<string, RoChatCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            AddPlayerDefinitions(definitions);
            return definitions.Values;
        }

        private static IEnumerable<RoChatCommandDefinition> BuildSuperPlayerDefinitions() {
            var definitions = new Dictionary<string, RoChatCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            AddPlayerDefinitions(definitions);
            Add(
                definitions,
                "@alootid",
                "Autoloot specific item",
                new[] { "<item name/ID>" },
                RoChatCommandArgumentKind.Item);
            Add(
                definitions,
                "@autoloottype",
                "Autoloot item type",
                new[] { "<type>" },
                RoChatCommandArgumentKind.FreeText);
            Add(definitions, "@request", "Send a support request", new[] { "<message>" }, RoChatCommandArgumentKind.FreeText);
            Add(definitions, "@go", "Warp to a town", new[] { "<city name/number>" }, RoChatCommandArgumentKind.GoTown);
            Add(definitions, "@breakguild", "Disband guild", new[] { "<guild name>" }, RoChatCommandArgumentKind.FreeText);
            Add(definitions, "@camerainfo", "Show camera information", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@setcamera", "@viewpointvalue");
            return definitions.Values;
        }

        private static IEnumerable<RoChatCommandDefinition> BuildAllDefinitions() {
            var definitions = new Dictionary<string, RoChatCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            AddPlayerDefinitions(definitions);
            Add(
                definitions,
                "@monster",
                "Spawn monster",
                new[] { "<monster name/ID> [count]" },
                RoChatCommandArgumentKind.Monster,
                "@spawn");
            Add(definitions, "@mobsearch", "Search monsters by name", new[] { "<monster name>" }, RoChatCommandArgumentKind.Monster);
            Add(definitions, "@item", "Create item", new[] { "<item name/ID> [amount]" }, RoChatCommandArgumentKind.Item);
            Add(
                definitions,
                "@item2",
                "Create refined or slotted item",
                new[] {
                    new RoChatCommandArgumentSpec("<item name/ID>", RoChatCommandArgumentKind.Item),
                    new RoChatCommandArgumentSpec("[amount]", RoChatCommandArgumentKind.None),
                    new RoChatCommandArgumentSpec("[identified]", RoChatCommandArgumentKind.None),
                    new RoChatCommandArgumentSpec("[refine]", RoChatCommandArgumentKind.None),
                    new RoChatCommandArgumentSpec("[attribute]", RoChatCommandArgumentKind.None),
                    new RoChatCommandArgumentSpec("[card1]", RoChatCommandArgumentKind.Item),
                    new RoChatCommandArgumentSpec("[card2]", RoChatCommandArgumentKind.Item),
                    new RoChatCommandArgumentSpec("[card3]", RoChatCommandArgumentKind.Item),
                    new RoChatCommandArgumentSpec("[card4]", RoChatCommandArgumentKind.Item)
                });
            Add(definitions, "@idsearch", "Search item by name", new[] { "<item name>" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@alootid", "Autoloot specific item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@alootignoreid", "Ignore specific autoloot item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@produce", "Produce item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@delitem", "Delete item", new[] { "<item name/ID> [amount]" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@warp", "Warp to map coordinates", new[] { "<map> [x y]" }, RoChatCommandArgumentKind.Map, "@rura", "@mapmove");
            Add(definitions, "@addwarp", "Add permanent warp", new[] { "<map> <x> <y>" }, RoChatCommandArgumentKind.Map);
            Add(definitions, "@mapinfo", "Show map information", new[] { "[map]" }, RoChatCommandArgumentKind.Map);
            Add(definitions, "@whomap", "Show players on map", new[] { "[map]" }, RoChatCommandArgumentKind.Map);
            Add(definitions, "@whomap2", "Show detailed players on map", new[] { "[map]" }, RoChatCommandArgumentKind.Map);
            Add(definitions, "@whomap3", "Show compact players on map", new[] { "[map]" }, RoChatCommandArgumentKind.Map);
            Add(definitions, "@go", "Warp to a town", new[] { "<city name/number>" }, RoChatCommandArgumentKind.GoTown);
            Add(definitions, "@jobchange", "Change job", new[] { "<job name/ID>" }, RoChatCommandArgumentKind.FreeText, "@job");
            Add(definitions, "@jumpto", "Jump to player", new[] { "<player name>" }, RoChatCommandArgumentKind.PlayerName, "@goto", "@warpto");
            Add(definitions, "@who", "Search players", new[] { "[player name]" }, RoChatCommandArgumentKind.PlayerName, "@whois");
            Add(definitions, "@load", "Return to save point", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@return");
            Add(definitions, "@guildstorage", "Open guild storage", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@gstorage");
            Add(definitions, "@accinfo", "Show account information", new[] { "<player name>" }, RoChatCommandArgumentKind.PlayerName, "@accountinfo");
            Add(definitions, "@itemreset", "Clear inventory", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@clearinventory");
            Add(definitions, "@camerainfo", "Show camera information", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@setcamera", "@viewpointvalue");

            foreach (string command in CustomBaseCommands) {
                string normalized = RoChatCommandDefinition.NormalizeCommand(command);
                if (!definitions.ContainsKey(normalized)) {
                    Add(definitions, normalized);
                }
            }

            return definitions.Values;
        }

        private static void AddPlayerDefinitions(Dictionary<string, RoChatCommandDefinition> definitions) {
            Add(definitions, "@commands", "Show available commands");
            Add(definitions, "@help", "Show command help", new[] { "[command]" }, RoChatCommandArgumentKind.FreeText, "@h");
            Add(definitions, "@rates", "Show server rates");
            Add(definitions, "@showexp", "Toggle EXP gain messages");
            Add(definitions, "@mobinfo", "Monster information", new[] { "<monster name/ID>" }, RoChatCommandArgumentKind.Monster, "@monsterinfo", "@mi");
            Add(definitions, "@whereis", "Find monster spawn maps", new[] { "<monster name/ID>" }, RoChatCommandArgumentKind.Monster);
            Add(definitions, "@whodrops", "Find monsters that drop an item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item);
            Add(definitions, "@whobuy", "Find buying stores for an item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item, "@wb");
            Add(definitions, "@whosell", "Find vending stores for an item", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item, "@ws");
            Add(definitions, "@autotrade", "Start autotrade", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@at");
            Add(definitions, "@refresh", "Refresh client state");
            Add(definitions, "@iteminfo", "Item information", new[] { "<item name/ID>" }, RoChatCommandArgumentKind.Item, "@ii");
            Add(definitions, "@time", "Show server time", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@date", "@serverdate", "@servertime");
            Add(definitions, "@noks", "Toggle no kill-steal mode");
            Add(definitions, "@channel", "Channel command", new[] { "<channel> [message]" }, RoChatCommandArgumentKind.FreeText, "@main");
            Add(definitions, "@hominfo", "Show homunculus information");
            Add(definitions, "@pettalk", "Make pet talk", new[] { "<message>" }, RoChatCommandArgumentKind.FreeText);
            Add(definitions, "@noask", "Toggle request blocking");
            Add(definitions, "@lasttp", "Return to last teleport");
            Add(definitions, "@broadcast_item_drop", "Toggle item drop broadcast", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@broadcastitem");
            Add(definitions, "@hidepet", "Toggle pet visibility");

            Add(definitions, "@autoloot", "Open autoloot settings or switch set", new[] { "[set id]" }, RoChatCommandArgumentKind.FreeText);
            Add(definitions, "@killcount", "Open kill counter settings");
            Add(definitions, "@trophies", "Show monster trophy progress", Array.Empty<string>(), RoChatCommandArgumentKind.None, "@trophy", "@trohpies");
            Add(definitions, "@ready", "Confirm PvP ready check");
            Add(definitions, "@searchstore", "Open store search");
        }

        private static void Add(
            Dictionary<string, RoChatCommandDefinition> definitions,
            string command,
            string description = "Server command",
            string[] argumentHints = null,
            RoChatCommandArgumentKind firstArgumentKind = RoChatCommandArgumentKind.None,
            params string[] aliases) {
            if (definitions == null) {
                return;
            }

            var definition = new RoChatCommandDefinition(
                command,
                description,
                argumentHints ?? Array.Empty<string>(),
                firstArgumentKind,
                aliases);
            if (!string.IsNullOrWhiteSpace(definition.CanonicalCommand)) {
                definitions[definition.CanonicalCommand] = definition;
            }
        }

        private static void Add(
            Dictionary<string, RoChatCommandDefinition> definitions,
            string command,
            string description,
            RoChatCommandArgumentSpec[] argumentSpecs,
            params string[] aliases) {
            if (definitions == null) {
                return;
            }

            var definition = new RoChatCommandDefinition(
                command,
                description,
                argumentSpecs ?? Array.Empty<RoChatCommandArgumentSpec>(),
                aliases);
            if (!string.IsNullOrWhiteSpace(definition.CanonicalCommand)) {
                definitions[definition.CanonicalCommand] = definition;
            }
        }
    }

    public sealed class RoChatCommandSuggester {
        private const int DefaultMaxSuggestions = 6;

        private readonly RoChatCommandCatalog catalog;
        private readonly int maxSuggestions;

        public RoChatCommandSuggester(RoChatCommandCatalog commandCatalog = null, int maximumSuggestions = DefaultMaxSuggestions) {
            catalog = commandCatalog ?? RoChatCommandCatalog.Default;
            maxSuggestions = Math.Max(1, maximumSuggestions);
        }

        public RoChatCommandSuggestionState Analyze(string text, int caretPosition, int requestedActiveIndex = 0) {
            return Analyze(text, caretPosition, requestedActiveIndex, null);
        }

        public RoChatCommandSuggestionState Analyze(
            string text,
            int caretPosition,
            int requestedActiveIndex,
            Func<RoChatCommandDefinition, RoChatCommandArgumentKind, string, int, RoChatCommandArgumentSuggestion[]> argumentSuggestionProvider) {
            text ??= string.Empty;
            caretPosition = Math.Max(0, Math.Min(caretPosition, text.Length));

            if (!TryFindFirstCommandToken(text, out int tokenStart, out int tokenEnd)) {
                return RoChatCommandSuggestionState.Empty;
            }

            if (caretPosition < tokenStart) {
                return RoChatCommandSuggestionState.Empty;
            }

            string token = text.Substring(tokenStart, tokenEnd - tokenStart);
            bool caretInCommandToken = caretPosition <= tokenEnd;
            bool caretAfterCommandToken = caretPosition > tokenEnd;
            bool hasWhitespaceAfterToken = tokenEnd < text.Length && char.IsWhiteSpace(text[tokenEnd]);

            if (caretInCommandToken) {
                RoChatCommandSuggestion[] suggestions = catalog.FindMatches(token, maxSuggestions);
                RoChatCommandDefinition resolved = catalog.TryResolve(token, out RoChatCommandDefinition exact)
                    ? exact
                    : null;
                return new RoChatCommandSuggestionState(
                    suggestions,
                    requestedActiveIndex,
                    tokenStart,
                    token.Length,
                    string.Empty,
                    resolved);
            }

            if ((caretAfterCommandToken || hasWhitespaceAfterToken) &&
                catalog.TryResolve(token, out RoChatCommandDefinition command)) {
                int argumentStart = tokenEnd;
                while (argumentStart < text.Length && char.IsWhiteSpace(text[argumentStart])) {
                    argumentStart++;
                }

                if (argumentSuggestionProvider != null &&
                    TryResolveActiveArgument(command, text, argumentStart, caretPosition, out RoChatCommandArgumentSpec argumentSpec, out int replaceStart, out int replaceLength, out string query)) {
                    if (argumentSpec.Kind != RoChatCommandArgumentKind.None && !string.IsNullOrWhiteSpace(query)) {
                        RoChatCommandArgumentSuggestion[] argumentSuggestions = argumentSuggestionProvider(command, argumentSpec.Kind, query, maxSuggestions)
                            ?? Array.Empty<RoChatCommandArgumentSuggestion>();
                        if (argumentSuggestions.Length > 0) {
                            return new RoChatCommandSuggestionState(
                                Array.Empty<RoChatCommandSuggestion>(),
                                requestedActiveIndex,
                                replaceStart,
                                replaceLength,
                                command.Usage,
                                command,
                                argumentSuggestions);
                        }
                    }
                }

                return new RoChatCommandSuggestionState(
                    Array.Empty<RoChatCommandSuggestion>(),
                    -1,
                    tokenStart,
                    token.Length,
                    command.Usage,
                    command);
            }

            return RoChatCommandSuggestionState.Empty;
        }

        public bool TryAccept(
            string text,
            int caretPosition,
            int activeIndex,
            out string acceptedText,
            out int acceptedCaretPosition) {
            RoChatCommandSuggestionState state = Analyze(text, caretPosition, activeIndex);
            return TryAccept(text, state, out acceptedText, out acceptedCaretPosition);
        }

        public bool TryAccept(
            string text,
            RoChatCommandSuggestionState state,
            out string acceptedText,
            out int acceptedCaretPosition) {
            text ??= string.Empty;
            if ((!state.HasSuggestions && !state.HasArgumentSuggestions) || state.ReplaceStart < 0 || state.ReplaceStart > text.Length) {
                acceptedText = text;
                acceptedCaretPosition = Math.Max(0, Math.Min(text.Length, state.ReplaceStart));
                return false;
            }

            string replacement;
            if (state.HasArgumentSuggestions) {
                RoChatCommandArgumentSuggestion argument = state.ActiveArgumentSuggestion;
                replacement = argument.ReplacementText;
            } else {
                RoChatCommandDefinition command = state.ActiveSuggestion.Definition;
                if (command == null) {
                    acceptedText = text;
                    acceptedCaretPosition = Math.Max(0, Math.Min(text.Length, state.ReplaceStart));
                    return false;
                }

                replacement = command.CanonicalCommand;
            }

            int replaceEnd = Math.Min(text.Length, state.ReplaceStart + Math.Max(0, state.ReplaceLength));
            string prefix = text.Substring(0, state.ReplaceStart);
            string suffix = text.Substring(replaceEnd).TrimStart();
            acceptedText = prefix + replacement + " " + suffix;
            acceptedCaretPosition = (prefix + replacement + " ").Length;
            return true;
        }

        private static bool TryFindFirstCommandToken(string text, out int start, out int end) {
            start = 0;
            end = 0;
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            while (start < text.Length && char.IsWhiteSpace(text[start])) {
                start++;
            }

            if (start >= text.Length || text[start] != '@') {
                return false;
            }

            end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) {
                end++;
            }

            return end > start + 1;
        }

        private static bool TryResolveActiveArgument(
            RoChatCommandDefinition command,
            string text,
            int argumentStart,
            int caretPosition,
            out RoChatCommandArgumentSpec argumentSpec,
            out int replaceStart,
            out int replaceLength,
            out string query) {
            argumentSpec = default;
            replaceStart = argumentStart;
            replaceLength = 0;
            query = string.Empty;
            if (command == null || command.ArgumentSpecs.Length == 0 || caretPosition < argumentStart) {
                return false;
            }

            int position = argumentStart;
            for (int i = 0; i < command.ArgumentSpecs.Length; i++) {
                position = SkipWhitespace(text, position, caretPosition);
                argumentSpec = command.ArgumentSpecs[i];
                replaceStart = position;

                if (position >= caretPosition) {
                    replaceLength = 0;
                    return true;
                }

                int tokenEnd = FindTokenEnd(text, position, caretPosition);
                bool hasWhitespaceAfterToken = tokenEnd < caretPosition;
                if (IsSuggestible(argumentSpec.Kind)) {
                    if (ShouldConsumeSuggestibleArgument(argumentSpec.Kind, text, position, tokenEnd, hasWhitespaceAfterToken)) {
                        position = tokenEnd;
                        continue;
                    }

                    replaceLength = caretPosition - position;
                    query = text.Substring(position, replaceLength).Trim();
                    return true;
                }

                if (!hasWhitespaceAfterToken) {
                    replaceLength = caretPosition - position;
                    query = text.Substring(position, replaceLength).Trim();
                    return true;
                }

                position = tokenEnd;
            }

            return false;
        }

        private static int SkipWhitespace(string text, int position, int limit) {
            while (position < limit && position < text.Length && char.IsWhiteSpace(text[position])) {
                position++;
            }

            return position;
        }

        private static int FindTokenEnd(string text, int position, int limit) {
            while (position < limit && position < text.Length && !char.IsWhiteSpace(text[position])) {
                position++;
            }

            return position;
        }

        private static bool IsSuggestible(RoChatCommandArgumentKind kind) {
            return kind == RoChatCommandArgumentKind.Item ||
                kind == RoChatCommandArgumentKind.Monster ||
                kind == RoChatCommandArgumentKind.Map ||
                kind == RoChatCommandArgumentKind.GoTown;
        }

        private static bool ShouldConsumeSuggestibleArgument(
            RoChatCommandArgumentKind kind,
            string text,
            int start,
            int tokenEnd,
            bool hasWhitespaceAfterToken) {
            if (!hasWhitespaceAfterToken) {
                return false;
            }

            if (kind == RoChatCommandArgumentKind.Map || kind == RoChatCommandArgumentKind.GoTown) {
                return true;
            }

            if (kind != RoChatCommandArgumentKind.Item && kind != RoChatCommandArgumentKind.Monster) {
                return false;
            }

            string token = start >= 0 && tokenEnd > start && tokenEnd <= text.Length
                ? text.Substring(start, tokenEnd - start)
                : string.Empty;
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
    }

    /// <summary>
    /// The argument suggestions of the chat commands (an item, a monster, a map or a town of @go by name, prefix, word
    /// prefix or part), ranked and cut to the requested count: the suggester's argument provider. It has no UI (the
    /// legacy chat drew the list; the classic chat, like the original client, has no command autocomplete) and reads the
    /// tables it is given.
    /// </summary>
    public sealed class RoChatCommandArgumentSource {
        private static readonly GoTownAutocompleteEntry[] GoTownAutocompleteEntries = {
            new GoTownAutocompleteEntry(-3, "memo2", "Memo point 2", "memo 2", "memo2"),
            new GoTownAutocompleteEntry(-2, "memo1", "Memo point 1", "memo 1", "memo1"),
            new GoTownAutocompleteEntry(-1, "memo0", "Memo point 0", "memo 0", "memo0"),
            new GoTownAutocompleteEntry(0, "prontera", "Prontera", "pron", "pront"),
            new GoTownAutocompleteEntry(1, "morocc", "Morocc", "morroc", "morroc town"),
            new GoTownAutocompleteEntry(2, "geffen", "Geffen"),
            new GoTownAutocompleteEntry(3, "payon", "Payon"),
            new GoTownAutocompleteEntry(4, "alberta", "Alberta"),
            new GoTownAutocompleteEntry(5, "izlude", "Izlude"),
            new GoTownAutocompleteEntry(6, "aldebaran", "Al De Baran", "alde", "al de baran"),
            new GoTownAutocompleteEntry(7, "xmas", "Lutie", "lutie", "christmas"),
            new GoTownAutocompleteEntry(8, "comodo", "Comodo"),
            new GoTownAutocompleteEntry(9, "yuno", "Juno", "juno"),
            new GoTownAutocompleteEntry(10, "amatsu", "Amatsu"),
            new GoTownAutocompleteEntry(11, "gonryun", "Kunlun", "kunlun"),
            new GoTownAutocompleteEntry(12, "umbala", "Umbala"),
            new GoTownAutocompleteEntry(13, "niflheim", "Niflheim"),
            new GoTownAutocompleteEntry(14, "louyang", "Louyang", "luoyang"),
            new GoTownAutocompleteEntry(15, "startpoint", "Start point", "beginning", "beginner"),
            new GoTownAutocompleteEntry(16, "jail", "Jail", "prison"),
            new GoTownAutocompleteEntry(17, "jawaii", "Jawaii"),
            new GoTownAutocompleteEntry(18, "ayothaya", "Ayothaya"),
            new GoTownAutocompleteEntry(19, "einbroch", "Einbroch"),
            new GoTownAutocompleteEntry(20, "lighthalzen", "Lighthalzen", "lhz"),
            new GoTownAutocompleteEntry(21, "einbech", "Einbech"),
            new GoTownAutocompleteEntry(22, "hugel", "Hugel"),
            new GoTownAutocompleteEntry(23, "rachel", "Rachel", "rael"),
            new GoTownAutocompleteEntry(24, "veins", "Veins"),
            new GoTownAutocompleteEntry(25, "moscovia", "Moscovia"),
            new GoTownAutocompleteEntry(26, "mid_camp", "Midgard Camp", "midcamp", "midgard camp", "midgard"),
            new GoTownAutocompleteEntry(27, "manuk", "Manuk"),
            new GoTownAutocompleteEntry(28, "splendide", "Splendide"),
            new GoTownAutocompleteEntry(29, "brasilis", "Brasilis"),
            new GoTownAutocompleteEntry(30, "dicastes", "El Dicastes", "el dicastes"),
            new GoTownAutocompleteEntry(31, "mora", "Mora"),
            new GoTownAutocompleteEntry(32, "dewata", "Dewata"),
            new GoTownAutocompleteEntry(33, "malangdo", "Malangdo", "malangdo island"),
            new GoTownAutocompleteEntry(34, "malaya", "Malaya Port", "malaya port"),
            new GoTownAutocompleteEntry(35, "eclage", "Eclage")
        };

        private RoItemMetadataTable itemMetadata = RoItemMetadataTable.Empty;
        private RoItemMetadataTable itemIndexSource;
        private ItemAutocompleteCandidate[] itemIndex = Array.Empty<ItemAutocompleteCandidate>();

        /// <summary>The items searched (the item index is rebuilt when the table changes).</summary>
        public RoItemMetadataTable ItemMetadata {
            get => itemMetadata;
            set {
                itemMetadata = value ?? RoItemMetadataTable.Empty;
                ClearItemIndex();
            }
        }

        /// <summary>The monsters searched.</summary>
        public RoEntityDisplayMetadataTable EntityMetadata { get; set; } = RoEntityDisplayMetadataTable.Empty;

        /// <summary>The maps searched (the map table).</summary>
        public RoAssetTables AssetTables { get; set; } = RoAssetTables.Empty;

        public RoChatCommandArgumentSuggestion[] FindArgumentSuggestions(
            RoChatCommandDefinition command,
            RoChatCommandArgumentKind argumentKind,
            string query,
            int maxResults) {
            if (command == null) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            switch (argumentKind) {
                case RoChatCommandArgumentKind.Item:
                    return FindItemArgumentSuggestions(query, maxResults);
                case RoChatCommandArgumentKind.Monster:
                    return FindMonsterArgumentSuggestions(query, maxResults);
                case RoChatCommandArgumentKind.Map:
                    return FindMapArgumentSuggestions(query, maxResults);
                case RoChatCommandArgumentKind.GoTown:
                    return FindGoTownArgumentSuggestions(query, maxResults);
                default:
                    return Array.Empty<RoChatCommandArgumentSuggestion>();
            }
        }

        private RoChatCommandArgumentSuggestion[] FindItemArgumentSuggestions(string query, int maxResults) {
            string normalizedQuery = NormalizeAutocompleteSearch(query);
            if (normalizedQuery.Length == 0 || maxResults <= 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            RoItemMetadataTable metadata = ItemMetadata;
            if (metadata == null || metadata.Count == 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            ItemAutocompleteCandidate[] index = GetCommandAutocompleteItemIndex(metadata);
            if (index.Length == 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            var matches = new List<ArgumentCandidate>(Math.Min(maxResults, index.Length));
            for (int i = 0; i < index.Length; i++) {
                ItemAutocompleteCandidate candidate = index[i];
                int score = ScoreNormalizedItemAutocompleteMatch(candidate, normalizedQuery);
                if (score < 0) {
                    continue;
                }

                InsertBestArgumentCandidate(
                    matches,
                    new ArgumentCandidate(score, candidate.DisplayName, candidate.SortKey, candidate.Suggestion),
                    maxResults);
            }

            return BuildArgumentSuggestions(matches, matches.Count);
        }

        private RoChatCommandArgumentSuggestion[] FindMonsterArgumentSuggestions(string query, int maxResults) {
            string normalizedQuery = NormalizeAutocompleteSearch(query);
            if (normalizedQuery.Length == 0 || maxResults <= 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            RoEntityDisplayMetadataTable metadata = EntityMetadata;
            if (metadata == null || metadata.Count == 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            var matches = new List<ArgumentCandidate>(Math.Min(maxResults * 4, metadata.Count));
            foreach (KeyValuePair<int, RoEntityDisplayMetadata> entry in metadata.Entities) {
                RoEntityDisplayMetadata monster = entry.Value;
                if (monster == null || monster.Kind != RoEntityDisplayKind.Monster) {
                    continue;
                }

                string displayName = FirstNonEmpty(monster.DisplayName, monster.AegisName, monster.ResourceName);
                if (string.IsNullOrWhiteSpace(displayName)) {
                    continue;
                }

                int score = ScoreAutocompleteMatch(displayName, monster.AegisName, monster.ResourceName, normalizedQuery);
                if (score < 0) {
                    continue;
                }

                string secondary = monster.Level > 0
                    ? "Lv " + monster.Level.ToString(CultureInfo.InvariantCulture) + " / ID " + entry.Key.ToString(CultureInfo.InvariantCulture)
                    : "ID " + entry.Key.ToString(CultureInfo.InvariantCulture);
                matches.Add(new ArgumentCandidate(
                    score,
                    displayName,
                    entry.Key.ToString(CultureInfo.InvariantCulture),
                    new RoChatCommandArgumentSuggestion(displayName, entry.Key, secondary)));
            }

            return BuildArgumentSuggestions(matches, maxResults);
        }

        private RoChatCommandArgumentSuggestion[] FindMapArgumentSuggestions(string query, int maxResults) {
            string normalizedQuery = NormalizeAutocompleteSearch(query);
            if (normalizedQuery.Length == 0 || maxResults <= 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            RoAssetTables tables = AssetTables;
            if (tables == null || tables.MapTable.Count == 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            var matches = new List<ArgumentCandidate>(Math.Min(maxResults * 4, tables.MapTable.Count));
            foreach (KeyValuePair<string, MapTableStruct> entry in tables.MapTable) {
                string mapId = NormalizeMapCommandId(entry.Key);
                if (string.IsNullOrWhiteSpace(mapId)) {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(entry.Value.name) ? mapId : entry.Value.name.Trim();
                int score = ScoreAutocompleteMatch(mapId, displayName, string.Empty, normalizedQuery);
                if (score < 0) {
                    continue;
                }

                string secondary = string.Equals(displayName, mapId, StringComparison.Ordinal) ? string.Empty : displayName;
                matches.Add(new ArgumentCandidate(
                    score,
                    mapId,
                    mapId,
                    new RoChatCommandArgumentSuggestion(mapId, mapId, secondary)));
            }

            return BuildArgumentSuggestions(matches, maxResults);
        }

        private RoChatCommandArgumentSuggestion[] FindGoTownArgumentSuggestions(string query, int maxResults) {
            string normalizedQuery = NormalizeAutocompleteSearch(query);
            if (normalizedQuery.Length == 0 || maxResults <= 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            var matches = new List<ArgumentCandidate>(Math.Min(maxResults * 2, GoTownAutocompleteEntries.Length));
            for (int i = 0; i < GoTownAutocompleteEntries.Length; i++) {
                GoTownAutocompleteEntry entry = GoTownAutocompleteEntries[i];
                int score = ScoreGoTownAutocompleteMatch(entry, normalizedQuery);
                if (score < 0) {
                    continue;
                }

                string secondary = entry.Number.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(entry.DisplayName)) {
                    secondary += " " + entry.DisplayName;
                }

                matches.Add(new ArgumentCandidate(
                    score,
                    entry.Canonical,
                    entry.Number.ToString(CultureInfo.InvariantCulture),
                    new RoChatCommandArgumentSuggestion(entry.Canonical, entry.Canonical, secondary)));
            }

            return BuildArgumentSuggestions(matches, maxResults);
        }

        private static RoChatCommandArgumentSuggestion[] BuildArgumentSuggestions(List<ArgumentCandidate> matches, int maxResults) {
            if (matches == null || matches.Count == 0 || maxResults <= 0) {
                return Array.Empty<RoChatCommandArgumentSuggestion>();
            }

            matches.Sort((left, right) => {
                int score = left.Score.CompareTo(right.Score);
                if (score != 0) {
                    return score;
                }

                int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
                return name != 0 ? name : string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
            });

            int count = Math.Min(maxResults, matches.Count);
            var suggestions = new RoChatCommandArgumentSuggestion[count];
            for (int i = 0; i < count; i++) {
                suggestions[i] = matches[i].Suggestion;
            }

            return suggestions;
        }

        private ItemAutocompleteCandidate[] GetCommandAutocompleteItemIndex(RoItemMetadataTable metadata) {
            if (metadata == null || metadata.Count == 0) {
                if (!ReferenceEquals(itemIndexSource, metadata)) {
                    itemIndexSource = metadata;
                    itemIndex = Array.Empty<ItemAutocompleteCandidate>();
                }

                return itemIndex;
            }

            if (ReferenceEquals(itemIndexSource, metadata) && itemIndex != null) {
                return itemIndex;
            }

            itemIndexSource = metadata;
            itemIndex = BuildCommandAutocompleteItemIndex(metadata);
            return itemIndex;
        }

        private void ClearItemIndex() {
            itemIndexSource = null;
            itemIndex = Array.Empty<ItemAutocompleteCandidate>();
        }

        private static ItemAutocompleteCandidate[] BuildCommandAutocompleteItemIndex(RoItemMetadataTable metadata) {
            if (metadata == null || metadata.Count == 0) {
                return Array.Empty<ItemAutocompleteCandidate>();
            }

            var candidates = new List<ItemAutocompleteCandidate>(metadata.Count);
            foreach (KeyValuePair<int, RoItemMetadata> entry in metadata.Items) {
                RoItemMetadata item = entry.Value;
                if (item == null) {
                    continue;
                }

                string displayName = item.GetDisplayName(true);
                if (RoItemLink.IsFallbackDisplayName(displayName)) {
                    continue;
                }

                string suggestionName = FormatItemAutocompleteDisplayName(displayName, item);
                string itemId = entry.Key.ToString(CultureInfo.InvariantCulture);
                candidates.Add(new ItemAutocompleteCandidate(
                    suggestionName,
                    itemId,
                    NormalizeAutocompleteSearch(displayName),
                    NormalizeAutocompleteSearch(suggestionName),
                    NormalizeAutocompleteSearch(item.AegisName),
                    new RoChatCommandArgumentSuggestion(
                        suggestionName,
                        entry.Key,
                        "ID " + itemId)));
            }

            return candidates.ToArray();
        }

        private static void InsertBestArgumentCandidate(List<ArgumentCandidate> matches, ArgumentCandidate candidate, int maxResults) {
            if (matches == null || maxResults <= 0) {
                return;
            }

            int insertIndex = 0;
            while (insertIndex < matches.Count && CompareArgumentCandidates(matches[insertIndex], candidate) <= 0) {
                insertIndex++;
            }

            if (insertIndex >= maxResults) {
                return;
            }

            matches.Insert(insertIndex, candidate);
            if (matches.Count > maxResults) {
                matches.RemoveAt(matches.Count - 1);
            }
        }

        private static int CompareArgumentCandidates(ArgumentCandidate left, ArgumentCandidate right) {
            int score = left.Score.CompareTo(right.Score);
            if (score != 0) {
                return score;
            }

            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
        }

        private static int ScoreNormalizedItemAutocompleteMatch(ItemAutocompleteCandidate candidate, string normalizedQuery) {
            if (candidate.NormalizedDisplayName.Length > 0) {
                if (string.Equals(candidate.NormalizedDisplayName, normalizedQuery, StringComparison.Ordinal)) {
                    return 0;
                }

                if (candidate.NormalizedDisplayName.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 1;
                }

                if (ContainsWordPrefix(candidate.NormalizedDisplayName, normalizedQuery)) {
                    return 2;
                }

                if (candidate.NormalizedDisplayName.Contains(normalizedQuery)) {
                    return 3;
                }
            }

            if (candidate.NormalizedDisplayNameWithSlots.Length > 0) {
                if (string.Equals(candidate.NormalizedDisplayNameWithSlots, normalizedQuery, StringComparison.Ordinal)) {
                    return 0;
                }

                if (candidate.NormalizedDisplayNameWithSlots.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 1;
                }

                if (ContainsWordPrefix(candidate.NormalizedDisplayNameWithSlots, normalizedQuery)) {
                    return 2;
                }

                if (candidate.NormalizedDisplayNameWithSlots.Contains(normalizedQuery)) {
                    return 3;
                }
            }

            if (candidate.NormalizedAegisName.Length > 0) {
                if (string.Equals(candidate.NormalizedAegisName, normalizedQuery, StringComparison.Ordinal)) {
                    return 4;
                }

                if (candidate.NormalizedAegisName.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 5;
                }

                if (candidate.NormalizedAegisName.Contains(normalizedQuery)) {
                    return 6;
                }
            }

            return -1;
        }

        private static string FormatItemAutocompleteDisplayName(string displayName, RoItemMetadata item) {
            string name = displayName ?? string.Empty;
            int slotCount = item != null ? Math.Max(0, item.SlotCount) : 0;
            return slotCount > 0 ? name + " [" + slotCount.ToString(CultureInfo.InvariantCulture) + "]" : name;
        }

        private static int ScoreAutocompleteMatch(string primary, string secondary, string tertiary, string normalizedQuery) {
            string normalizedName = NormalizeAutocompleteSearch(primary);
            if (normalizedName.Length > 0) {
                if (string.Equals(normalizedName, normalizedQuery, StringComparison.Ordinal)) {
                    return 0;
                }

                if (normalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 1;
                }

                if (ContainsWordPrefix(normalizedName, normalizedQuery)) {
                    return 2;
                }

                if (normalizedName.Contains(normalizedQuery)) {
                    return 3;
                }
            }

            string normalizedSecondary = NormalizeAutocompleteSearch(secondary);
            if (normalizedSecondary.Length > 0) {
                if (string.Equals(normalizedSecondary, normalizedQuery, StringComparison.Ordinal)) {
                    return 4;
                }

                if (normalizedSecondary.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 5;
                }

                if (normalizedSecondary.Contains(normalizedQuery)) {
                    return 6;
                }
            }

            string normalizedTertiary = NormalizeAutocompleteSearch(tertiary);
            if (normalizedTertiary.Length > 0) {
                if (string.Equals(normalizedTertiary, normalizedQuery, StringComparison.Ordinal)) {
                    return 7;
                }

                if (normalizedTertiary.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 8;
                }

                if (normalizedTertiary.Contains(normalizedQuery)) {
                    return 9;
                }
            }

            return -1;
        }

        private static int ScoreGoTownAutocompleteMatch(GoTownAutocompleteEntry entry, string normalizedQuery) {
            int score = ScoreAutocompleteMatch(entry.Canonical, entry.DisplayName, entry.Number.ToString(CultureInfo.InvariantCulture), normalizedQuery);
            if (score >= 0) {
                return score;
            }

            if (entry.Aliases == null) {
                return -1;
            }

            for (int i = 0; i < entry.Aliases.Length; i++) {
                string normalizedAlias = NormalizeAutocompleteSearch(entry.Aliases[i]);
                if (normalizedAlias.Length == 0) {
                    continue;
                }

                if (normalizedAlias.StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return 1;
                }

                if (ContainsWordPrefix(normalizedAlias, normalizedQuery)) {
                    return 2;
                }

                if (normalizedAlias.Contains(normalizedQuery)) {
                    return 4;
                }
            }

            return -1;
        }

        private static string NormalizeMapCommandId(string mapId) {
            string value = (mapId ?? string.Empty).Trim();
            if (value.EndsWith(".rsw", StringComparison.OrdinalIgnoreCase)) {
                value = value.Substring(0, value.Length - 4);
            }

            return value;
        }

        private static string FirstNonEmpty(params string[] values) {
            if (values == null) {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++) {
                if (!string.IsNullOrWhiteSpace(values[i])) {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        private static bool ContainsWordPrefix(string normalizedText, string normalizedQuery) {
            int index = normalizedText.IndexOf(' ');
            while (index >= 0 && index + 1 < normalizedText.Length) {
                if (normalizedText.Substring(index + 1).StartsWith(normalizedQuery, StringComparison.Ordinal)) {
                    return true;
                }

                index = normalizedText.IndexOf(' ', index + 1);
            }

            return false;
        }

        private static string NormalizeAutocompleteSearch(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < value.Length; i++) {
                char ch = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(ch)) {
                    builder.Append(ch);
                    previousWasSpace = false;
                    continue;
                }

                if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == '\'') {
                    if (!previousWasSpace && builder.Length > 0) {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == ' ') {
                builder.Length--;
            }

            return builder.ToString();
        }

        private readonly struct ArgumentCandidate {
            public ArgumentCandidate(int score, string displayName, string sortKey, RoChatCommandArgumentSuggestion suggestion) {
                Score = score;
                DisplayName = displayName ?? string.Empty;
                SortKey = sortKey ?? string.Empty;
                Suggestion = suggestion;
            }

            public int Score { get; }
            public string DisplayName { get; }
            public string SortKey { get; }
            public RoChatCommandArgumentSuggestion Suggestion { get; }
        }

        private readonly struct ItemAutocompleteCandidate {
            public ItemAutocompleteCandidate(
                string displayName,
                string sortKey,
                string normalizedDisplayName,
                string normalizedDisplayNameWithSlots,
                string normalizedAegisName,
                RoChatCommandArgumentSuggestion suggestion) {
                DisplayName = displayName ?? string.Empty;
                SortKey = sortKey ?? string.Empty;
                NormalizedDisplayName = normalizedDisplayName ?? string.Empty;
                NormalizedDisplayNameWithSlots = normalizedDisplayNameWithSlots ?? string.Empty;
                NormalizedAegisName = normalizedAegisName ?? string.Empty;
                Suggestion = suggestion;
            }

            public string DisplayName { get; }
            public string SortKey { get; }
            public string NormalizedDisplayName { get; }
            public string NormalizedDisplayNameWithSlots { get; }
            public string NormalizedAegisName { get; }
            public RoChatCommandArgumentSuggestion Suggestion { get; }
        }

        private readonly struct GoTownAutocompleteEntry {
            public GoTownAutocompleteEntry(int number, string canonical, string displayName, params string[] aliases) {
                Number = number;
                Canonical = canonical ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Aliases = aliases ?? Array.Empty<string>();
            }

            public int Number { get; }
            public string Canonical { get; }
            public string DisplayName { get; }
            public string[] Aliases { get; }
        }
    }
}
