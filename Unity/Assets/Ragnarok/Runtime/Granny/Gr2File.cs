// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyFile.js (itself a port of Rasetsuu/blendergranny, MIT). See LICENSE-granny-ro-js.txt.

using System;
using System.Buffers.Binary;

namespace Ragnarok.Granny {
    /// <summary>Raised on malformed or unsupported .gr2 input (bad magic, truncated tables, corrupt codec streams).</summary>
    public sealed class Gr2FormatException : Exception {
        public Gr2FormatException(string message) : base(message) {
        }
    }

    /// <summary>A <c>[section, offset]</c> reference into the decompressed sections.</summary>
    public readonly struct Gr2Ref : IEquatable<Gr2Ref> {
        public readonly int Section;
        public readonly int Offset;

        public Gr2Ref(int section, int offset) {
            Section = section;
            Offset = offset;
        }

        /// <summary>Stable key for dictionaries (section and offset packed into one value).</summary>
        public long Key => ((long)Section << 32) | (uint)Offset;

        public bool Equals(Gr2Ref other) => Section == other.Section && Offset == other.Offset;
        public override bool Equals(object obj) => obj is Gr2Ref other && Equals(other);
        public override int GetHashCode() => Key.GetHashCode();
        public override string ToString() => Section + ":" + Offset;
    }

    /// <summary>One entry of the section table (44 bytes on disk, docs/gr2-format.md section 4).</summary>
    public sealed class Gr2Section {
        public int Index;
        /// <summary>0 = none, 1 = Oodle0, 2 = Oodle1, 3 = BitKnit, 4 = BitKnit2 (only 0 and 1 are supported).</summary>
        public uint Compression;
        public uint DataOffset;
        public uint DataSize;
        public uint ExpandedSize;
        public uint InternalAlignment;
        public uint First16Bit;
        public uint First8Bit;
        public uint PointerFixupOffset;
        public uint PointerFixupCount;
        public uint MixedMarshallingOffset;
        public uint MixedMarshallingCount;
    }

    /// <summary>The .gr2 header and section table (no section is decompressed yet).</summary>
    public sealed class Gr2FileInfo {
        public uint Version;
        public uint TotalSize;
        public uint Crc;
        public uint SectionArrayOffset;
        public Gr2Ref RootType;
        public Gr2Ref RootObject;
        public uint TypeTag;
        /// <summary>Pointer width in bits baked into the file (32 or 64).</summary>
        public int PointerSize;
        /// <summary>True when the u32s are stored big-endian.</summary>
        public bool ByteReversed;
        public Gr2Section[] Sections;
    }

    internal static class Gr2File {
        public const int MagicSize = 32;
        public const int SectionRecordSize = 44;
        public const uint CompressionNone = 0;
        public const uint CompressionOodle0 = 1;

        static readonly uint[] MagicOld = { 0xCAB067B8, 0x0FB16DF8, 0x7E8C7284, 0x1E00195E };
        static readonly uint[] Magic32Le = { 0xC06CDE29, 0x2B53A4BA, 0xA5B7F525, 0xEEE266F6 };
        static readonly uint[] Magic32Be = { 0xB595110E, 0x4BB5A56A, 0x502828EB, 0x04B37825 };
        static readonly uint[] Magic64Le = { 0x5E499BE5, 0x141F636F, 0xA9EB131E, 0xC4EDBE90 };
        static readonly uint[] Magic64Be = { 0xE3D49531, 0x624FDC20, 0x3AD036CC, 0x89FF82B1 };

        /// <summary>Detects a Granny file by its first 16 bytes; returns false for anything else.</summary>
        public static bool Detect(byte[] data, out bool byteReversed, out int pointerSize) {
            byteReversed = false;
            pointerSize = 0;
            if (data == null || data.Length < MagicSize) {
                return false;
            }

            Span<uint> words = stackalloc uint[4];
            for (int i = 0; i < 4; i++) {
                words[i] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * 4));
            }

            if (Equal(words, MagicOld) || Equal(words, Magic32Le)) {
                pointerSize = 32;
                return true;
            }

            if (Equal(words, Magic64Le)) {
                pointerSize = 64;
                return true;
            }

            for (int i = 0; i < 4; i++) {
                words[i] = BinaryPrimitives.ReverseEndianness(words[i]);
            }

