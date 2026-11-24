using System;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace ROIO.Utils {
    /// <summary>
    /// RSM2 node transforms as the client computes them (C3dNodeV2, vtable 0x00fbb874).
    ///
    /// Matrices are the client's 4x3 row-vector layout: 12 floats, rows m0..m8 then the translation m9..m11, a
    /// point transforms as v' = v * M and A * B applies A first (FUN_005334c0). A node's pose at keyframe time t
    /// is M(t) = S(t) * R(t) with the translation T(t) (C3dNodeV2::vf02 0x9ea000); its world matrix is
    /// M(t) * world(parent), and the actor appends FlipY * scale * rotations * position (C3dActorV2::vf02
    /// 0x9ec760: an identity with m4 = -1, no bounding box offset, unlike RSM1).
    ///
    /// The Unity side keeps the RSM1 instance transform, so the actor's FlipY is moved in front of the nodes:
    /// v * W * F == (v * F) * (F * W * F). Vertices get their Y negated (<see cref="FlipY"/>) and every node
    /// matrix is conjugated by F (<see cref="ToUnityMatrix"/>); the conjugation keeps the determinants positive
    /// (no reflection in the node transforms) and the flipped vertex order gives the culling the client's
    /// screen-space winding test (C3dNodeV2::vf03 0x9e92a0) gives.
    /// </summary>
    public static class Rsm2Pose {
        public const int MatrixLength = 12;

        [ThreadStatic] private static float[] scaleScratch;

        public static float[] Identity() {
            return new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };
        }

        public static void SetIdentity(float[] m) {
            m[0] = 1; m[1] = 0; m[2] = 0;
            m[3] = 0; m[4] = 1; m[5] = 0;
            m[6] = 0; m[7] = 0; m[8] = 1;
            m[9] = 0; m[10] = 0; m[11] = 0;
        }

        /// <summary>FUN_005334c0: result = a * b (a first). result may be a or b.</summary>
        public static void Multiply(float[] result, float[] a, float[] b) {
            float r0 = a[0] * b[0] + b[3] * a[1] + b[6] * a[2];
            float r3 = a[4] * b[3] + a[3] * b[0] + a[5] * b[6];
            float r6 = a[7] * b[3] + a[6] * b[0] + a[8] * b[6];
            float r9 = a[10] * b[3] + a[9] * b[0] + a[11] * b[6] + b[9];
            float r1 = b[4] * a[1] + b[1] * a[0] + b[7] * a[2];
            float r4 = a[4] * b[4] + a[3] * b[1] + a[5] * b[7];
            float r7 = a[7] * b[4] + a[6] * b[1] + a[8] * b[7];
            float r10 = a[10] * b[4] + a[9] * b[1] + a[11] * b[7] + b[10];
            float r2 = b[5] * a[1] + b[2] * a[0] + a[2] * b[8];
            float r5 = b[5] * a[4] + b[2] * a[3] + a[5] * b[8];
            float r8 = b[5] * a[7] + b[2] * a[6] + a[8] * b[8];
            float r11 = b[5] * a[10] + b[2] * a[9] + a[11] * b[8] + b[11];
            result[0] = r0; result[1] = r1; result[2] = r2;
            result[3] = r3; result[4] = r4; result[5] = r5;
            result[6] = r6; result[7] = r7; result[8] = r8;
            result[9] = r9; result[10] = r10; result[11] = r11;
        }

        /// <summary>FUN_00532930: result = inverse(a). result may be a.</summary>
        public static void Invert(float[] result, float[] a) {
            float c0 = a[4] * a[8] - a[5] * a[7];
            float inv = 1f / ((c0 * a[0] - (a[3] * a[8] - a[6] * a[5]) * a[1]) + (a[3] * a[7] - a[6] * a[4]) * a[2]);
            float neg = -inv;
            float m0 = c0 * inv;
            float m1 = (a[1] * a[8] - a[7] * a[2]) * neg;
            float m2 = (a[1] * a[5] - a[2] * a[4]) * inv;
            float m3 = (a[3] * a[8] - a[6] * a[5]) * neg;
            float m4 = (a[0] * a[8] - a[2] * a[6]) * inv;
            float m5 = (a[0] * a[5] - a[2] * a[3]) * neg;
            float m6 = (a[7] * a[3] - a[6] * a[4]) * inv;
            float m7 = (a[7] * a[0] - a[1] * a[6]) * neg;
            float m8 = (a[4] * a[0] - a[1] * a[3]) * inv;
            float m9 = -(m3 * a[10] + m0 * a[9] + m6 * a[11]);
            float m10 = -(m1 * a[9] + m4 * a[10] + m7 * a[11]);
            float m11 = -(m5 * a[10] + m2 * a[9] + m8 * a[11]);
            result[0] = m0; result[1] = m1; result[2] = m2;
            result[3] = m3; result[4] = m4; result[5] = m5;
            result[6] = m6; result[7] = m7; result[8] = m8;
            result[9] = m9; result[10] = m10; result[11] = m11;
        }

        /// <summary>FUN_00532ba0: the rotation of quaternion (x, y, z, w), translation 0 (not normalized).</summary>
        public static void FromQuaternion(float[] m, float x, float y, float z, float w) {
            float z2 = z + z;
            float wx2 = w * (x + x);
            float y2 = y + y;
            float xx2 = x * (x + x);
            m[9] = 0f;
            m[10] = 0f;
            m[11] = 0f;
            m[0] = 1f - (z * z2 + y * y2);
            m[1] = x * y2 + w * z2;
            m[3] = x * y2 - w * z2;
            m[2] = x * z2 - w * y2;
            m[4] = 1f - (z * z2 + xx2);
            m[8] = 1f - (y * y2 + xx2);
            m[5] = y * z2 + wx2;
            m[7] = y * z2 - wx2;
            m[6] = x * z2 + w * y2;
        }

        /// <summary>
        /// FUN_00532350: a quaternion from a rotation matrix, as the client writes it: w = sqrt(1 + trace) / 2 and
        /// (m7 - m5, m2 - m6, m3 - m1) / 4w, which is the conjugate of the quaternion <see cref="FromQuaternion"/>
        /// turns into that matrix. Only the first rotation segment (time before the first key) starts from it.
        /// </summary>
        public static Quaternion ToQuaternion(float[] m) {
            float w = (float) Math.Sqrt(m[0] + 1f + m[4] + m[8]) * 0.5f;
            float d = w * 4f;
            return new Quaternion((m[7] - m[5]) / d, (m[2] - m[6]) / d, (m[3] - m[1]) / d, w);
        }

        /// <summary>
        /// C3dNodeV2::vf02 (0x9ea000): the node's matrix at keyframe time <paramref name="frame"/> (frames, the
        /// actor passes fps * ms / 1000): identity, scale keys (FUN_0051eba0), then rotation keys or the rest
        /// matrix (FUN_0051e6e0), then the translation from position keys or the rest translation (FUN_0051e3e0).
        /// </summary>
        public static void EvaluateLocal(RSM.Node node, float frame, float[] result) {
            float[] rest = node.rsm2Rest ?? node.rsm2Local;
            EvaluateScale(node.rsm2ScaleKeys, frame, out float sx, out float sy, out float sz);
            EvaluateRotation(node.rsm2RotationKeys, rest, frame, result);

            // M = S * R (FUN_005334c0 with the scale matrix as the first operand: the full products, so the
            // zero terms give the client's signs of zero).
            float[] scale = scaleScratch ?? (scaleScratch = new float[MatrixLength]);
            SetIdentity(scale);
            scale[0] = sx;
            scale[4] = sy;
            scale[8] = sz;
            Multiply(result, scale, result);

            EvaluatePosition(node.rsm2PositionKeys, rest, frame, result);
        }

        /// <summary>FUN_0051eba0: no key = 1, one key = it, from the last key on = the last; before the first key
        /// the scale goes from 1 at frame 0 to the first key; linear between keys.</summary>
        public static void EvaluateScale(RSM.Rsm2VectorKey[] keys, float frame, out float x, out float y, out float z) {
            int count = keys?.Length ?? 0;
            if (count == 0) {
                x = y = z = 1f;
                return;
            }

            if (count == 1 || frame >= keys[count - 1].frame) {
                Vector3 v = keys[count == 1 ? 0 : count - 1].value;
                x = v.x; y = v.y; z = v.z;
                return;
            }

            int toFrame = 0;
            int fromFrame = 0;
            float fx = 1f, fy = 1f, fz = 1f;
            float tx = 1f, ty = 1f, tz = 1f;
            for (int i = 0; i < count; i++) {
                if (frame < keys[i].frame) {
                    toFrame = keys[i].frame;
                    tx = keys[i].value.x; ty = keys[i].value.y; tz = keys[i].value.z;
                    if (i > 0) {
                        fromFrame = keys[i - 1].frame;
                        fx = keys[i - 1].value.x; fy = keys[i - 1].value.y; fz = keys[i - 1].value.z;
                    }

                    break;
                }
            }

            float t = toFrame != fromFrame ? (frame - fromFrame) / (toFrame - fromFrame) : 0f;
            float u = 1f - t;
            x = u * fx + tx * t;
            y = u * fy + ty * t;
            z = fz * u + tz * t;
        }

        /// <summary>
        /// FUN_0051e6e0 (rotation part): no key = the rest matrix (with its translation); one key, or from the last
        /// key on = that key's rotation; before the first key from <see cref="ToQuaternion"/>(rest) at frame 0;
        /// between keys slerp (shorter arc, linear above a dot of 0.95). The rotation has translation 0.
        /// </summary>
        public static void EvaluateRotation(RSM.Rsm2RotationKey[] keys, float[] rest, float frame, float[] result) {
            int count = keys?.Length ?? 0;
            if (count == 0) {
                Array.Copy(rest, result, MatrixLength);
                return;
            }

            if (count == 1 || frame >= keys[count - 1].frame) {
                Quaternion q = keys[count == 1 ? 0 : count - 1].q;
                FromQuaternion(result, q.x, q.y, q.z, q.w);
                return;
            }

            int fromFrame = 0;
            int toFrame = 0;
            Quaternion from = new Quaternion(0f, 0f, 0f, 1f);
            Quaternion to = from;
            for (int i = 0; i < count; i++) {
                if (frame < keys[i].frame) {
                    to = keys[i].q;
                    toFrame = keys[i].frame;
                    if (i == 0) {
                        from = ToQuaternion(rest);
                        fromFrame = 0;
                    } else {
                        from = keys[i - 1].q;
                        fromFrame = keys[i - 1].frame;
                    }

                    break;
                }
            }

            float t = toFrame != fromFrame ? (frame - fromFrame) / (toFrame - fromFrame) : 0f;
            float dot = to.x * from.x + to.y * from.y + to.z * from.z + to.w * from.w;
            float ax = from.x, ay = from.y, az = from.z, aw = from.w;
            if (dot < 0f) {
                ax = -ax; ay = -ay; az = -az; aw = -aw;
                dot = -dot;
            }

            if (!(dot > -0.95f)) {
                // Only a NaN dot gets here after the sign flip: the client's opposite-quaternion branch, from
                // rotated to (-y, x, -w, z) with sin((0.5 - t) * PI) / sin(t * PI) weights.
                float fromSin = Sin((0.5f - t) * Pi);
                float toSin = Sin(t * Pi);
                FromQuaternion(
                    result,
                    -ay * toSin + fromSin * ax,
                    ax * toSin + fromSin * ay,
                    -aw * toSin + az * fromSin,
                    az * toSin + aw * fromSin);
                return;
            }

            float fromWeight = 1f - t;
            float toWeight = t;
            if (!(dot > 0.95f)) {
                // FUN_00534e60 returns sin rounded to float; the x87 divisions round to float when stored.
                float theta = (float) Math.Acos(dot);
                float sinTheta = Sin(theta);
                fromWeight = (float) ((double) Sin(fromWeight * theta) / sinTheta);
                toWeight = (float) ((double) Sin(theta * t) / sinTheta);
            }

            FromQuaternion(
                result,
                to.x * toWeight + fromWeight * ax,
                to.y * toWeight + fromWeight * ay,
                to.z * toWeight + az * fromWeight,
                to.w * toWeight + aw * fromWeight);
        }

        /// <summary>The client's PI (3.141592, 0xf4a46c).</summary>
        private const float Pi = 3.141592f;

        /// <summary>FUN_00534e60: fsin of a float, rounded to float.</summary>
        private static float Sin(float value) {
            return (float) Math.Sin(value);
        }

        /// <summary>FUN_0051e3e0: sets the translation (m9..m11): no key = the rest translation, one key, or from
        /// the last key on = that key; before the first key from the rest translation at frame 0; linear.</summary>
        public static void EvaluatePosition(RSM.Rsm2VectorKey[] keys, float[] rest, float frame, float[] result) {
            int count = keys?.Length ?? 0;
            if (count == 0) {
                result[9] = rest[9];
                result[10] = rest[10];
                result[11] = rest[11];
                return;
            }

            if (count == 1 || frame >= keys[count - 1].frame) {
                Vector3 v = keys[count == 1 ? 0 : count - 1].value;
                result[9] = v.x;
                result[10] = v.y;
                result[11] = v.z;
                return;
            }

            int fromFrame = 0;
            int toFrame = 0;
            float fx = rest[9], fy = rest[10], fz = rest[11];
            float tx = fx, ty = fy, tz = fz;
            for (int i = 0; i < count; i++) {
                if (frame < keys[i].frame) {
                    toFrame = keys[i].frame;
                    tx = keys[i].value.x; ty = keys[i].value.y; tz = keys[i].value.z;
                    if (i > 0) {
                        fromFrame = keys[i - 1].frame;
                        fx = keys[i - 1].value.x; fy = keys[i - 1].value.y; fz = keys[i - 1].value.z;
                    }

                    break;
                }
            }

            float t = (frame - fromFrame) / (toFrame - fromFrame);
            float u = 1f - t;
            result[9] = fx * u + tx * t;
            result[10] = fy * u + ty * t;
            result[11] = fz * u + tz * t;
        }

        /// <summary>
        /// FUN_0051ede0: one texture animation value at <paramref name="frame"/>: from the last key on the last
        /// value, otherwise linear between the first key after index 0 that lies past the frame and the key
        /// before it (so before the first key it extrapolates from keys 0 and 1). A single key is its value.
        /// </summary>
        public static float EvaluateTextureValue(RSM.Rsm2FloatKey[] keys, float frame) {
            int count = keys?.Length ?? 0;
            if (count == 0) {
                return 0f;
            }

            if (frame >= keys[count - 1].frame || count == 1) {
                return keys[count - 1].value;
            }

            int i = 1;
            while (i < count - 1 && !(frame < keys[i].frame)) {
                i++;
            }

            int fromFrame = keys[i - 1].frame;
            float t = (frame - fromFrame) / (keys[i].frame - fromFrame);
            return (1f - t) * keys[i - 1].value + t * keys[i].value;
        }

        /// <summary>
        /// The texture animation of one node texture slot at <paramref name="frame"/> (C3dNodeV2::vf02 0x9ea000:
        /// defaults offset 0, scale 1, rotation 0, then FUN_0051ede0 per type). Returns false when the slot has
        /// no texture animation.
        /// </summary>
        public static bool EvaluateTextureAnimation(
            RSM.Node node,
            int textureSlot,
            float frame,
            out float offsetU,
            out float offsetV,
            out float scaleU,
            out float scaleV,
            out float rotation) {
            offsetU = 0f;
            offsetV = 0f;
            scaleU = 1f;
            scaleV = 1f;
            rotation = 0f;
            RSM.Rsm2TextureAnimation[] animations = node?.rsm2TextureAnimations;
            if (animations == null) {
                return false;
            }

            bool found = false;
            for (int i = 0; i < animations.Length; i++) {
                RSM.Rsm2TextureAnimation animation = animations[i];
                if (animation.textureSlot != textureSlot) {
                    continue;
                }

                found = true;
                float value = EvaluateTextureValue(animation.keys, frame);
                switch (animation.type) {
                    case 0: offsetU = value; break;
                    case 1: offsetV = value; break;
                    case 2: scaleU = value; break;
                    case 3: scaleV = value; break;
                    case 4: rotation = value; break;
                }
            }

            return found;
        }

        /// <summary>C3dNodeV2::vf03 (0x9e92a0): a UV after the slot's texture animation (rotation and scale about
        /// the texture centre, then the offset).</summary>
        public static Vector2 ApplyTextureAnimation(Vector2 uv, float offsetU, float offsetV, float scaleU, float scaleV, float rotation) {
            float cos = (float) Math.Cos(rotation);
            float sin = (float) Math.Sin(rotation);
            float u = uv.x - 0.5f;
            float v = uv.y - 0.5f;
            return new Vector2(
                (cos * u - sin * v) * scaleU + 0.5f + offsetU,
                (sin * u + cos * v) * scaleV + 0.5f + offsetV);
        }

        /// <summary>A vertex with the actor's Y flip (C3dActorV2::vf02 m4 = -1) applied.</summary>
        public static Vector3 FlipY(Vector3 v) {
            return new Vector3(v.x, -v.y, v.z);
        }

        /// <summary>
        /// The Unity (column-vector) matrix of client matrix <paramref name="m"/> conjugated by the actor's Y
        /// flip (F * M * F, see the class summary): the transpose of the 4x3 with the entries that mix Y with
        /// another axis, and the Y translation, negated.
        /// </summary>
        public static Matrix4x4 ToUnityMatrix(float[] m) {
            var result = new Matrix4x4();
            result.m00 = m[0]; result.m01 = -m[3]; result.m02 = m[6]; result.m03 = m[9];
            result.m10 = -m[1]; result.m11 = m[4]; result.m12 = -m[7]; result.m13 = -m[10];
            result.m20 = m[2]; result.m21 = -m[5]; result.m22 = m[8]; result.m23 = m[11];
            result.m30 = 0f; result.m31 = 0f; result.m32 = 0f; result.m33 = 1f;
            return result;
        }

        /// <summary><see cref="ToUnityMatrix"/> into a gl-matrix (column-major) <see cref="Mat4"/>.</summary>
        public static void ToUnityMat4(float[] m, Mat4 dest) {
            dest[0] = m[0]; dest[1] = -m[1]; dest[2] = m[2]; dest[3] = 0f;
            dest[4] = -m[3]; dest[5] = m[4]; dest[6] = -m[5]; dest[7] = 0f;
            dest[8] = m[6]; dest[9] = -m[7]; dest[10] = m[8]; dest[11] = 0f;
            dest[12] = m[9]; dest[13] = -m[10]; dest[14] = m[11]; dest[15] = 1f;
        }

        /// <summary>The determinant of the 3x3 part of client matrix <paramref name="m"/>.</summary>
        public static float Determinant(float[] m) {
            return m[0] * (m[4] * m[8] - m[5] * m[7])
                - m[1] * (m[3] * m[8] - m[5] * m[6])
                + m[2] * (m[3] * m[7] - m[4] * m[6]);
        }

        /// <summary>
        /// A node matrix as a Unity position / rotation / scale: the columns' lengths as the scale, the Y scale
        /// negative when the matrix mirrors (so the reflection survives; RSM1's decomposition drops it). Skew is
        /// lost (a moving RSM2 node rarely has any; static ones are baked, see RSM.Node.rsm2Baked).
        /// </summary>
        public static void Decompose(Matrix4x4 m, out Vector3 position, out Quaternion rotation, out Vector3 scale) {
            position = m.GetColumn(3);
            Vector3 right = new Vector3(m.m00, m.m10, m.m20);
            Vector3 up = new Vector3(m.m01, m.m11, m.m21);
            Vector3 forward = new Vector3(m.m02, m.m12, m.m22);
            float determinant = Vector3.Dot(Vector3.Cross(right, up), forward);
            float sign = determinant < 0f ? -1f : 1f;
            scale = new Vector3(right.magnitude, up.magnitude * sign, forward.magnitude);
            up *= sign;
            rotation = forward.sqrMagnitude <= 0.000001f || up.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.LookRotation(forward, up);
        }

        /// <summary>A point through client matrix <paramref name="m"/> (v * M).</summary>
        public static Vector3 TransformPoint(float[] m, Vector3 v) {
            return new Vector3(
                v.x * m[0] + v.y * m[3] + v.z * m[6] + m[9],
                v.x * m[1] + v.y * m[4] + v.z * m[7] + m[10],
                v.x * m[2] + v.y * m[5] + v.z * m[8] + m[11]);
        }
    }
}
