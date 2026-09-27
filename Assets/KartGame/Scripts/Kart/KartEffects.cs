using UnityEngine;
using UnityEngine.Rendering;

namespace KartGame
{
    /// <summary>Shared one-shot particle helpers (bursts, explosions, sparks, dust) and trails.</summary>
    public static class Fx
    {
        static ParticleSystem burst, smoke, flash;

        static ParticleSystem Shared(ref ParticleSystem ps, string name, Texture tex, bool additive, float gravity, float drag)
        {
            if (ps != null) return ps;
            var go = new GameObject("Fx_" + name);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.playOnAwake = false;
            main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, additive ? AnimationCurve.Linear(0, 1, 1, 0.3f) : AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = drag;
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = Mats.Particle(tex, additive, Color.white, additive ? 1.6f : 1f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static ParticleSystem BurstPS => Shared(ref burst, "Burst", ProcTex.SoftDot(), true, 0.8f, 1.5f);
        static ParticleSystem SmokePS => Shared(ref smoke, "Smoke", ProcTex.Smoke(), false, -0.05f, 2f);
        static ParticleSystem FlashPS => Shared(ref flash, "Flash", ProcTex.SoftDot(), true, 0f, 0f);

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size = 0.25f, float life = 0.6f)
        {
            var ps = BurstPS;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.velocity = Random.onUnitSphere * speed * Random.Range(0.4f, 1f) + Vector3.up * speed * 0.3f;
                ep.startColor = color;
                ep.startSize = size * Random.Range(0.6f, 1.3f);
                ep.startLifetime = life * Random.Range(0.6f, 1.2f);
                ps.Emit(ep, 1);
            }
        }

        public static void Sparks(Vector3 pos, Vector3 normal, int count)
        {
            var ps = BurstPS;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                ep.velocity = (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(4f, 11f) + Vector3.up * 2f;
                ep.startColor = Color.Lerp(new Color(1f, 0.8f, 0.3f), Color.white, Random.value * 0.5f);
                ep.startSize = Random.Range(0.06f, 0.14f);
                ep.startLifetime = Random.Range(0.2f, 0.45f);
                ps.Emit(ep, 1);
            }
        }

