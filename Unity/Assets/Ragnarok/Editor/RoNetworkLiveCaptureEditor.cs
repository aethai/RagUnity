using System;
using System.Globalization;
using System.IO;
using Ragnarok.Network;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    public static class RoNetworkLiveCaptureEditor {
        private const string AccountEnv = "RO_NETWORK_ACCOUNT";
        private const string PasswordEnv = "RO_NETWORK_PASSWORD";
        private const string CharacterEnv = "RO_NETWORK_CHARACTER";
        private const string CharacterSlotEnv = "RO_NETWORK_CHARACTER_SLOT";
        private const string HairColorEnv = "RO_NETWORK_HAIR_COLOR";
        private const string HairStyleEnv = "RO_NETWORK_HAIR_STYLE";
        private const string StartJobEnv = "RO_NETWORK_START_JOB";
        private const string SexEnv = "RO_NETWORK_SEX";
        private const string RequestMoveEnv = "RO_NETWORK_REQUEST_MOVE";
        private const string MoveXEnv = "RO_NETWORK_MOVE_X";
        private const string MoveYEnv = "RO_NETWORK_MOVE_Y";
        private const string MoveDirectionEnv = "RO_NETWORK_MOVE_DIR";
        private const string TimeoutEnv = "RO_NETWORK_TIMEOUT_MS";
        private const string CapacityEnv = "RO_NETWORK_CAPTURE_CAPACITY";
        private const string RedactSensitiveEnv = "RO_NETWORK_REDACT_SENSITIVE";
        private const string CaptureOutputDirectoryEnv = "RO_NETWORK_CAPTURE_OUTPUT_DIR";
        private const string CapturePathEnv = "RO_NETWORK_CAPTURE_PATH";
        private const string ReportPathEnv = "RO_NETWORK_REPORT_PATH";
        private const string LoginHostEnv = "RO_NETWORK_LOGIN_HOST";
        private const string LoginPortEnv = "RO_NETWORK_LOGIN_PORT";
        private const string CharacterHostEnv = "RO_NETWORK_CHAR_HOST";
        private const string CharacterPortEnv = "RO_NETWORK_CHAR_PORT";
        private const string MapHostEnv = "RO_NETWORK_MAP_HOST";
        private const string MapPortEnv = "RO_NETWORK_MAP_PORT";
        private const string DefaultOutputDirectory = "Temp/NetworkCaptures";

        [MenuItem("Ragnarok/Network/Capture Local rAthena Session")]
        public static void RunFromMenu() {
            RunFromEnvironment();
        }

        public static void RunFromEnvironment() {
            try {
                RoLiveCaptureRequest request = CreateRequestFromEnvironment();
                Debug.Log(
                    $"Starting RO live network capture: profile='{request.Profile.Name}', account='{request.Account}', " +
                    $"character='{request.CharacterName}', move={request.RequestMove}, capture='{request.CapturePath}', report='{request.ReportPath}'.");

                RoLiveCaptureResult result = RoLiveCaptureRunner.RunLoginCreateEnterLoadAndOptionalMove(request);
                Debug.Log(
                    $"Finished RO live network capture: packets={result.Capture.Entries.Length}, " +
                    $"decoded={result.Report.DecodedCount}, unknown={result.Report.UnknownPacketCount}, " +
                    $"invalid={result.Report.InvalidLengthCount}, failed={result.Report.DecodeFailedCount}, " +
                    $"capture='{result.CapturePath}', report='{result.ReportPath}'.");

                if (Application.isBatchMode) {
                    EditorApplication.Exit(0);
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
                if (Application.isBatchMode) {
                    EditorApplication.Exit(1);
                    return;
                }

                throw;
            }
        }

        private static RoLiveCaptureRequest CreateRequestFromEnvironment() {
            string account = RequireEnvironment(AccountEnv);
            string password = Environment.GetEnvironmentVariable(PasswordEnv) ?? string.Empty;
            string characterName = RequireEnvironment(CharacterEnv);
            string outputDirectory = ReadString(CaptureOutputDirectoryEnv, DefaultOutputDirectory);
            string filePrefix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + SanitizeFileName(characterName);

            string capturePath = ReadString(CapturePathEnv, Path.Combine(outputDirectory, filePrefix + ".ropcap"));
            string reportPath = ReadString(ReportPathEnv, Path.Combine(outputDirectory, filePrefix + ".txt"));
            capturePath = NormalizeAndPreparePath(capturePath);
            reportPath = NormalizeAndPreparePath(reportPath);

            var request = new RoLiveCaptureRequest(account, password, characterName) {
                Profile = BuildProfileFromEnvironment(),
                CharacterSlot = ReadInt(CharacterSlotEnv, 0),
                HairColor = ReadUshort(HairColorEnv, 0),
                HairStyle = ReadUshort(HairStyleEnv, 1),
                StartJob = ReadInt(StartJobEnv, 0),
                Sex = ReadOptionalByte(SexEnv),
                RequestMove = ReadBool(RequestMoveEnv, true),
                MoveX = ReadInt(MoveXEnv, -1),
                MoveY = ReadInt(MoveYEnv, -1),
                MoveDirection = ReadInt(MoveDirectionEnv, 6),
                TimeoutMilliseconds = ReadInt(TimeoutEnv, 5000),
                CaptureCapacity = ReadInt(CapacityEnv, 8192),
                RedactSensitivePackets = ReadBool(RedactSensitiveEnv, true),
                CapturePath = capturePath,
                ReportPath = reportPath
            };

            return request;
        }

        private static RoServerProfile BuildProfileFromEnvironment() {
            RoServerProfile profile = RoServerProfile.LocalRathena20200401();
            RoServerEndpoint login = ReadEndpoint(LoginHostEnv, LoginPortEnv, profile.Login);
            RoServerEndpoint character = ReadEndpoint(CharacterHostEnv, CharacterPortEnv, profile.Character);
            RoServerEndpoint map = ReadEndpoint(MapHostEnv, MapPortEnv, profile.Map);

            return new RoServerProfile(
                profile.Name,
                profile.PacketVersion,
                profile.ClientVersion,
                profile.LanguageType,
                profile.Renewal,
                profile.PacketObfuscation,
                login,
                character,
                map,
                profile.WebSocketProxy,
                profile.LegacyTextCodePage);
        }

        private static RoServerEndpoint ReadEndpoint(string hostEnv, string portEnv, RoServerEndpoint fallback) {
            string host = ReadString(hostEnv, fallback.Host);
            int port = ReadInt(portEnv, fallback.Port);
            return new RoServerEndpoint(host, port);
        }

        private static string NormalizeAndPreparePath(string path) {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            return fullPath;
        }

        private static string RequireEnvironment(string name) {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException($"{name} is required for RO live network capture.");
            }

            return value.Trim();
        }

        private static string ReadString(string name, string fallback) {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int ReadInt(string name, int fallback) {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) {
                return parsed;
            }

            throw new InvalidOperationException($"{name} must be an integer, got '{value}'.");
        }

        private static ushort ReadUshort(string name, ushort fallback) {
            int parsed = ReadInt(name, fallback);
            if (parsed < ushort.MinValue || parsed > ushort.MaxValue) {
                throw new InvalidOperationException($"{name} must fit UInt16, got '{parsed}'.");
            }

            return (ushort) parsed;
        }

        private static byte? ReadOptionalByte(string name) {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) {
                return null;
            }

            int parsed = ReadInt(name, 0);
            if (parsed < byte.MinValue || parsed > byte.MaxValue) {
                throw new InvalidOperationException($"{name} must fit byte, got '{parsed}'.");
            }

            return (byte) parsed;
        }

        private static bool ReadBool(string name, bool fallback) {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            switch (value.Trim().ToLowerInvariant()) {
                case "1":
                case "true":
                case "yes":
                case "on":
                    return true;
                case "0":
                case "false":
                case "no":
                case "off":
                    return false;
                default:
                    throw new InvalidOperationException($"{name} must be boolean-like, got '{value}'.");
            }
        }

        private static string SanitizeFileName(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "capture";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var sanitized = value.Trim().ToCharArray();
            for (int i = 0; i < sanitized.Length; i++) {
                for (int invalidIndex = 0; invalidIndex < invalid.Length; invalidIndex++) {
                    if (sanitized[i] == invalid[invalidIndex]) {
                        sanitized[i] = '_';
                        break;
                    }
                }
            }

            return new string(sanitized);
        }
    }
}
