// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyTexture.js. See LICENSE-granny-ro-js.txt.

using System;
using System.Collections.Generic;

namespace Ragnarok.Granny {
    public static partial class Gr2Reader {
        const int EncodingRaw = 1;
        const int EncodingS3tc = 2;
        const int EncodingIgc = 3;

        /// <summary>root.Textures -> Images -> MIPLevels -> Pixels, one record per MIP, decoded to RGBA8.</summary>
        static Gr2Texture[] ExtractTextures(Gr2Loaded loaded, bool decode) {
            const int maxTextures = 256;
            const int maxImages = 8;
            const int maxMips = 32;

            Gr2Field textureField = ParseRoot(loaded, maxTextures)["Textures"];
            if (textureField == null || !textureField.ReferenceType.HasValue || textureField.ElementRefs == null || textureField.ElementRefs.Length == 0) {
                return Array.Empty<Gr2Texture>();
            }

            Gr2Ref textureType = textureField.ReferenceType.Value;
            Gr2TypeMember[] textureMembers = loaded.ParseTypeTree(textureType);
            var records = new List<Gr2Texture>();
            int textureCount = Math.Min(textureField.ElementRefs.Length, maxTextures);
            for (int ti = 0; ti < textureCount; ti++) {
                Gr2Ref textureRef = textureField.ElementRefs[ti];
                Gr2Object fields = loaded.ParseObject(textureMembers, textureRef, 64);
                Gr2Field fromFileName = fields["FromFileName"];
                string fileName = fromFileName != null && fromFileName.ValueKind == Gr2ValueKind.String ? fromFileName.String : "";
                int width = (int)fields.NumberOr("Width", 0);
                int height = (int)fields.NumberOr("Height", 0);
                int encoding = (int)fields.NumberOr("Encoding", 0);
                int subFormat = (int)fields.NumberOr("SubFormat", 0);
                bool hasAlpha = ReadAlphaFromLayout(loaded, textureMembers, fields, textureRef);

                Gr2Field images = fields["Images"];
                Gr2ArrayObject[] imageObjects = images != null && images.ReferenceType.HasValue
                    ? loaded.ReadReferenceArrayObjects(images.Target, images.Count, images.ReferenceType, maxImages)
                    : Array.Empty<Gr2ArrayObject>();
                for (int ii = 0; ii < imageObjects.Length; ii++) {
                    Gr2Field mipsField = imageObjects[ii].Fields["MIPLevels"];
                    Gr2ArrayObject[] mips = mipsField != null && mipsField.ReferenceType.HasValue
                        ? loaded.ReadReferenceArrayObjects(mipsField.Target, mipsField.Count, mipsField.ReferenceType, maxMips)
                        : Array.Empty<Gr2ArrayObject>();
                    for (int mi = 0; mi < mips.Length; mi++) {
                        // The 2002 RO schema names the array "Pixels", the later SDK "PixelBytes".
                        Gr2Field pixelField = mips[mi].Fields["Pixels"] ?? mips[mi].Fields["PixelBytes"];
                        var record = new Gr2Texture {
                            TextureIndex = ti,
                            ImageIndex = ii,
                            MipIndex = mi,
                            Name = fileName.Length > 0 ? fileName : "tex" + ti,
                            FromFileName = fileName,
                            Width = width,
                            Height = height,
                            Encoding = encoding,
                            SubFormat = subFormat,
                            HasAlpha = hasAlpha
                        };
                        if (decode) {
                            DecodeRecord(loaded, record, pixelField);
                        }

                        records.Add(record);
                    }
                }
            }

            return records.ToArray();
        }

        static void DecodeRecord(Gr2Loaded loaded, Gr2Texture record, Gr2Field pixelField) {
            long pixelCount = pixelField?.Count ?? 0;
            Gr2Ref? target = pixelField?.Target;
            byte[] section = null;
            if (target.HasValue && pixelCount > 0) {
                byte[] bytes = loaded.Original[target.Value.Section];
                if (target.Value.Offset >= 0 && target.Value.Offset + pixelCount <= bytes.Length) {
                    section = bytes;
                }
            }

            try {
                if (section == null) {
                    throw new Gr2FormatException("texture pixels unresolved");
                }

                var pixels = new ReadOnlySpan<byte>(section, target.Value.Offset, (int)pixelCount);
                switch (record.Encoding) {
                    case EncodingRaw:
                        record.Rgba = DecodeRaw(pixels, record.Width, record.Height);
                        break;
                    case EncodingIgc:
                        record.Rgba = Gr2IgcDecoder.Decode(pixels, record.Width, record.Height, record.HasAlpha);
                        break;
                    case EncodingS3tc:
                        throw new Gr2FormatException("S3TC textures (encoding 2) are not supported");
                    default:
                        throw new Gr2FormatException("unknown texture encoding " + record.Encoding);
                }
            } catch (Exception e) when (e is Gr2FormatException || e is IndexOutOfRangeException || e is ArgumentException) {
                record.Rgba = null;
                record.DecodeError = e.Message;
            }
        }

        /// <summary>Raw encoding: 32 bpp BGRA (Windows DIB order) swizzled to RGBA.</summary>
        static byte[] DecodeRaw(ReadOnlySpan<byte> pixels, int width, int height) {
            long expected = (long)width * height * 4;
            if (pixels.Length != expected) {
                throw new Gr2FormatException("raw texture byte count " + pixels.Length + " != W*H*4 (" + expected + ")");
            }

            var rgba = new byte[expected];
            for (int i = 0; i < rgba.Length; i += 4) {
                rgba[i] = pixels[i + 2];
                rgba[i + 1] = pixels[i + 1];
                rgba[i + 2] = pixels[i];
                rgba[i + 3] = pixels[i + 3];
            }

            return rgba;
        }

        /// <summary>
        /// Alpha flag from the texture's inline Layout struct: BytesPerPixel 3 = RGB (no A plane in the IGC stream),
        /// anything else / unreadable = RGBA. Deliberate deviation: the JS's Layout lookup reads a property its type
        /// refs don't have and always lands on RGBA, so the six RGB textures of 1_attack.gr2 (A_rag_*.bmp) get stuck
        /// in its decoder; read correctly they decode to proper images. Every other RO texture is RGBA either way.
        /// </summary>
        static bool ReadAlphaFromLayout(Gr2Loaded loaded, Gr2TypeMember[] textureMembers, Gr2Object fields, Gr2Ref textureRef) {
            Gr2Field layoutField = fields["Layout"];
            if (layoutField == null || layoutField.MemberType != Gr2MemberType.Inline) {
                return true;
            }

            Gr2TypeMember layoutMember = null;
            for (int i = 0; i < textureMembers.Length; i++) {
                if (textureMembers[i].Name == "Layout") {
                    layoutMember = textureMembers[i];
                    break;
                }
            }

            if (layoutMember == null || !layoutMember.ReferenceType.HasValue) {
                return true;
            }

            try {
                Gr2Object layout = loaded.ParseObject(loaded.ParseTypeTree(layoutMember.ReferenceType.Value), new Gr2Ref(textureRef.Section, layoutField.Offset));
                return !(layout.TryNumber("BytesPerPixel", out double bpp) && bpp == 3);
            } catch (Gr2FormatException) {
                return true;
            }
        }
    }
}