        public static void Dust(Vector3 pos, Color color, int count, float size = 1.2f)
        {
            var ps = SmokePS;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.5f;
                ep.velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(0.5f, 2f), Random.Range(-2f, 2f));
                ep.startColor = color;
                ep.startSize = size * Random.Range(0.7f, 1.3f);
                ep.startLifetime = Random.Range(0.5f, 0.9f);
                ep.rotation = Random.Range(0f, 360f);
                ps.Emit(ep, 1);
            }
        }

        public static void Explosion(Vector3 pos)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, startSize = 5f, startLifetime = 0.25f, startColor = new Color(1f, 0.85f, 0.5f), velocity = Vector3.zero };
            FlashPS.Emit(ep, 1);
            Burst(pos, new Color(1f, 0.55f, 0.15f), 45, 12f, 0.45f, 0.7f);
            Dust(pos, new Color(0.35f, 0.33f, 0.32f, 0.85f), 16, 2.2f);
        }

        public static TrailRenderer AttachTrail(Transform parent, Vector3 localPos, Color color, float width, float time)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.2f;
            tr.material = Mats.Particle(ProcTex.Streak(), true, Color.white, 1.5f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.textureMode = LineTextureMode.Stretch;
            return tr;
        }
    }

    /// <summary>Per-kart visual feedback.</summary>
    [RequireComponent(typeof(KartController))]
    public class KartEffects : MonoBehaviour
    {
        static readonly Color[] LevelColors =
        {
            new Color(1f, 0.6f, 0.15f),
            new Color(1f, 0.92f, 0.3f),
            new Color(0.35f, 0.75f, 1f),
        };

        KartController kart;
        TrailRenderer[] skids;
        TrailRenderer[] windTrails;
        ParticleSystem smoke, sparks;
        Transform[] flames;
        Renderer[] flameRenderers;
        Material flameMat;
        GameObject shield, bubble;
        float flameScale;
        Color smokeColor = new Color(0.95f, 0.95f, 0.95f, 0.55f);

        void Start()
        {
            kart = GetComponent<KartController>();
            var rig = kart.rig;
            Transform vis = rig != null ? rig.Visual : transform;

            skids = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = rig != null ? rig.RearContacts[i] : new Vector3(i == 0 ? -0.6f : 0.6f, 0.02f, -0.7f);
                skids[i] = MakeSkid(vis, p);
            }

            smoke = MakeSmoke(vis);
            sparks = MakeSparks(vis);

            // exhaust flames: two crossed additive quads per pipe
            var ex = rig != null && rig.Exhausts != null && rig.Exhausts.Length > 0 ? rig.Exhausts : new[] { new Vector3(0, 0.45f, -1.1f) };
            flames = new Transform[ex.Length];
            flameRenderers = new Renderer[ex.Length * 2];
            flameMat = Mats.Particle(ProcTex.Flame(), true, new Color(0.5f, 0.8f, 1f, 1f), 2.2f);
            var quad = FlameMesh();
            for (int i = 0; i < ex.Length; i++)
            {
                var f = new GameObject("Flame").transform;
                f.SetParent(vis, false);
                f.localPosition = ex[i];
                f.localRotation = Quaternion.identity;
                flames[i] = f;
                for (int k = 0; k < 2; k++)
                {
                    var q = new GameObject("Q", typeof(MeshFilter), typeof(MeshRenderer));
                    q.transform.SetParent(f, false);
                    q.transform.localRotation = Quaternion.Euler(0, 0, k * 90f);
                    q.GetComponent<MeshFilter>().sharedMesh = quad;
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = flameMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    flameRenderers[i * 2 + k] = mr;
                }
                f.localScale = Vector3.zero;
            }

            // boost wind lines from the kart's flanks
            windTrails = new[]
            {
                Fx.AttachTrail(vis, new Vector3(-0.75f, 0.55f, -0.9f), new Color(0.6f, 0.85f, 1f), 0.12f, 0.35f),
                Fx.AttachTrail(vis, new Vector3(0.75f, 0.55f, -0.9f), new Color(0.6f, 0.85f, 1f), 0.12f, 0.35f),
            };
            foreach (var w in windTrails) w.emitting = false;

            shield = MakeBubble(vis, 1.9f, new Color(1f, 0.9f, 0.45f, 0.5f));
            bubble = MakeBubble(vis, 2.1f, new Color(0.5f, 0.8f, 1f, 0.65f));

            kart.OnWallHit += (impact, point) =>
            {
                if (impact > 4f) Fx.Sparks(point, (transform.position - point).normalized, Mathf.Clamp((int)impact * 2, 6, 30));
            };
            kart.OnLand += impact =>
            {
                if (impact > 3f) Fx.Dust(transform.position, smokeColor, 6, 1.4f);
            };
            kart.OnBoost += kind =>
            {
                if (kind == BoostKind.Mini) foreach (var f in flames) Fx.Burst(f.position, new Color(0.4f, 0.8f, 1f), 10, 3f, 0.3f, 0.3f);
            };
            kart.OnTankFilled += () => Fx.Burst(transform.position + Vector3.up * 0.8f, new Color(0.4f, 0.8f, 1f), 18, 4f, 0.2f, 0.5f);
            kart.OnShieldBlock += () => Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.9f, 0.5f), 40, 8f, 0.3f, 0.6f);

            if (RaceManager.Instance != null && RaceManager.Instance.World != null && RaceManager.Instance.World.Style.Snowy)
                smokeColor = new Color(0.9f, 0.95f, 1f, 0.7f);
        }

        static Mesh flameMesh;
        static Mesh FlameMesh()
        {
            if (flameMesh != null) return flameMesh;
            // quad lying along -Z (backwards), v = 0 at the nozzle
            flameMesh = new Mesh { name = "Flame" };
            flameMesh.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, 0, -1f), new Vector3(-0.5f, 0, -1f) };
            flameMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            flameMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            flameMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            flameMesh.RecalculateBounds();
            return flameMesh;
        }

        void Update()
        {
            if (kart == null) return;
            bool drift = kart.IsDrifting && kart.Grounded && kart.ForwardSpeed > 5f;
            foreach (var s in skids) s.emitting = drift || (kart.Grounded && kart.SlipAngle > 14f);

            var em = smoke.emission;
            em.rateOverTime = drift ? 38f : (kart.Grounded && kart.SlipAngle > 12f ? 12f : 0f);
            var sm = smoke.main;
            sm.startColor = smokeColor;

            var spk = sparks.emission;
            bool sparking = drift && kart.DriftTime > 0.25f;
            spk.rateOverTime = sparking ? 35f + kart.DriftLevel * 30f : 0f;
            if (sparking)
            {
                var main = sparks.main;
                main.startColor = kart.chargeEnabled ? LevelColors[Mathf.Clamp(kart.DriftLevel, 0, 2)] : LevelColors[0];
            }

            // flames
            float target = 0f;
            Color fc = new Color(0.45f, 0.75f, 1f);
            switch (kart.Boost)
            {
                case BoostKind.Nitro: target = 1.3f; fc = new Color(0.5f, 0.75f, 1f); break;
                case BoostKind.Item: target = 1.2f; fc = new Color(0.6f, 0.7f, 1f); break;
                case BoostKind.Start: target = 1f; fc = new Color(1f, 0.6f, 0.25f); break;
                case BoostKind.Pad: target = 1f; fc = new Color(1f, 0.55f, 0.15f); break;
                case BoostKind.Mini: target = 0.75f; fc = new Color(0.4f, 0.85f, 1f); break;
            }
            if (kart.BoostTimer <= 0f) target = kart.MiniWindow > 0f ? 0.25f : (kart.ThrottleInput > 0.1f ? 0.12f : 0f);
            flameScale = Mathf.Lerp(flameScale, target, Time.deltaTime * 12f);
            float flicker = 1f + Mathf.Sin(Time.time * 60f) * 0.12f + Random.Range(-0.08f, 0.08f);
            flameMat.SetColor("_TintColor", fc);
            foreach (var f in flames)
                f.localScale = flameScale < 0.02f ? Vector3.zero : new Vector3(0.28f * flameScale, 0.28f * flameScale, 1.1f * flameScale * flicker);

            bool fast = kart.BoostTimer > 0f && (kart.Boost == BoostKind.Nitro || kart.Boost == BoostKind.Item || kart.Boost == BoostKind.Pad || kart.Boost == BoostKind.Start);
            foreach (var w in windTrails) w.emitting = fast;

            shield.SetActive(kart.ShieldTimer > 0f);
            if (shield.activeSelf) shield.transform.localScale = Vector3.one * (1.9f + Mathf.Sin(Time.time * 6f) * 0.05f);
            bool trapped = kart.StunTimer > 0f && kart.StunKind == HitKind.Trap;
            bubble.SetActive(trapped);
        }

        TrailRenderer MakeSkid(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Skid");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos + Vector3.up * 0.03f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 7f;
            tr.startWidth = 0.28f;
            tr.endWidth = 0.28f;
            tr.minVertexDistance = 0.15f;
            tr.alignment = LineAlignment.TransformZ;
            tr.emitting = false;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.material = Mats.Cached("SkidMat", () => Mats.Particle(ProcTex.SoftDot(), false, new Color(0.05f, 0.05f, 0.06f, 1f)));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.45f, 0.7f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.textureMode = LineTextureMode.Stretch;
            return tr;
        }

        ParticleSystem MakeSmoke(Transform parent)
        {
            var ps = BaseSystem("DriftSmoke", parent, new Vector3(0, 0.15f, -0.75f), ProcTex.Smoke(), false);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.05f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.3f, 0.1f, 0.2f);
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2f));
            return ps;
        }

        ParticleSystem MakeSparks(Transform parent)
        {
            var ps = BaseSystem("DriftSparks", parent, new Vector3(0, 0.12f, -0.8f), ProcTex.SoftDot(), true);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.15f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.gravityModifier = 1.2f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(0, 180, 0);
            return ps;
        }

        ParticleSystem BaseSystem(string name, Transform parent, Vector3 localPos, Texture tex, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = Mats.Particle(tex, additive, Color.white, additive ? 1.8f : 1f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        GameObject MakeBubble(Transform parent, float size, Color tint)
        {
            var b = new MeshBuilder();
            b.Quad(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0), Color.white);
            var go = b.Build("Bubble", parent, Mats.Particle(ProcTex.Bubble(), false, tint), false);
            go.transform.localPosition = new Vector3(0, 0.65f, 0);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<Billboard>();
            go.SetActive(false);
            return go;
        }
    }
}
