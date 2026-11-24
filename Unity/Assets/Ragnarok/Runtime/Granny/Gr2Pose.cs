// Ported from granny-ro-js (MIT, Copyright (c) 2026 Stitchuuuu), https://github.com/stitchuuuu/granny-ro-js
// Source: src/GrannyPose.js + evaluateTransformTrack / evaluateAnimation of src/GrannyAnimation.js.
// See LICENSE-granny-ro-js.txt.

using System;
using System.Collections.Generic;

namespace Ragnarok.Granny {
    /// <summary>
    /// Samples an animation on a skeleton (granny-ro-js poseAt / GrannyGetWorldPoseComposite4x4Array):
    /// per-bone local transforms, world matrices Mworld[i] = Mworld[parent] * Mlocal[i] and skinning matrices
    /// Mskin[i] = Mworld[i] * InverseWorldTransform[i].
    /// <para>
    /// Matrix convention: 16 floats per bone, column-major (element [col * 4 + row], translation in 12..14), column
    /// vectors (v' = M * v), Mlocal = T * R * S with S the ScaleShear 3x3 applied first. Granny space is right-handed
    /// (RO's 3dmob files are authored Y-up); converting to Unity's left-handed space is the caller's job.
    /// </para>
    /// <para>
    /// Composition runs in double like the JS (world matrices are rounded to float only at the output, the skinning
    /// product uses those rounded world matrices). Bones without a track keep their bind transform; a track's
    /// missing curves sample as identity. <see cref="Sample"/> does not allocate. Not thread-safe; one sampler per
    /// actor (or per thread).
    /// </para>
    /// </summary>
    public sealed class Gr2PoseSampler {
        readonly Gr2Skeleton skeleton;
        readonly int boneCount;
        readonly Gr2TransformTrack[] boneTracks;
        readonly double[] worldDouble;
        readonly double[] local = new double[16];
        readonly double[] skinScratch = new double[16];

        /// <summary>Per-bone local transform of the last sample (bind transform for bones without a track).</summary>
        public readonly Gr2Transform[] LocalTransforms;
        /// <summary>BoneCount * 16 floats, column-major world matrix per bone.</summary>
        public readonly float[] WorldMatrices;
        /// <summary>BoneCount * 16 floats, column-major skinning matrix per bone (identity at the bind pose).</summary>
        public readonly float[] SkinningMatrices;

        public Gr2PoseSampler(Gr2Skeleton skeleton) {
            this.skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
            boneCount = skeleton.Bones.Length;
            boneTracks = new Gr2TransformTrack[boneCount];
            worldDouble = new double[boneCount * 16];
            LocalTransforms = new Gr2Transform[boneCount];
            WorldMatrices = new float[boneCount * 16];
            SkinningMatrices = new float[boneCount * 16];
        }

        public Gr2Skeleton Skeleton => skeleton;
        public int BoneCount => boneCount;
        public Gr2Animation Animation { get; private set; }

        /// <summary>
        /// Binds the bones to the animation's transform tracks by name (a later track group wins on duplicates, like
        /// the JS evaluateAnimation map). Null binds the bind pose. Allocates only here, never in <see cref="Sample"/>.
        /// </summary>
        public void SetAnimation(Gr2Animation animation) {
            Animation = animation;
            Array.Clear(boneTracks, 0, boneTracks.Length);
            if (animation == null) {
                return;
            }

            var byName = new Dictionary<string, Gr2TransformTrack>(StringComparer.Ordinal);
            foreach (Gr2TrackGroup group in animation.TrackGroups) {
                foreach (Gr2TransformTrack track in group.TransformTracks) {
                    byName[track.Name] = track;
                }
            }

            for (int i = 0; i < boneCount; i++) {
                byName.TryGetValue(skeleton.Bones[i].Name, out boneTracks[i]);
            }
        }

