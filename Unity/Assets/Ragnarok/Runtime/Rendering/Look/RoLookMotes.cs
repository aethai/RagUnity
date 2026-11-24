using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Rendering {
    /// <summary>
    /// Dust in the air around the camera's target (the HD-2D look): one particle system following the view, slow
    /// drifting specks drawn additive in HDR (RO Look Mote), so the bloom turns the ones near a light into sparks.
    /// Off at rate 0.
    /// </summary>
    public sealed class RoLookMotes : MonoBehaviour {
        public float Rate;
        public float Intensity = 1.5f;
        public float Size = 0.08f;

        ParticleSystem system;
        Ragnarok.Camera.RoCameraController cameraController;

        public void Configure(float rate, float intensity, float size) {
            Rate = Mathf.Max(0f, rate);
            Intensity = Mathf.Max(0f, intensity);
            Size = Mathf.Max(0.005f, size);
            Apply();
        }

        void Apply() {
            if (system == null) {
                if (Rate <= 0f) return;
                Build();
                if (system == null) return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Rate;
            ParticleSystem.MainModule main = system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Size * 0.5f, Size * 1.2f);
            main.startColor = new Color(1f, 0.86f, 0.62f, 1f) * Intensity;
            if (Rate <= 0f) system.Clear();
        }

        void Build() {
            Shader shader = Shader.Find("Ragnarok/RO Look Mote");
            if (shader == null) return;
            var go = new GameObject("RO Look Motes");
            go.transform.SetParent(transform, false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 7f, 36f);
            shape.position = new Vector3(0f, 3f, 0f);

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = new Material(shader) { name = "RO Look Mote" };
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
        }

        void LateUpdate() {
            if (system == null) return;
            if (cameraController == null && Time.frameCount % 30 == 0) cameraController = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return;
            float distance = cameraController != null ? cameraController.Distance : 70f;
            system.transform.position = cam.transform.position + cam.transform.forward * distance;
        }
    }
}
