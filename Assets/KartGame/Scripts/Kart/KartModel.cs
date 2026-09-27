using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    public enum KartDesign { Comet, Bubble, Arrow, Brick }

    public class KartLivery
    {
        public Color Primary, Secondary, Helmet, Suit, Skin = new Color(1f, 0.84f, 0.7f);
        public KartLivery(Color primary, Color secondary, Color helmet, Color suit)
        {
            Primary = primary; Secondary = secondary; Helmet = helmet; Suit = suit;
        }
    }

    /// <summary>Transforms the controller animates (steering, spinning wheels, driver lean).</summary>
    public class KartRig
    {
        public Transform Visual;
        public Transform[] Wheels = new Transform[4];     // FL, FR, RL, RR (spin around local X)
        public Transform[] SteerPivots = new Transform[2];
        public Transform SteeringWheel;
        public Transform Driver;
        public Transform Head;
        public Vector3[] Exhausts;                        // local to Visual, pointing -Z
        public Vector3[] RearContacts = new Vector3[2];   // local tire contact points
        public float[] WheelRadius = new float[4];
    }

    /// <summary>
    /// Builds a detailed kart from lofted body shells, lathed tires/rims,
    /// tube frames and a chibi driver (open-face helmet with goggles).
    /// About 2.3 m long; +Z forward, ground at y = 0.
    /// </summary>
    public static class KartModel
    {
        struct Ring { public float z, w, top, bot, pow; public Ring(float z, float w, float top, float bot, float pow = 3f) { this.z = z; this.w = w; this.top = top; this.bot = bot; this.pow = pow; } }

        class Spec
        {
            public Ring[] Body;
            public float FrontR = 0.24f, FrontW = 0.22f, RearR = 0.29f, RearW = 0.32f;
            public float FrontX = 0.62f, FrontZ = 0.78f, RearX = 0.64f, RearZ = -0.66f;
            public bool Sidepods = true, FrontWing = true, Fenders, RollBar, BullBar, EyeLights;
            public float WingSpan = 1.25f, WingHeight = 0.86f;
            public float ExhaustR = 0.055f;
            public int Exhausts = 2;
            public float SeatZ = -0.42f, DriverZ = -0.3f;
            public float CockpitY = 0.5f;
        }

        static Spec GetSpec(KartDesign d)
        {
            switch (d)
            {
                case KartDesign.Bubble:
                    return new Spec
                    {
                        Body = new[]
                        {
                            new Ring(-1.02f, 0.70f, 0.52f, 0.22f, 2.4f), new Ring(-0.95f, 0.90f, 0.62f, 0.16f, 2.6f),
                            new Ring(-0.7f, 0.98f, 0.66f, 0.15f, 2.8f), new Ring(-0.35f, 1.0f, 0.60f, 0.15f, 3f),
                            new Ring(0.0f, 0.98f, 0.60f, 0.15f, 3f), new Ring(0.35f, 0.96f, 0.64f, 0.15f, 2.8f),
                            new Ring(0.7f, 0.92f, 0.62f, 0.16f, 2.6f), new Ring(0.98f, 0.82f, 0.54f, 0.18f, 2.4f),
                            new Ring(1.12f, 0.62f, 0.44f, 0.22f, 2.2f), new Ring(1.18f, 0.3f, 0.36f, 0.27f, 2f),
                        },
                        Sidepods = false, FrontWing = false, Fenders = true, EyeLights = true,
                        WingSpan = 0f, FrontR = 0.25f, RearR = 0.28f, RearW = 0.28f, FrontX = 0.6f, RearX = 0.6f,
                        CockpitY = 0.6f,
                    };
                case KartDesign.Arrow:
                    return new Spec
                    {
                        Body = new[]
                        {
                            new Ring(-1.0f, 0.62f, 0.50f, 0.15f), new Ring(-0.85f, 0.74f, 0.55f, 0.13f),
                            new Ring(-0.5f, 0.78f, 0.56f, 0.13f), new Ring(-0.15f, 0.76f, 0.48f, 0.13f),
                            new Ring(0.2f, 0.66f, 0.42f, 0.13f), new Ring(0.55f, 0.5f, 0.36f, 0.13f),
                            new Ring(0.9f, 0.36f, 0.3f, 0.13f), new Ring(1.2f, 0.24f, 0.25f, 0.14f),
                            new Ring(1.36f, 0.08f, 0.2f, 0.16f, 2f),
                        },
                        WingSpan = 1.45f, WingHeight = 0.95f, RollBar = true, FrontZ = 0.84f,
                        FrontR = 0.23f, RearR = 0.3f, RearW = 0.36f,
                    };
                case KartDesign.Brick:
                    return new Spec
                    {
                        Body = new[]
                        {
                            new Ring(-1.0f, 0.92f, 0.62f, 0.2f, 5f), new Ring(-0.8f, 1.0f, 0.66f, 0.18f, 5f),
                            new Ring(-0.4f, 1.0f, 0.60f, 0.18f, 5f), new Ring(0.0f, 0.98f, 0.54f, 0.18f, 5f),
                            new Ring(0.45f, 0.94f, 0.56f, 0.18f, 5f), new Ring(0.85f, 0.9f, 0.52f, 0.2f, 5f),
                            new Ring(1.05f, 0.84f, 0.46f, 0.22f, 4.5f), new Ring(1.14f, 0.7f, 0.4f, 0.25f, 4f),
                        },
                        Sidepods = false, FrontWing = false, Fenders = true, RollBar = true, BullBar = true,
                        WingSpan = 0f, FrontR = 0.27f, FrontW = 0.26f, RearR = 0.34f, RearW = 0.38f,
                        FrontX = 0.66f, RearX = 0.68f, RearZ = -0.62f, ExhaustR = 0.075f, CockpitY = 0.58f,
                    };
                default:
                    return new Spec
                    {
                        Body = new[]
                        {
                            new Ring(-0.98f, 0.62f, 0.50f, 0.15f), new Ring(-0.85f, 0.78f, 0.56f, 0.14f),
                            new Ring(-0.5f, 0.82f, 0.58f, 0.14f), new Ring(-0.2f, 0.84f, 0.52f, 0.14f),
                            new Ring(0.1f, 0.8f, 0.47f, 0.14f), new Ring(0.4f, 0.66f, 0.45f, 0.14f),
                            new Ring(0.7f, 0.52f, 0.42f, 0.14f), new Ring(0.95f, 0.42f, 0.38f, 0.15f),
                            new Ring(1.12f, 0.3f, 0.33f, 0.16f), new Ring(1.22f, 0.1f, 0.28f, 0.2f, 2f),
                        },
                    };
            }
        }

        public static KartRig Build(Transform parent, KartDesign design, KartLivery liv)
        {
            var spec = GetSpec(design);
            var rig = new KartRig();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(parent, false);
            rig.Visual = visual;
            var mat = Mats.VertexLit;

            var b = new MeshBuilder();
            BuildChassis(b, spec, liv, design, rig);
            b.Build("Chassis", visual, mat);

            // wheels
            var frontWheel = WheelMesh(spec.FrontR, spec.FrontW, liv);
            var rearWheel = WheelMesh(spec.RearR, spec.RearW, liv);
            Vector3[] centers =
            {
                new Vector3(-spec.FrontX, spec.FrontR, spec.FrontZ), new Vector3(spec.FrontX, spec.FrontR, spec.FrontZ),
                new Vector3(-spec.RearX, spec.RearR, spec.RearZ), new Vector3(spec.RearX, spec.RearR, spec.RearZ),
            };
            for (int i = 0; i < 4; i++)
            {
                Transform holder = visual;
                if (i < 2)
                {
                    var pivot = new GameObject(i == 0 ? "SteerFL" : "SteerFR").transform;
                    pivot.SetParent(visual, false);
                    pivot.localPosition = centers[i];
                    rig.SteerPivots[i] = pivot;
                    holder = pivot;
                }
                var w = new GameObject("Wheel" + i, typeof(MeshFilter), typeof(MeshRenderer));
                w.transform.SetParent(holder, false);
                w.transform.localPosition = i < 2 ? Vector3.zero : centers[i];
                w.transform.localRotation = i % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                w.GetComponent<MeshFilter>().sharedMesh = i < 2 ? frontWheel : rearWheel;
                w.GetComponent<MeshRenderer>().sharedMaterial = mat;
                rig.Wheels[i] = w.transform;
                rig.WheelRadius[i] = i < 2 ? spec.FrontR : spec.RearR;
            }
            rig.RearContacts[0] = new Vector3(-spec.RearX, 0.02f, spec.RearZ);
            rig.RearContacts[1] = new Vector3(spec.RearX, 0.02f, spec.RearZ);

            // steering wheel
            var sw = new GameObject("SteeringWheel", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            sw.SetParent(visual, false);
            float swY = spec.CockpitY + 0.2f, swZ = spec.DriverZ + 0.42f;
            sw.localPosition = new Vector3(0, swY, swZ);
            sw.localRotation = Quaternion.Euler(-55f, 0, 0);   // wheel plane tilted toward the driver
            var swb = new MeshBuilder();
            swb.Gloss = 0.4f;
            swb.Torus(Vector3.zero, Quaternion.identity, 0.15f, 0.024f, 20, 8, new Color(0.12f, 0.12f, 0.14f));
            swb.Cylinder(new Vector3(0, -0.02f, 0), new Vector3(0, 0.03f, 0), 0.045f, 10, liv.Secondary);
            swb.Box(new Vector3(0, 0, 0), new Vector3(0.28f, 0.02f, 0.035f), new Color(0.2f, 0.2f, 0.22f));
            swb.Box(new Vector3(0, 0, -0.07f), new Vector3(0.03f, 0.02f, 0.14f), new Color(0.2f, 0.2f, 0.22f));
            sw.GetComponent<MeshFilter>().sharedMesh = swb.ToMesh("SteeringWheel");
            sw.GetComponent<MeshRenderer>().sharedMaterial = mat;
            rig.SteeringWheel = sw;

            BuildDriver(visual, spec, liv, rig, swY, swZ);
            return rig;
        }

        // ------------------------------------------------------------ chassis

        static void BuildChassis(MeshBuilder b, Spec s, KartLivery liv, KartDesign design, KartRig rig)
        {
            Color dark = new Color(0.13f, 0.13f, 0.15f);
            Color metal = new Color(0.62f, 0.63f, 0.66f);
            Color chrome = new Color(0.85f, 0.86f, 0.9f);
            Color prim = liv.Primary, sec = liv.Secondary;

            // floor pan
            b.Gloss = 0.2f;
            b.RoundedBox(new Vector3(0, 0.15f, 0), new Vector3(0.95f, 0.07f, 1.95f), 0.03f, dark, 2);

            // body shell
            var rings = new List<Vector3[]>();
            foreach (var r in SubdivideRings(s.Body, 4))
            {
                var shape = MeshBuilder.Superellipse(r.w, r.top - r.bot, r.pow, 44, 0.2f);
                float yc = (r.top + r.bot) * 0.5f;
                var ring = new Vector3[shape.Length];
                for (int k = 0; k < shape.Length; k++) ring[k] = new Vector3(shape[k].x, yc + shape[k].y, r.z);
                rings.Add(ring);
            }
            float stripeW = design == KartDesign.Bubble ? 0.14f : 0.1f;
            b.Gloss = 0.6f;
            b.Loft(rings, (p, n) =>
            {
                if (n.y < -0.25f || p.y < 0.19f) return prim * 0.5f;                     // two-tone skirt
                float stripe = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stripeW + 0.04f, stripeW - 0.02f, Mathf.Abs(p.x)))
                               * Mathf.Clamp01((n.y - 0.2f) * 4f);                        // racing stripe
                return Color.Lerp(prim, sec, stripe);
            });

            // cockpit collar (hides where the driver meets the shell)
            b.Gloss = 0.15f;
            b.Push(new Vector3(0, s.CockpitY - 0.02f, s.DriverZ + 0.02f), Quaternion.identity, new Vector3(1f, 1f, 1.25f));
            b.Torus(Vector3.zero, Quaternion.identity, 0.24f, 0.05f, 20, 8, dark);
            b.Pop();
            b.Sphere(new Vector3(0, s.CockpitY - 0.05f, s.DriverZ + 0.02f), new Vector3(0.25f, 0.06f, 0.3f), 14, 6, dark);

            // seat
            b.Gloss = 0.25f;
            b.Push(new Vector3(0, s.CockpitY + 0.12f, s.SeatZ - 0.12f), Quaternion.Euler(-14f, 0, 0));
            b.RoundedBox(Vector3.zero, new Vector3(0.5f, 0.5f, 0.1f), 0.05f, new Color(0.18f, 0.18f, 0.22f), 2);
            b.RoundedBox(new Vector3(0, 0.3f, 0.01f), new Vector3(0.3f, 0.16f, 0.12f), 0.05f, sec * 0.9f, 2);
            b.Pop();

            // steering column + dash
            b.Gloss = 0.4f;
            b.Cylinder(new Vector3(0, s.CockpitY - 0.05f, s.DriverZ + 0.72f), new Vector3(0, s.CockpitY + 0.19f, s.DriverZ + 0.44f), 0.025f, 8, metal);
            b.RoundedBox(new Vector3(0, s.CockpitY + 0.0f, s.DriverZ + 0.66f), new Vector3(0.36f, 0.08f, 0.14f), 0.03f, dark, 2);

            // axles + suspension arms
            b.Gloss = 0.5f;
            b.Cylinder(new Vector3(-s.RearX + 0.05f, s.RearR, s.RearZ), new Vector3(s.RearX - 0.05f, s.RearR, s.RearZ), 0.035f, 8, metal);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 hub = new Vector3(side * (s.FrontX - 0.08f), s.FrontR, s.FrontZ);
                b.Tube(new[] { new Vector3(side * 0.3f, 0.2f, s.FrontZ - 0.12f), hub }, 0.022f, 6, metal);
                b.Tube(new[] { new Vector3(side * 0.3f, 0.3f, s.FrontZ + 0.08f), hub + Vector3.up * 0.06f }, 0.02f, 6, metal);
                b.Sphere(hub, 0.045f, 8, 6, dark);
            }

            // side pods
            if (s.Sidepods)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var pod = new List<Vector3[]>();
                    float[] zs = { -0.38f, -0.3f, 0.0f, 0.3f, 0.44f };
                    float[] ws = { 0.1f, 0.22f, 0.26f, 0.22f, 0.08f };
                    float[] hs = { 0.12f, 0.2f, 0.22f, 0.18f, 0.08f };
                    for (int i = 0; i < zs.Length; i++)
                    {
                        var shape = MeshBuilder.Superellipse(ws[i], hs[i], 3f, 16, 0.3f);
                        var ring = new Vector3[shape.Length];
                        for (int k = 0; k < shape.Length; k++)
                            ring[k] = new Vector3(side * 0.53f + shape[k].x, 0.28f + shape[k].y, zs[i]);
                        pod.Add(ring);
                    }
                    b.Gloss = 0.55f;
                    b.Loft(pod, (p, n) => n.y > 0.5f && Mathf.Abs(p.z) < 0.18f ? Color.white : sec);
                    // number roundel
                    b.Gloss = 0.3f;
                    b.Disc(new Vector3(side * 0.66f, 0.29f, 0.02f), new Vector3(side, 0, 0), 0.075f, 14, Color.white);
                }
            }

            // front wing / bumper
            b.Gloss = 0.55f;
            if (s.FrontWing)
            {
                b.Push(new Vector3(0, 0.16f, 1.12f), Quaternion.Euler(6f, 0, 0));
                b.RoundedBox(Vector3.zero, new Vector3(1.28f, 0.045f, 0.3f), 0.02f, sec, 2);
                b.RoundedBox(new Vector3(0, 0.05f, -0.05f), new Vector3(1.1f, 0.03f, 0.14f), 0.012f, prim, 1);
                for (int side = -1; side <= 1; side += 2)
                    b.RoundedBox(new Vector3(side * 0.64f, 0.06f, 0), new Vector3(0.035f, 0.17f, 0.34f), 0.015f, prim, 1);
                b.Pop();
            }
            else
            {
                var bumper = new[] { new Vector3(-0.55f, 0.22f, 1.0f), new Vector3(-0.42f, 0.2f, 1.18f), new Vector3(0, 0.2f, 1.26f), new Vector3(0.42f, 0.2f, 1.18f), new Vector3(0.55f, 0.22f, 1.0f) };
                b.Tube(SmoothPath(bumper, 4), 0.055f, 10, dark);
            }
            if (s.BullBar)
            {
                b.Gloss = 0.85f;
                var bar = new[] { new Vector3(-0.42f, 0.2f, 1.17f), new Vector3(-0.38f, 0.5f, 1.2f), new Vector3(0, 0.56f, 1.22f), new Vector3(0.38f, 0.5f, 1.2f), new Vector3(0.42f, 0.2f, 1.17f) };
                b.Tube(SmoothPath(bar, 4), 0.04f, 8, chrome);
                b.Tube(new[] { new Vector3(-0.4f, 0.36f, 1.19f), new Vector3(0.4f, 0.36f, 1.19f) }, 0.03f, 8, chrome);
            }

            // rear bumper
            b.Gloss = 0.3f;
            var rear = new[] { new Vector3(-0.72f, 0.24f, s.RearZ - 0.28f), new Vector3(-0.5f, 0.22f, -1.12f), new Vector3(0.5f, 0.22f, -1.12f), new Vector3(0.72f, 0.24f, s.RearZ - 0.28f) };
            b.Tube(SmoothPath(rear, 4), 0.045f, 8, dark);

            // engine + exhausts
            b.Gloss = 0.7f;
            b.RoundedBox(new Vector3(0, 0.42f, -0.8f), new Vector3(0.48f, 0.26f, 0.3f), 0.05f, metal * 0.8f, 2);
            for (int f = 0; f < 4; f++)
                b.RoundedBox(new Vector3(0, 0.58f + f * 0.035f, -0.8f), new Vector3(0.4f - f * 0.04f, 0.018f, 0.26f), 0.008f, metal, 1);
            var exhausts = new List<Vector3>();
            int count = s.Exhausts;
            for (int e = 0; e < count; e++)
            {
                float x = count == 1 ? 0f : Mathf.Lerp(-0.17f, 0.17f, (float)e / (count - 1));
                var path = new[] { new Vector3(x * 0.6f, 0.4f, -0.8f), new Vector3(x, 0.38f, -0.98f), new Vector3(x * 1.1f, 0.46f, -1.14f), new Vector3(x * 1.15f, 0.5f, -1.24f) };
                b.Gloss = 0.9f;
                b.Tube(SmoothPath(path, 4), s.ExhaustR, 10, chrome, false);
                Vector3 tip = path[path.Length - 1];
                Vector3 dir = (tip - path[path.Length - 2]).normalized;
                b.Cylinder(tip - dir * 0.06f, tip + dir * 0.01f, s.ExhaustR * 1.25f, 12, chrome);
                b.Gloss = 0f;
                b.Disc(tip + dir * 0.012f, dir, s.ExhaustR * 0.95f, 12, new Color(0.05f, 0.05f, 0.05f));
                exhausts.Add(tip + dir * 0.03f);
            }
            rig.Exhausts = exhausts.ToArray();

            // tail lights
            b.Emissive = 0.9f;
            b.Gloss = 0.5f;
            for (int side = -1; side <= 1; side += 2)
                b.RoundedBox(new Vector3(side * 0.28f, 0.34f, -1.02f), new Vector3(0.14f, 0.06f, 0.04f), 0.015f, new Color(1f, 0.12f, 0.1f), 1);
            b.Emissive = 0f;

            // rear wing
            if (s.WingSpan > 0f)
            {
                b.Gloss = 0.5f;
                for (int side = -1; side <= 1; side += 2)
                    b.Tube(new[] { new Vector3(side * 0.28f, 0.5f, -0.88f), new Vector3(side * 0.3f, s.WingHeight - 0.02f, -1.02f) }, 0.028f, 6, dark);
                b.Push(new Vector3(0, s.WingHeight, -1.04f), Quaternion.Euler(-10f, 0, 0));
                b.Gloss = 0.6f;
                b.RoundedBox(Vector3.zero, new Vector3(s.WingSpan, 0.05f, 0.32f), 0.022f, (p, n) => n.y > 0.5f && Mathf.Abs(p.x) < 0.1f ? Color.white : prim, 2);
                b.RoundedBox(new Vector3(0, 0.07f, -0.1f), new Vector3(s.WingSpan * 0.94f, 0.03f, 0.14f), 0.012f, sec, 1);
                for (int side = -1; side <= 1; side += 2)
                    b.RoundedBox(new Vector3(side * s.WingSpan * 0.5f, 0.03f, -0.02f), new Vector3(0.035f, 0.26f, 0.4f), 0.015f, sec, 1);
                b.Pop();
            }

            // roll bar
            if (s.RollBar)
            {
                b.Gloss = 0.85f;
                var bar = new[] { new Vector3(-0.3f, s.CockpitY - 0.05f, s.SeatZ - 0.2f), new Vector3(-0.28f, s.CockpitY + 0.5f, s.SeatZ - 0.26f), new Vector3(0, s.CockpitY + 0.6f, s.SeatZ - 0.28f), new Vector3(0.28f, s.CockpitY + 0.5f, s.SeatZ - 0.26f), new Vector3(0.3f, s.CockpitY - 0.05f, s.SeatZ - 0.2f) };
                b.Tube(SmoothPath(bar, 4), 0.035f, 8, chrome);
                b.Tube(new[] { new Vector3(0, s.CockpitY + 0.58f, s.SeatZ - 0.28f), new Vector3(0, 0.5f, -0.95f) }, 0.028f, 6, chrome);
            }

            // fenders over each wheel
            if (s.Fenders)
            {
                b.Gloss = 0.6f;
                Vector3[] wc =
                {
                    new Vector3(-s.FrontX, s.FrontR, s.FrontZ), new Vector3(s.FrontX, s.FrontR, s.FrontZ),
                    new Vector3(-s.RearX, s.RearR, s.RearZ), new Vector3(s.RearX, s.RearR, s.RearZ),
                };
                for (int i = 0; i < 4; i++)
                {
                    float r = (i < 2 ? s.FrontR : s.RearR) + 0.07f;
                    float w = (i < 2 ? s.FrontW : s.RearW) + 0.08f;
                    var path = new List<Vector3>();
                    for (int k = 0; k <= 12; k++)
                    {
                        float a = Mathf.Lerp(-15f, 165f, k / 12f) * Mathf.Deg2Rad;
                        path.Add(wc[i] + new Vector3(0, Mathf.Sin(a) * r, Mathf.Cos(a) * r));
                    }
                    var shape = MeshBuilder.RoundRect(w, 0.06f, 0.025f, 2);
                    // start "up" radially so the section's width lies along the axle (X)
                    Vector3 radial = (path[0] - wc[i]).normalized;
                    int wheelIdx = i;
                    b.Sweep(path, shape, (p, n) => wheelIdx >= 2 && n.y > 0.8f ? sec : prim, radial, true, true);
                }
            }

            // headlights
            if (s.EyeLights)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 c = new Vector3(side * 0.24f, 0.46f, 1.02f);
                    b.Gloss = 0.7f;
                    b.Sphere(c, new Vector3(0.12f, 0.12f, 0.08f), 14, 10, Color.white);
                    b.Gloss = 0.9f;
                    b.Sphere(c + new Vector3(side * -0.015f, 0.01f, 0.06f), new Vector3(0.055f, 0.065f, 0.03f), 10, 8, new Color(0.08f, 0.08f, 0.12f));
                    b.Emissive = 1f;
                    b.Sphere(c + new Vector3(side * -0.035f, 0.035f, 0.085f), 0.015f, 6, 4, Color.white);
                    b.Emissive = 0f;
                }
            }
            else
            {
                b.Emissive = 0.6f;
                b.Gloss = 0.8f;
                for (int side = -1; side <= 1; side += 2)
                    b.Sphere(new Vector3(side * 0.16f, 0.3f, s.Body[s.Body.Length - 2].z + 0.02f), new Vector3(0.05f, 0.035f, 0.03f), 8, 6, new Color(1f, 0.95f, 0.8f));
                b.Emissive = 0f;
            }

            // small antenna flag (KartRider flair)
            b.Gloss = 0.4f;
            Vector3 ant = new Vector3(0.36f, 0.45f, -0.95f);
            b.Cylinder(ant, ant + new Vector3(0.02f, 0.75f, -0.08f), 0.008f, 4, dark, false);
            Vector3 top = ant + new Vector3(0.02f, 0.75f, -0.08f);
            int f0 = b.Vert(top, Vector3.right, sec), f1 = b.Vert(top + new Vector3(0, -0.14f, -0.02f), Vector3.right, sec), f2 = b.Vert(top + new Vector3(0, -0.07f, -0.24f), Vector3.right, sec);
            b.Tri(f0, f1, f2); b.Tri(f0, f2, f1);
            b.Gloss = 0f;
        }

        static List<Ring> SubdivideRings(Ring[] rs, int sub)
        {
            var list = new List<Ring>();
            int n = rs.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Ring a = rs[Mathf.Max(i - 1, 0)], b0 = rs[i], c = rs[i + 1], d = rs[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < sub; k++)
                {
                    float t = (float)k / sub;
                    list.Add(new Ring(
                        CR(a.z, b0.z, c.z, d.z, t), Mathf.Max(0.02f, CR(a.w, b0.w, c.w, d.w, t)),
                        CR(a.top, b0.top, c.top, d.top, t), CR(a.bot, b0.bot, c.bot, d.bot, t),
                        Mathf.Lerp(b0.pow, c.pow, t)));
                }
            }
            list.Add(rs[n - 1]);
            return list;
        }

        static float CR(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static List<Vector3> SmoothPath(Vector3[] pts, int sub)
        {
            var list = new List<Vector3>();
            int n = pts.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < sub; k++)
                {
                    float t = (float)k / sub, t2 = t * t, t3 = t2 * t;
                    list.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            list.Add(pts[n - 1]);
            return list;
        }

        // ------------------------------------------------------------ wheels

        static Mesh WheelMesh(float R, float W, KartLivery liv)
        {
            var b = new MeshBuilder();
            float rim = R * 0.62f;
            float hw = W * 0.5f;
            // tire, built around Y then rotated so the axle is X
            b.Push(Vector3.zero, Quaternion.Euler(0, 0, 90f));
            var tire = new[]
            {
                new Vector2(rim, -hw * 0.92f), new Vector2(R * 0.86f, -hw), new Vector2(R * 0.97f, -hw * 0.86f),
                new Vector2(R, -hw * 0.6f), new Vector2(R, hw * 0.6f), new Vector2(R * 0.97f, hw * 0.86f),
                new Vector2(R * 0.86f, hw), new Vector2(rim, hw * 0.92f),
            };
            b.Gloss = 0.12f;
            b.Lathe(tire, 28, (p, n) =>
            {
                float ang = Mathf.Atan2(p.z, p.x);
                bool groove = Mathf.Abs(p.y) < hw * 0.62f && Mathf.Repeat(ang * 18f / Mathf.PI, 1f) < 0.22f;
                return groove ? new Color(0.05f, 0.05f, 0.06f) : new Color(0.14f, 0.14f, 0.15f);
            });
            // rim dish (outer face at -Y after rotation -> visible outward on the left wheel)
            var dish = new[]
            {
                new Vector2(rim * 1.02f, -hw * 0.95f), new Vector2(rim * 0.98f, -hw * 0.75f), new Vector2(rim * 0.9f, -hw * 0.55f),
                new Vector2(rim * 0.35f, -hw * 0.42f), new Vector2(rim * 0.3f, -hw * 0.6f), new Vector2(0f, -hw * 0.65f),
            };
            b.Gloss = 0.85f;
            b.Lathe(dish, 20, (p, n) => new Vector2(p.x, p.z).magnitude > rim * 0.88f ? liv.Secondary : new Color(0.82f, 0.83f, 0.87f));
            var inner = new[] { new Vector2(0, hw * 0.5f), new Vector2(rim * 0.95f, hw * 0.7f), new Vector2(rim * 1.02f, hw * 0.92f) };
            b.Gloss = 0.4f;
            b.Lathe(inner, 16, new Color(0.3f, 0.3f, 0.33f));
            // spokes
            b.Gloss = 0.85f;
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f;
                b.Push(new Vector3(0, -hw * 0.55f, 0), Quaternion.Euler(0, a, 0));
                b.RoundedBox(new Vector3(rim * 0.55f, 0, 0), new Vector3(rim * 0.72f, hw * 0.25f, rim * 0.2f), 0.01f, new Color(0.9f, 0.9f, 0.93f), 1);
                b.Pop();
            }
            b.Cone(new Vector3(0, -hw * 0.62f, 0), new Vector3(0, -hw * 0.95f, 0), rim * 0.22f, 10, liv.Secondary);
            b.Pop();
            b.Gloss = 0f;
            return b.ToMesh("Wheel");
        }

        // ------------------------------------------------------------ driver

        static void BuildDriver(Transform visual, Spec s, KartLivery liv, KartRig rig, float swY, float swZ)
        {
            var mat = Mats.VertexLit;
            var driver = new GameObject("Driver").transform;
            driver.SetParent(visual, false);
            driver.localPosition = new Vector3(0, s.CockpitY - 0.12f, s.DriverZ);
            rig.Driver = driver;

            var b = new MeshBuilder();
            Color suit = liv.Suit;
            // torso
            b.Gloss = 0.2f;
            b.Push(new Vector3(0, 0.28f, 0.0f), Quaternion.Euler(-8f, 0, 0));
            b.Sphere(Vector3.zero, new Vector3(0.21f, 0.27f, 0.17f), 16, 12, (p, n) =>
            {
                if (Mathf.Abs(p.x) < 0.035f && p.z > 0.05f) return Color.white;            // zipper stripe
                if (Mathf.Abs(Mathf.Abs(p.x) - 0.13f) < 0.025f) return liv.Secondary;    // side stripes
                return suit;
            });
            b.Pop();
            // collar + neck
            b.Torus(new Vector3(0, 0.53f, -0.01f), Quaternion.identity, 0.085f, 0.035f, 14, 6, liv.Secondary);
            b.Cylinder(new Vector3(0, 0.5f, -0.01f), new Vector3(0, 0.6f, -0.01f), 0.06f, 8, liv.Skin * 0.9f);
            // arms to the steering wheel (in driver space)
            Vector3 wheelLocal = new Vector3(0, swY - driver.localPosition.y, swZ - driver.localPosition.z);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoulder = new Vector3(side * 0.2f, 0.44f, 0.0f);
                Vector3 hand = wheelLocal + new Vector3(side * 0.13f, 0.03f, -0.03f);
                Vector3 elbow = Vector3.Lerp(shoulder, hand, 0.5f) + new Vector3(side * 0.08f, -0.1f, -0.02f);
                b.Gloss = 0.2f;
                b.Sphere(shoulder, 0.075f, 10, 8, suit);
                b.Tube(SmoothPath(new[] { shoulder, elbow, hand }, 3), 0.055f, 8, suit, false);
                b.Gloss = 0.3f;
                b.Sphere(hand, new Vector3(0.06f, 0.055f, 0.07f), 10, 8, Color.white);
                b.Torus(hand + (shoulder - hand).normalized * 0.06f, Quaternion.FromToRotation(Vector3.up, (shoulder - hand).normalized), 0.055f, 0.018f, 10, 5, liv.Secondary);
            }
            b.Build("Body", driver, mat);

            // head: face + open helmet + goggles
            var head = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            head.SetParent(driver, false);
            head.localPosition = new Vector3(0, 0.6f, 0.0f);
            rig.Head = head;
            var h = new MeshBuilder();
            Vector3 hc = new Vector3(0, 0.2f, 0.01f);
            h.Gloss = 0.1f;
            h.Sphere(hc, new Vector3(0.205f, 0.2f, 0.2f), 18, 14, liv.Skin);
            // eyes
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 eye = hc + new Vector3(side * 0.075f, 0.0f, 0.18f);
                h.Gloss = 0.8f;
                h.Sphere(eye, new Vector3(0.032f, 0.048f, 0.025f), 10, 8, new Color(0.08f, 0.06f, 0.1f));
                h.Emissive = 0.6f;
                h.Sphere(eye + new Vector3(side * -0.01f, 0.018f, 0.02f), 0.011f, 6, 4, Color.white);
                h.Emissive = 0f;
                h.Gloss = 0f;
                h.Sphere(hc + new Vector3(side * 0.125f, -0.06f, 0.14f), new Vector3(0.035f, 0.02f, 0.02f), 8, 6, new Color(1f, 0.6f, 0.6f));
            }
            // smile
            var smile = new List<Vector3>();
            for (int k = 0; k <= 6; k++)
            {
                float a = Mathf.Lerp(-40f, 40f, k / 6f) * Mathf.Deg2Rad;
                smile.Add(hc + new Vector3(Mathf.Sin(a) * 0.05f, -0.075f - Mathf.Cos(a) * 0.015f + 0.015f, 0.19f - Mathf.Abs(Mathf.Sin(a)) * 0.012f));
            }
            h.Tube(smile, 0.008f, 5, new Color(0.45f, 0.15f, 0.15f));
            // helmet shell: full cap above the brow + back/sides leaving a face opening
            Color hel = liv.Helmet;
            ColorFn helmetCol = (p, n) =>
            {
                Vector3 q = p - hc;
                if (Mathf.Abs(q.x) < 0.04f && q.y > 0.05f) return liv.Secondary;
                if (Mathf.Abs(Mathf.Abs(q.x) - 0.07f) < 0.012f && q.y > 0.05f) return Color.white;
                return hel;
            };
            Vector3 hr = new Vector3(0.245f, 0.24f, 0.25f);
            h.Gloss = 0.75f;
            h.Sphere(hc, hr, 20, 6, helmetCol, 22f, 90f, 0f, 360f);
            h.Sphere(hc, hr, 16, 8, helmetCol, -38f, 22f, 125f, 415f);
            // rim around the opening
            h.Gloss = 0.3f;
            var rimPath = new List<Vector3>();
            for (int k = 0; k <= 16; k++)
            {
                float t = k / 16f;
                float lat, lon;
                if (t < 0.25f) { lat = Mathf.Lerp(-38f, 22f, t / 0.25f); lon = 55f; }
                else if (t < 0.75f) { lat = 22f; lon = Mathf.Lerp(55f, 125f, (t - 0.25f) / 0.5f); }
                else { lat = Mathf.Lerp(22f, -38f, (t - 0.75f) / 0.25f); lon = 125f; }
                float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
                rimPath.Add(hc + Vector3.Scale(new Vector3(Mathf.Cos(la) * Mathf.Cos(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Sin(lo)), hr));
            }
            h.Tube(rimPath, 0.018f, 6, liv.Secondary);
            // goggles on the forehead
            h.Gloss = 0.3f;
            h.Push(hc + new Vector3(0, 0.1f, 0), Quaternion.Euler(-18f, 0, 0), new Vector3(1f, 1f, 1.03f));
            h.Torus(Vector3.zero, Quaternion.identity, 0.24f, 0.02f, 24, 5, new Color(0.2f, 0.2f, 0.25f));
            h.Pop();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 g = hc + new Vector3(side * 0.075f, 0.155f, 0.19f);
                h.Gloss = 0.5f;
                h.Torus(g, Quaternion.Euler(70f, 0, 0), 0.055f, 0.016f, 14, 6, new Color(0.85f, 0.65f, 0.2f));
                h.Gloss = 0.95f;
                h.Sphere(g + new Vector3(0, 0.005f, 0.01f), new Vector3(0.05f, 0.045f, 0.02f), 10, 6, new Color(0.3f, 0.75f, 0.95f));
            }
            // little pom-pom on top
            h.Gloss = 0.1f;
            h.Blob(hc + new Vector3(0, 0.25f, -0.03f), Vector3.one * 0.05f, 1, 0.2f, 3f, 7, (p, n) => liv.Secondary, false);
            h.Gloss = 0f;
            head.GetComponent<MeshFilter>().sharedMesh = h.ToMesh("Head");
            head.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
