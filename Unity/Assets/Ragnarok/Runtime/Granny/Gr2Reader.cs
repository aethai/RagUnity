// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/Granny.js (parseAll), src/GrannySkeleton.js, src/GrannyModel.js (themselves ports of
// Rasetsuu/blendergranny, MIT). See LICENSE-granny-ro-js.txt.

using System;
using System.Collections.Generic;

namespace Ragnarok.Granny {
    /// <summary>
    /// Pure C# .gr2 (Granny 2, file format 6/7) reader: Oodle0 decompression, pointer fixups, type-tree walk, and
    /// skeleton / mesh / material / texture / model / animation extraction. Thread-safe (no shared state); meant to
    /// run on a worker thread at load time.
    /// </summary>
    public static partial class Gr2Reader {
        // Default caps of the JS extractors (parseAll with no options).
        const int MaxSkeletons = 16;
        const int MaxBones = 4096;
        const int MaxModels = 64;
        const int MaxBindings = 256;

        /// <summary>Parses everything in one pass over the decompressed sections (granny-ro-js parseAll).</summary>
        /// <exception cref="Gr2FormatException">Non-Granny input, unsupported compression, corrupt section data.</exception>
        public static Gr2Content Parse(byte[] data) {
            return Parse(data, true);
        }

        /// <summary>
        /// Like <see cref="Parse(byte[])"/>; with <paramref name="decodeTextures"/> false the texture records are listed
        /// with <see cref="Gr2Texture.Rgba"/> null and no decode cost (animation-only consumers).
        /// A texture that fails to decode never fails the parse: it gets <see cref="Gr2Texture.DecodeError"/> instead.
        /// </summary>
        public static Gr2Content Parse(byte[] data, bool decodeTextures) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            Gr2FileInfo file = Gr2File.Parse(data);
            var loaded = new Gr2Loaded(data, file);
            Gr2Material[] materials = WalkRootMaterials(loaded, 4096, out Dictionary<long, Gr2Material> materialCache);
            return new Gr2Content {
                File = file,
                Skeletons = ExtractSkeletons(loaded),
                Meshes = ExtractMeshes(loaded, materialCache),
                Materials = materials,
                Textures = ExtractTextures(loaded, decodeTextures),
                Models = ExtractModels(loaded),
                Animations = ExtractAnimations(loaded)
            };
        }

        /// <summary>Decompresses the sections only (diagnostics / tooling); index = section index.</summary>
        public static byte[][] DecompressSections(byte[] data) {
            Gr2FileInfo file = Gr2File.Parse(data);
            var result = new byte[file.Sections.Length][];
            for (int i = 0; i < result.Length; i++) {
                result[i] = Gr2File.Decompress(file.Sections[i], Gr2File.SectionBytes(data, file.Sections[i]));
            }

            return result;
        }

        static Gr2Object ParseRoot(Gr2Loaded loaded, int maxArrayRefs) {
            return loaded.ParseObject(loaded.ParseTypeTree(loaded.File.RootType), loaded.File.RootObject, maxArrayRefs);
        }

        // --- skeletons --------------------------------------------------------------------------------------------

        static Gr2Skeleton[] ExtractSkeletons(Gr2Loaded loaded) {
            Gr2Field skeletonField = ParseRoot(loaded, MaxSkeletons)["Skeletons"];
            if (skeletonField == null || !skeletonField.ReferenceType.HasValue || skeletonField.ElementRefs == null || skeletonField.ElementRefs.Length == 0) {
                return Array.Empty<Gr2Skeleton>();
            }

            Gr2TypeMember[] skeletonType = loaded.ParseTypeTree(skeletonField.ReferenceType.Value);
            int limit = Math.Min(skeletonField.ElementRefs.Length, MaxSkeletons);
            var skeletons = new List<Gr2Skeleton>(limit);
            for (int i = 0; i < limit; i++) {
                Gr2Object fields = loaded.ParseObject(skeletonType, skeletonField.ElementRefs[i], MaxBones);
                Gr2Field boneField = fields["Bones"];
                Gr2ArrayObject[] boneObjects = boneField == null
                    ? Array.Empty<Gr2ArrayObject>()
                    : loaded.ReadReferenceArrayObjects(boneField.Target, boneField.Count, boneField.ReferenceType, MaxBones);
                var bones = new Gr2Bone[boneObjects.Length];
                for (int b = 0; b < bones.Length; b++) {
                    bones[b] = ReadBone(loaded, b, boneObjects[b]);
                }

                skeletons.Add(new Gr2Skeleton {
                    Name = fields.NonEmptyString("Name") ?? "Skeleton_" + skeletons.Count,
                    LodType = (int)fields.NumberOr("LODType", 0),
                    Bones = bones
                });
            }

            return skeletons.ToArray();
        }

