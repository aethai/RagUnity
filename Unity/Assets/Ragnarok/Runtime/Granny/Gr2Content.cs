// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: the typedefs of src/GrannySkeleton.js, GrannyMesh.js, GrannyModel.js, GrannyTexture.js, GrannyAnimation.js,
// GrannyTransform.js. See LICENSE-granny-ro-js.txt.

namespace Ragnarok.Granny {
    /// <summary>
    /// Everything <see cref="Gr2Reader.Parse"/> extracts from one .gr2. RO ships model and animations in separate
    /// files (data/model/3dmob/*.gr2 and 3dmob_bone/{job}_{motion}.gr2); every file carries a skeleton, so an
    /// animation file can also be sampled on its own skeleton.
    /// </summary>
    public sealed class Gr2Content {
        public Gr2FileInfo File;
        public Gr2Skeleton[] Skeletons;
        public Gr2Mesh[] Meshes;
        /// <summary>Top-level <c>root.Materials</c> in source order.</summary>
        public Gr2Material[] Materials;
        /// <summary>One entry per (texture, image, MIP) record.</summary>
        public Gr2Texture[] Textures;
        public Gr2Model[] Models;
        public Gr2Animation[] Animations;
    }

    /// <summary>
    /// Granny's 68-byte Transform. ScaleShear is the 3x3 matrix in on-disk order; <see cref="Gr2PoseSampler"/> lifts
    /// ScaleShear r0, r1, r2 into row r of the upper-left 3x3, i.e. Mlocal = T * R(quat) * S.
    /// </summary>
    public struct Gr2Transform {
        /// <summary>Granny TRANSFORM_FLAGS (HAS_POSITION / HAS_ORIENTATION / HAS_SCALESHEAR); informational.</summary>
        public uint Flags;
        public float PositionX, PositionY, PositionZ;
        /// <summary>Rotation quaternion (x, y, z, w).</summary>
        public float OrientationX, OrientationY, OrientationZ, OrientationW;
        public float ScaleShear00, ScaleShear01, ScaleShear02;
        public float ScaleShear10, ScaleShear11, ScaleShear12;
        public float ScaleShear20, ScaleShear21, ScaleShear22;

        public static readonly Gr2Transform Identity = new Gr2Transform {
            OrientationW = 1f,
            ScaleShear00 = 1f,
            ScaleShear11 = 1f,
            ScaleShear22 = 1f
        };
    }

    public sealed class Gr2Skeleton {
        public string Name;
        public int LodType;
        /// <summary>Bones in source order; a parent always precedes its children.</summary>
        public Gr2Bone[] Bones;
    }

    public sealed class Gr2Bone {
        public int Index;
        public string Name;
        /// <summary>Parent bone index, negative for a root.</summary>
        public int ParentIndex;
        /// <summary>Bind-pose local transform relative to the parent.</summary>
        public Gr2Transform Transform;
        /// <summary>
        /// Inverse bind-pose world matrix, 16 floats as stored on disk, used directly as a column-major 4x4 like the
        /// <see cref="Gr2PoseSampler"/> matrices (translation in 12..14). Where a skeleton's bind transforms agree with
        /// these, the bind-pose skinning matrices are identity (to ~1e-5); several RO files (kguardian90_7 and the
        /// 7_* / 9_* motions, the treasure box root) carry inverse matrices that don't match their bind transforms.
        /// Null when the file elides it (the pose then uses identity).
        /// </summary>
        public float[] InverseWorldTransform;
    }

    /// <summary>One declared component of the vertex layout (Position, Normal, BoneWeights...).</summary>
    public sealed class Gr2VertexComponent {
        public string Name;
        /// <summary>Byte offset inside one vertex.</summary>
        public int Offset;
        public int Width;
        /// <summary>Granny MEMBER_TYPE (10 = real32, 12 = uint8, 14 = normal_uint8...).</summary>
        public int MemberType;
    }

