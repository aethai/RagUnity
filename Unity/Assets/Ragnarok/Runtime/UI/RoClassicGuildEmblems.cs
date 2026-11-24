using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Ragnarok.UI.Classic;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// Guild emblems (guild-emblem-castle-spec A). CGuildMgr's version map (gid → ver, 0x5eb7f0) is filled by 0x016c, the
    /// guild info packets, ZC_CHANGE_GUILD 0x0b47 / 0x01b4 and the actors' spawn packets. An emblem resolves (0x5eada0) to
    /// the cache file "&lt;server&gt;_&lt;gid&gt;_&lt;ver + 0x7FFFFFFF&gt;.ebm" (zlib of the raw 24 × 24 BMP or GIF); a missing one is
    /// queued and one request goes out per frame, each pair at most once a minute (0x5e93c0): POST &lt;web API&gt;/emblem/download
    /// (AID, AuthToken, WorldName, GDID, Version), whose body is replayed as ZC_GUILD_EMBLEM_IMG 0x0b36 (clear, chunks of at
    /// most 1010 bytes, complete; 0x5aeff0). Without a web API (no token or address) this port asks the map server with
    /// CZ_REQ_GUILD_EMBLEM_IMG3 0x0b46 {gid, ver}, which answers with the same 0x0b36 sequence. The master's upload (A.7):
    /// POST /emblem/upload (AID, AuthToken, WorldName, GDID, ImgType, Img); Type 1 → 0x0b46 {gid, version}, else MSI 0xe02.
    /// The cache is written to this client's own _tmpEmblem and read from it and from the Custom client's; a file that
    /// does not decode shows a white 24 × 24 square (CEmblemRes::Load 0x6c6ee0).
    /// </summary>
    public sealed class RoClassicGuildEmblems : IRoClassicUiProducer {
        private const int RetryMs = 60000;
        private static readonly Dictionary<uint, int> Versions = new Dictionary<uint, int>();
        private static RoClassicGuildEmblems instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            Versions.Clear();
            Resolved.Clear();
            instance = null;
            ShowInBalloons = false;
        }

        /// <summary>DAT_01552e84, toggled by /emblem (no initializer: off at start).</summary>
        public static bool ShowInBalloons { get; private set; }

        /// <summary>Bumped when an emblem lands, so the drawers repaint.</summary>
        public static int Serial { get; private set; }

        private RoClassicUiSystem system;
        private readonly List<(uint Gid, int Ver)> queue = new List<(uint, int)>();
        private readonly List<(uint Gid, int Ver, float At)> requested = new List<(uint, int, float)>();
        private readonly Dictionary<string, string> loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private uint rxGid;
        private int rxVer;
        private readonly MemoryStream rx = new MemoryStream();

        private static RoClassicGuildEmblems Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) {
                instance = new RoClassicGuildEmblems { system = system };
                system.Register(instance);
            }

            return instance;
        }

        /// <summary>0x5eb7f0: the version of a guild's emblem (only versions from 1 count for drawing).</summary>
        public static void SetVersion(uint gid, int ver) {
            if (gid == 0) return;
            if (Versions.TryGetValue(gid, out int old) && old == ver) return;
            Versions[gid] = ver;
            Serial++;
        }

        public static bool IsRaw(ushort id) => id == 0x0b36 || id == 0x0b47 || id == 0x01b4;

        /// <summary>The image key of a guild's emblem for DrawBitmap, or null (no version yet, or not cached: then queued).</summary>
        public static string Resolve(uint gid) {
            if (gid == 0 || !Versions.TryGetValue(gid, out int ver) || ver < 1) return null;
            // asked every frame for every player of a guild on a siege map: the answer per guild and version is kept, and a
            // missing file is looked for again once a second (the download that brings it is asynchronous anyway); the
            // file name strings and two File.Exists calls per player per frame were the cost
            float now = Time.unscaledTime;
            RoClassicGuildEmblems e = Ensure();
            if (Resolved.TryGetValue(gid, out ResolvedEmblem known) && known.Version == ver && known.Serial == Serial &&
                ReferenceEquals(known.Owner, e) && (known.Key != null || now < known.RetryAt)) {
                return known.Key;
            }

            string key = e?.Load(gid, ver);
            Resolved[gid] = new ResolvedEmblem { Version = ver, Serial = Serial, Owner = e, Key = key, RetryAt = now + 1f };
            return key;
        }

        private struct ResolvedEmblem {
            public int Version;
            public int Serial;
            public RoClassicGuildEmblems Owner;
            public string Key;
            public float RetryAt;
        }

        private static readonly Dictionary<uint, ResolvedEmblem> Resolved = new Dictionary<uint, ResolvedEmblem>();

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A guild's emblem as a texture (a Granny guild flag's emblem mesh, bea450), rows top-down like the Granny textures
        /// whose UVs address them unchanged; null while there is no version or the file has not arrived (then queued).
        /// </summary>
        public static Texture2D ResolveTexture(uint gid) {
            if (Resolve(gid) == null || !Versions.TryGetValue(gid, out int ver)) return null;
            string name = FileName(gid, ver);
            if (Textures.TryGetValue(name, out Texture2D cached) && cached != null) return cached;
            string path = Path.Combine(OwnCacheDir(), name);
            if (!File.Exists(path)) {
                string client = ClientCacheDir();
                path = client != null ? Path.Combine(client, name) : null;
            }

            if (path == null || !File.Exists(path) || !TryDecode(ReadAll(path), out int w, out int h, out Color32[] pixels)) return null;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) {
                name = "Guild emblem " + gid, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Textures[name] = texture;
            return texture;
        }

        private static string ServerName() => RoClassicEntryQueue.CurrentSession?.SelectedServerName ?? string.Empty;

        /// <summary>0x5eb650: "&lt;server&gt;_&lt;gid&gt;_&lt;(u32)(ver + 0x7FFFFFFF)&gt;.ebm".</summary>
        private static string FileName(uint gid, int ver) => ServerName() + "_" + gid + "_" + unchecked((uint) (ver + 0x7FFFFFFF)) + ".ebm";

        private static string OwnCacheDir() => Path.Combine(Application.persistentDataPath, "_tmpEmblem");

        private static string ClientCacheDir() {
            string root = RoClassicUiHost.GameAssets()?.ClientRootPath;
            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "_tmpEmblem");
        }

        private string Load(uint gid, int ver) {
            string name = FileName(gid, ver);
            if (loaded.TryGetValue(name, out string key)) return key;
            string path = Path.Combine(OwnCacheDir(), name);
            if (!File.Exists(path)) {
                string client = ClientCacheDir();
                path = client != null ? Path.Combine(client, name) : null;
            }

            if (path == null || !File.Exists(path)) {
                Enqueue(gid, ver);
                return null;
            }

            key = "emblem\\" + name;
            if (!TryDecode(ReadAll(path), out int w, out int h, out Color32[] pixels)) {
                (w, h, pixels) = (24, 24, White24());
            }

            if (!system.Images.SetDynamic(key, w, h, pixels)) return null;
            loaded[name] = key;
            return key;
        }

        private static byte[] ReadAll(string path) {
            try {
                return File.ReadAllBytes(path);
            } catch (IOException) {
                return null;
            }
        }

        private static Color32[] White24() {
            var pixels = new Color32[24 * 24];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            return pixels;
        }

        /// <summary>0x5e92c0: queued once while waiting (deduplicated against the queue).</summary>
        private void Enqueue(uint gid, int ver) {
            if (!queue.Contains((gid, ver))) queue.Add((gid, ver));
        }

        /// <summary>0x5e93c0 per frame: the minute-old requests forgotten, one pair popped and asked for unless asked within the minute.</summary>
        public void ClassicUiTick(RoClassicUiSystem ui) {
            float now = Time.unscaledTime;
            requested.RemoveAll(r => (now - r.At) * 1000f > RetryMs);
            if (queue.Count == 0) return;
            (uint gid, int ver) = queue[0];
            queue.RemoveAt(0);
            if (requested.Exists(r => r.Gid == gid && r.Ver == ver) || gid == 0) return;
            requested.Add((gid, ver, now));
            RoLoginShellSession session = RoClassicEntryQueue.CurrentSession;
            if (RoClassicWebApi.Available(session)) {
                // CEmblemDataMgr::Download 0x5aeb90
                var fields = RoClassicWebApi.Common(session, false);
                fields.Add(("GDID", gid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                fields.Add(("Version", ver.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                RoClassicWebApi.Post(session, "/emblem/download", fields, null, (ok, image) => {
                    if (ok && image.Length > 0) Replay(gid, ver, image);
                });
                return;
            }

            AskMapServer(gid, ver);
        }

        private static void AskMapServer(uint gid, int ver) {
            var body = new byte[8];
            BitConverter.GetBytes(gid).CopyTo(body, 0);
            BitConverter.GetBytes(ver).CopyTo(body, 4);
            _ = RoClassicEntryQueue.CurrentSession?.BeginClassicPacket(0x0b46, body, "an emblem request");
        }

        /// <summary>0x5aeff0: the downloaded image as 0x0b36 type 0, the body in chunks of at most 0x3f2 bytes as type 1, then type 2.</summary>
        private void Replay(uint gid, int ver, byte[] image) {
            Receive(Packet0b36(0, gid, ver, image, 0, 0));
            for (int at = 0; at < image.Length; at += 0x3f2) Receive(Packet0b36(1, gid, ver, image, at, Math.Min(0x3f2, image.Length - at)));
            Receive(Packet0b36(2, gid, ver, image, 0, 0));
        }

        private static byte[] Packet0b36(int type, uint gid, int ver, byte[] data, int at, int n) {
            var p = new byte[14 + n];
            BitConverter.GetBytes((ushort) 0x0b36).CopyTo(p, 0);
            BitConverter.GetBytes((ushort) p.Length).CopyTo(p, 2);
            BitConverter.GetBytes((ushort) type).CopyTo(p, 4);
            BitConverter.GetBytes(gid).CopyTo(p, 6);
            BitConverter.GetBytes(ver).CopyTo(p, 10);
            Array.Copy(data, at, p, 14, n);
            return p;
        }

        private static bool uploading;   // CEmblemDataMgr + 0x38

        /// <summary>
        /// CEmblemDataMgr::Upload 0x5aed40: false while another upload runs (or without a web API); the answer's Type 1
        /// sends 0x0b46 {gid, version}, anything else shows MSI 0xe02 "Please try again in a few minutes.".
        /// </summary>
        public static bool Upload(uint gid, string fileName, byte[] data) {
            RoLoginShellSession session = RoClassicEntryQueue.CurrentSession;
            if (uploading || session == null) return false;
            string ext = System.IO.Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
            var fields = RoClassicWebApi.Common(session, false);
            fields.Add(("GDID", gid.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            fields.Add(("ImgType", ext));
            bool sent = RoClassicWebApi.Post(session, "/emblem/upload", fields, ("Img", fileName, ext == "GIF" ? "image/gif" : "application/octet-stream", data), (ok, body) => {
                uploading = false;
                Newtonsoft.Json.Linq.JObject json = RoClassicWebApi.Json(body);
                if (ok && json != null && json.Value<int?>("Type") == 1) {
                    AskMapServer(gid, json.Value<int?>("version") ?? 0);
                    return;
                }

                RoClassicMessageBox.Show(RoClassicUiHost.MsgStr(0xe02, "Please try again in a few minutes."), RoClassicMessageBox.TypeOk, true, null, true, "Message");
            });
            if (sent) uploading = true;
            return sent;
        }

        /// <summary>A raw (not zlib) BMP or GIF as the emblem loader decodes it (the Edit button's 24 × 24 check).</summary>
        internal static bool TryDecodeImage(byte[] raw, out int width, out int height, out Color32[] pixels) {
            width = height = 0;
            pixels = null;
            if (raw == null || raw.Length < 6) return false;
            if (raw[0] == 'B' && raw[1] == 'M') return RoClassicUiImages.TryDecode(raw, out width, out height, out pixels);
            return raw[0] == 'G' && raw[1] == 'I' && raw[2] == 'F' && TryDecodeGif(raw, out width, out height, out pixels);
        }

        /// <summary>0x0b36 (0xc79430), 0x0b47 (0xc87e10) and 0x01b4 from the raw queue.</summary>
        public static void Handle(byte[] b) {
            if (b == null || b.Length < 2) return;
            switch (BitConverter.ToUInt16(b, 0)) {
                case 0x0b47:
                    // {i32 gid, u32 ver, u32 AID}: the version only for a guild (gid ≠ 0)
                    if (b.Length >= 14 && BitConverter.ToUInt32(b, 2) != 0) SetVersion(BitConverter.ToUInt32(b, 2), BitConverter.ToInt32(b, 6));
                    break;
                case 0x01b4:
                    // {u32 AID, i32 gid, u16 ver}
                    if (b.Length >= 12) SetVersion(BitConverter.ToUInt32(b, 6), BitConverter.ToInt16(b, 10));
                    break;
                case 0x0b36:
                    Ensure()?.Receive(b);
                    break;
            }
        }

        /// <summary>{u16 len, u16 type, i32 gid, u32 ver, data[len − 14]}: 0 clears, 1 appends when gid / ver match, 2 saves.</summary>
        private void Receive(byte[] b) {
            if (b.Length < 14) return;
            int type = BitConverter.ToUInt16(b, 4);
            uint gid = BitConverter.ToUInt32(b, 6);
            int ver = BitConverter.ToInt32(b, 10);
            int n = Math.Min(BitConverter.ToUInt16(b, 2), b.Length) - 14;
            if (type == 0) {
                rxGid = gid;
                rxVer = ver;
                rx.SetLength(0);
                return;
            }

            if (gid != rxGid || ver != rxVer) return;
            if (type == 1) {
                if (n > 0) rx.Write(b, 14, n);
                return;
            }

            if (type == 2) Save(gid, ver, rx.ToArray());
        }

        /// <summary>0x5eb1e0: zlib of the raw image, the guild's older files deleted, then the new one; 0x5eb7a0 repaints.</summary>
        private void Save(uint gid, int ver, byte[] image) {
            try {
                string dir = OwnCacheDir();
                Directory.CreateDirectory(dir);
                string prefix = ServerName() + "_" + gid + "_";
                foreach (string old in Directory.GetFiles(dir, prefix + "*.ebm")) File.Delete(old);
                string name = FileName(gid, ver);
                File.WriteAllBytes(Path.Combine(dir, name), Zlib(image));
                loaded.Remove(name);
            } catch (IOException e) {
                Debug.LogWarning("[emblem] cache write failed: " + e.Message);
            } catch (UnauthorizedAccessException e) {
                Debug.LogWarning("[emblem] cache write failed: " + e.Message);
            }

            Serial++;
        }

        // ---------------------------------------------------------------- zlib, BMP / GIF

        private static byte[] Zlib(byte[] data) {
            using var output = new MemoryStream();
            output.WriteByte(0x78);
            output.WriteByte(0x9c);
            using (var deflate = new DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) deflate.Write(data, 0, data.Length);
            uint a = 1, s = 0;
            foreach (byte x in data) {
                a = (a + x) % 65521;
                s = (s + a) % 65521;
            }

            uint adler = (s << 16) | a;
            output.WriteByte((byte) (adler >> 24));
            output.WriteByte((byte) (adler >> 16));
            output.WriteByte((byte) (adler >> 8));
            output.WriteByte((byte) adler);
            return output.ToArray();
        }

        private static byte[] Unzlib(byte[] data) {
            if (data == null || data.Length < 6) return null;
            try {
                using var input = new MemoryStream(data, 2, data.Length - 2);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            } catch (InvalidDataException) {
                return null;
            }
        }

        /// <summary>The cached file: zlib, then a BMP (the classic key applied) or a GIF's first frame.</summary>
        internal static bool TryDecode(byte[] file, out int width, out int height, out Color32[] pixels) {
            width = height = 0;
            pixels = null;
            byte[] raw = Unzlib(file);
            if (raw == null) return false;
            if (raw.Length > 2 && raw[0] == 'B' && raw[1] == 'M') return RoClassicUiImages.TryDecode(raw, out width, out height, out pixels);
            return raw.Length > 6 && raw[0] == 'G' && raw[1] == 'I' && raw[2] == 'F' && TryDecodeGif(raw, out width, out height, out pixels);
        }

        /// <summary>A GIF87a / 89a's first image on the logical screen (transparent index → alpha 0), top-down.</summary>
        private static bool TryDecodeGif(byte[] g, out int width, out int height, out Color32[] pixels) {
            width = height = 0;
            pixels = null;
            try {
                int w = g[6] | (g[7] << 8), h = g[8] | (g[9] << 8), flags = g[10], p = 13;
                if (w <= 0 || h <= 0 || w > 256 || h > 256) return false;
                Color32[] global = null;
                if ((flags & 0x80) != 0) {
                    global = ReadPalette(g, p, 2 << (flags & 7));
                    p += 3 * (2 << (flags & 7));
                }

                int transparent = -1;
                while (p < g.Length) {
                    byte block = g[p++];
                    if (block == 0x21) {
                        byte label = g[p++];
                        if (label == 0xf9 && g[p] >= 4 && (g[p + 1] & 1) != 0) transparent = g[p + 4];
                        while (g[p] != 0) p += g[p] + 1;
                        p++;
                        continue;
                    }

                    if (block != 0x2c) return false;
                    int ix = g[p] | (g[p + 1] << 8), iy = g[p + 2] | (g[p + 3] << 8), iw = g[p + 4] | (g[p + 5] << 8), ih = g[p + 6] | (g[p + 7] << 8);
                    int iflags = g[p + 8];
                    p += 9;
                    Color32[] palette = global;
                    if ((iflags & 0x80) != 0) {
                        palette = ReadPalette(g, p, 2 << (iflags & 7));
                        p += 3 * (2 << (iflags & 7));
                    }

                    if (palette == null) return false;
                    int minCode = g[p++];
                    var data = new MemoryStream();
                    while (g[p] != 0) {
                        data.Write(g, p + 1, g[p]);
                        p += g[p] + 1;
                    }

                    byte[] indices = Lzw(data.ToArray(), minCode, iw * ih);
                    if ((iflags & 0x40) != 0) indices = Deinterlace(indices, iw, ih);
                    var result = new Color32[w * h];
                    for (int y = 0; y < ih; y++) {
                        for (int x = 0; x < iw; x++) {
                            int tx = ix + x, ty = iy + y;
                            if (tx >= w || ty >= h) continue;
                            int idx = indices[y * iw + x];
                            if (idx == transparent || idx >= palette.Length) continue;
                            Color32 c = palette[idx];
                            // the classic key: magenta (RGB555) is a hole
                            result[ty * w + tx] = RoClassicUiAtlas.IsClassicColorKey(c) ? default : c;
                        }
                    }

                    width = w;
                    height = h;
                    pixels = result;
                    return true;
                }
            } catch (IndexOutOfRangeException) {
            }

            return false;
        }

        private static Color32[] ReadPalette(byte[] g, int p, int count) {
            var palette = new Color32[count];
            for (int i = 0; i < count; i++) palette[i] = new Color32(g[p + 3 * i], g[p + 3 * i + 1], g[p + 3 * i + 2], 255);
            return palette;
        }

        private static byte[] Lzw(byte[] data, int minCode, int count) {
            var output = new byte[count];
            int clear = 1 << minCode, end = clear + 1, size = minCode + 1, next = end + 1, prev = -1, o = 0;
            var prefix = new int[4096];
            var suffix = new byte[4096];
            var stack = new byte[4097];
            for (int i = 0; i < clear; i++) suffix[i] = (byte) i;
            int bits = 0, buffer = 0, d = 0;
            byte first = 0;
            while (o < count) {
                while (bits < size) {
                    if (d >= data.Length) return output;
                    buffer |= data[d++] << bits;
                    bits += 8;
                }

                int code = buffer & ((1 << size) - 1);
                buffer >>= size;
                bits -= size;
                if (code == clear) {
                    size = minCode + 1;
                    next = end + 1;
                    prev = -1;
                    continue;
                }

                if (code == end) break;
                int sp = 0, c = code;
                if (prev >= 0 && code >= next) {
                    stack[sp++] = first;
                    c = prev;
                }

                while (c >= clear) {
                    stack[sp++] = suffix[c];
                    c = prefix[c];
                }

                stack[sp++] = (byte) c;
                first = (byte) c;
                while (sp > 0 && o < count) output[o++] = stack[--sp];
                if (prev >= 0 && next < 4096) {
                    prefix[next] = prev;
                    suffix[next] = first;
                    next++;
                    if (next == 1 << size && size < 12) size++;
                }

                prev = code;
            }

            return output;
        }

        private static byte[] Deinterlace(byte[] src, int w, int h) {
            var dst = new byte[src.Length];
            int row = 0;
            foreach ((int start, int step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) }) {
                for (int y = start; y < h; y += step) Array.Copy(src, row++ * w, dst, y * w, w);
            }

            return dst;
        }

        // ---------------------------------------------------------------- /emblem

        /// <summary>"/emblem" (case 0xc0a566): the balloons' emblems on / off, "Guild Emblem On" / "Guild Emblem Off" in yellow.</summary>
        public static bool TryCommand(string line, RoClassicChat chat) {
            if (line == null || !line.Trim().Equals("/emblem", StringComparison.OrdinalIgnoreCase)) return false;
            ShowInBalloons = !ShowInBalloons;
            Serial++;
            chat?.AddLine(ShowInBalloons ? "Guild Emblem On" : "Guild Emblem Off", "#FFFF00");
            return true;
        }

        /// <summary>!emblem state | ver gid v | file gid | balloon for the sweep.</summary>
        internal static string Sweep(string args) {
            string[] t = (args ?? string.Empty).Trim().Split(' ');
            if (t.Length >= 3 && t[0] == "ver") SetVersion(uint.Parse(t[1]), int.Parse(t[2]));
            if (t[0] == "balloon") {
                ShowInBalloons = !ShowInBalloons;
                Serial++;
            }

            RoClassicGuildEmblems e = Ensure();
            string resolved = t.Length >= 2 && t[0] == "file" ? " resolve=" + (Resolve(uint.Parse(t[1])) ?? "null") : string.Empty;
            return RoClassicSiegeEmblems.Sweep() + " versions=" + Versions.Count + " balloons=" + ShowInBalloons + " queue=" + (e?.queue.Count ?? 0) + " requested=" + (e?.requested.Count ?? 0) +
                " loaded=" + (e?.loaded.Count ?? 0) + " own=" + OwnCacheDir() + " client=" + ClientCacheDir() + " server='" + ServerName() + "'" + resolved;
        }
    }
}
