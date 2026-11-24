// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyTypeTree.js, src/GrannyTransform.js (themselves ports of Rasetsuu/blendergranny, MIT).
// See LICENSE-granny-ro-js.txt.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Granny {
    /// <summary>Granny MEMBER_TYPE values (DataTypeDefinition records).</summary>
    internal static class Gr2MemberType {
        public const int End = 0;
        public const int Inline = 1;
        public const int Reference = 2;
        public const int ReferenceToArray = 3;
        public const int ArrayOfReferences = 4;
        public const int VariantReference = 5;
        public const int Unsupported = 6;
        public const int ReferenceToVariantArray = 7;
        public const int String = 8;
        public const int Transform = 9;
        public const int Real32 = 10;
        public const int Int8 = 11;
        public const int UInt8 = 12;
        public const int BinormalInt8 = 13;
        public const int NormalUInt8 = 14;
        public const int Int16 = 15;
        public const int UInt16 = 16;
        public const int BinormalInt16 = 17;
        public const int NormalUInt16 = 18;
        public const int Int32 = 19;
        public const int UInt32 = 20;
        public const int Real16 = 21;
        public const int EmptyReference = 22;

        /// <summary>On-disk size of a scalar member, 0 for non-scalars.</summary>
        public static int ScalarSize(int memberType) {
            switch (memberType) {
                case Real32:
                case Int32:
                case UInt32:
                    return 4;
                case Int8:
                case UInt8:
                case BinormalInt8:
                case NormalUInt8:
                    return 1;
                case Int16:
                case UInt16:
                case BinormalInt16:
                case NormalUInt16:
                case Real16:
                    return 2;
                default:
                    return 0;
            }
        }
    }

    /// <summary>One 32-byte DataTypeDefinition record.</summary>
    internal sealed class Gr2TypeMember {
        public int MemberType;
        public string Name;
        public Gr2Ref? ReferenceType;
        public int ArrayWidth;
        public int Offset;
    }

    internal enum Gr2ValueKind : byte {
        None,
        Number,
        String,
        Transform
    }

    /// <summary>One materialized member of a struct instance (the JS ParsedField).</summary>
    internal sealed class Gr2Field {
        public int MemberType;
        /// <summary>Absolute byte offset of the member inside its section.</summary>
        public int Offset;
        public Gr2Ref? ReferenceType;
        public Gr2ValueKind ValueKind;
        public double Number;
        public string String;
        public Gr2Transform Transform;
        public Gr2Ref? Target;
        public long Count;
        public Gr2Ref[] ElementRefs;
        public Gr2Ref? VariantType;
        public Gr2Object Inline;
    }

    /// <summary>A struct instance materialized against its type tree, keyed by member name (later duplicates win).</summary>
    internal sealed class Gr2Object {
        readonly Dictionary<string, Gr2Field> fields = new Dictionary<string, Gr2Field>(StringComparer.Ordinal);

        public Gr2Field this[string name] => fields.TryGetValue(name, out Gr2Field field) ? field : null;

        public void Set(string name, Gr2Field field) {
            fields[name] = field;
        }

        /// <summary>The member's numeric value, or <paramref name="fallback"/> when absent or not a number.</summary>
        public double NumberOr(string name, double fallback) {
            Gr2Field field = this[name];
            return field != null && field.ValueKind == Gr2ValueKind.Number ? field.Number : fallback;
        }

        public bool TryNumber(string name, out double value) {
            Gr2Field field = this[name];
            if (field != null && field.ValueKind == Gr2ValueKind.Number) {
                value = field.Number;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>The member's string value, or null when absent or empty.</summary>
        public string NonEmptyString(string name) {
            Gr2Field field = this[name];
            return field != null && field.ValueKind == Gr2ValueKind.String && field.String.Length > 0 ? field.String : null;
        }
    }

    /// <summary>A parsed array element: its <c>[section, offset]</c> and its fields.</summary>
    internal readonly struct Gr2ArrayObject {
        public readonly Gr2Ref Ref;
        public readonly Gr2Object Fields;

        public Gr2ArrayObject(Gr2Ref reference, Gr2Object fields) {
            Ref = reference;
            Fields = fields;
        }
    }

    /// <summary>
    /// Decompressed sections with the pointer fixups applied, plus the type-tree walker. Every pointer slot named by
    /// the fixup table is rewritten with a fake pointer encoding <c>[section, offset]</c>; <see cref="Original"/> keeps
    /// the untouched bytes for scalar reads (a section without fixups shares one array for both views).
    /// </summary>
    internal sealed class Gr2Loaded {
        public const uint FakePointerBase = 0x10000000;
        public const uint FakeSectionStride = 0x100000;
        public const int MaxInlineDepth = 64;
        const int DefaultMaxMembers = 512;

        public readonly Gr2FileInfo File;
        public readonly byte[][] Original;
        public readonly byte[][] Fixed;
        public readonly int PointerSize;
        public readonly bool Little;

        readonly Dictionary<long, Gr2TypeMember[]> typeTrees = new Dictionary<long, Gr2TypeMember[]>();
        readonly Dictionary<long, int> storageSizes = new Dictionary<long, int>();

        public Gr2Loaded(byte[] data, Gr2FileInfo file) {
            File = file;
            Little = !file.ByteReversed;
            PointerSize = file.PointerSize >> 3;
            if (PointerSize != 4 && PointerSize != 8) {
                throw new Gr2FormatException("unsupported pointer size " + file.PointerSize);
            }

            Gr2Section[] sections = file.Sections;
            int sectionCount = sections.Length;
            Original = new byte[sectionCount][];
            Fixed = new byte[sectionCount][];
            for (int i = 0; i < sectionCount; i++) {
                Original[i] = Gr2File.Decompress(sections[i], Gr2File.SectionBytes(data, sections[i]));
                Fixed[i] = Original[i];
            }

            // Fixup tables: 12-byte (source_offset, target_section, target_offset) entries per section. The
            // mixed-marshalling tables (16-byte entries) only matter for cross-endian assets, which are rejected.
            bool hasMixed = false;
            for (int s = 0; s < sectionCount; s++) {
                Gr2Section section = sections[s];
                if (section.MixedMarshallingCount == 0 || section.MixedMarshallingOffset == 0) {
                    continue;
                }

                if (section.MixedMarshallingOffset + (long)section.MixedMarshallingCount * 16 > data.Length) {
                    throw new Gr2FormatException("section " + s + " mixed-marshalling table escapes file");
                }

                hasMixed = true;
            }

            if (file.ByteReversed && hasMixed) {
                throw new Gr2FormatException("cross-endian asset (byte_reversed + mixed-marshalling table) is not supported");
            }

            for (int s = 0; s < sectionCount; s++) {
                Gr2Section section = sections[s];
                uint count = section.PointerFixupCount;
                uint offset = section.PointerFixupOffset;
                if (count == 0 || offset == 0) {
                    continue;
                }

                if (offset + (long)count * 12 > data.Length) {
                    throw new Gr2FormatException("section " + s + " pointer fixup table escapes file");
                }

                byte[] target = Fixed[s];
                if (ReferenceEquals(target, Original[s]) && target.Length > 0) {
                    target = (byte[])target.Clone();
                    Fixed[s] = target;
                }

                for (uint i = 0; i < count; i++) {
                    int b = (int)(offset + i * 12);
                    uint sourceOffset = Gr2File.ReadU32(data, b, Little);
                    uint targetSection = Gr2File.ReadU32(data, b + 4, Little);
                    uint targetOffset = Gr2File.ReadU32(data, b + 8, Little);
                    if (sourceOffset + (long)PointerSize > target.Length) {
                        continue;
                    }

                    uint fake = unchecked(FakePointerBase + targetSection * FakeSectionStride + targetOffset);
                    WritePointer(target, (int)sourceOffset, fake);
                }
            }
        }

        public int SectionCount => Original.Length;

        // --- pointers ---------------------------------------------------------------------------------------------

        public ulong ReadPointer(byte[] bytes, int offset) {
            return ReadPointer(bytes, offset, Little);
        }

        public ulong ReadPointer(byte[] bytes, int offset, bool little) {
            if (PointerSize == 4) {
                return ReadU32(bytes, offset, little);
            }

            ulong lo = ReadU32(bytes, offset, little);
            ulong hi = ReadU32(bytes, offset + 4, little);
            return (hi << 32) | lo;
        }

        void WritePointer(byte[] bytes, int offset, uint value) {
            Span<byte> span = bytes.AsSpan(offset);
            if (Little) {
                BinaryPrimitives.WriteUInt32LittleEndian(span, value);
                if (PointerSize == 8) {
                    BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4), 0);
                }
            } else {
                BinaryPrimitives.WriteUInt32BigEndian(span, value);
                if (PointerSize == 8) {
                    BinaryPrimitives.WriteUInt32BigEndian(span.Slice(4), 0);
                }
            }
        }

        /// <summary>Decodes a fake pointer back to <c>[section, offset]</c>; null for anything outside the encoding.</summary>
        public Gr2Ref? DecodeFakePointer(ulong pointer) {
            if (pointer < FakePointerBase) {
                return null;
            }

            uint value = unchecked((uint)(pointer - FakePointerBase));
            uint section = value / FakeSectionStride;
            uint offset = value - section * FakeSectionStride;
            if (section >= (uint)SectionCount) {
                return null;
            }

            return new Gr2Ref((int)section, (int)offset);
        }

        public static uint ReadU32(byte[] bytes, int offset, bool little) {
            if (offset < 0 || offset + 4 > bytes.Length) {
                throw new Gr2FormatException("gr2 type-tree read out of range at 0x" + offset.ToString("x") + " (cap 0x" + bytes.Length.ToString("x") + ")");
            }

            return little
                ? BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset))
                : BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
        }

        // --- strings ----------------------------------------------------------------------------------------------

        /// <summary>
        /// A Granny string at <paramref name="reference"/>: length-prefixed if the u32 there gives a plausible text
        /// run, otherwise NUL-terminated. Empty when the bytes don't look like text.
        /// </summary>
        public string ReadString(Gr2Ref reference, int maxLength = 1024) {
            if (reference.Section >= Original.Length) {
                return "";
            }

            byte[] data = Original[reference.Section];
            int offset = reference.Offset;
            if (offset < 0 || offset >= data.Length) {
                return "";
            }

            if (offset + 4 <= data.Length) {
                uint length = ReadU32(data, offset, Little);
                long end = offset + 4L + length;
                if (length > 0 && length <= maxLength && end <= data.Length) {
                    var slice = new ReadOnlySpan<byte>(data, offset + 4, (int)length);
                    if (LooksText(slice)) {
                        return Encoding.UTF8.GetString(slice).TrimEnd('\0');
                    }
                }
            }

            int cap = Math.Min(data.Length, offset + maxLength);
            int stop = cap;
            for (int i = offset; i < cap; i++) {
                if (data[i] == 0) {
                    stop = i;
                    break;
                }
            }

            var text = new ReadOnlySpan<byte>(data, offset, stop - offset);
            return LooksText(text) ? Encoding.UTF8.GetString(text) : "";
        }

        public string ReadStringPointer(ulong pointer) {
            Gr2Ref? reference = DecodeFakePointer(pointer);
            return reference.HasValue ? ReadString(reference.Value) : "";
        }

        static bool LooksText(ReadOnlySpan<byte> bytes) {
            if (bytes.Length == 0) {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++) {
                byte v = bytes[i];
                if (v == 9 || v == 10 || v == 13) {
                    continue;
                }

                if (v < 32 || v == 127) {
                    return false;
                }
            }

            return true;
        }

        // --- type trees -------------------------------------------------------------------------------------------

        /// <summary>Walks a DataTypeDefinition chain (32-byte records up to MT_END). Cached per reference.</summary>
        public Gr2TypeMember[] ParseTypeTree(Gr2Ref reference) {
            if (typeTrees.TryGetValue(reference.Key, out Gr2TypeMember[] cached)) {
                return cached;
            }

            var entries = new List<Gr2TypeMember>();
            if (reference.Section < Fixed.Length) {
                byte[] data = Fixed[reference.Section];
                int offset = reference.Offset;
                for (int i = 0; i < DefaultMaxMembers; i++) {
                    if (offset < 0 || offset + 32 > data.Length) {
                        break;
                    }

                    uint memberType = ReadU32(data, offset, Little);
                    if (memberType == Gr2MemberType.End) {
                        break;
                    }

                    uint namePtr = ReadU32(data, offset + 4, Little);
                    uint typePtr = ReadU32(data, offset + 8, Little);
                    uint arrayWidth = ReadU32(data, offset + 12, Little);
                    string name = ReadStringPointer(namePtr);
                    entries.Add(new Gr2TypeMember {
                        MemberType = (int)memberType,
                        Name = name.Length > 0 ? name : "member_" + entries.Count,
                        ReferenceType = DecodeFakePointer(typePtr),
                        ArrayWidth = arrayWidth != 0 ? (int)arrayWidth : 1,
                        Offset = offset
                    });
                    offset += 32;
                }
            }

            Gr2TypeMember[] result = entries.ToArray();
            typeTrees[reference.Key] = result;
            return result;
        }

        /// <summary>Byte size of a non-inline member inside its parent struct.</summary>
        public int MemberStorageSize(Gr2TypeMember member) {
            int width = member.ArrayWidth;
            int t = member.MemberType;
            int scalar = Gr2MemberType.ScalarSize(t);
            if (scalar != 0) {
                return scalar * width;
            }

            switch (t) {
                case Gr2MemberType.Reference:
                case Gr2MemberType.EmptyReference:
                case Gr2MemberType.String:
                    return PointerSize;
                case Gr2MemberType.ReferenceToArray:
                case Gr2MemberType.ArrayOfReferences:
                    return 4 + PointerSize;
                case Gr2MemberType.VariantReference:
                    return PointerSize * 2;
                case Gr2MemberType.ReferenceToVariantArray:
                    return PointerSize + 4 + PointerSize;
                case Gr2MemberType.Transform:
                    return 68;
                case Gr2MemberType.Inline:
                    return 0;
                default:
                    return PointerSize;
            }
        }

        /// <summary>Total storage size (stride) of the struct whose type tree lives at <paramref name="typeRef"/>.</summary>
        public int StorageSize(Gr2Ref typeRef) {
            if (storageSizes.TryGetValue(typeRef.Key, out int cached)) {
                return cached;
            }

            int size = ObjectStorageSize(ParseTypeTree(typeRef), new HashSet<long>(), new Dictionary<long, int>());
            storageSizes[typeRef.Key] = size;
            return size;
        }

        // Inline members recurse; each sibling gets its own copy of the cycle guard, so repeated inline members of
        // one type (TransformTrack's three curves) are all counted. A shared sub-type is sized once per call (memo).
        int ObjectStorageSize(Gr2TypeMember[] members, HashSet<long> seen, Dictionary<long, int> memo) {
            int size = 0;
            for (int i = 0; i < members.Length; i++) {
                Gr2TypeMember member = members[i];
                if (member.MemberType == Gr2MemberType.Inline && member.ReferenceType.HasValue) {
                    long key = member.ReferenceType.Value.Key;
                    if (seen.Contains(key)) {
                        continue;
                    }

                    if (memo.TryGetValue(key, out int known)) {
                        size += known;
                        continue;
                    }

                    var childSeen = new HashSet<long>(seen) { key };
                    int subSize = ObjectStorageSize(ParseTypeTree(member.ReferenceType.Value), childSeen, memo);
                    memo[key] = subSize;
                    size += subSize;
                } else {
                    size += MemberStorageSize(member);
                }
            }

            return size;
        }

        // --- objects ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Materializes one instance of <paramref name="typeTree"/> at <paramref name="reference"/>. Pointers come
        /// from the fixed bytes, scalars from the original ones. <paramref name="maxArrayRefs"/> caps the element
        /// references decoded per array-of-references member.
        /// </summary>
        public Gr2Object ParseObject(Gr2TypeMember[] typeTree, Gr2Ref reference, int maxArrayRefs = 256) {
            return ParseObject(typeTree, reference, maxArrayRefs, 0, null);
        }

        Gr2Object ParseObject(Gr2TypeMember[] typeTree, Gr2Ref reference, int maxArrayRefs, int depth, HashSet<long> seen) {
            var result = new Gr2Object();
            int section = reference.Section;
            if (section >= Fixed.Length) {
                return result;
            }

            byte[] fixedBytes = Fixed[section];
            byte[] raw = Original[section];
            int offset = reference.Offset;
            for (int i = 0; i < typeTree.Length; i++) {
                Gr2TypeMember member = typeTree[i];
                int size = MemberStorageSize(member);
                if (offset + (long)size > fixedBytes.Length) {
                    break;
                }

                var field = new Gr2Field {
                    MemberType = member.MemberType,
                    Offset = offset,
                    ReferenceType = member.ReferenceType
                };
                int t = member.MemberType;
                switch (t) {
                    case Gr2MemberType.Reference:
                    case Gr2MemberType.EmptyReference:
                        field.Target = DecodeFakePointer(ReadPointer(fixedBytes, offset));
                        break;
                    case Gr2MemberType.String: {
                        ulong pointer = ReadPointer(fixedBytes, offset);
                        field.ValueKind = Gr2ValueKind.String;
                        field.String = ReadStringPointer(pointer);
                        field.Target = DecodeFakePointer(pointer);
                        break;
                    }
                    case Gr2MemberType.ReferenceToArray:
                    case Gr2MemberType.ArrayOfReferences: {
                        uint count = ReadU32(raw, offset, Little);
                        Gr2Ref? target = DecodeFakePointer(ReadPointer(fixedBytes, offset + 4));
                        field.Count = count;
                        field.Target = target;
                        if (t == Gr2MemberType.ArrayOfReferences) {
                            field.ElementRefs = ReadArrayReferences(target, count, maxArrayRefs);
                        }

                        break;
                    }
                    case Gr2MemberType.ReferenceToVariantArray: {
                        ulong typePointer = ReadPointer(fixedBytes, offset);
                        uint count = ReadU32(raw, offset + PointerSize, Little);
                        ulong objectPointer = ReadPointer(fixedBytes, offset + PointerSize + 4);
                        field.VariantType = DecodeFakePointer(typePointer);
                        field.Count = count;
                        field.Target = DecodeFakePointer(objectPointer);
                        break;
                    }
                    case Gr2MemberType.VariantReference:
                        field.VariantType = DecodeFakePointer(ReadPointer(fixedBytes, offset));
                        field.Target = DecodeFakePointer(ReadPointer(fixedBytes, offset + PointerSize));
                        break;
                    case Gr2MemberType.Int32:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = unchecked((int)ReadU32(raw, offset, Little));
                        break;
                    case Gr2MemberType.UInt32:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = ReadU32(raw, offset, Little);
                        break;
                    case Gr2MemberType.Int16:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = unchecked((short)ReadU16(raw, offset));
                        break;
                    case Gr2MemberType.UInt16:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = ReadU16(raw, offset);
                        break;
                    case Gr2MemberType.Int8:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = unchecked((sbyte)raw[offset]);
                        break;
                    case Gr2MemberType.UInt8:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = raw[offset];
                        break;
                    case Gr2MemberType.Real32:
                        field.ValueKind = Gr2ValueKind.Number;
                        field.Number = BitConverter.Int32BitsToSingle(unchecked((int)ReadU32(raw, offset, Little)));
                        break;
                    case Gr2MemberType.Transform:
                        field.ValueKind = Gr2ValueKind.Transform;
                        field.Transform = ReadTransform(section, offset);
                        break;
                    case Gr2MemberType.Inline:
                        if (member.ReferenceType.HasValue) {
                            Gr2Ref subType = member.ReferenceType.Value;
                            long key = subType.Key;
                            if (depth >= MaxInlineDepth) {
                                throw new Gr2FormatException("inline nesting exceeds " + MaxInlineDepth + " at section " + section + " offset 0x" + offset.ToString("x"));
                            }

                            if (seen != null && seen.Contains(key)) {
                                throw new Gr2FormatException("self-referential inline type at section " + section + " offset 0x" + offset.ToString("x"));
                            }

                            HashSet<long> childSeen = seen != null ? new HashSet<long>(seen) : new HashSet<long>();
                            childSeen.Add(key);
                            field.Inline = ParseObject(ParseTypeTree(subType), new Gr2Ref(section, offset), maxArrayRefs, depth + 1, childSeen);
                            result.Set(member.Name, field);
                            offset += StorageSize(subType);
                            continue;
                        }

                        break;
                }

                result.Set(member.Name, field);
                offset += size;
            }

            return result;
        }

        Gr2Ref[] ReadArrayReferences(Gr2Ref? arrayRef, long count, int maxCount) {
            if (!arrayRef.HasValue || count <= 0) {
                return Array.Empty<Gr2Ref>();
            }

            byte[] data = Fixed[arrayRef.Value.Section];
            long limit = Math.Min(count, maxCount);
            var refs = new List<Gr2Ref>((int)Math.Max(0, limit));
            for (long i = 0; i < limit; i++) {
                long off = arrayRef.Value.Offset + i * PointerSize;
                if (off + PointerSize > data.Length) {
                    break;
                }

                Gr2Ref? decoded = DecodeFakePointer(ReadPointer(data, (int)off));
                if (decoded.HasValue) {
                    refs.Add(decoded.Value);
                }
            }

            return refs.ToArray();
        }

        /// <summary>
        /// Walks an inline-struct array: <paramref name="count"/> packed instances of the type at
        /// <paramref name="typeRef"/>, capped at <paramref name="maxCount"/>.
        /// </summary>
        public Gr2ArrayObject[] ReadReferenceArrayObjects(Gr2Ref? arrayRef, long count, Gr2Ref? typeRef, int maxCount = 64, int maxArrayRefs = -1) {
            if (!arrayRef.HasValue || !typeRef.HasValue || count <= 0) {
                return Array.Empty<Gr2ArrayObject>();
            }

            if (maxArrayRefs < 0) {
                maxArrayRefs = maxCount;
            }

            Gr2TypeMember[] members = ParseTypeTree(typeRef.Value);
            int stride = StorageSize(typeRef.Value);
            if (stride <= 0) {
                return Array.Empty<Gr2ArrayObject>();
            }

            int limit = (int)Math.Min(count, maxCount);
            var result = new Gr2ArrayObject[limit];
            for (int i = 0; i < limit; i++) {
                var element = new Gr2Ref(arrayRef.Value.Section, arrayRef.Value.Offset + i * stride);
                result[i] = new Gr2ArrayObject(element, ParseObject(members, element, maxArrayRefs));
            }

            return result;
        }

        // --- raw reads --------------------------------------------------------------------------------------------

        ushort ReadU16(byte[] bytes, int offset) {
            return Little
                ? BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2))
                : BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
        }

        /// <summary>The 68-byte Transform (flags + position[3] + orientation[4] + scaleShear[9]); identity when out of range.</summary>
        public Gr2Transform ReadTransform(int section, int offset) {
            byte[] data = section >= 0 && section < Original.Length ? Original[section] : null;
            if (data == null || offset < 0 || offset + 68 > data.Length) {
                return Gr2Transform.Identity;
            }

            ReadOnlySpan<byte> s = data.AsSpan(offset, 68);
            return new Gr2Transform {
                Flags = BinaryPrimitives.ReadUInt32LittleEndian(s),
                PositionX = F32(s, 4), PositionY = F32(s, 8), PositionZ = F32(s, 12),
                OrientationX = F32(s, 16), OrientationY = F32(s, 20), OrientationZ = F32(s, 24), OrientationW = F32(s, 28),
                ScaleShear00 = F32(s, 32), ScaleShear01 = F32(s, 36), ScaleShear02 = F32(s, 40),
                ScaleShear10 = F32(s, 44), ScaleShear11 = F32(s, 48), ScaleShear12 = F32(s, 52),
                ScaleShear20 = F32(s, 56), ScaleShear21 = F32(s, 60), ScaleShear22 = F32(s, 64)
            };
        }

        /// <summary><paramref name="count"/> LE floats at (section, offset), or null when the range escapes the section.</summary>
        public float[] ReadFloats(int section, int offset, int count) {
            byte[] data = section >= 0 && section < Original.Length ? Original[section] : null;
            if (data == null || offset < 0 || offset + (long)count * 4 > data.Length) {
                return null;
            }

            var result = new float[count];
            for (int i = 0; i < count; i++) {
                result[i] = F32(data, offset + i * 4);
            }

            return result;
        }

        public static float F32(ReadOnlySpan<byte> bytes, int offset) {
            return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)));
        }
    }
}
