using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Rendering {
    /// <summary>
    /// A lamp of the enhanced look (put on every model lamp light by RoLookRig): a glow in the air around the flame
    /// (RO Look Glow) that follows the light - off by day, on at night - and a slight flame flicker on the light itself.
    /// </summary>
    public sealed class RoLookLampGlow : MonoBehaviour {
        static Mesh quad;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");

        public static float GlowStrength = 0.25f;
        public static float GlowRadius = 0.8f;
        public static float Flicker = 0.07f;
        // a lantern lights a pool, not the square: the database ranges were made for the old gamma falloff
        public static float RangeScale = 0.65f;
        public static float IntensityScale = 3.5f;

        Light lamp;
        MeshRenderer glow;
        MaterialPropertyBlock block;
        float baseIntensity = -1f;
        float baseRange = -1f;
        float lastSetIntensity = -1f;
        float seed;

        public static float FireFlicker = 0.22f;
        public static float FireGlow = 0.3f;
        public static float FireIntensityScale = 2.2f;
        public static float FireRangeScale = 1f;
        public static float FireGlowRadius = 1.2f;

        bool fire;

        public static void Attach(Light light, Material material, bool fire = false) {
            if (light == null || light.GetComponent<RoLookLampGlow>() != null) return;
            var glow = light.gameObject.AddComponent<RoLookLampGlow>();
            glow.fire = fire;
            glow.Build(light, material);
        }

        void Build(Light light, Material material) {
            lamp = light;
            baseRange = light.range;
            seed = Random.value * 100f;
            var go = new GameObject("RO Look Glow");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = SharedQuad();
            glow = go.AddComponent<MeshRenderer>();
            glow.sharedMaterial = material;
            glow.shadowCastingMode = ShadowCastingMode.Off;
            glow.receiveShadows = false;
            glow.lightProbeUsage = LightProbeUsage.Off;
            glow.reflectionProbeUsage = ReflectionProbeUsage.Off;
            block = new MaterialPropertyBlock();
        }

        void LateUpdate() {
            if (lamp == null || glow == null) return;
            // someone else (RoMapNightLight on a day / night flip) set the intensity: that is the new base
            if (!Mathf.Approximately(lamp.intensity, lastSetIntensity)) baseIntensity = lamp.intensity;
            bool on = lamp.enabled && baseIntensity > 0.0001f;
            glow.enabled = on && GlowStrength > 0f;
            if (!on) return;

            float t = Time.time * 7.3f + seed;
            float flicker = 1f + (fire ? FireFlicker : Flicker) * ((Mathf.PerlinNoise(t, seed) - 0.5f) * 1.6f + (Mathf.PerlinNoise(t * 3.1f, seed + 7f) - 0.5f) * 0.6f);
            lastSetIntensity = baseIntensity * (fire ? FireIntensityScale : IntensityScale) * flicker;
            lamp.intensity = lastSetIntensity;
            lamp.range = baseRange * (fire ? FireRangeScale : RangeScale);

            Color c = lamp.color.linear * (baseIntensity * (fire ? FireGlow : GlowStrength) * flicker);
            block ??= new MaterialPropertyBlock();
            block.SetColor(ColorId, c);
            block.SetFloat(RadiusId, fire ? FireGlowRadius : GlowRadius);
            glow.SetPropertyBlock(block);
        }

        static Mesh SharedQuad() {
            if (quad != null) return quad;
            quad = new Mesh { name = "RO Look Glow Quad" };
            quad.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) });
            quad.SetUVs(0, new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            // the vertex shader places the quad; bounds big enough that the lamp is never culled while on screen
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 8f);
            return quad;
        }
    }
}
