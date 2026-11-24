using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    public static class RoServerProfileConfig {
        public const string RelativePath = "Ragnarok/server-profile.cfg";

        public static RoServerProfile LoadFromStreamingAssets(RoServerProfile fallback, out string diagnostic) {
            string path = Path.Combine(Application.streamingAssetsPath, RelativePath.Replace('/', Path.DirectorySeparatorChar));
            return TryLoadFromFile(path, fallback, out RoServerProfile profile, out diagnostic)
                ? profile
                : fallback;
        }

        public static bool TryLoadFromFile(string path, RoServerProfile fallback, out RoServerProfile profile, out string diagnostic) {
            profile = fallback.PacketVersion == 0 ? RoServerProfile.LocalRathena20200401() : fallback;
            diagnostic = string.Empty;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return false;
            }

            try {
                profile = ApplyText(File.ReadAllText(path), profile, out diagnostic);
                if (string.IsNullOrWhiteSpace(diagnostic)) {
                    diagnostic = $"Loaded RO server profile from {path}.";
                } else {
                    diagnostic = $"{diagnostic} Source: {path}.";
                }

                return true;
            } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is OverflowException) {
                diagnostic = $"Failed to load RO server profile config '{path}': {ex.Message}";
                return false;
            }
        }

        public static RoServerProfile ApplyText(string text, RoServerProfile fallback, out string diagnostic) {
            RoServerProfile baseProfile = fallback.PacketVersion == 0 ? RoServerProfile.LocalRathena20200401() : fallback;
            Dictionary<string, string> values = Parse(text);

            int packetVersion = ReadInt(values, baseProfile.PacketVersion, "packetver", "packetversion");
            RoServerProfile metadataDefaults = MetadataDefaultsFor(packetVersion, baseProfile);

            string name = ReadString(values, metadataDefaults.Name, "name", "profile", "profilename");
            int clientVersion = ReadInt(values, metadataDefaults.ClientVersion, "clientversion", "clientver");
            int languageType = ReadInt(values, metadataDefaults.LanguageType, "languagetype", "language", "langtype");
            bool renewal = ReadBool(values, metadataDefaults.Renewal, "renewal");
            bool packetObfuscation = ReadBool(values, metadataDefaults.PacketObfuscation, "packetobfuscation", "obfuscation");
            int legacyTextCodePage = ReadInt(values, metadataDefaults.LegacyTextCodePage, "legacytextcodepage", "codepage", "textcodepage");

            string sharedHost = ReadString(values, null, "host", "serverhost");
            string loginHost = ReadString(values, sharedHost ?? baseProfile.Login.Host, "loginhost");
            string characterHost = ReadString(values, sharedHost ?? baseProfile.Character.Host, "characterhost", "charhost");
            string mapHost = ReadString(values, sharedHost ?? baseProfile.Map.Host, "maphost");
            int loginPort = ReadInt(values, baseProfile.Login.Port, "loginport");
            int characterPort = ReadInt(values, baseProfile.Character.Port, "characterport", "charport");
            int mapPort = ReadInt(values, baseProfile.Map.Port, "mapport");

            string webSocketProxy = ReadString(values, metadataDefaults.WebSocketProxy, "websocketproxy", "wsproxy");
            RoPacketCryptConfig cryptConfig = ReadPacketCryptConfig(values, metadataDefaults.PacketCryptConfig);

            diagnostic = $"Configured RO profile '{name}' packetver={packetVersion} login={loginHost}:{loginPort}.";
            return new RoServerProfile(
                name,
                packetVersion,
                clientVersion,
                languageType,
                renewal,
                packetObfuscation,
                new RoServerEndpoint(loginHost, loginPort),
                new RoServerEndpoint(characterHost, characterPort),
                new RoServerEndpoint(mapHost, mapPort),
                string.IsNullOrWhiteSpace(webSocketProxy) ? null : webSocketProxy,
                legacyTextCodePage,
                cryptConfig);
        }

        private static Dictionary<string, string> Parse(string text) {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) {
                return values;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0) {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0) {
                    throw new FormatException($"Line {i + 1} must be key=value.");
                }

                string key = NormalizeKey(line.Substring(0, separator));
                string value = line.Substring(separator + 1).Trim();
                if (key.Length == 0) {
                    throw new FormatException($"Line {i + 1} has an empty key.");
                }

                values[key] = value;
            }

            return values;
        }

        private static string StripComment(string line) {
            if (string.IsNullOrEmpty(line)) {
                return string.Empty;
            }

            int hash = line.IndexOf('#');
            int semicolon = line.IndexOf(';');
            int comment = hash < 0 ? semicolon : semicolon < 0 ? hash : Math.Min(hash, semicolon);
            return comment < 0 ? line : line.Substring(0, comment);
        }

        private static string NormalizeKey(string key) {
            string value = key?.Trim() ?? string.Empty;
            return value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).ToLowerInvariant();
        }

        private static RoServerProfile MetadataDefaultsFor(int packetVersion, RoServerProfile fallback) {
            if (packetVersion == 20240619) {
                return new RoServerProfile(
                    "rAthena 20240619",
                    packetVersion: 20240619,
                    clientVersion: 45,
                    languageType: 0,
                    renewal: true,
                    packetObfuscation: false,
                    login: fallback.Login,
                    character: fallback.Character,
                    map: fallback.Map,
                    webSocketProxy: fallback.WebSocketProxy,
                    legacyTextCodePage: fallback.LegacyTextCodePage,
                    packetCryptConfig: fallback.PacketCryptConfig);
            }

            return new RoServerProfile(
                fallback.Name,
                packetVersion,
                fallback.ClientVersion,
                fallback.LanguageType,
                fallback.Renewal,
                fallback.PacketObfuscation,
                fallback.Login,
                fallback.Character,
                fallback.Map,
                fallback.WebSocketProxy,
                fallback.LegacyTextCodePage,
                fallback.PacketCryptConfig);
        }

        private static string ReadString(Dictionary<string, string> values, string defaultValue, params string[] keys) {
            return TryGet(values, out string value, keys) ? value.Trim() : defaultValue;
        }

        private static int ReadInt(Dictionary<string, string> values, int defaultValue, params string[] keys) {
            if (!TryGet(values, out string value, keys) || string.IsNullOrWhiteSpace(value)) {
                return defaultValue;
            }

            return int.Parse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static bool ReadBool(Dictionary<string, string> values, bool defaultValue, params string[] keys) {
            if (!TryGet(values, out string value, keys) || string.IsNullOrWhiteSpace(value)) {
                return defaultValue;
            }

            string normalized = value.Trim().ToLowerInvariant();
            switch (normalized) {
                case "1":
                case "true":
                case "yes":
                case "on":
                case "enabled":
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                case "disabled":
                    return false;
                default:
                    throw new FormatException($"'{value}' is not a valid boolean value.");
            }
        }

        private static RoPacketCryptConfig ReadPacketCryptConfig(Dictionary<string, string> values, RoPacketCryptConfig defaultValue) {
            if (!TryGet(values, out string mode, "packetcrypt", "crypt") || string.IsNullOrWhiteSpace(mode)) {
                return defaultValue;
            }

            string normalized = mode.Trim().ToLowerInvariant();
            switch (normalized) {
                case "0":
                case "false":
                case "off":
                case "disabled":
                case "none":
                    return RoPacketCryptConfig.Disabled;
                case "1":
                case "true":
                case "on":
                case "enabled":
                case "default":
                    bool required = ReadBool(values, true, "packetcryptrequired", "cryptrequired");
                    bool transformInbound = ReadBool(values, false, "packetcrypttransforminbound", "crypttransforminbound");
                    return RoPacketCryptConfig.EnableDefault(required, transformInbound);
                default:
                    throw new FormatException($"Unsupported packetCrypt mode '{mode}'. Use disabled or default.");
            }
        }

        private static bool TryGet(Dictionary<string, string> values, out string value, params string[] keys) {
            for (int i = 0; i < keys.Length; i++) {
                if (values.TryGetValue(NormalizeKey(keys[i]), out value)) {
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
