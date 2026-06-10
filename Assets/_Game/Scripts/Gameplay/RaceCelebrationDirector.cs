using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>Runtime-built finish celebration: confetti, sparks, and soft firework pops.</summary>
    public class RaceCelebrationDirector : MonoBehaviour
    {
        [Header("Timing")]
        [Range(3f, 5f)] public float defaultDuration = 5f;

        [Header("Placement")]
        public float verticalOffset = 1.35f;
        public float racerBurstRadius = 0.35f;
        public float fireworkRadius = 3.4f;
        public float fireworkHeight = 2.8f;

        [Header("Density")]
        public int confettiPerRacer = 90;
        public int sparklePerRacer = 42;
        public int fireworks = 4;

        [Header("Rendering")]
        public Material particleMaterial;

        private GameObject _activeRoot;
        private Material _runtimeMaterial;

        public float Play(IReadOnlyList<PhysicsSkater> racers, Transform focus, float duration)
        {
            duration = Mathf.Clamp(duration <= 0f ? defaultDuration : duration, 3f, 5f);

            if (_activeRoot != null) Destroy(_activeRoot);
            _activeRoot = new GameObject("Runtime Win Celebration");
            _activeRoot.transform.SetParent(transform, false);

            int made = 0;
            if (racers != null)
            {
                for (int i = 0; i < racers.Count; i++)
                {
                    var racer = racers[i];
                    if (racer == null) continue;
                    Vector3 basePos = racer.transform.position + Vector3.up * verticalOffset;
                    CreateConfettiBurst(basePos, i);
                    CreateSparkBurst(basePos + racer.transform.right * 0.35f, i);
                    made++;
                }
            }

            Vector3 center = focus != null ? focus.position : transform.position;
            if (focus == null && racers != null)
            {
                for (int i = 0; i < racers.Count; i++)
                {
                    if (racers[i] == null) continue;
                    center = racers[i].transform.position;
                    break;
                }
            }
            CreateFireworks(center + Vector3.up * fireworkHeight);

            Destroy(_activeRoot, duration + 3f);
            return duration;
        }

        private void CreateConfettiBurst(Vector3 position, int seed)
        {
            var ps = CreateParticleObject("Confetti Burst", position);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.45f;
            main.maxParticles = Mathf.Max(10, confettiPerRacer);
            main.startColor = seed % 2 == 0
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.22f, 0.28f), new Color(0.18f, 0.72f, 1f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.12f), new Color(0.32f, 1f, 0.58f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(10, confettiPerRacer)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = racerBurstRadius;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 3.4f);
            velocity.x = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.8f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 3f;

            ps.gameObject.SetActive(true);
            ps.Play(true);
        }

        private void CreateSparkBurst(Vector3 position, int seed)
        {
            var ps = CreateParticleObject("Gold Spark Burst", position);
            var main = ps.main;
            main.duration = 0.12f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.gravityModifier = 0.08f;
            main.maxParticles = Mathf.Max(8, sparklePerRacer);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.48f), Color.white);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.08f + seed * 0.04f, (short)Mathf.Max(8, sparklePerRacer)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 0.55f;
            trails.lifetime = 0.18f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 4f;

            ps.gameObject.SetActive(true);
            ps.Play(true);
        }

        private void CreateFireworks(Vector3 center)
        {
            int count = Mathf.Max(0, fireworks);
            for (int i = 0; i < count; i++)
            {
                float angle = count <= 1 ? 0f : i * Mathf.PI * 2f / count;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0.25f * (i % 2), Mathf.Sin(angle)) * fireworkRadius;
                var ps = CreateParticleObject("Soft Firework Pop", pos);

                var main = ps.main;
                main.duration = 0.1f;
                main.loop = false;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startDelay = 0.35f + i * 0.32f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.35f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 7.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.11f);
                main.gravityModifier = 0.12f;
                main.maxParticles = 64;
                main.startColor = i % 2 == 0
                    ? new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.7f), new Color(0.2f, 0.85f, 1f))
                    : new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.24f), new Color(0.52f, 1f, 0.62f));

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)48) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.08f;

                var trails = ps.trails;
                trails.enabled = true;
                trails.ratio = 0.65f;
                trails.lifetime = 0.24f;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingFudge = 5f;

                ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }

        private ParticleSystem CreateParticleObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_activeRoot.transform, true);
            go.transform.position = position;
            go.SetActive(false);

            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Material mat = particleMaterial != null ? particleMaterial : GetRuntimeParticleMaterial();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
                // Trails are enabled on sparks/fireworks — without an explicit trail material
                // they render with the magenta error shader (the "purple lines").
                renderer.trailMaterial = mat;
            }
            return ps;
        }

        private Material GetRuntimeParticleMaterial()
        {
            if (_runtimeMaterial != null) return _runtimeMaterial;

            // Built-in RP project: use the built-in alpha-blended particle shaders (vertex-color
            // tinted, mobile-cheap). NOTE Shader.Find only works in a device build if the shader
            // ships — assign a real material asset to 'particleMaterial' for builds; this runtime
            // fallback covers the sandbox/missing-reference case.
            Shader shader =
                Shader.Find("Mobile/Particles/Alpha Blended") ??
                Shader.Find("Legacy Shaders/Particles/Alpha Blended") ??
                Shader.Find("Sprites/Default");

            if (shader == null) return null;
            _runtimeMaterial = new Material(shader) { name = "Runtime Celebration Particle Material" };
#if UNITY_EDITOR
            var tex = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) _runtimeMaterial.mainTexture = tex;   // soft round dot instead of a hard quad
#endif
            return _runtimeMaterial;
        }
    }
}
