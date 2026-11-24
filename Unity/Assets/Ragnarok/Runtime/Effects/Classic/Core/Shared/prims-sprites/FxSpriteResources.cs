// Synced from reference/port/src/RoClassicFx/Shared/prims-sprites/FxSpriteResources.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Effects.Classic
{
    /// <summary>One decoded SPR image (C#-side copy of the pixels for the host renderer).</summary>
    public sealed class FxSprImage
    {
        public int Width, Height;
        /// <summary>Palette images: one palette index per pixel, row-major, top row first (0 = transparent key).</summary>
        public byte[] Indices;
        /// <summary>RGBA (32-bit) images: 0xAARRGGBB per pixel, row-major, top row first.</summary>
        public uint[] Argb;
    }

    /// <summary>
    /// C#-side model of a loaded CSprRes (vf05 0x6d9880): what the engine reads lives in FxMemory
    /// (see <see cref="FxSpriteResources"/>); this keeps the decoded pixels and the file palette.
    /// </summary>
    public sealed class FxSprite
    {
        public string Name;
        public uint Address;
        public int Version;
        /// <summary>File palette, 256 × 0xAARRGGBB (alpha forced to 0xff; index 0 is the transparent key).</summary>
        public uint[] Palette = new uint[256];
        public readonly List<FxSprImage> PalImages = new List<FxSprImage>();
        public readonly List<FxSprImage> RgbaImages = new List<FxSprImage>();

        /// <summary>Image by clip type (0 = palette, 1 = RGBA) and index; null when out of range.</summary>
        public FxSprImage Get(int type, int index)
        {
            var list = type == 0 ? PalImages : type == 1 ? RgbaImages : null;
            return list != null && index >= 0 && index < list.Count ? list[index] : null;
        }
    }

    /// <summary>C#-side model of a loaded CActRes (vf05 0x6bdf90); the engine reads the FxMemory copy.</summary>
    public sealed class FxAct
    {
        public string Name;
        public uint Address;
        public int Version;
    }

    /// <summary>
    /// Builders of the CSprRes / CActRes heap images. Layouts (from the client's constructors
    /// 0x6d97d0 / 0x6bde90 and loaders 0x6d9880 / 0x6bdf90):
    /// <code>
    /// CRes     +0 vtable, +4 lock count, +8 timestamp, +0xc ext index, +0x10 djb2 hash, +0x14 name
    /// CSprRes  (0x52c) +0x110 palette[256] (ARGB1555 as dwords, 0x54d5d0), +0x510 vector&lt;SprImg*&gt; pal,
    ///          +0x51c vector&lt;SprImg*&gt; rgba, +0x528 pal image count
    /// SprImg   (0x10) +0 short w, +2 short h, +4 short isHalfW, +6 short isHalfH, +8 texture, +0xc pixels
    /// CActRes  (0x138) +0x110 vector&lt;CAction&gt; (CAction = vector&lt;CMotion&gt;, 0xc), +0x11c max clip count,
    ///          +0x120 vector&lt;string&gt; events, +0x12c vector&lt;float&gt; delays
    /// CMotion  (0x44) +0 RECT range1, +0x10 RECT range2, +0x20 vector&lt;CSprClip&gt;, +0x2c clip count,
    ///          +0x30 event id, +0x34 vector&lt;attach point {x,y,attr}&gt;, +0x40 attach count
    /// CSprClip (0x24) +0 x, +4 y (top-left offsets after the loader's centre fix-up), +8 spr index (-1 none),
    ///          +0xc flags (bit0 mirror), +0x10 r,g,b,a bytes, +0x14 zoom x, +0x18 zoom y, +0x1c angle, +0x20 spr type
    /// </code>
    /// std::vector = {begin, end, capacity}; here capacity == end. Things the engine never reads are
    /// not reproduced: textures (SprImg+8/+0xc are 0; pixels stay in <see cref="FxSprite"/>) and
    /// the event-name strings (empty vector).
    /// </summary>
    public static class FxSpriteResources
    {
        public const uint VtCSprRes = 0x00f837c8, VtCActRes = 0x00f82e4c;
        public const uint SprResSize = 0x52c, ActResSize = 0x138;
        public const uint SprImgSize = 0x10, MotionSize = 0x44, ActionSize = 0xc, ClipSize = 0x24, AttachSize = 0xc;

        /// <summary>CRes name buffer: +0x14 up to +0x110 (0xfc bytes incl. terminator).</summary>
        const int NameCapacity = 0xfb;

        /// <summary>Writes the CRes base: vtable, name (byte string, ASCII letters lower-cased) and its djb2 hash (FUN_00a19860).</summary>
        public static void InitRes(FxMemory m, uint res, uint vtable, string name)
        {
            m.U32(res, vtable);
            byte[] b = NameBytes(name);
            for (int i = 0; i < b.Length; i++) m.U8(res + 0x14 + (uint)i, b[i]);
            m.U32(res + 0x10, Djb2(b));
        }

        public static byte[] NameBytes(string name)
        {
            byte[] b = new byte[Math.Min(name.Length, NameCapacity)];
            for (int i = 0; i < b.Length; i++) { char ch = name[i]; b[i] = (byte)(ch >= (char)65 && ch <= (char)90 ? ch + 32 : ch); }
            return b;
        }

        /// <summary>FUN_00a19860 hash: h = 5381; h = h*33 + (signed char)c.</summary>
        public static uint Djb2(byte[] b)
        {
            uint h = 0x1505;
            foreach (byte c in b) h = h * 33 + (uint)(sbyte)c;
            return h;
        }

        /// <summary>True when <paramref name="res"/> still holds a resource of this kind and name.</summary>
        public static bool IsRes(FxMemory m, uint res, uint vtable, string name)
        {
            if (res == 0 || !m.Contains(res, 0x110) || res >= m.BumpPointer || m.U32(res) != vtable) return false;
            // NameBytes / Djb2 without the byte array (this runs on every cached resource lookup)
            int n = Math.Min(name.Length, NameCapacity);
            uint h = 0x1505;
            for (int i = 0; i < n; i++) h = h * 33 + (uint)(sbyte)NameByte(name[i]);
            if (m.U32(res + 0x10) != h) return false;
            for (int i = 0; i < n; i++) if (m.U8(res + 0x14 + (uint)i) != NameByte(name[i])) return false;
            return m.U8(res + 0x14 + (uint)n) == 0;
        }

        /// <summary>One byte of <see cref="NameBytes"/>: ASCII upper case folded to lower case, truncated to a byte.</summary>
        static byte NameByte(char ch) => (byte)(ch >= (char)65 && ch <= (char)90 ? ch + 32 : ch);

        static uint Vector(FxMemory m, uint vec, uint count, uint elementSize)
        {
            if (count == 0) { m.U32(vec, 0); m.U32(vec + 4, 0); m.U32(vec + 8, 0); return 0; }
            uint a = m.Alloc(count * elementSize);
            m.U32(vec, a);
            m.U32(vec + 4, a + count * elementSize);
            m.U32(vec + 8, a + count * elementSize);
            return a;
        }

        // ---- SPR ------------------------------------------------------------------------

        /// <summary>
        /// CSprRes::Load (0x6d9880) into FxMemory. Returns 0 (load failure) for a bad magic or a
        /// version above 0x201, like the client.
        /// </summary>
        public static uint LoadSpr(FxMemory m, string name, byte[] file, out FxSprite model)
        {
            model = null;
            if (file == null || file.Length < 6 || file[0] != 'S' || file[1] != 'P') return 0;
            int ver = BitConverter.ToUInt16(file, 2);
            if (ver > 0x201) return 0;
            int palCount = BitConverter.ToUInt16(file, 4);
            int p = 6;
            int rgbaCount = 0;
            if (ver >= 0x200) { rgbaCount = BitConverter.ToUInt16(file, p); p += 2; }

            var spr = new FxSprite { Name = name, Version = ver };
            uint res = m.Alloc(SprResSize);
            InitRes(m, res, VtCSprRes, name);
            m.I32(res + 0x528, palCount);

            // palette: last 0x400 bytes of the file (ver >= 0x101), converted by FUN_0054d5d0
            if (ver >= 0x101 && file.Length >= 0x400)
            {
                int q = file.Length - 0x400;
                for (int i = 0; i < 256; i++)
                {
                    byte r = file[q + i * 4], g = file[q + i * 4 + 1], b = file[q + i * 4 + 2];
                    spr.Palette[i] = 0xff000000u | (uint)r << 16 | (uint)g << 8 | b;
                    m.U32(res + 0x110 + (uint)i * 4, Pal1555(r, g, b));
                }
            }

            for (int i = 0; i < palCount; i++)
            {
                int w = BitConverter.ToUInt16(file, p), h = BitConverter.ToUInt16(file, p + 2);
                p += 4;
                var img = new FxSprImage { Width = w, Height = h, Indices = new byte[w * h] };
                if (ver >= 0x201)
                {
                    int size = BitConverter.ToUInt16(file, p); p += 2;
                    DecodeRle(file, p, size, img.Indices);
                    p += size;
                }
                else
                {
                    Buffer.BlockCopy(file, p, img.Indices, 0, w * h);
                    p += w * h;
                }
                spr.PalImages.Add(img);
            }
            for (int i = 0; i < rgbaCount; i++)
            {
                int w = BitConverter.ToUInt16(file, p), h = BitConverter.ToUInt16(file, p + 2);
                p += 4;
                var img = new FxSprImage { Width = w, Height = h, Argb = new uint[w * h] };
                for (int y = h - 1; y >= 0; y--)            // stored bottom-up, bytes A,B,G,R
                    for (int x = 0; x < w; x++, p += 4)
                        img.Argb[y * w + x] = (uint)file[p] << 24 | (uint)file[p + 3] << 16 | (uint)file[p + 2] << 8 | file[p + 1];
                spr.RgbaImages.Add(img);
            }

            WriteImages(m, res + 0x510, spr.PalImages);
            WriteImages(m, res + 0x51c, spr.RgbaImages);
            spr.Address = res;
            model = spr;
            return res;
        }

        static void WriteImages(FxMemory m, uint vec, List<FxSprImage> images)
        {
            uint arr = Vector(m, vec, (uint)images.Count, 4);
            for (int i = 0; i < images.Count; i++)
            {
                uint img = m.Alloc(SprImgSize);
                m.I16(img, (short)images[i].Width);
                m.I16(img + 2, (short)images[i].Height);
                m.U32(arr + (uint)i * 4, img);
            }
        }

        /// <summary>FUN_0054d5d0: file RGB → ARGB1555 dword; near-magenta (r,b ≥ 0xfe, g &lt; 4) → 0x421 (key).</summary>
        public static uint Pal1555(byte r, byte g, byte b)
        {
            if (r >= 0xfe && g < 4 && b >= 0xfe) return 0x421;
            return ((((uint)r & 0xf8) << 5 | ((uint)g & 0xf8)) << 2) | (((uint)b | 0x40000u) >> 3);
        }

        /// <summary>SPR 2.1 RLE: a 0 byte is followed by a run length of zeros (a run of 0 means one zero).</summary>
        static void DecodeRle(byte[] src, int p, int size, byte[] dst)
        {
            int end = p + size, o = 0;
            while (p < end && o < dst.Length)
            {
                byte c = src[p++];
                if (c == 0 && p < end)
                {
                    int n = src[p++];
                    if (n == 0) n = 1;
                    for (int k = 0; k < n && o < dst.Length; k++) dst[o++] = 0;
                }
                else dst[o++] = c;
            }
        }

        // ---- ACT ------------------------------------------------------------------------

        /// <summary>
        /// CActRes::Load (0x6bdf90) into FxMemory. <paramref name="getSpr"/> is the resource manager lookup the
        /// loader performs for versions 0x200–0x204 (clip centring needs the SPR image size).
        /// Returns 0 (load failure) for a bad magic or a version above 0x205.
        /// </summary>
        public static uint LoadAct(FxMemory m, string name, byte[] file, Func<string, uint> getSpr, out FxAct model)
        {
            model = null;
            if (file == null || file.Length < 16 || file[0] != 'A' || file[1] != 'C') return 0;
            int ver = BitConverter.ToUInt16(file, 2);
            if (ver > 0x205) return 0;
            int numActions = BitConverter.ToUInt16(file, 4);
            int p = 16;
            int I32() { int v = BitConverter.ToInt32(file, p); p += 4; return v; }

            uint res = m.Alloc(ActResSize);
            InitRes(m, res, VtCActRes, name);
            uint actions = Vector(m, res + 0x110, (uint)numActions, ActionSize);
            uint delays = Vector(m, res + 0x12c, (uint)numActions, 4);
            for (int a = 0; a < numActions; a++) m.F32(delays + (uint)a * 4, 4.0f);

            string sprName = name.Length >= 4 ? name.Substring(0, name.Length - 4) + ".spr" : name + ".spr";
            uint sprRes = uint.MaxValue; // resolved lazily, once (the client asks the manager per clip; it caches)
            int maxClips = 0;

            for (int a = 0; a < numActions; a++)
            {
                int numMotions = I32();
                uint motions = Vector(m, actions + (uint)a * ActionSize, (uint)Math.Max(numMotions, 0), MotionSize);
                for (int f = 0; f < numMotions; f++)
                {
                    uint mo = motions + (uint)f * MotionSize;
                    for (int k = 0; k < 8; k++) m.I32(mo + (uint)k * 4, I32());   // range1, range2
                    int numClips = I32();
                    m.I32(mo + 0x2c, numClips);
                    m.I32(mo + 0x30, -1);
                    if (numClips > maxClips) maxClips = numClips;
                    uint clips = Vector(m, mo + 0x20, (uint)Math.Min(Math.Max(numClips, 0), 1000), ClipSize);
                    for (int c = 0; c < numClips; c++)
                    {
                        uint cl = clips + (uint)c * ClipSize;
                        if (c >= 1000) { p += ver < 0x200 ? 16 : ver < 0x204 ? 32 : ver >= 0x205 ? 44 : 36; continue; }
                        if (ver < 0x200)
                        {
                            for (int k = 0; k < 4; k++) m.I32(cl + (uint)k * 4, I32());
                            m.U32(cl + 0x10, 0xffffffff);
                            m.F32(cl + 0x14, 1f); m.F32(cl + 0x18, 1f);
                            m.I32(cl + 0x1c, 0); m.I32(cl + 0x20, 0);
                            continue;
                        }
                        int w = 0, h = 0;
                        bool haveSize = false;
                        if (ver < 0x204)
                        {
                            m.I32(cl, I32()); m.I32(cl + 4, I32()); m.I32(cl + 8, I32()); m.I32(cl + 0xc, I32());
                            for (int k = 0; k < 4; k++) m.U8(cl + 0x10 + (uint)k, file[p + k]);
                            p += 4;
                            float zoom = BitConverter.ToSingle(file, p); p += 4;
                            m.F32(cl + 0x14, zoom); m.F32(cl + 0x18, zoom);
                            m.I32(cl + 0x1c, I32());
                            m.I32(cl + 0x20, I32());
                        }
                        else
                        {
                            for (int k = 0; k < 9; k++) m.I32(cl + (uint)k * 4, I32());
                            if (ver >= 0x205) { w = I32(); h = I32(); haveSize = true; }
                        }
                        if (m.I32(cl + 8) == -1) continue;
                        if (!haveSize)
                        {
                            if (sprRes == uint.MaxValue) sprRes = getSpr != null ? getSpr(sprName) : 0;
                            if (sprRes == 0) continue;
                            uint vec = sprRes + (uint)((m.I32(cl + 0x20) + 0x6c) * 0xc);
                            uint begin = m.U32(vec), end = m.U32(vec + 4);
                            int idx = m.I32(cl + 8);
                            if (idx < 0 || (ulong)idx * 4 >= (ulong)(end - begin)) continue; // client: reads past the vector
                            uint img = m.U32(begin + (uint)idx * 4);
                            if (img == 0) continue;
                            w = m.I16(img); h = m.I16(img + 2);
                            if (m.I16(img + 4) != 0) w += w;
                            if (m.I16(img + 6) != 0) h += h;
                        }
                        m.I32(cl, FxOps.Cvtt(((float)m.I32(cl) - (float)(w / 2 + w % 2) * m.F32(cl + 0x14)) / m.F32(cl + 0x14)));
                        m.I32(cl + 4, FxOps.Cvtt(((float)m.I32(cl + 4) - (float)(h / 2 + h % 2) * m.F32(cl + 0x18)) / m.F32(cl + 0x18)));
                    }
                    if (ver >= 0x200)
                    {
                        int ev = I32();
                        m.I32(mo + 0x30, ver == 0x200 ? -1 : ev);
                    }
                    if (ver >= 0x203)
                    {
                        int n = I32();
                        m.I32(mo + 0x40, n);
                        uint att = Vector(m, mo + 0x34, (uint)Math.Max(n, 0), AttachSize);
                        for (int k = 0; k < n; k++)
                        {
                            p += 4;
                            m.I32(att + (uint)k * AttachSize, I32());
                            m.I32(att + (uint)k * AttachSize + 4, I32());
                            m.I32(att + (uint)k * AttachSize + 8, I32());
                        }
                    }
                }
            }
            m.I32(res + 0x11c, maxClips);
            if (ver >= 0x201)
            {
                int events = I32();
                p += Math.Max(events, 0) * 0x28;
            }
            if (ver >= 0x202)
                for (int a = 0; a < numActions; a++) m.F32(delays + (uint)a * 4, BitConverter.ToSingle(file, p + a * 4));
            model = new FxAct { Name = name, Address = res, Version = ver };
            return res;
        }
    }
}
