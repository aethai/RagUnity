using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// A/B check for the actor sprites' depth write: renders the main camera twice in one frame — as today (the depth
    /// prepass, then the forward pass) and with the prepass off and the forward pass writing depth and stencil itself —
    /// and counts the pixels that differ. The two renders see the same frame (time, animation, effects), so every
    /// difference comes from the depth mode. Writes &lt;name&gt;_a.png, _b.png and _diff.png (differences ×8) to the shots folder.
    /// </summary>
    public static class RoSpriteDepthModeProbe {
        const string SpriteShaderName = "Ragnarok/RO Sprite Enhanced";
        static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        static readonly int StencilPassId = Shader.PropertyToID("_RoSpriteStencilPass");

        /// <summary>
        /// Diagnostics (!toggle merged): every sprite material in the scene switched to the merged mode (no depth pass,
        /// the forward pass writes depth and stencil) or back; for timing the depth pass, not for the look.
        /// </summary>
        public static string SetMergedForTiming(bool merged) {
            int count = 0;
            var seen = new HashSet<Material>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null || material.shader.name != SpriteShaderName || !seen.Add(material)) {
                    continue;
                }

                SetMerged(material, merged);
                if (!merged) {
                    material.SetFloat(ZWriteId, 0f);
                    material.SetFloat(StencilPassId, 0f);
                }

                count++;
            }

            return count + " sprite materials " + (merged ? "merged" : "two-pass");
        }

        /// <summary>Diagnostics (!toggle spriteshadows): the sprite renderers cast shadows or not; for timing.</summary>
        public static string SetSpriteShadowsForTiming(bool on) {
            int count = 0;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null || material.shader.name != SpriteShaderName) {
                    continue;
                }

                renderer.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                count++;
            }

            return count + " sprite renderers, shadows " + on;
        }

        /// <summary>
        /// <paramref name="singlePass"/>: A = every layer with its two passes, B = the one-pass layers as the animators pick
        /// them (RoActSpriteAnimator.SinglePassDepth); the two must not differ. Otherwise A = today, B = the merged mode.
        /// </summary>
        public static string Compare(string name, string outputDir, bool singlePass = false) {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null) {
                return "no main camera";
            }

            var materials = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                Material material = renderer.sharedMaterial;
                if (material != null && material.shader != null && material.shader.name == SpriteShaderName && seen.Add(material)) {
                    materials.Add(material);
                }
            }

            var zWrite = new float[materials.Count];
            var stencil = new float[materials.Count];
            for (int i = 0; i < materials.Count; i++) {
                zWrite[i] = materials[i].GetFloat(ZWriteId);
                stencil[i] = materials[i].GetFloat(StencilPassId);
            }

            int width = camera.pixelWidth, height = camera.pixelHeight;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) {
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
            };
            var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D a, b;
            bool singlePassBefore = Ragnarok.Sprites.RoActSpriteAnimator.SinglePassDepth;
            try {
                if (singlePass) {
                    Ragnarok.Sprites.RoActSpriteAnimator.SinglePassDepth = false;
                    Ragnarok.Sprites.RoActSpriteAnimator.ReapplyDepthModeForAll();
                }

                a = Render(camera, target, resolved);
                if (singlePass) {
                    Ragnarok.Sprites.RoActSpriteAnimator.SinglePassDepth = true;
                    Ragnarok.Sprites.RoActSpriteAnimator.ReapplyDepthModeForAll();
                } else {
                    for (int i = 0; i < materials.Count; i++) {
                        SetMerged(materials[i], true);
                    }
                }

                b = Render(camera, target, resolved);
            } finally {
                if (singlePass) {
                    Ragnarok.Sprites.RoActSpriteAnimator.SinglePassDepth = singlePassBefore;
                    Ragnarok.Sprites.RoActSpriteAnimator.ReapplyDepthModeForAll();
                } else {
                    for (int i = 0; i < materials.Count; i++) {
                        SetMerged(materials[i], false);
                        materials[i].SetFloat(ZWriteId, zWrite[i]);
                        materials[i].SetFloat(StencilPassId, stencil[i]);
                    }
                }

                camera.targetTexture = previousTarget;
                target.Release();
                resolved.Release();
                Object.Destroy(target);
                Object.Destroy(resolved);
            }

            Color32[] pa = a.GetPixels32(), pb = b.GetPixels32();
            var diff = new Color32[pa.Length];
            int any = 0, visible = 0, strong = 0, maxDelta = 0;
            for (int i = 0; i < pa.Length; i++) {
                int d = Mathf.Max(Mathf.Abs(pa[i].r - pb[i].r), Mathf.Max(Mathf.Abs(pa[i].g - pb[i].g), Mathf.Abs(pa[i].b - pb[i].b)));
                maxDelta = Mathf.Max(maxDelta, d);
                if (d > 0) any++;
                if (d > 8) visible++;
                if (d > 48) strong++;
                byte v = (byte) Mathf.Min(255, d * 8);
                diff[i] = d > 0 ? new Color32(v, v, v, 255) : new Color32(0, 0, 0, 255);
            }

            Directory.CreateDirectory(outputDir);
            File.WriteAllBytes(Path.Combine(outputDir, name + "_a.png"), a.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(outputDir, name + "_b.png"), b.EncodeToPNG());
            var diffTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            diffTexture.SetPixels32(diff);
            File.WriteAllBytes(Path.Combine(outputDir, name + "_diff.png"), diffTexture.EncodeToPNG());
            Object.Destroy(a);
            Object.Destroy(b);
            Object.Destroy(diffTexture);
            return $"{materials.Count} sprite materials, {width}x{height}, msaa {target.antiAliasing}: " +
                $"{any} px differ, {visible} by >8, {strong} by >48, max {maxDelta}";
        }

        /// <summary>The prepass off and the forward pass writing depth and stencil (or back to today's state).</summary>
        static void SetMerged(Material material, bool merged) {
            // the prepass has no LightMode tag (SRPDefaultUnlit); its name too, whichever the lookup uses
            material.SetShaderPassEnabled("SRPDefaultUnlit", !merged);
            material.SetShaderPassEnabled("SpriteDepthPrepass", !merged);
            material.SetShaderPassEnabled("RoSpriteDepthPrepass", !merged);
            if (merged) {
                material.SetFloat(ZWriteId, 1f);
                material.SetFloat(StencilPassId, 2f);   // StencilOp.Replace
            }
        }

        static Texture2D Render(UnityEngine.Camera camera, RenderTexture target, RenderTexture resolved) {
            camera.targetTexture = target;
            camera.Render();
            Graphics.Blit(target, resolved);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = resolved;
            var texture = new Texture2D(resolved.width, resolved.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, resolved.width, resolved.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }
    }
}
