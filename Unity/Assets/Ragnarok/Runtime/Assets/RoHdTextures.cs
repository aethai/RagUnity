using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Ragnarok.Assets {
    /// <summary>
    /// The HD texture pack: upscaled replacements of GRF textures, kept outside the GRF as PNGs. A manifest
    /// (manifest.tsv: kind, asset path, file, has transparent pixels, has partial alpha) maps the client's normalized
    /// asset path to its PNG. Ground textures are the plain GRF image upscaled; model textures are the client's prepared
    /// RGBA (keyed, dilated) upscaled, so they load as they are. Root: RO_HD_TEXTURES, else &lt;project&gt;/../HdTextures.
    /// !hdexport writes the current map's textures and a manifest for the upscaler; HdPack scale 2 = 512 px ground cells.
    /// </summary>
    public static class RoHdTextures {
        public const string ManifestName = "manifest.tsv";

        public readonly struct Entry {
            public Entry(string kind, string file, bool transparent, bool partial) {
                Kind = kind;
                File = file;
                HasTransparentPixels = transparent;
                HasPartialAlphaPixels = partial;
            }

            public string Kind { get; }
            public string File { get; }
            public bool HasTransparentPixels { get; }
            public bool HasPartialAlphaPixels { get; }
        }

        static Dictionary<string, Entry> entries;
        static string root;

        /// <summary>
        /// Off, and only the dev console (!hd on) turns it on: textures outside the GRF may not ship, the pack has to go into
        /// the realm GRF first.
        /// </summary>
        public static bool Enabled;

        /// <summary>
        /// Set when the pack changes (!hd on / off / reload): the ground atlases and prepared model textures cached across
        /// maps are dropped on the next map load, once the old map is gone (dropping them at once destroyed the textures
        /// the map on screen still drew with: a black ground and magenta models).
        /// </summary>
        public static bool CachesStale;

        public static string Root {
            get {
                if (root == null) {
                    string env = Environment.GetEnvironmentVariable("RO_HD_TEXTURES");
                    root = !string.IsNullOrWhiteSpace(env)
                        ? Path.GetFullPath(env)
                        : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "HdTextures"));
                }

                return root;
            }
        }

        public static void Reload() {
            entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            string manifest = Path.Combine(Root, ManifestName);
            if (!File.Exists(manifest)) return;
            foreach (string line in File.ReadAllLines(manifest)) {
                string[] p = line.Split('\t');
                if (p.Length < 3 || p[0].StartsWith("#", StringComparison.Ordinal)) continue;
                string file = Path.Combine(Root, "hd", p[2]);
                if (!File.Exists(file)) continue;
                entries[Key(p[1])] = new Entry(p[0], file, p.Length > 3 && p[3] == "1", p.Length > 4 && p[4] == "1");
            }
        }

        public static int Count => Entries.Count;

        static Dictionary<string, Entry> Entries {
            get {
                if (entries == null) Reload();
                return entries;
            }
        }

        public static string Key(string assetPath) => RoAssetPath.Normalize(assetPath ?? string.Empty);

        public static bool Has(string assetPath) => Enabled && Entries.ContainsKey(Key(assetPath));

        public static bool TryLoad(string assetPath, out Texture2D texture, out Entry entry) {
            texture = null;
            entry = default;
            if (!Enabled || !Entries.TryGetValue(Key(assetPath), out entry)) return false;
            try {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false) { name = Path.GetFileName(assetPath) + " (HD)" };
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(entry.File), false)) {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    return false;
                }

                // a failed upscale writes an all-zero PNG (black, fully transparent): the classic texture beats a black map
                if (IsEmpty(texture)) {
                    Debug.LogWarning("RO HD texture '" + entry.File + "' is empty (all black, transparent): the classic texture is used");
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    return false;
                }

                texture.anisoLevel = 8;
                texture.filterMode = FilterMode.Trilinear;
                texture.Apply(true, false);
                return true;
            } catch (Exception ex) {
                Debug.LogWarning("RO HD texture '" + entry.File + "' could not be read: " + ex.Message);
                texture = null;
                return false;
            }
        }

        static bool IsEmpty(Texture2D texture) {
            // a 16 x 16 sample grid: any colour or alpha means the file holds an image
            for (int y = 0; y < 16; y++) {
                for (int x = 0; x < 16; x++) {
                    Color c = texture.GetPixel((x * 2 + 1) * texture.width / 32, (y * 2 + 1) * texture.height / 32);
                    if (c.a > 0f || c.r > 0f || c.g > 0f || c.b > 0f) return false;
                }
            }

            return true;
        }

        /// <summary>The export file name of an asset path (stable, file-system safe).</summary>
        public static string ExportName(string assetPath) {
            string key = Key(assetPath);
            ulong hash = 14695981039346656037UL;
            foreach (char c in key) {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            string name = Path.GetFileNameWithoutExtension(key);
            var safe = new StringBuilder();
            foreach (char c in name) safe.Append(c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_');
            return safe.ToString().TrimEnd('_') + "_" + hash.ToString("x16", CultureInfo.InvariantCulture) + ".png";
        }

        /// <summary>Writes a texture (readable) to &lt;root&gt;/src for the upscaler and returns its manifest line.</summary>
        public static string Export(string kind, string assetPath, Texture2D texture) {
            if (texture == null || !texture.isReadable) return null;
            string dir = Path.Combine(Root, "src");
            Directory.CreateDirectory(dir);
            string file = ExportName(assetPath);
            Color32[] pixels = texture.GetPixels32();
            bool transparent = false, partial = false;
            foreach (Color32 p in pixels) {
                if (p.a == 0) transparent = true;
                else if (p.a < 255) partial = true;
            }

            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            copy.SetPixels32(pixels);
            copy.Apply(false);
            File.WriteAllBytes(Path.Combine(dir, file), copy.EncodeToPNG());
            UnityEngine.Object.Destroy(copy);
            return kind + "\t" + Key(assetPath) + "\t" + file + "\t" + (transparent ? "1" : "0") + "\t" + (partial ? "1" : "0");
        }
    }
}
