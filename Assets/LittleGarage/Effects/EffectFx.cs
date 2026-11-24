using UnityEngine;

namespace LittleGarage
{
    /// <summary>Small pooled particle helpers. Uses simple ParticleSystem API only.</summary>
    public static class EffectFx
    {
        public static ParticleSystem CreateSpray(Transform parent, Color color, float rate)
        {
            ParticleSystem ps = CreateBase(parent, "SprayFx", color, 0.08f, 0.22f, 0.9f, 2.2f, 200);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
            var shape = ps.shape;
            shape.enabled = true;
            shape.radius = 0.05f;
            return ps;
        }

        public static ParticleSystem CreateFoam(Transform parent)
        {
            ParticleSystem ps = CreateBase(parent, "FoamFx", new Color(0.95f, 0.98f, 1f, 0.9f), 0.12f, 0.28f, 0.3f, 1.2f, 200);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.radius = 0.08f;
            return ps;
        }

        public static ParticleSystem CreateDust(Transform parent)
        {
            ParticleSystem ps = CreateBase(parent, "DustFx", new Color(0.9f, 0.8f, 0.55f, 0.7f), 0.05f, 0.16f, 0.4f, 1.5f, 200);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.radius = 0.06f;
            return ps;
        }

        public static void Burst(ParticleSystem ps, Vector3 pos, int count)
        {
            if (ps == null) return;
            if (ps.transform != null) ps.transform.position = pos;
            ps.Emit(Mathf.Max(1, count));
        }

        private static ParticleSystem CreateBase(Transform parent, string name, Color color, float sizeMin, float sizeMax, float life, float speed, int max)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve((sizeMin + sizeMax) * 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            main.maxParticles = Mathf.Clamp(max, 16, 200);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) renderer = go.AddComponent<ParticleSystemRenderer>();
            return ps;
        }
    }
}