        static Gr2Bone ReadBone(Gr2Loaded loaded, int index, Gr2ArrayObject boneObject) {
            Gr2Object fields = boneObject.Fields;
            int transformOffset = fields["Transform"]?.Offset ?? 0;
            int inverseOffset = fields["InverseWorldTransform"]?.Offset ?? 0;
            return new Gr2Bone {
                Index = index,
                Name = fields.NonEmptyString("Name") ?? "Bone_" + index,
                ParentIndex = (int)fields.NumberOr("ParentIndex", -1),
                Transform = loaded.ReadTransform(boneObject.Ref.Section, transformOffset),
                InverseWorldTransform = loaded.ReadFloats(boneObject.Ref.Section, inverseOffset, 16)
            };
        }

        // --- models -----------------------------------------------------------------------------------------------

        static Gr2Model[] ExtractModels(Gr2Loaded loaded) {
            Gr2Object root = ParseRoot(loaded, 256);
            Gr2Field modelField = root["Models"];
            if (modelField == null || !modelField.ReferenceType.HasValue || modelField.ElementRefs == null || modelField.ElementRefs.Length == 0) {
                return Array.Empty<Gr2Model>();
            }

            Dictionary<long, int> skeletonIndex = BuildRefIndex(root["Skeletons"]);
            Dictionary<long, int> meshIndex = BuildRefIndex(root["Meshes"]);
            Gr2TypeMember[] modelType = loaded.ParseTypeTree(modelField.ReferenceType.Value);
            int limit = Math.Min(modelField.ElementRefs.Length, MaxModels);
            var models = new List<Gr2Model>(limit);
            for (int i = 0; i < limit; i++) {
                Gr2Ref modelRef = modelField.ElementRefs[i];
                Gr2Object fields = loaded.ParseObject(modelType, modelRef);
                Gr2Field skeleton = fields["Skeleton"];
                Gr2Field placement = fields["InitialPlacement"];
                models.Add(new Gr2Model {
                    Name = fields.NonEmptyString("Name") ?? "Model_" + models.Count,
                    SkeletonIndex = LookupIndex(skeletonIndex, skeleton?.Target),
                    InitialPlacement = placement != null && placement.ValueKind == Gr2ValueKind.Transform
                        ? placement.Transform
                        : loaded.ReadTransform(modelRef.Section, placement?.Offset ?? 0),
                    MeshIndices = ReadModelMeshBindings(loaded, fields, meshIndex)
                });
            }

            return models.ToArray();
        }

        static int[] ReadModelMeshBindings(Gr2Loaded loaded, Gr2Object modelFields, Dictionary<long, int> meshIndex) {
            Gr2Field bindings = modelFields["MeshBindings"];
            if (bindings == null || !bindings.ReferenceType.HasValue || !bindings.Target.HasValue || bindings.Count <= 0) {
                return Array.Empty<int>();
            }

            Gr2ArrayObject[] objects = loaded.ReadReferenceArrayObjects(bindings.Target, bindings.Count, bindings.ReferenceType, MaxBindings);
            var result = new int[objects.Length];
            for (int i = 0; i < objects.Length; i++) {
                result[i] = LookupIndex(meshIndex, objects[i].Fields["Mesh"]?.Target);
            }

            return result;
        }

        static Dictionary<long, int> BuildRefIndex(Gr2Field field) {
            var index = new Dictionary<long, int>();
            if (field?.ElementRefs != null) {
                for (int i = 0; i < field.ElementRefs.Length; i++) {
                    index[field.ElementRefs[i].Key] = i;
                }
            }

            return index;
        }

        static int LookupIndex(Dictionary<long, int> index, Gr2Ref? target) {
            return target.HasValue && index.TryGetValue(target.Value.Key, out int value) ? value : -1;
        }
    }
}