        /// <summary>Samples the bound animation at <paramref name="t"/> seconds (clamped to the curves' knot range).</summary>
        public void Sample(double t) {
            Gr2Bone[] bones = skeleton.Bones;
            Span<double> position = stackalloc double[16];
            Span<double> orientation = stackalloc double[16];
            Span<double> scaleShear = stackalloc double[16];
            for (int i = 0; i < boneCount; i++) {
                Gr2Bone bone = bones[i];
                Gr2TransformTrack track = boneTracks[i];
                if (track == null) {
                    Gr2Transform bind = bone.Transform;
                    LocalTransforms[i] = bind;
                    position[0] = bind.PositionX;
                    position[1] = bind.PositionY;
                    position[2] = bind.PositionZ;
                    orientation[0] = bind.OrientationX;
                    orientation[1] = bind.OrientationY;
                    orientation[2] = bind.OrientationZ;
                    orientation[3] = bind.OrientationW;
                    scaleShear[0] = bind.ScaleShear00;
                    scaleShear[1] = bind.ScaleShear01;
                    scaleShear[2] = bind.ScaleShear02;
                    scaleShear[3] = bind.ScaleShear10;
                    scaleShear[4] = bind.ScaleShear11;
                    scaleShear[5] = bind.ScaleShear12;
                    scaleShear[6] = bind.ScaleShear20;
                    scaleShear[7] = bind.ScaleShear21;
                    scaleShear[8] = bind.ScaleShear22;
                } else {
                    EvaluateTrack(track, t, position, orientation, scaleShear);
                    LocalTransforms[i] = ToTransform(position, orientation, scaleShear);
                }

                ComposeLocal(position, orientation, scaleShear, local);
                int parent = bone.ParentIndex;
                int w = i * 16;
                if (parent < 0 || parent >= i) {
                    for (int k = 0; k < 16; k++) {
                        worldDouble[w + k] = local[k];
                    }
                } else {
                    Multiply(worldDouble, parent * 16, local, 0, worldDouble, w);
                }

                for (int k = 0; k < 16; k++) {
                    WorldMatrices[w + k] = (float)worldDouble[w + k];
                }
            }

            for (int i = 0; i < boneCount; i++) {
                int w = i * 16;
                float[] iwt = bones[i].InverseWorldTransform;
                if (iwt == null || iwt.Length != 16) {
                    for (int k = 0; k < 16; k++) {
                        SkinningMatrices[w + k] = WorldMatrices[w + k];
                    }

                    continue;
                }

                MultiplyFloat(WorldMatrices, w, iwt, skinScratch);
                for (int k = 0; k < 16; k++) {
                    SkinningMatrices[w + k] = (float)skinScratch[k];
                }
            }
        }

        /// <summary>
        /// One track at time t: position (3), orientation (4) and scale-shear (9, a uniform-scale dimension-3 curve
        /// expands to a diagonal). Missing or mismatched curves give identity values.
        /// </summary>
        public static void EvaluateTrack(Gr2TransformTrack track, double t, Span<double> position, Span<double> orientation, Span<double> scaleShear) {
            int n = track.PositionCurve != null ? track.PositionCurve.Evaluate(t, position) : 0;
            if (n != 3) {
                position[0] = 0;
                position[1] = 0;
                position[2] = 0;
            }

            n = track.OrientationCurve != null ? track.OrientationCurve.Evaluate(t, orientation) : 0;
            if (n != 4) {
                orientation[0] = 0;
                orientation[1] = 0;
                orientation[2] = 0;
                orientation[3] = 1;
            }

            n = track.ScaleShearCurve != null ? track.ScaleShearCurve.Evaluate(t, scaleShear) : 0;
            if (n == 3) {
                double sx = scaleShear[0];
                double sy = scaleShear[1];
                double sz = scaleShear[2];
                scaleShear.Slice(0, 9).Clear();
                scaleShear[0] = sx;
                scaleShear[4] = sy;
                scaleShear[8] = sz;
            } else if (n != 9) {
                scaleShear.Slice(0, 9).Clear();
                scaleShear[0] = 1;
                scaleShear[4] = 1;
                scaleShear[8] = 1;
            }
        }

