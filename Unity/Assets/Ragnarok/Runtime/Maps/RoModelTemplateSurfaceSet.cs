using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Maps {
    [DisallowMultipleComponent]
    public sealed class RoModelTemplateSurfaceSet : MonoBehaviour {
        [SerializeField] private List<RoModelSurfaceInfo> surfaces = new List<RoModelSurfaceInfo>(32);

        public void Initialize(IReadOnlyList<RoModelSurfaceInfo> templateSurfaces) {
            surfaces.Clear();
            if (templateSurfaces == null) {
                return;
            }

            for (int i = 0; i < templateSurfaces.Count; i++) {
                RoModelSurfaceInfo surface = templateSurfaces[i];
                if (surface != null) {
                    surfaces.Add(surface);
                }
            }
        }

        public bool ReferencesInstance(Transform instanceRoot) {
            if (instanceRoot == null || surfaces == null || surfaces.Count == 0) {
                return false;
            }

            for (int i = 0; i < surfaces.Count; i++) {
                RoModelSurfaceInfo surface = surfaces[i];
                if (surface == null) {
                    continue;
                }

                Transform surfaceTransform = surface.transform;
                return surfaceTransform == instanceRoot || surfaceTransform.IsChildOf(instanceRoot);
            }

            return false;
        }

        public void UpdateInstances(
            int instanceIndex,
            float[] descriptorPosition,
            float[] descriptorRotation,
            float[] descriptorScale
        ) {
            if (surfaces == null || surfaces.Count == 0) {
                return;
            }

            for (int i = 0; i < surfaces.Count; i++) {
                RoModelSurfaceInfo surface = surfaces[i];
                if (surface != null) {
                    surface.UpdateInstance(instanceIndex, descriptorPosition, descriptorRotation, descriptorScale);
                }
            }
        }
    }
}