    public sealed class Gr2Mesh {
        public string Name;
        public int VertexCount;
        /// <summary>Bytes per vertex in the source buffer.</summary>
        public int VertexStride;
        public Gr2VertexComponent[] Components;
        /// <summary>xyz per vertex (VertexCount * 3), null when the layout has no real32 Position.</summary>
        public float[] Positions;
        /// <summary>xyz per vertex, null when absent.</summary>
        public float[] Normals;
        /// <summary>
        /// uv per vertex (VertexCount * 2) from the first real32 <c>TextureCoordinates*</c> component, null when absent.
        /// Direct3D convention: v indexes the rows of <see cref="Gr2Texture.Rgba"/> from the top (row = v * Height).
        /// </summary>
        public float[] UVs;
        /// <summary>Triangle list (16-bit Indices16 or 32-bit Indices on disk).</summary>
        public int[] Indices;
        /// <summary>Bone names this mesh skins to; <see cref="BoneIndices"/> index into this table, not the skeleton.</summary>
        public string[] BoneBindings;
        /// <summary>Influence slots per vertex in <see cref="BoneIndices"/> / <see cref="BoneWeights"/> (0 when not skinned).</summary>
        public int InfluencesPerVertex;
        /// <summary>
        /// VertexCount * InfluencesPerVertex binding indices. A slot whose weight is not positive or whose index is
        /// outside <see cref="BoneBindings"/> is stored as index 0 / weight 0 (the JS drops it from the list).
        /// </summary>
        public int[] BoneIndices;
        /// <summary>
        /// Weights per slot: normal_uint8 / normal_uint16 normalised to [0, 1], real32 clamped to [0, 1], plain uint
        /// formats left raw (as in the JS).
        /// </summary>
        public float[] BoneWeights;
        /// <summary>Material per MaterialBindings slot; <see cref="Gr2TriangleGroup.MaterialIndex"/> indexes this.</summary>
        public Gr2Material[] Materials;
        public Gr2TriangleGroup[] TriangleGroups;
    }

    public struct Gr2TriangleGroup {
        public int MaterialIndex;
        public int TriFirst;
        public int TriCount;
    }

    public sealed class Gr2Material {
        /// <summary>Index in root.Materials (-1 for a material only reached through another material's Maps).</summary>
        public int Index;
        public string Name;
        /// <summary>The first texture's authored FromFileName (an artist path such as D:\...\foo.tif), or "".</summary>
        public string TextureFile;
        /// <summary>Texture size declared by the Texture struct, -1 when unknown.</summary>
        public int TextureWidth = -1;
        public int TextureHeight = -1;
    }

    /// <summary>One decoded (texture, image, MIP) record.</summary>
    public sealed class Gr2Texture {
        public int TextureIndex;
        public int ImageIndex;
        public int MipIndex;
        /// <summary>FromFileName, or "tex{TextureIndex}" when empty. Matches <see cref="Gr2Material.TextureFile"/>.</summary>
        public string Name;
        public string FromFileName;
        public int Width;
        public int Height;
        /// <summary>1 = raw BGRA, 2 = S3TC (unsupported), 3 = IGC (RAD BinkTC wavelet; all of RO).</summary>
        public int Encoding;
        public int SubFormat;
        /// <summary>
        /// False when the texture's Layout says 3 bytes per pixel (the IGC stream then has no A plane). granny-ro-js
        /// never reads the Layout and always decodes 4 planes, which fails on the six RGB textures of 1_attack.gr2;
        /// this port decodes them.
        /// </summary>
        public bool HasAlpha;
        /// <summary>
        /// Width * Height * 4 bytes RGBA8, straight (non-premultiplied) alpha (255 when <see cref="HasAlpha"/> is
        /// false), rows top-down (row 0 = top of the authored image). Mesh UVs address these rows directly, so
        /// uploading the bytes unchanged to a Unity Texture2D (whose first row is the bottom) with unchanged UVs maps
        /// correctly (only the asset preview looks upside down); flipping the rows requires v' = 1 - v.
        /// Null when decoding failed, see <see cref="DecodeError"/>.
        /// </summary>
        public byte[] Rgba;
        public string DecodeError;
    }

    public sealed class Gr2Model {
        public string Name;
        /// <summary>Index into <see cref="Gr2Content.Skeletons"/>, -1 when unresolved.</summary>
        public int SkeletonIndex;
        public Gr2Transform InitialPlacement;
        /// <summary>One index into <see cref="Gr2Content.Meshes"/> per MeshBinding (-1 when unresolved).</summary>
        public int[] MeshIndices;
    }

    public sealed class Gr2Animation {
        public int Index;
        public string Name;
        /// <summary>Seconds.</summary>
        public float Duration;
        public float TimeStep;
        public float Oversampling;
        public int DefaultLoopCount;
        public int Flags;
        public string[] TrackGroupNames;
        /// <summary>TrackGroups resolved by name (missing names are skipped).</summary>
        public Gr2TrackGroup[] TrackGroups;
    }

    public sealed class Gr2TrackGroup {
        public int Index;
        public string Name;
        public int VectorTrackCount;
        public int TransformTrackCount;
        public int TextTrackCount;
        public int AccumulationFlags;
        public float LoopTranslation;
        /// <summary>Vector track names (values are not decoded; RO has none).</summary>
        public string[] VectorTrackNames;
        public Gr2TransformTrack[] TransformTracks;
    }

    /// <summary>One bone's timeline; joins to <see cref="Gr2Bone.Name"/> by name.</summary>
    public sealed class Gr2TransformTrack {
        public int Index;
        public string Name;
        public int Flags;
        /// <summary>Null when the file omits a curve (sampled as identity).</summary>
        public Gr2Curve PositionCurve;
        public Gr2Curve OrientationCurve;
        public Gr2Curve ScaleShearCurve;
    }
}