        /// <summary>
        /// Column-major Mlocal = T * R * S in double. The quaternion is used as is (granny2.dll does not renormalise
        /// here; the sampler already fast-normalised it).
        /// </summary>
        public static void ComposeLocal(ReadOnlySpan<double> pos, ReadOnlySpan<double> quat, ReadOnlySpan<double> ss, Span<double> output) {
            double qx = quat[0], qy = quat[1], qz = quat[2], qw = quat[3];
            double xx = qx * qx, yy = qy * qy, zz = qz * qz;
            double xy = qx * qy, xz = qx * qz, yz = qy * qz;
            double wx = qw * qx, wy = qw * qy, wz = qw * qz;
            double r00 = 1 - 2 * (yy + zz);
            double r01 = 2 * (xy - wz);
            double r02 = 2 * (xz + wy);
            double r10 = 2 * (xy + wz);
            double r11 = 1 - 2 * (xx + zz);
            double r12 = 2 * (yz - wx);
            double r20 = 2 * (xz - wy);
            double r21 = 2 * (yz + wx);
            double r22 = 1 - 2 * (xx + yy);
            double s00 = ss[0], s01 = ss[1], s02 = ss[2];
            double s10 = ss[3], s11 = ss[4], s12 = ss[5];
            double s20 = ss[6], s21 = ss[7], s22 = ss[8];
            output[0] = r00 * s00 + r01 * s10 + r02 * s20;
            output[1] = r10 * s00 + r11 * s10 + r12 * s20;
            output[2] = r20 * s00 + r21 * s10 + r22 * s20;
            output[3] = 0;
            output[4] = r00 * s01 + r01 * s11 + r02 * s21;
            output[5] = r10 * s01 + r11 * s11 + r12 * s21;
            output[6] = r20 * s01 + r21 * s11 + r22 * s21;
            output[7] = 0;
            output[8] = r00 * s02 + r01 * s12 + r02 * s22;
            output[9] = r10 * s02 + r11 * s12 + r12 * s22;
            output[10] = r20 * s02 + r21 * s12 + r22 * s22;
            output[11] = 0;
            output[12] = pos[0];
            output[13] = pos[1];
            output[14] = pos[2];
            output[15] = 1;
        }

        static Gr2Transform ToTransform(ReadOnlySpan<double> p, ReadOnlySpan<double> q, ReadOnlySpan<double> s) {
            return new Gr2Transform {
                PositionX = (float)p[0], PositionY = (float)p[1], PositionZ = (float)p[2],
                OrientationX = (float)q[0], OrientationY = (float)q[1], OrientationZ = (float)q[2], OrientationW = (float)q[3],
                ScaleShear00 = (float)s[0], ScaleShear01 = (float)s[1], ScaleShear02 = (float)s[2],
                ScaleShear10 = (float)s[3], ScaleShear11 = (float)s[4], ScaleShear12 = (float)s[5],
                ScaleShear20 = (float)s[6], ScaleShear21 = (float)s[7], ScaleShear22 = (float)s[8]
            };
        }

        /// <summary>dst = a * b (column-major, double). dst may not alias a or b.</summary>
        static void Multiply(double[] a, int ao, double[] b, int bo, double[] dst, int d) {
            for (int col = 0; col < 4; col++) {
                double b0 = b[bo + col * 4];
                double b1 = b[bo + col * 4 + 1];
                double b2 = b[bo + col * 4 + 2];
                double b3 = b[bo + col * 4 + 3];
                for (int row = 0; row < 4; row++) {
                    dst[d + col * 4 + row] = a[ao + row] * b0 + a[ao + 4 + row] * b1 + a[ao + 8 + row] * b2 + a[ao + 12 + row] * b3;
                }
            }
        }

        static void MultiplyFloat(float[] a, int ao, float[] b, double[] dst) {
            for (int col = 0; col < 4; col++) {
                double b0 = b[col * 4];
                double b1 = b[col * 4 + 1];
                double b2 = b[col * 4 + 2];
                double b3 = b[col * 4 + 3];
                for (int row = 0; row < 4; row++) {
                    dst[col * 4 + row] = (double)a[ao + row] * b0 + (double)a[ao + 4 + row] * b1 + (double)a[ao + 8 + row] * b2 + (double)a[ao + 12 + row] * b3;
                }
            }
        }
    }
}
