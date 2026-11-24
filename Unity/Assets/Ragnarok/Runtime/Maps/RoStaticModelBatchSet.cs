using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    /// <summary>
    /// The map's static model batches and where each batched occlusion-fade candidate lies in them. The occlusion fader
    /// fades a candidate by taking its triangles out of the batch (its index range becomes degenerate) and drawing the
    /// candidate's own renderer with the fade material instead; restoring does the reverse. Lives on the batch root.
    /// </summary>
    public sealed class RoStaticModelBatchSet : MonoBehaviour {
        /// <summary>One combined mesh holding fade candidates, with its original index buffer for restoring ranges.</summary>
        internal sealed class Batch {
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public ushort[] Indices16;
            public int[] Indices32;
        }

        internal readonly struct Member {
            public Member(Batch batch, int indexStart, int indexCount) {
                Batch = batch;
                IndexStart = indexStart;
                IndexCount = indexCount;
            }

            public Batch Batch { get; }
            public int IndexStart { get; }
            public int IndexCount { get; }
        }

        const MeshUpdateFlags RangeUpdateFlags =
            MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers;

        static ushort[] zeros16 = new ushort[0];
        static int[] zeros32 = new int[0];

        readonly Dictionary<MeshRenderer, Member> members = new Dictionary<MeshRenderer, Member>();

        public int MemberCount => members.Count;

        internal void Add(MeshRenderer source, Member member) => members[source] = member;

        internal bool TryGetMember(MeshRenderer source, out Member member) {
            if (source != null && members.TryGetValue(source, out member)) {
                return true;
            }

            member = default;
            return false;
        }

        /// <summary>Is the batch the member is drawn in shown at all (the map's model layer can be switched off)?</summary>
        internal static bool IsBatchVisible(in Member member) =>
            member.Batch.Renderer != null && member.Batch.Renderer.enabled && member.Batch.Renderer.gameObject.activeInHierarchy;

        /// <summary>The member's triangles leave the batch (all its indices point at vertex 0: degenerate, not drawn).</summary>
        internal static void Hide(in Member member) {
            Batch batch = member.Batch;
            if (batch.Mesh == null || member.IndexCount <= 0) {
                return;
            }

            if (batch.Indices16 != null) {
                if (zeros16.Length < member.IndexCount) zeros16 = new ushort[member.IndexCount];
                batch.Mesh.SetIndexBufferData(zeros16, 0, member.IndexStart, member.IndexCount, RangeUpdateFlags);
            } else if (batch.Indices32 != null) {
                if (zeros32.Length < member.IndexCount) zeros32 = new int[member.IndexCount];
                batch.Mesh.SetIndexBufferData(zeros32, 0, member.IndexStart, member.IndexCount, RangeUpdateFlags);
            }
        }

        /// <summary>The member's triangles are drawn by the batch again.</summary>
        internal static void Show(in Member member) {
            Batch batch = member.Batch;
            if (batch.Mesh == null || member.IndexCount <= 0) {
                return;
            }

            if (batch.Indices16 != null) {
                batch.Mesh.SetIndexBufferData(batch.Indices16, member.IndexStart, member.IndexStart, member.IndexCount, RangeUpdateFlags);
            } else if (batch.Indices32 != null) {
                batch.Mesh.SetIndexBufferData(batch.Indices32, member.IndexStart, member.IndexStart, member.IndexCount, RangeUpdateFlags);
            }
        }
    }
}