            if (Equal(words, MagicOld) || Equal(words, Magic32Be)) {
                byteReversed = true;
                pointerSize = 32;
                return true;
            }

            if (Equal(words, Magic64Be)) {
                byteReversed = true;
                pointerSize = 64;
                return true;
            }

            return false;
        }

        /// <summary>Reads the magic, the header (60 bytes before v7, 72 from v7) and the section table.</summary>
        public static Gr2FileInfo Parse(byte[] data) {
            if (!Detect(data, out bool byteReversed, out int pointerSize)) {
                throw new Gr2FormatException("not a Granny2 file");
            }

            bool little = !byteReversed;
            const int h = MagicSize;
            var info = new Gr2FileInfo {
                Version = ReadU32(data, h, little),
                TotalSize = ReadU32(data, h + 4, little),
                Crc = ReadU32(data, h + 8, little),
                SectionArrayOffset = ReadU32(data, h + 12, little),
                PointerSize = pointerSize,
                ByteReversed = byteReversed
            };
            uint sectionCount = ReadU32(data, h + 16, little);
            info.RootType = new Gr2Ref((int)ReadU32(data, h + 20, little), (int)ReadU32(data, h + 24, little));
            info.RootObject = new Gr2Ref((int)ReadU32(data, h + 28, little), (int)ReadU32(data, h + 32, little));
            info.TypeTag = ReadU32(data, h + 36, little);

            long sectionsBase = MagicSize + (long)info.SectionArrayOffset;
            if (sectionsBase + (long)sectionCount * SectionRecordSize > data.Length) {
                throw new Gr2FormatException("gr2 section table escapes the file");
            }

            var sections = new Gr2Section[sectionCount];
            for (int index = 0; index < sections.Length; index++) {
                int off = (int)(sectionsBase + (long)index * SectionRecordSize);
                sections[index] = new Gr2Section {
                    Index = index,
                    Compression = ReadU32(data, off, little),
                    DataOffset = ReadU32(data, off + 4, little),
                    DataSize = ReadU32(data, off + 8, little),
                    ExpandedSize = ReadU32(data, off + 12, little),
                    InternalAlignment = ReadU32(data, off + 16, little),
                    First16Bit = ReadU32(data, off + 20, little),
                    First8Bit = ReadU32(data, off + 24, little),
                    PointerFixupOffset = ReadU32(data, off + 28, little),
                    PointerFixupCount = ReadU32(data, off + 32, little),
                    MixedMarshallingOffset = ReadU32(data, off + 36, little),
                    MixedMarshallingCount = ReadU32(data, off + 40, little)
                };
            }

            info.Sections = sections;
            return info;
        }

        /// <summary>The on-disk (compressed) bytes of a section.</summary>
        public static ReadOnlySpan<byte> SectionBytes(byte[] data, Gr2Section section) {
            long start = section.DataOffset;
            long end = start + section.DataSize;
            if (end > data.Length) {
                throw new Gr2FormatException("section " + section.Index + " points outside file");
            }

            return new ReadOnlySpan<byte>(data, (int)start, (int)section.DataSize);
        }

        /// <summary>Decompresses one section (none / Oodle0); the result is exactly ExpandedSize bytes.</summary>
        public static byte[] Decompress(Gr2Section section, ReadOnlySpan<byte> compressed) {
            if (section.ExpandedSize == 0) {
                return Array.Empty<byte>();
            }

            if (section.Compression == CompressionNone) {
                if (compressed.Length < section.ExpandedSize) {
                    throw new Gr2FormatException("section " + section.Index + " short: " + compressed.Length + " < " + section.ExpandedSize);
                }

                return compressed.Slice(0, (int)section.ExpandedSize).ToArray();
            }

            if (section.Compression == CompressionOodle0) {
                return Gr2Oodle0.Decompress(section, compressed);
            }

            throw new Gr2FormatException("unsupported compression " + section.Compression);
        }

        internal static uint ReadU32(byte[] data, int offset, bool little) {
            if (offset < 0 || offset + 4 > data.Length) {
                throw new Gr2FormatException("gr2 read out of range at 0x" + offset.ToString("x") + " (cap 0x" + data.Length.ToString("x") + ")");
            }

            return little
                ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset))
                : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
        }

        static bool Equal(ReadOnlySpan<uint> a, uint[] b) {
            for (int i = 0; i < 4; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }

            return true;
        }
    }
}
