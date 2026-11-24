using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Effects.Classic {
    /// <summary>
    /// The client's CBlurPC: a frozen copy of an actor left behind by Snap / Mirror Image. The original clones
    /// the whole player actor (sprites, palette, current motion frame) and fades it; this copies the actor's
    /// current layer meshes and materials at the given position, tints them with the colour the effect sent
    /// (Mirror Image: 155, 155, 255; Snap: none) and fades them out over <see cref="LifetimeSeconds"/>.
    /// </summary>
    public sealed class RoClassicAfterImage : MonoBehaviour {
        public const float LifetimeSeconds = 0.5f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly List<Color> baseColors = new List<Color>();
        MaterialPropertyBlock block;
        float bornAt;

        /// <summary>Creates the after-image of <paramref name="source"/> at <paramref name="position"/>; null when the actor has no meshes.</summary>
        public static RoClassicAfterImage Spawn(Transform source, Vector3 position, Color tint) {
            if (source == null) {
                return null;
            }

            MeshRenderer[] sourceRenderers = source.GetComponentsInChildren<MeshRenderer>(false);
            if (sourceRenderers.Length == 0) {
                return null;
            }

            var root = new GameObject("RO after-image") { hideFlags = HideFlags.DontSave };
            root.transform.position = position;
            root.transform.rotation = source.rotation;
            var image = root.AddComponent<RoClassicAfterImage>();
            image.block = new MaterialPropertyBlock();
            Vector3 delta = position - source.position;
            foreach (MeshRenderer src in sourceRenderers) {
                MeshFilter filter = src.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !src.enabled || src.GetComponent<RoClassicAfterImage>() != null) {
                    continue;
                }

                var copy = new GameObject(src.name) { hideFlags = HideFlags.DontSave };
                copy.transform.SetParent(root.transform, false);
                copy.transform.position = src.transform.position + delta;
                copy.transform.rotation = src.transform.rotation;
                copy.transform.localScale = src.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = src.sharedMaterials;
                renderer.sortingLayerID = src.sortingLayerID;
                renderer.sortingOrder = src.sortingOrder - 1;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                src.GetPropertyBlock(image.block);
                renderer.SetPropertyBlock(image.block);
                Color baseColor = image.block.GetColor(BaseColorId);
                if (baseColor == default) {
                    baseColor = Color.white;
                }

                image.renderers.Add(renderer);
                image.baseColors.Add(baseColor * tint);
            }

            if (image.renderers.Count == 0) {
                Destroy(root);
                return null;
            }

            image.bornAt = Time.time;
            image.Apply(1f);
            return image;
        }

        void Update() {
            float t = (Time.time - bornAt) / LifetimeSeconds;
            if (t >= 1f) {
                Destroy(gameObject);
                return;
            }

            Apply(1f - t);
        }

        void Apply(float alpha) {
            for (int i = 0; i < renderers.Count; i++) {
                MeshRenderer renderer = renderers[i];
                if (renderer == null) {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                Color c = baseColors[i];
                c.a *= alpha;
                block.SetColor(BaseColorId, c);
                block.SetColor(ColorId, c);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
