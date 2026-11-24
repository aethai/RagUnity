using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Maps {
    /// <summary>
    /// Destroys the meshes, materials and textures created at runtime for its GameObject when the GameObject goes. A map
    /// change destroys the old map's GameObjects only and nothing unloads orphaned assets, so an asset created per map
    /// load and not destroyed here stays in memory for the rest of the session. Never register a shared or cached asset.
    /// </summary>
    public sealed class RoOwnedRuntimeAssets : MonoBehaviour {
        readonly List<Object> owned = new List<Object>(4);

        /// <summary>Hands <paramref name="asset"/> to <paramref name="owner"/>: destroyed together with it.</summary>
        public static void Own(GameObject owner, Object asset) {
            if (owner == null || asset == null) {
                return;
            }

            if (!owner.TryGetComponent(out RoOwnedRuntimeAssets assets)) {
                assets = owner.AddComponent<RoOwnedRuntimeAssets>();
            }

            if (!assets.owned.Contains(asset)) {
                assets.owned.Add(asset);
            }
        }

        void OnDestroy() {
            for (int i = 0; i < owned.Count; i++) {
                if (owned[i] != null) {
                    Destroy(owned[i]);
                }
            }

            owned.Clear();
        }
    }
}
