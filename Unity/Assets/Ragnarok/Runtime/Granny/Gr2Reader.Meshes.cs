// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyMesh.js (itself a port of Rasetsuu/blendergranny, MIT). See LICENSE-granny-ro-js.txt.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Ragnarok.Granny {
    public static partial class Gr2Reader {
        const int MaxMeshes = 32;

        static Gr2Mesh[] ExtractMeshes(Gr2Loaded loaded, Dictionary<long, Gr2Material> materialCache) {
            Gr2TypeMember[] rootType = loaded.ParseTypeTree(loaded.File.RootType);
            Gr2TypeMember meshMember = null;
            for (int i = 0; i < rootType.Length; i++) {
                if (rootType[i].Name == "Meshes") {
                    meshMember = rootType[i];
                    break;
                }
            }

            if (meshMember == null || !meshMember.ReferenceType.HasValue) {
                return Array.Empty<Gr2Mesh>();
            }

            Gr2Ref[] elementRefs = loaded.ParseObject(rootType, loaded.File.RootObject, MaxMeshes)["Meshes"]?.ElementRefs;
            if (elementRefs == null || elementRefs.Length == 0) {
                return Array.Empty<Gr2Mesh>();
            }

            Gr2TypeMember[] meshType = loaded.ParseTypeTree(meshMember.ReferenceType.Value);
            int limit = Math.Min(elementRefs.Length, MaxMeshes);
            var meshes = new List<Gr2Mesh>(limit);
            for (int i = 0; i < limit; i++) {
                Gr2Object meshFields = loaded.ParseObject(meshType, elementRefs[i]);
                Gr2Object vertexData = HydrateSub(loaded, meshFields, meshType, "PrimaryVertexData");
                Gr2Object topology = HydrateSub(loaded, meshFields, meshType, "PrimaryTopology");
                Gr2Mesh mesh = ReadMesh(loaded, meshFields.NonEmptyString("Name") ?? "Mesh_" + i, meshFields, vertexData, topology, materialCache);
                if (mesh != null) {
                    meshes.Add(mesh);
                }
            }

            return meshes.ToArray();
        }

        static Gr2Object HydrateSub(Gr2Loaded loaded, Gr2Object meshFields, Gr2TypeMember[] meshType, string fieldName) {
            Gr2Ref? target = meshFields[fieldName]?.Target;
            Gr2TypeMember member = null;
            for (int i = 0; i < meshType.Length; i++) {
                if (meshType[i].Name == fieldName) {
                    member = meshType[i];
                }
            }

            if (!target.HasValue || member == null || !member.ReferenceType.HasValue) {
                return null;
            }

            return loaded.ParseObject(loaded.ParseTypeTree(member.ReferenceType.Value), target.Value);
        }

        static Gr2Mesh ReadMesh(Gr2Loaded loaded, string name, Gr2Object meshFields, Gr2Object vertexData, Gr2Object topology, Dictionary<long, Gr2Material> materialCache) {
            Gr2Field vertices = vertexData?["Vertices"];
            if (vertices == null || !vertices.Target.HasValue || !vertices.VariantType.HasValue || vertices.Count <= 0) {
                return null;
            }

            int vertexCount = (int)vertices.Count;
            Gr2TypeMember[] vertexType = loaded.ParseTypeTree(vertices.VariantType.Value);
            Gr2VertexComponent[] components = VertexComponents(vertexType);
            int stride = VertexStride(components);
            Gr2Ref vertexRef = vertices.Target.Value;
            byte[] section = loaded.Original[vertexRef.Section];
            long byteCount = (long)vertexCount * stride;
            if (vertexRef.Offset < 0 || vertexRef.Offset + byteCount > section.Length) {
                return null;
            }

            var vertexBytes = new ReadOnlySpan<byte>(section, vertexRef.Offset, (int)byteCount);

            // Indices16 wins when it has entries, then Indices; 32-bit only for the "Indices" member.
            Gr2Field indexField = null;
            bool wide = false;
            if (topology != null) {
                Gr2Field indices16 = topology["Indices16"];
                Gr2Field indices32 = topology["Indices"];
                if (indices16 != null && indices16.Count > 0) {
                    indexField = indices16;
                } else if (indices32 != null && indices32.Count > 0) {
                    indexField = indices32;
                    wide = true;
                } else if (indices16 != null) {
                    indexField = indices16;
                } else if (indices32 != null) {
                    indexField = indices32;
                    wide = true;
                }
            }

            string[] boneBindings = ReadBoneBindings(loaded, meshFields);
            var mesh = new Gr2Mesh {
                Name = name,
                VertexCount = vertexCount,
                VertexStride = stride,
                Components = components,
                Indices = ReadIndices(loaded, indexField?.Target, indexField?.Count ?? 0, wide),
                BoneBindings = boneBindings,
                Materials = ReadMaterialBindings(loaded, meshFields, materialCache),
                TriangleGroups = topology != null ? ReadTriangleGroups(loaded, topology) : Array.Empty<Gr2TriangleGroup>(),
                Positions = ReadFloatComponent(vertexBytes, vertexCount, stride, components, "Position", 3, false),
                Normals = ReadFloatComponent(vertexBytes, vertexCount, stride, components, "Normal", 3, false),
                UVs = ReadFloatComponent(vertexBytes, vertexCount, stride, components, "TextureCoordinates", 2, true)
            };
            ReadVertexWeights(mesh, vertexBytes, components, boneBindings.Length);
            return mesh;
        }

        static Gr2VertexComponent[] VertexComponents(Gr2TypeMember[] members) {
            var components = new Gr2VertexComponent[members.Length];
            int offset = 0;
            for (int i = 0; i < members.Length; i++) {
                Gr2TypeMember member = members[i];
                components[i] = new Gr2VertexComponent {
                    Name = member.Name,
                    Offset = offset,
                    Width = member.ArrayWidth,
                    MemberType = member.MemberType
                };
                offset += ComponentSize(member.MemberType, member.ArrayWidth);
            }

            return components;
        }

        static int VertexStride(Gr2VertexComponent[] components) {
            int max = 0;
            for (int i = 0; i < components.Length; i++) {
                int end = components[i].Offset + ComponentSize(components[i].MemberType, components[i].Width);
                if (end > max) {
                    max = end;
                }
            }

            return max;
        }

        static int ComponentSize(int memberType, int width) {
            switch (memberType) {
                case Gr2MemberType.Int16:
                case Gr2MemberType.UInt16:
                case Gr2MemberType.BinormalInt16:
                case Gr2MemberType.NormalUInt16:
                    return 2 * width;
                case Gr2MemberType.Int8:
                case Gr2MemberType.UInt8:
                case Gr2MemberType.BinormalInt8:
                case Gr2MemberType.NormalUInt8:
                    return width;
                default:
                    return 4 * width;
            }
        }

        /// <summary>First real32 component named <paramref name="name"/> (or prefixed by it) with at least <paramref name="width"/> lanes.</summary>
        static float[] ReadFloatComponent(ReadOnlySpan<byte> data, int vertexCount, int stride, Gr2VertexComponent[] components, string name, int width, bool prefix) {
            Gr2VertexComponent component = null;
            for (int i = 0; i < components.Length; i++) {
                Gr2VertexComponent c = components[i];
                if (c.MemberType != Gr2MemberType.Real32 || c.Width < width) {
                    continue;
                }

                if (prefix ? c.Name.StartsWith(name, StringComparison.Ordinal) : c.Name == name) {
                    component = c;
                    break;
                }
            }

            if (component == null) {
                return null;
            }

            int available = 0;
            for (int v = 0; v < vertexCount; v++) {
                if ((long)v * stride + component.Offset + width * 4 > data.Length) {
                    break;
                }

                available++;
            }

            var result = new float[available * width];
            for (int v = 0; v < available; v++) {
                int b = v * stride + component.Offset;
                for (int k = 0; k < width; k++) {
                    result[v * width + k] = Gr2Loaded.F32(data, b + k * 4);
                }
            }

            return result;
        }

        static int[] ReadIndices(Gr2Loaded loaded, Gr2Ref? reference, long count, bool wide) {
            if (!reference.HasValue || count <= 0) {
                return Array.Empty<int>();
            }

            byte[] data = loaded.Original[reference.Value.Section];
            int offset = reference.Value.Offset;
            long bytes = count * (wide ? 4 : 2);
            if (offset < 0 || offset + bytes > data.Length) {
                return Array.Empty<int>();
            }

            var result = new int[count];
            for (int i = 0; i < result.Length; i++) {
                result[i] = wide
                    ? unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + i * 4)))
                    : BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + i * 2));
            }

            return result;
        }

        static void ReadVertexWeights(Gr2Mesh mesh, ReadOnlySpan<byte> data, Gr2VertexComponent[] components, int bindingCount) {
            Gr2VertexComponent weights = null;
            Gr2VertexComponent indices = null;
            for (int i = 0; i < components.Length; i++) {
                if (components[i].Name == "BoneWeights") {
                    weights = components[i];
                } else if (components[i].Name == "BoneIndices") {
                    indices = components[i];
                }
            }

            if (weights == null || indices == null || bindingCount <= 0) {
                return;
            }

            int lanes = Math.Min(weights.Width, indices.Width);
            int vertexCount = mesh.VertexCount;
            int stride = mesh.VertexStride;
            mesh.InfluencesPerVertex = lanes;
            mesh.BoneIndices = new int[vertexCount * lanes];
            mesh.BoneWeights = new float[vertexCount * lanes];
            for (int v = 0; v < vertexCount; v++) {
                int b = v * stride;
                for (int lane = 0; lane < lanes; lane++) {
                    double weight = ReadWeightValue(data, b + weights.Offset, lane, weights.MemberType);
                    long boneIndex = ReadIntValue(data, b + indices.Offset, lane, indices.MemberType);
                    if (weight <= 0.0 || boneIndex < 0 || boneIndex >= bindingCount) {
                        continue;
                    }

                    mesh.BoneIndices[v * lanes + lane] = (int)boneIndex;
                    mesh.BoneWeights[v * lanes + lane] = (float)weight;
                }
            }
        }

        static double ReadWeightValue(ReadOnlySpan<byte> data, int offset, int lane, int memberType) {
            switch (memberType) {
                case Gr2MemberType.Real32: {
                    int start = offset + lane * 4;
                    if (start + 4 > data.Length) {
                        return 0.0;
                    }

                    double value = Gr2Loaded.F32(data, start);
                    return value <= 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
                }
                case Gr2MemberType.UInt16:
                case Gr2MemberType.NormalUInt16: {
                    int start = offset + lane * 2;
                    if (start + 2 > data.Length) {
                        return 0.0;
                    }

                    ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(start));
                    return memberType == Gr2MemberType.NormalUInt16 ? value / 65535.0 : value;
                }
                case Gr2MemberType.UInt8:
                case Gr2MemberType.NormalUInt8: {
                    int start = offset + lane;
                    if (start >= data.Length) {
                        return 0.0;
                    }

                    byte value = data[start];
                    return memberType == Gr2MemberType.NormalUInt8 ? value / 255.0 : value;
                }
                default:
                    return 0.0;
            }
        }

        static long ReadIntValue(ReadOnlySpan<byte> data, int offset, int lane, int memberType) {
            switch (memberType) {
                case Gr2MemberType.Int32:
                case Gr2MemberType.UInt32: {
                    int start = offset + lane * 4;
                    if (start + 4 > data.Length) {
                        return -1;
                    }

                    return memberType == Gr2MemberType.Int32
                        ? BinaryPrimitives.ReadInt32LittleEndian(data.Slice(start))
                        : BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(start));
                }
                case Gr2MemberType.Int16:
                case Gr2MemberType.UInt16: {
                    int start = offset + lane * 2;
                    if (start + 2 > data.Length) {
                        return -1;
                    }

                    return memberType == Gr2MemberType.Int16
                        ? BinaryPrimitives.ReadInt16LittleEndian(data.Slice(start))
                        : BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(start));
                }
                case Gr2MemberType.Int8:
                case Gr2MemberType.UInt8: {
                    int start = offset + lane;
                    if (start >= data.Length) {
                        return -1;
                    }

                    return memberType == Gr2MemberType.Int8 ? unchecked((sbyte)data[start]) : data[start];
                }
                default:
                    return -1;
            }
        }

        static string[] ReadBoneBindings(Gr2Loaded loaded, Gr2Object meshFields) {
            Gr2Field field = meshFields["BoneBindings"];
            if (field == null) {
                return Array.Empty<string>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(field.Target, field.Count, field.ReferenceType, MaxBones);
            var names = new string[objects.Length];
            for (int i = 0; i < names.Length; i++) {
                names[i] = objects[i].Fields.NonEmptyString("BoneName") ?? "Bone_" + i;
            }

            return names;
        }

        static Gr2Material[] ReadMaterialBindings(Gr2Loaded loaded, Gr2Object meshFields, Dictionary<long, Gr2Material> materialCache) {
            Gr2Field field = meshFields["MaterialBindings"];
            if (field == null) {
                return Array.Empty<Gr2Material>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(field.Target, field.Count, field.ReferenceType, MaxBones);
            var materials = new Gr2Material[objects.Length];
            for (int i = 0; i < materials.Length; i++) {
                Gr2Ref? target = objects[i].Fields["Material"]?.Target;
                Gr2Material material = null;
                if (target.HasValue) {
                    materialCache.TryGetValue(target.Value.Key, out material);
                }

                materials[i] = material ?? new Gr2Material { Index = i, Name = "Material_" + i, TextureFile = "" };
            }

            return materials;
        }

        static Gr2TriangleGroup[] ReadTriangleGroups(Gr2Loaded loaded, Gr2Object topology) {
            Gr2Field field = topology["Groups"];
            if (field == null) {
                return Array.Empty<Gr2TriangleGroup>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(field.Target, field.Count, field.ReferenceType, 4096);
            var groups = new Gr2TriangleGroup[objects.Length];
            for (int i = 0; i < groups.Length; i++) {
                Gr2Object fields = objects[i].Fields;
                groups[i] = new Gr2TriangleGroup {
                    MaterialIndex = (int)fields.NumberOr("MaterialIndex", 0),
                    TriFirst = (int)fields.NumberOr("TriFirst", 0),
                    TriCount = (int)fields.NumberOr("TriCount", 0)
                };
            }

            return groups;
        }

        // --- materials --------------------------------------------------------------------------------------------

        /// <summary>
        /// Walks root.Materials once: the ordered top-level list plus a <c>[section, offset]</c> cache the mesh
        /// bindings resolve through. A material without its own Texture takes the first textured material in Maps.
        /// </summary>
        static Gr2Material[] WalkRootMaterials(Gr2Loaded loaded, int maxMaterials, out Dictionary<long, Gr2Material> cache) {
            cache = new Dictionary<long, Gr2Material>();
            Gr2Field field = ParseRoot(loaded, maxMaterials)["Materials"];
            if (field == null) {
                return Array.Empty<Gr2Material>();
            }

            Gr2Ref[] refs = field.ElementRefs ?? Array.Empty<Gr2Ref>();
            int limit = Math.Min(refs.Length, maxMaterials);
            var materials = new Gr2Material[limit];
            for (int i = 0; i < limit; i++) {
                materials[i] = ReadMaterial(loaded, refs[i], field.ReferenceType, i, cache, new HashSet<long>());
            }

            return materials;
        }

        static Gr2Material ReadMaterial(Gr2Loaded loaded, Gr2Ref reference, Gr2Ref? materialType, int index, Dictionary<long, Gr2Material> cache, HashSet<long> seen) {
            long key = reference.Key;
            if (cache.TryGetValue(key, out Gr2Material cached)) {
                return cached;
            }

            if (!materialType.HasValue || seen.Contains(key)) {
                return new Gr2Material { Index = index, Name = "Material_" + index, TextureFile = "" };
            }

            Gr2Object fields = loaded.ParseObject(loaded.ParseTypeTree(materialType.Value), reference, 16);
            var material = new Gr2Material {
                Index = index,
                Name = fields.NonEmptyString("Name") ?? "Material_" + index,
                TextureFile = ""
            };
            if (!ReadTextureField(loaded, fields["Texture"], material)) {
                var childSeen = new HashSet<long>(seen) { key };
                ReadFirstMapTexture(loaded, fields["Maps"], materialType, cache, childSeen, material);
            }

            cache[key] = material;
            return material;
        }

        static bool ReadTextureField(Gr2Loaded loaded, Gr2Field textureField, Gr2Material material) {
            if (textureField == null || !textureField.Target.HasValue || !textureField.ReferenceType.HasValue) {
                return false;
            }

            Gr2Object fields = loaded.ParseObject(loaded.ParseTypeTree(textureField.ReferenceType.Value), textureField.Target.Value, 8);
            if (fields.TryNumber("Width", out double width) && fields.TryNumber("Height", out double height)) {
                material.TextureWidth = (int)width;
                material.TextureHeight = (int)height;
            }

            Gr2Field fileName = fields["FromFileName"];
            material.TextureFile = fileName != null && fileName.ValueKind == Gr2ValueKind.String ? fileName.String : "";
            return true;
        }

        static void ReadFirstMapTexture(Gr2Loaded loaded, Gr2Field mapsField, Gr2Ref? materialType, Dictionary<long, Gr2Material> cache, HashSet<long> seen, Gr2Material material) {
            if (mapsField == null) {
                return;
            }

            Gr2ArrayObject[] maps = loaded.ReadReferenceArrayObjects(mapsField.Target, mapsField.Count, mapsField.ReferenceType, 16);
            for (int i = 0; i < maps.Length; i++) {
                Gr2Ref? mapRef = maps[i].Fields["Map"]?.Target;
                if (!mapRef.HasValue) {
                    continue;
                }

                Gr2Material mapped = ReadMaterial(loaded, mapRef.Value, materialType, -1, cache, seen);
                if (mapped.TextureFile.Length > 0) {
                    material.TextureFile = mapped.TextureFile;
                    material.TextureWidth = mapped.TextureWidth;
                    material.TextureHeight = mapped.TextureHeight;
                    return;
                }
            }
        }
    }
}
