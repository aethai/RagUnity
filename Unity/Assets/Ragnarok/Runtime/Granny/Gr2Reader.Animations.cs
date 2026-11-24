// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyAnimation.js (itself a port of Rasetsuu/blendergranny, MIT). See LICENSE-granny-ro-js.txt.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Ragnarok.Granny {
    public static partial class Gr2Reader {
        const int MaxTrackGroups = 64;
        const int MaxTracksPerGroup = 512;
        const int MaxAnimations = 64;

        // Quaternion scale / offset table: one (scale, offset) pair per 4-bit nibble of ScaleOffsetTableEntries.
        const double OneOverSqrt2 = 0.707106781;
        static readonly double[] QuaternionScales = {
            OneOverSqrt2 * 2.0, OneOverSqrt2 * 1.0, OneOverSqrt2 * 0.5, OneOverSqrt2 * 0.5,
            OneOverSqrt2 * 0.5, OneOverSqrt2 * 0.25, OneOverSqrt2 * 0.25, OneOverSqrt2 * 0.25,
            -OneOverSqrt2 * 2.0, -OneOverSqrt2 * 1.0, -OneOverSqrt2 * 0.5, -OneOverSqrt2 * 0.5,
            -OneOverSqrt2 * 0.5, -OneOverSqrt2 * 0.25, -OneOverSqrt2 * 0.25, -OneOverSqrt2 * 0.25
        };

        static readonly double[] QuaternionOffsets = {
            -OneOverSqrt2, -OneOverSqrt2 * 0.5, -OneOverSqrt2 * 0.75, -OneOverSqrt2 * 0.25,
            OneOverSqrt2 * 0.25, -OneOverSqrt2 * 0.250, -OneOverSqrt2 * 0.125, OneOverSqrt2 * 0.000,
            OneOverSqrt2, OneOverSqrt2 * 0.5, OneOverSqrt2 * 0.75, OneOverSqrt2 * 0.25,
            -OneOverSqrt2 * 0.25, OneOverSqrt2 * 0.250, OneOverSqrt2 * 0.125, -OneOverSqrt2 * 0.000
        };

        static Gr2Animation[] ExtractAnimations(Gr2Loaded loaded) {
            Gr2Object root = ParseRoot(loaded, Math.Max(MaxTrackGroups, MaxAnimations));

            var trackGroups = new List<Gr2TrackGroup>();
            Gr2Field trackGroupField = root["TrackGroups"];
            if (trackGroupField != null && trackGroupField.ReferenceType.HasValue && trackGroupField.ElementRefs != null && trackGroupField.ElementRefs.Length > 0) {
                Gr2TypeMember[] type = loaded.ParseTypeTree(trackGroupField.ReferenceType.Value);
                int limit = Math.Min(trackGroupField.ElementRefs.Length, MaxTrackGroups);
                for (int i = 0; i < limit; i++) {
                    trackGroups.Add(ReadTrackGroup(loaded, i, trackGroupField.ElementRefs[i], type));
                }
            }

            var animations = new List<Gr2Animation>();
            Gr2Field animationField = root["Animations"];
            if (animationField != null && animationField.ReferenceType.HasValue && animationField.ElementRefs != null && animationField.ElementRefs.Length > 0) {
                Gr2TypeMember[] type = loaded.ParseTypeTree(animationField.ReferenceType.Value);
                var byName = new Dictionary<string, Gr2TrackGroup>(StringComparer.Ordinal);
                foreach (Gr2TrackGroup group in trackGroups) {
                    byName[group.Name] = group;
                }

                int limit = Math.Min(animationField.ElementRefs.Length, MaxAnimations);
                for (int i = 0; i < limit; i++) {
                    animations.Add(ReadAnimation(loaded, i, animationField.ElementRefs[i], type, byName));
                }
            }

            return animations.ToArray();
        }

        static Gr2Animation ReadAnimation(Gr2Loaded loaded, int index, Gr2Ref reference, Gr2TypeMember[] type, Dictionary<string, Gr2TrackGroup> byName) {
            Gr2Object fields = loaded.ParseObject(type, reference, 64);
            string[] names = ReadTrackGroupReferenceNames(loaded, fields["TrackGroups"], 64);
            var groups = new List<Gr2TrackGroup>(names.Length);
            foreach (string name in names) {
                if (byName.TryGetValue(name, out Gr2TrackGroup group)) {
                    groups.Add(group);
                }
            }

            return new Gr2Animation {
                Index = index,
                Name = fields.NonEmptyString("Name") ?? "Animation_" + index,
                Duration = (float)fields.NumberOr("Duration", 0.0),
                TimeStep = (float)fields.NumberOr("TimeStep", 0.0),
                Oversampling = (float)fields.NumberOr("Oversampling", 0.0),
                DefaultLoopCount = (int)fields.NumberOr("DefaultLoopCount", 0),
                Flags = (int)fields.NumberOr("Flags", 0),
                TrackGroupNames = names,
                TrackGroups = groups.ToArray()
            };
        }

        static string[] ReadTrackGroupReferenceNames(Gr2Loaded loaded, Gr2Field field, int maxRefs) {
            if (field == null || !field.Target.HasValue || field.Count <= 0) {
                return Array.Empty<string>();
            }

            Gr2Ref[] refs = field.ElementRefs ?? Array.Empty<Gr2Ref>();
            if (!field.ReferenceType.HasValue) {
                var fallback = new string[refs.Length];
                for (int i = 0; i < fallback.Length; i++) {
                    fallback[i] = "TrackGroup_" + i;
                }

                return fallback;
            }

            Gr2TypeMember[] type = loaded.ParseTypeTree(field.ReferenceType.Value);
            int limit = Math.Min(refs.Length, maxRefs);
            var names = new string[limit];
            for (int i = 0; i < limit; i++) {
                names[i] = loaded.ParseObject(type, refs[i], 0).NonEmptyString("Name") ?? "TrackGroup_" + i;
            }

            return names;
        }

        static Gr2TrackGroup ReadTrackGroup(Gr2Loaded loaded, int index, Gr2Ref reference, Gr2TypeMember[] type) {
            Gr2Object fields = loaded.ParseObject(type, reference, MaxTracksPerGroup);
            Gr2Field transformTracks = fields["TransformTracks"];
            Gr2Field vectorTracks = fields["VectorTracks"];
            return new Gr2TrackGroup {
                Index = index,
                Name = fields.NonEmptyString("Name") ?? "TrackGroup_" + index,
                VectorTrackCount = (int)(vectorTracks?.Count ?? 0),
                TransformTrackCount = (int)(transformTracks?.Count ?? 0),
                TextTrackCount = (int)(fields["TextTracks"]?.Count ?? 0),
                AccumulationFlags = (int)fields.NumberOr("AccumulationFlags", 0),
                LoopTranslation = (float)fields.NumberOr("LoopTranslation", 0.0),
                VectorTrackNames = ReadVectorTrackNames(loaded, vectorTracks),
                TransformTracks = ReadTransformTracks(loaded, transformTracks)
            };
        }

        static string[] ReadVectorTrackNames(Gr2Loaded loaded, Gr2Field field) {
            if (field == null || !field.Target.HasValue || !field.ReferenceType.HasValue || field.Count <= 0) {
                return Array.Empty<string>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(field.Target, field.Count, field.ReferenceType, MaxTracksPerGroup, 64);
            var names = new string[objects.Length];
            for (int i = 0; i < names.Length; i++) {
                names[i] = objects[i].Fields.NonEmptyString("Name") ?? "Track_" + i;
            }

            return names;
        }

        static Gr2TransformTrack[] ReadTransformTracks(Gr2Loaded loaded, Gr2Field field) {
            if (field == null || !field.Target.HasValue || !field.ReferenceType.HasValue || field.Count <= 0) {
                return Array.Empty<Gr2TransformTrack>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(field.Target, field.Count, field.ReferenceType, MaxTracksPerGroup, 64);
            var tracks = new Gr2TransformTrack[objects.Length];
            for (int i = 0; i < tracks.Length; i++) {
                tracks[i] = ReadTransformTrack(loaded, i, objects[i]);
            }

            return tracks;
        }

        static Gr2TransformTrack ReadTransformTrack(Gr2Loaded loaded, int index, Gr2ArrayObject parsed) {
            Gr2Object fields = parsed.Fields;
            Gr2Ref reference = parsed.Ref;

            // Flags is read at offset 4 when the type has no Flags member (RO's tracks don't); those bytes are the
            // position curve's degree. Kept for parity with the JS / blendergranny.
            Gr2Field flagsField = fields["Flags"];
            int flagsOffset = flagsField != null ? flagsField.Offset : reference.Offset + 4;
            byte[] raw = loaded.Original[reference.Section];
            int flags = flagsOffset >= 0 && flagsOffset + 4 <= raw.Length ? BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(flagsOffset)) : 0;

            Gr2Field orientation = fields["OrientationCurve"];
            Gr2Field position = fields["PositionCurve"];
            Gr2Field scaleShear = fields["ScaleShearCurve"];
            return new Gr2TransformTrack {
                Index = index,
                Name = fields.NonEmptyString("Name") ?? "TransformTrack_" + index,
                Flags = flags,
                OrientationCurve = orientation != null ? ReadCurve(loaded, reference.Section, orientation.Offset) : null,
                PositionCurve = position != null ? ReadCurve(loaded, reference.Section, position.Offset) : null,
                ScaleShearCurve = scaleShear != null ? ReadCurve(loaded, reference.Section, scaleShear.Offset) : null
            };
        }

        // --- curves -----------------------------------------------------------------------------------------------

        /// <summary>A curve stored inline at (section, offset): the legacy layout first, else the variant-ref header.</summary>
        static Gr2Curve ReadCurve(Gr2Loaded loaded, int section, int curveOffset) {
            int pointerSize = loaded.PointerSize;
            byte[] fixedBytes = loaded.Fixed[section];
            if (curveOffset + pointerSize * 2 > fixedBytes.Length) {
                return null;
            }

            Gr2Curve legacy = ReadLegacyCurve(loaded, section, curveOffset);
            if (legacy != null) {
                legacy.KnotsSorted = Gr2Curve.IsNonDecreasing(legacy.Knots);
                return legacy;
            }

            Gr2Ref? typeRef = loaded.DecodeFakePointer(loaded.ReadPointer(fixedBytes, curveOffset, true));
            Gr2Ref? objectRef = loaded.DecodeFakePointer(loaded.ReadPointer(fixedBytes, curveOffset + pointerSize, true));
            if (!typeRef.HasValue || !objectRef.HasValue) {
                return null;
            }

            Gr2TypeMember[] members = loaded.ParseTypeTree(typeRef.Value);
            if (members.Length == 0) {
                return null;
            }

            string codec = members[0].Name;
            if (codec.StartsWith("CurveDataHeader_", StringComparison.Ordinal)) {
                codec = codec.Substring("CurveDataHeader_".Length);
            }

            Dictionary<string, int> offsets = MemberOffsets(loaded, members);
            byte[] raw = loaded.Original[objectRef.Value.Section];
            int objectOffset = objectRef.Value.Offset;
            if (objectOffset + 2 > raw.Length) {
                return null;
            }

            int format = raw[objectOffset];
            int degree = raw[objectOffset + 1];
            int dimension = CurveDimension(codec);
            if (codec == "DaIdentity") {
                int dimOffset = objectOffset + OffsetOr(offsets, "Dimension", 2);
                if (dimOffset + 2 <= raw.Length) {
                    dimension = BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(dimOffset));
                }
            }

            int knotControlCount = 0;
            if (offsets.TryGetValue("KnotsControls", out int kcOffset) && objectOffset + kcOffset + 4 <= raw.Length) {
                knotControlCount = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(objectOffset + kcOffset)));
            }

            var curve = new Gr2Curve {
                Codec = codec,
                Format = format,
                Degree = degree,
                Dimension = dimension,
                KnotControlCount = knotControlCount,
                SampleValue = ReadStaticCurveValue(codec, dimension, raw, objectOffset),
                Knots = Array.Empty<float>(),
                Controls = Array.Empty<float>()
            };
            ReadKnotsControls(loaded, curve, raw, loaded.Fixed[objectRef.Value.Section], objectOffset, offsets);
            curve.KnotsSorted = Gr2Curve.IsNonDecreasing(curve.Knots);
            return curve;
        }

        /// <summary>
        /// Legacy layout: (degree i32, knot count, knot pointer, control count, control pointer). The degree (0..3)
        /// tells it apart from a variant reference, whose first slot is a fake pointer.
        /// </summary>
        static Gr2Curve ReadLegacyCurve(Gr2Loaded loaded, int section, int offset) {
            int pointerSize = loaded.PointerSize;
            byte[] raw = loaded.Original[section];
            byte[] fixedBytes = loaded.Fixed[section];
            if (offset + 4 + (4 + pointerSize) * 2 > raw.Length) {
                return null;
            }

            int degree = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset));
            if (degree < 0 || degree > 3) {
                return null;
            }

            uint knotCount = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 4));
            uint controlCount = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 4 + pointerSize + 4));
            if (knotCount == 0 || controlCount == 0 || controlCount % knotCount != 0) {
                return null;
            }

            uint dimension = controlCount / knotCount;
            if (dimension == 0 || dimension > 16) {
                return null;
            }

            Gr2Ref? knotsRef = loaded.DecodeFakePointer(loaded.ReadPointer(fixedBytes, offset + 4 + 4, true));
            Gr2Ref? controlsRef = loaded.DecodeFakePointer(loaded.ReadPointer(fixedBytes, offset + 4 + pointerSize + 4 + 4, true));
            if (!knotsRef.HasValue || !controlsRef.HasValue) {
                return null;
            }

            byte[] knotBytes = loaded.Fixed[knotsRef.Value.Section];
            byte[] controlBytes = loaded.Fixed[controlsRef.Value.Section];
            if (knotsRef.Value.Offset + (long)knotCount * 4 > knotBytes.Length || controlsRef.Value.Offset + (long)controlCount * 4 > controlBytes.Length) {
                return null;
            }

            var knots = new float[knotCount];
            for (int i = 0; i < knots.Length; i++) {
                knots[i] = Gr2Loaded.F32(knotBytes, knotsRef.Value.Offset + i * 4);
            }

            var controls = new float[controlCount];
            for (int i = 0; i < controls.Length; i++) {
                controls[i] = Gr2Loaded.F32(controlBytes, controlsRef.Value.Offset + i * 4);
            }

            float[] sampleValue = Array.Empty<float>();
            if (knotCount == 1) {
                sampleValue = new float[dimension];
                Array.Copy(controls, sampleValue, (int)dimension);
            }

            return new Gr2Curve {
                Codec = "LegacyCurve32f",
                Format = -1,
                Degree = degree,
                Dimension = (int)dimension,
                KnotControlCount = (int)(knotCount + controlCount),
                SampleValue = sampleValue,
                Knots = knots,
                Controls = controls
            };
        }

        /// <summary>Name -> byte offset inside a curve header struct (inline members counted at full size).</summary>
        static Dictionary<string, int> MemberOffsets(Gr2Loaded loaded, Gr2TypeMember[] members) {
            var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
            int offset = 0;
            for (int i = 0; i < members.Length; i++) {
                Gr2TypeMember member = members[i];
                offsets[member.Name] = offset;
                offset += member.MemberType == Gr2MemberType.Inline && member.ReferenceType.HasValue
                    ? loaded.StorageSize(member.ReferenceType.Value)
                    : loaded.MemberStorageSize(member);
            }

            return offsets;
        }

        static int OffsetOr(Dictionary<string, int> offsets, string name, int fallback) {
            return offsets.TryGetValue(name, out int value) ? value : fallback;
        }

        /// <summary>"Dn..." codecs carry their dimension in the second character.</summary>
        static int CurveDimension(string codec) {
            if (codec.Length >= 2 && codec[0] == 'D') {
                int digit = codec[1] - '0';
                if (digit >= 0 && digit <= 9) {
                    return digit;
                }
            }

            return 0;
        }

        static float[] ReadStaticCurveValue(string codec, int dimension, byte[] raw, int offset) {
            if (dimension <= 0) {
                return Array.Empty<float>();
            }

            if (codec.EndsWith("Constant32f", StringComparison.Ordinal)) {
                int controlsOffset = offset + 4;
                if (controlsOffset + (long)dimension * 4 > raw.Length) {
                    return Array.Empty<float>();
                }

                var value = new float[dimension];
                for (int d = 0; d < dimension; d++) {
                    value[d] = Gr2Loaded.F32(raw, controlsOffset + d * 4);
                }

                return value;
            }

            if (codec == "DaIdentity") {
                if (dimension == 9) {
                    return new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
                }

                if (dimension == 4) {
                    return new float[] { 0, 0, 0, 1 };
                }

                return new float[dimension];
            }

            return Array.Empty<float>();
        }

        static void ReadKnotsControls(Gr2Loaded loaded, Gr2Curve curve, byte[] raw, byte[] fixedBytes, int objectOffset, Dictionary<string, int> offsets) {
            int count = curve.KnotControlCount;
            int dimension = curve.Dimension;
            if (count <= 0 || dimension <= 0 || !offsets.TryGetValue("KnotsControls", out int kcOffset)) {
                return;
            }

            int pointerOffset = objectOffset + kcOffset + 4;
            if (pointerOffset + loaded.PointerSize > fixedBytes.Length) {
                return;
            }

            Gr2Ref? arrayRef = loaded.DecodeFakePointer(loaded.ReadPointer(fixedBytes, pointerOffset, true));
            if (!arrayRef.HasValue) {
                return;
            }

            byte[] array = loaded.Fixed[arrayRef.Value.Section];
            int arrayOffset = arrayRef.Value.Offset;
            switch (curve.Codec) {
                case "D3K16uC16u":
                case "D3I1K16uC16u":
                    DecodeD3K16uC16u(curve, raw, objectOffset, offsets, array, arrayOffset);
                    break;
                case "D4nK8uC7u":
                    DecodeD4nK8uC7u(curve, raw, objectOffset, offsets, array, arrayOffset);
                    break;
                case "D4nK16uC15u":
                    DecodeD4nK16uC15u(curve, raw, objectOffset, offsets, array, arrayOffset);
                    break;
            }
        }

        /// <summary>
        /// D3K16uC16u / D3I1K16uC16u: u16 knots scaled by 1 / OneOverKnotScaleTrunc (a float's high 16 bits), u16
        /// controls dequantised per dimension; the I1 variant has one control per knot broadcast to every dimension.
        /// </summary>
        static void DecodeD3K16uC16u(Gr2Curve curve, byte[] raw, int objectOffset, Dictionary<string, int> offsets, byte[] array, int arrayOffset) {
            bool i1 = curve.Codec == "D3I1K16uC16u";
            int count = curve.KnotControlCount;
            int dimension = curve.Dimension;
            int knotCount = count / (i1 ? 2 : dimension + 1);
            if (knotCount <= 0) {
                return;
            }

            int oneOverOffset = objectOffset + OffsetOr(offsets, "OneOverKnotScaleTrunc", 2);
            int scalesOffset = objectOffset + OffsetOr(offsets, "ControlScales", 4);
            int offsetsOffset = objectOffset + OffsetOr(offsets, "ControlOffsets", 16);
            if (offsetsOffset + dimension * 4 > raw.Length) {
                return;
            }

            float oneOver = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(oneOverOffset)) << 16);
            if (oneOver == 0f) {
                return;
            }

            var controlScales = new float[dimension];
            var controlOffsets = new float[dimension];
            for (int d = 0; d < dimension; d++) {
                controlScales[d] = Gr2Loaded.F32(raw, scalesOffset + d * 4);
                controlOffsets[d] = Gr2Loaded.F32(raw, offsetsOffset + d * 4);
            }

            if (arrayOffset + (long)count * 2 > array.Length) {
                return;
            }

            double knotScale = 1.0 / oneOver;
            var knots = new float[knotCount];
            for (int i = 0; i < knotCount; i++) {
                knots[i] = (float)(knotScale * U16(array, arrayOffset, i));
            }

            var controls = new float[knotCount * dimension];
            for (int i = 0; i < knotCount; i++) {
                for (int d = 0; d < dimension; d++) {
                    int param = i1 ? U16(array, arrayOffset, knotCount + i) : U16(array, arrayOffset, knotCount + i * dimension + d);
                    controls[i * dimension + d] = (float)(controlOffsets[d] + (double)controlScales[d] * param);
                }
            }

            curve.Knots = knots;
            curve.Controls = controls;
        }

        /// <summary>D4nK8uC7u: u8 knots, 3 bytes per quaternion (sign + 7-bit magnitudes), 4th component from |q| = 1.</summary>
        static void DecodeD4nK8uC7u(Gr2Curve curve, byte[] raw, int objectOffset, Dictionary<string, int> offsets, byte[] array, int arrayOffset) {
            int count = curve.KnotControlCount;
            int knotCount = count / 4;
            if (knotCount <= 0) {
                return;
            }

            int scaleEntriesOffset = objectOffset + OffsetOr(offsets, "ScaleOffsetTableEntries", 2);
            int oneOverOffset = objectOffset + OffsetOr(offsets, "OneOverKnotScale", 4);
            if (oneOverOffset + 4 > raw.Length) {
                return;
            }

            float oneOver = Gr2Loaded.F32(raw, oneOverOffset);
            if (oneOver == 0f || arrayOffset + (long)count > array.Length) {
                return;
            }

            double knotScale = 1.0 / oneOver;
            var knots = new float[knotCount];
            for (int i = 0; i < knotCount; i++) {
                knots[i] = (float)(knotScale * array[arrayOffset + i]);
            }

            Span<float> scales = stackalloc float[4];
            Span<float> tableOffsets = stackalloc float[4];
            QuaternionScalesOffsets(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(scaleEntriesOffset)), 127.0, scales, tableOffsets);
            var controls = new float[knotCount * 4];
            int controlBase = arrayOffset + knotCount;
            Span<int> source = stackalloc int[3];
            for (int i = 0; i < knotCount; i++) {
                source[0] = array[controlBase + i * 3];
                source[1] = array[controlBase + i * 3 + 1];
                source[2] = array[controlBase + i * 3 + 2];
                bool missingNegative = (source[0] & 0x80) != 0;
                int missingIndex = (((source[1] >> 6) & 0x2) | (source[2] >> 7)) & 0x3;
                DecodeQuaternion(controls, i * 4, source, 0x7F, missingIndex, missingNegative, scales, tableOffsets);
            }

            curve.Knots = knots;
            curve.Controls = controls;
        }

        /// <summary>D4nK16uC15u: u16 knots, 3 u16 per quaternion (sign + 15-bit magnitudes).</summary>
        static void DecodeD4nK16uC15u(Gr2Curve curve, byte[] raw, int objectOffset, Dictionary<string, int> offsets, byte[] array, int arrayOffset) {
            int count = curve.KnotControlCount;
            int knotCount = count / 4;
            if (knotCount <= 0) {
                return;
            }

            int scaleEntriesOffset = objectOffset + OffsetOr(offsets, "ScaleOffsetTableEntries", 2);
            int oneOverOffset = objectOffset + OffsetOr(offsets, "OneOverKnotScale", 4);
            if (oneOverOffset + 4 > raw.Length) {
                return;
            }

            float oneOver = Gr2Loaded.F32(raw, oneOverOffset);
            if (oneOver == 0f || arrayOffset + (long)count * 2 > array.Length) {
                return;
            }

            double knotScale = 1.0 / oneOver;
            var knots = new float[knotCount];
            for (int i = 0; i < knotCount; i++) {
                knots[i] = (float)(knotScale * U16(array, arrayOffset, i));
            }

            Span<float> scales = stackalloc float[4];
            Span<float> tableOffsets = stackalloc float[4];
            QuaternionScalesOffsets(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(scaleEntriesOffset)), 32767.0, scales, tableOffsets);
            var controls = new float[knotCount * 4];
            Span<int> source = stackalloc int[3];
            for (int i = 0; i < knotCount; i++) {
                source[0] = U16(array, arrayOffset, knotCount + i * 3);
                source[1] = U16(array, arrayOffset, knotCount + i * 3 + 1);
                source[2] = U16(array, arrayOffset, knotCount + i * 3 + 2);
                bool missingNegative = (source[0] & 0x8000) != 0;
                int missingIndex = (((source[1] >> 14) & 0x2) | (source[2] >> 15)) & 0x3;
                DecodeQuaternion(controls, i * 4, source, 0x7FFF, missingIndex, missingNegative, scales, tableOffsets);
            }

            curve.Knots = knots;
            curve.Controls = controls;
        }

        static void QuaternionScalesOffsets(int entries, double quantizedMax, Span<float> scales, Span<float> offsets) {
            uint bits = (uint)entries;
            for (int i = 0; i < 4; i++) {
                int index = (int)(bits & 0xF);
                bits >>= 4;
                scales[i] = (float)(QuaternionScales[index] / quantizedMax);
                offsets[i] = (float)QuaternionOffsets[index];
            }
        }

        static void DecodeQuaternion(float[] controls, int b, ReadOnlySpan<int> source, int magnitudeMask, int missingIndex, bool missingNegative, ReadOnlySpan<float> scales, ReadOnlySpan<float> offsets) {
            Span<double> result = stackalloc double[4];
            result.Clear();
            int dst = missingIndex;
            double summedSq = 0.0;
            for (int s = 0; s < 3; s++) {
                dst = (dst + 1) & 0x3;
                double v = offsets[dst] + (double)scales[dst] * (source[s] & magnitudeMask);
                result[dst] = v;
                summedSq += v * v;
            }

            double missing = Math.Sqrt(Math.Max(0.0, 1.0 - summedSq));
            result[missingIndex] = missingNegative ? -missing : missing;
            for (int k = 0; k < 4; k++) {
                controls[b + k] = (float)result[k];
            }
        }

        static int U16(byte[] array, int arrayOffset, int index) {
            return BinaryPrimitives.ReadUInt16LittleEndian(array.AsSpan(arrayOffset + index * 2));
        }
    }
}
