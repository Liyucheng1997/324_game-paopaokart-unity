using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Runtime-built drift/boost feedback: tire skid marks on the ground,
    /// drift sparks that change color with the mini-turbo charge level, and
    /// nitro exhaust flames while boosting. Everything is generated in code,
    /// no scene-serialized assets needed.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartEffects : MonoBehaviour
    {
        static readonly Color[] LevelColors =
        {
            new Color(1f, 0.65f, 0.15f),   // level 0: charging, orange
            new Color(1f, 0.95f, 0.30f),   // level 1: yellow
            new Color(0.35f, 0.75f, 1f),   // level 2: blue (full charge)
        };

        KartController kart;
        TrailRenderer[] skids;
        ParticleSystem sparks;
        ParticleSystem[] flames;
        static Texture2D softDot;

        void Start()
        {
            kart = GetComponent<KartController>();
            skids = new[]
            {
                MakeSkid(new Vector3(-0.72f, 0.04f, -0.75f)),
                MakeSkid(new Vector3(0.72f, 0.04f, -0.75f)),
            };
            sparks = MakeSparks();
            flames = new[]
            {
                MakeFlame(new Vector3(-0.35f, 0.45f, -1.1f)),
                MakeFlame(new Vector3(0.35f, 0.45f, -1.1f)),
            };
        }

        void Update()
        {
            bool skidding = kart.IsDrifting && kart.IsGrounded && Mathf.Abs(kart.CurrentSpeed) > 5f;
            foreach (var s in skids) s.emitting = skidding;

            var sparkEmission = sparks.emission;
            sparkEmission.enabled = skidding;
            if (skidding)
            {
                var main = sparks.main;
                Color c = LevelColors[Mathf.Clamp(kart.DriftLevel, 0, 2)];
                main.startColor = c;
                sparkEmission.rateOverTime = 40f + kart.DriftLevel * 50f;
            }

            bool boosting = kart.BoostTimer > 0f;
            foreach (var f in flames)
            {
                var em = f.emission;
                em.enabled = boosting;
            }
        }

        // ---------- factories ----------

        static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int S = 64;
            softDot = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            softDot.Apply();
            return softDot;
        }

        TrailRenderer MakeSkid(Vector3 localPos)
        {
            var go = new GameObject("Skid");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Z up -> ribbon lies flat
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 6f;
            tr.startWidth = 0.30f;
            tr.endWidth = 0.22f;
            tr.minVertexDistance = 0.12f;
            tr.alignment = LineAlignment.TransformZ;
            tr.numCapVertices = 2;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 0f),
                        new GradientColorKey(new Color(0.08f, 0.08f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = grad;
            return tr;
        }

        ParticleSystem MakeSparks()
        {
            var ps = MakeParticleBase("DriftSparks", new Vector3(0f, 0.25f, -1.05f),
                Quaternion.Euler(0f, 180f, 0f));   // emit backwards
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.gravityModifier = 1.2f;
            var shape = ps.shape;
            shape.angle = 28f;
            var em = ps.emission;
            em.rateOverTime = 60f;
            em.enabled = false;
            return ps;
        }

        ParticleSystem MakeFlame(Vector3 localPos)
        {
            var ps = MakeParticleBase("NitroFlame", localPos, Quaternion.Euler(0f, 180f, 0f));
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.6f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.4f, 0.75f, 1f), new Color(1f, 0.55f, 0.1f));
            var shape = ps.shape;
            shape.angle = 9f;
            var colt = ps.colorOverLifetime;
            colt.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colt.color = grad;
            var em = ps.emission;
            em.rateOverTime = 130f;
            em.enabled = false;
            return ps;
        }

        ParticleSystem MakeParticleBase(string name, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.06f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            mat.mainTexture = SoftDot();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
