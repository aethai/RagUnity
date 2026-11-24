using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Ragnarok.Assets {
    public static class RoTextEncoding {
        public const int KoreanCodePage = 949;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Encoding LenientUtf8 = new UTF8Encoding(false, false);
        private static bool codePagesRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureCodePagesRegistered() {
            if (codePagesRegistered) {
                return;
            }

            LoadOptionalEncodingAssembly("I18N");
            LoadOptionalEncodingAssembly("I18N.CJK");
            LoadOptionalEncodingAssembly("I18N.West");
            LoadOptionalEncodingAssembly("System.Text.Encoding.CodePages");

            EncodingProvider provider = ResolveCodePagesProvider();
            if (provider != null) {
                Encoding.RegisterProvider(provider);
            }

            codePagesRegistered = true;
        }

        public static string Decode(byte[] data) {
            if (data == null) {
                return null;
            }

            if (data.Length == 0) {
                return string.Empty;
            }

            if (HasUtf8Bom(data)) {
                return LenientUtf8.GetString(data, 3, data.Length - 3);
            }

            if (HasUtf16LittleEndianBom(data)) {
                return Encoding.Unicode.GetString(data, 2, data.Length - 2);
            }

            if (HasUtf16BigEndianBom(data)) {
                return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
            }

            if (TryDecodeStrictUtf8(data, out string utf8)) {
                return utf8;
            }

            return GetKoreanEncoding().GetString(data);
        }

        public static bool TryDecodeStrictUtf8(byte[] data, out string text) {
            try {
                text = StrictUtf8.GetString(data);
                return true;
            } catch (DecoderFallbackException) {
                text = null;
                return false;
            }
        }

        public static Encoding GetKoreanEncoding() {
            EnsureCodePagesRegistered();
            return Encoding.GetEncoding(KoreanCodePage);
        }

        private static EncodingProvider ResolveCodePagesProvider() {
            const string providerTypeName = "System.Text.CodePagesEncodingProvider";
            const string assemblyName = "System.Text.Encoding.CodePages";

            Type providerType = Type.GetType(providerTypeName + ", " + assemblyName, throwOnError: false);
            if (providerType == null) {
                try {
                    providerType = Assembly.Load(assemblyName).GetType(providerTypeName, throwOnError: false);
                } catch {
                    providerType = null;
                }
            }

            PropertyInfo instanceProperty = providerType?.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            return instanceProperty?.GetValue(null) as EncodingProvider;
        }

        private static void LoadOptionalEncodingAssembly(string assemblyName) {
            try {
                Assembly.Load(assemblyName);
            } catch {
                // Unity profiles differ between Editor, Mono player, and IL2CPP player.
            }
        }

        public static bool ContainsLikelyMojibake(string text) {
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            return text.IndexOf('\uFFFD') >= 0 ||
                text.Contains("Ã") ||
                text.Contains("Â") ||
                text.Contains("â€") ||
                text.Contains("Ä") ||
                text.Contains("Å");
        }

        private static bool HasUtf8Bom(byte[] data) {
            return data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF;
        }

        private static bool HasUtf16LittleEndianBom(byte[] data) {
            return data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE;
        }

        private static bool HasUtf16BigEndianBom(byte[] data) {
            return data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF;
        }
    }
}
