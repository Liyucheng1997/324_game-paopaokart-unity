using UnityEngine;

namespace KartGame
{
    public class Rng
    {
        readonly System.Random r;
        public Rng(int seed) { r = new System.Random(seed); }
        public float Value => (float)r.NextDouble();
        public float Range(float a, float b) => a + (b - a) * Value;
        public int Range(int a, int bExclusive) => r.Next(a, bExclusive);
        public bool Chance(float p) => Value < p;
        public T Pick<T>(T[] arr) => arr[r.Next(arr.Length)];
        public Color Jitter(Color c, float amt) => new Color(
            Mathf.Clamp01(c.r * (1f + Range(-amt, amt))), Mathf.Clamp01(c.g * (1f + Range(-amt, amt))),
            Mathf.Clamp01(c.b * (1f + Range(-amt, amt))), c.a);
    }

    /// <summary>Procedural scenery pieces written into a (batched) MeshBuilder at a local transform.</summary>
    public static class Props
    {
        static readonly Color Bark = new Color(0.42f, 0.28f, 0.17f);
        static readonly Color Snow = new Color(0.94f, 0.97f, 1f);

        // ------------------------------------------------------------ vegetation

        public static void PineTree(MeshBuilder b, Vector3 pos, float h, Rng rng, Color leaf, bool snowy)
        {
            b.Push(pos, Quaternion.Euler(0, rng.Range(0f, 360f), 0));
            b.Gloss = 0f;
            b.Cylinder(Vector3.zero, new Vector3(0, h * 0.3f, 0), h * 0.045f, 6, Bark, false);
            int tiers = 4;
            for (int t = 0; t < tiers; t++)
            {
                float f = (float)t / tiers;
                float y0 = h * (0.18f + f * 0.62f);
                float rad = h * (0.36f - f * 0.22f) * rng.Range(0.9f, 1.1f);
                float th = h * 0.36f;
                Color c = rng.Jitter(Color.Lerp(leaf, leaf * 1.25f, f), 0.06f);
                var prof = new[]
                {
                    new Vector2(0, y0 - th * 0.05f), new Vector2(rad, y0 - th * 0.05f), new Vector2(rad, y0 - th * 0.05f),
                    new Vector2(rad * 0.93f, y0 + th * 0.05f), new Vector2(rad * 0.45f, y0 + th * 0.5f), new Vector2(0, y0 + th),
                };
                float snowLine = y0 + th * 0.12f;
                b.Lathe(prof, 9, (p, n) =>
                {
                    if (snowy && n.y > 0.45f && p.y > snowLine) return Snow;
                    float wob = Noise.Value3(p * 3.1f) * 0.18f;
                    return c * (0.9f + wob);
                });
            }
            b.Pop();
        }

        public static void RoundTree(MeshBuilder b, Vector3 pos, float h, Rng rng, Color leaf)
        {
            b.Push(pos, Quaternion.Euler(0, rng.Range(0f, 360f), 0));
            b.Gloss = 0f;
            var trunk = new[] { new Vector2(h * 0.08f, 0), new Vector2(h * 0.055f, h * 0.2f), new Vector2(h * 0.04f, h * 0.5f), new Vector2(h * 0.03f, h * 0.62f) };
            b.Lathe(trunk, 7, Bark);
            int blobs = rng.Range(3, 5);
            for (int i = 0; i < blobs; i++)
            {
                float a = i * Mathf.PI * 2f / blobs + rng.Range(-0.4f, 0.4f);
                float rr = i == 0 ? 0f : h * 0.17f;
                Vector3 c = new Vector3(Mathf.Cos(a) * rr, h * (0.62f + (i == 0 ? 0.14f : rng.Range(-0.02f, 0.08f))), Mathf.Sin(a) * rr);
                float s = h * (i == 0 ? 0.3f : rng.Range(0.2f, 0.26f));
                Color lc = rng.Jitter(leaf, 0.08f);
                int seed = rng.Range(0, 9999);
                b.Blob(c, new Vector3(s, s * 0.85f, s), 1, 0.22f, 2.2f, seed, (p, n) =>
                    lc * (0.82f + Mathf.Clamp01(n.y * 0.5f + 0.5f) * 0.3f), true);
            }
            b.Pop();
        }

        public static void Bush(MeshBuilder b, Vector3 pos, float s, Rng rng, Color leaf, Color? flower = null)
        {
            b.Gloss = 0f;
            int n = rng.Range(2, 4);
            for (int i = 0; i < n; i++)
            {
                Vector3 c = pos + new Vector3(rng.Range(-s, s) * 0.6f, s * 0.45f, rng.Range(-s, s) * 0.6f);
                float r = s * rng.Range(0.5f, 0.75f);
                Color lc = rng.Jitter(leaf, 0.1f);
                int seed = rng.Range(0, 9999);
                Color fc = flower ?? lc;
                bool hasFlowers = flower.HasValue;
                b.Blob(c, new Vector3(r, r * 0.8f, r), 1, 0.25f, 2.5f, seed, (p, nn) =>
                {
                    if (hasFlowers && Noise.Value3(p * 6f) > 0.72f) return fc;
                    return lc * (0.85f + nn.y * 0.2f);
                }, true, 0.4f);
            }
        }

        public static void Rock(MeshBuilder b, Vector3 pos, float s, Rng rng, Color col, bool snowy)
        {
            b.Gloss = 0.05f;
            Color c = rng.Jitter(col, 0.08f);
            b.Blob(pos + Vector3.up * s * 0.25f, new Vector3(s, s * rng.Range(0.5f, 0.8f), s * rng.Range(0.7f, 1.1f)), 1, 0.3f, 1.6f,
                rng.Range(0, 9999), (p, n) => snowy && n.y > 0.55f ? Snow : c * (0.85f + n.y * 0.2f), true, 0.5f);
            b.Gloss = 0f;
        }

        public static void Mushroom(MeshBuilder b, Vector3 pos, float h, Rng rng)
        {
            b.Push(pos, Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(0f, 360f), rng.Range(-6f, 6f)));
            b.Gloss = 0.1f;
            var stem = new[] { new Vector2(h * 0.16f, 0), new Vector2(h * 0.12f, h * 0.35f), new Vector2(h * 0.11f, h * 0.7f), new Vector2(0.0f, h * 0.72f) };
            b.Lathe(stem, 10, new Color(0.96f, 0.9f, 0.78f));
            Color cap = rng.Pick(new[] { new Color(0.9f, 0.18f, 0.15f), new Color(1f, 0.55f, 0.1f), new Color(0.6f, 0.3f, 0.85f) });
            var capProf = new[]
            {
                new Vector2(0, h * 0.62f), new Vector2(h * 0.4f, h * 0.62f), new Vector2(h * 0.46f, h * 0.66f),
                new Vector2(h * 0.44f, h * 0.78f), new Vector2(h * 0.32f, h * 0.93f), new Vector2(h * 0.15f, h * 1.0f), new Vector2(0, h * 1.02f),
            };
            b.Gloss = 0.35f;
            b.Lathe(capProf, 16, (p, n) =>
            {
                if (n.y < -0.3f) return new Color(0.95f, 0.88f, 0.75f);
                Vector3 d = p.normalized;
                float spot = Noise.Value3(d * 5.3f);
                return spot > 0.68f ? Color.white : cap;
            });
            b.Gloss = 0f;
            b.Pop();
        }

        // ------------------------------------------------------------ buildings

        public static void House(MeshBuilder b, Vector3 pos, float yaw, Rng rng, Color wall, Color roof, bool snowy)
        {
            b.Push(pos, Quaternion.Euler(0, yaw, 0));
            float w = rng.Range(6f, 8f), d = rng.Range(5f, 7f), h = rng.Range(3.5f, 4.5f);
            b.Gloss = 0.05f;
            // stone base + walls
            b.RoundedBox(new Vector3(0, 0.3f, 0), new Vector3(w + 0.3f, 0.6f, d + 0.3f), 0.1f, new Color(0.55f, 0.53f, 0.5f), 1);
            b.RoundedBox(new Vector3(0, h * 0.5f + 0.3f, 0), new Vector3(w, h, d), 0.12f, (p, n) => wall * (0.95f + Noise.Value3(p * 2f) * 0.08f), 1);
            // timber beams
            Color beam = new Color(0.36f, 0.22f, 0.13f);
            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(new Vector3(s * (w * 0.5f + 0.02f), h * 0.5f + 0.3f, 0), new Vector3(0.12f, h, 0.25f), beam);
                b.Box(new Vector3(0, h * 0.5f + 0.3f, s * (d * 0.5f + 0.02f)), new Vector3(0.25f, h, 0.12f), beam);
            }
            // roof: two slabs + gables
            float rh = w * 0.42f, over = 0.5f;
            float top = h + 0.3f;
            float half = w * 0.5f + over;
            float slopeLen = Mathf.Sqrt(half * half + rh * rh);
            float ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            b.Gloss = 0.15f;
            for (int s = -1; s <= 1; s += 2)
            {
                b.Push(new Vector3(-s * half * 0.5f, top + rh * 0.5f - 0.1f, 0), Quaternion.Euler(0, 0, s * ang));
                b.RoundedBox(Vector3.zero, new Vector3(slopeLen + 0.1f, 0.22f, d + over * 2f), 0.06f, (p, n) =>
                    snowy && n.y > 0.3f ? Snow : roof * (0.92f + Mathf.Repeat(p.x * 2.5f, 1f) * 0.12f), 1);
                b.Pop();
            }
            // gable triangles
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * d * 0.5f;
                Vector3 a = new Vector3(-w * 0.5f, top, z), c = new Vector3(w * 0.5f, top, z), t = new Vector3(0, top + rh * 0.95f, z);
                int i0 = b.Vert(a, new Vector3(0, 0, s), wall), i1 = b.Vert(c, new Vector3(0, 0, s), wall), i2 = b.Vert(t, new Vector3(0, 0, s), wall);
                b.TriFacing(i0, i1, i2, new Vector3(0, 0, s));
            }
            // chimney
            b.Gloss = 0.05f;
            b.RoundedBox(new Vector3(w * 0.22f, top + rh * 0.75f, d * 0.2f), new Vector3(0.7f, rh * 1.1f, 0.7f), 0.05f, new Color(0.62f, 0.35f, 0.28f), 1);
            // door + windows
            b.Box(new Vector3(0, 1.3f, d * 0.5f + 0.06f), new Vector3(1.2f, 2.0f, 0.1f), new Color(0.45f, 0.25f, 0.12f));
            b.Gloss = 0.8f;
            Color glass = new Color(0.35f, 0.55f, 0.75f);
            Color frame = new Color(0.95f, 0.95f, 0.9f);
            foreach (float x in new[] { -w * 0.3f, w * 0.3f })
            {
                b.Gloss = 0.05f;
                b.Box(new Vector3(x, h * 0.55f + 0.3f, d * 0.5f + 0.05f), new Vector3(1.25f, 1.25f, 0.08f), frame);
                b.Gloss = 0.85f;
                b.Box(new Vector3(x, h * 0.55f + 0.3f, d * 0.5f + 0.1f), new Vector3(1.0f, 1.0f, 0.04f), glass);
            }
            foreach (float z in new[] { -d * 0.2f, d * 0.2f })
                for (int s = -1; s <= 1; s += 2)
                {
                    b.Gloss = 0.05f;
                    b.Box(new Vector3(s * (w * 0.5f + 0.05f), h * 0.55f + 0.3f, z), new Vector3(0.08f, 1.2f, 1.2f), frame);
                    b.Gloss = 0.85f;
                    b.Box(new Vector3(s * (w * 0.5f + 0.1f), h * 0.55f + 0.3f, z), new Vector3(0.04f, 0.95f, 0.95f), glass);
                }
            b.Gloss = 0f;
            b.Pop();
        }

        public static void Cabin(MeshBuilder b, Vector3 pos, float yaw, Rng rng)
        {
            b.Push(pos, Quaternion.Euler(0, yaw, 0));
            float w = 6f, d = 5f;
            Color log = new Color(0.5f, 0.32f, 0.18f);
            b.Gloss = 0.05f;
            for (int i = 0; i < 9; i++)
            {
                float y = 0.2f + i * 0.36f;
                Color c = rng.Jitter(log, 0.08f);
                b.Cylinder(new Vector3(-w * 0.5f - 0.3f, y, d * 0.5f), new Vector3(w * 0.5f + 0.3f, y, d * 0.5f), 0.2f, 7, c);
                b.Cylinder(new Vector3(-w * 0.5f - 0.3f, y, -d * 0.5f), new Vector3(w * 0.5f + 0.3f, y, -d * 0.5f), 0.2f, 7, c);
                b.Cylinder(new Vector3(-w * 0.5f, y + 0.18f, -d * 0.5f - 0.3f), new Vector3(-w * 0.5f, y + 0.18f, d * 0.5f + 0.3f), 0.2f, 7, c);
                b.Cylinder(new Vector3(w * 0.5f, y + 0.18f, -d * 0.5f - 0.3f), new Vector3(w * 0.5f, y + 0.18f, d * 0.5f + 0.3f), 0.2f, 7, c);
            }
            b.Box(new Vector3(0, 1.7f, 0), new Vector3(w - 0.2f, 3.4f, d - 0.2f), log * 0.8f);
            float top = 3.5f, rh = 2.4f, half = w * 0.5f + 0.7f;
            float slopeLen = Mathf.Sqrt(half * half + rh * rh);
            float ang = Mathf.Atan2(rh, half) * Mathf.Rad2Deg;
            for (int s = -1; s <= 1; s += 2)
            {
                b.Push(new Vector3(-s * half * 0.5f, top + rh * 0.5f, 0), Quaternion.Euler(0, 0, s * ang));
                b.RoundedBox(Vector3.zero, new Vector3(slopeLen + 0.1f, 0.45f, d + 1.4f), 0.2f, (p, n) => n.y > 0.2f ? Snow : new Color(0.35f, 0.22f, 0.14f), 2);
                b.Pop();
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * (d * 0.5f + 0.1f);
                Vector3 a = new Vector3(-w * 0.5f, top, z), c = new Vector3(w * 0.5f, top, z), t = new Vector3(0, top + rh, z);
                int i0 = b.Vert(a, new Vector3(0, 0, s), log), i1 = b.Vert(c, new Vector3(0, 0, s), log), i2 = b.Vert(t, new Vector3(0, 0, s), log);
                b.TriFacing(i0, i1, i2, new Vector3(0, 0, s));
            }
            b.Emissive = 0.8f;
            b.Box(new Vector3(-1.4f, 1.8f, d * 0.5f + 0.25f), new Vector3(1.1f, 1.0f, 0.05f), new Color(1f, 0.75f, 0.35f));
            b.Box(new Vector3(1.4f, 1.8f, d * 0.5f + 0.25f), new Vector3(1.1f, 1.0f, 0.05f), new Color(1f, 0.75f, 0.35f));
            b.Emissive = 0f;
            b.Box(new Vector3(0, 1.2f, d * 0.5f + 0.25f), new Vector3(1.1f, 2.2f, 0.06f), new Color(0.3f, 0.18f, 0.1f));
            b.Pop();
        }

        public static void Snowman(MeshBuilder b, Vector3 pos, float s, Rng rng)
        {
            b.Push(pos, Quaternion.Euler(0, rng.Range(0f, 360f), 0), Vector3.one * s);
            b.Gloss = 0.15f;
            b.Sphere(new Vector3(0, 0.55f, 0), new Vector3(0.6f, 0.55f, 0.6f), 14, 10, Snow);
            b.Sphere(new Vector3(0, 1.35f, 0), 0.42f, 14, 10, Snow);
            b.Sphere(new Vector3(0, 1.98f, 0), 0.3f, 14, 10, Snow);
            b.Gloss = 0.4f;
            b.Cone(new Vector3(0, 1.98f, 0.26f), new Vector3(0, 1.96f, 0.6f), 0.06f, 8, new Color(1f, 0.5f, 0.1f));
            b.Sphere(new Vector3(-0.1f, 2.05f, 0.26f), 0.035f, 6, 4, Color.black);
            b.Sphere(new Vector3(0.1f, 2.05f, 0.26f), 0.035f, 6, 4, Color.black);
            Color scarf = rng.Pick(new[] { new Color(0.9f, 0.15f, 0.2f), new Color(0.2f, 0.5f, 0.95f), new Color(0.2f, 0.75f, 0.35f) });
            b.Torus(new Vector3(0, 1.7f, 0), Quaternion.identity, 0.3f, 0.07f, 14, 6, scarf);
            b.Cylinder(new Vector3(0, 2.2f, 0), new Vector3(0, 2.23f, 0), 0.34f, 12, new Color(0.1f, 0.1f, 0.12f));
            b.Cylinder(new Vector3(0, 2.23f, 0), new Vector3(0, 2.6f, 0), 0.22f, 12, new Color(0.1f, 0.1f, 0.12f));
            b.Gloss = 0f;
            b.Pop();
        }

        public static void IceCrystal(MeshBuilder b, Vector3 pos, float h, Rng rng)
        {
            b.Gloss = 0.9f;
            int n = rng.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                Quaternion q = Quaternion.Euler(rng.Range(-30f, 30f), rng.Range(0f, 360f), rng.Range(-30f, 30f));
                float hh = h * rng.Range(0.5f, 1f);
                float r = hh * 0.16f;
                b.Push(pos, q);
                var prof = new[] { new Vector2(r * 0.7f, -0.3f), new Vector2(r, hh * 0.1f), new Vector2(r, hh * 0.1f), new Vector2(r, hh * 0.75f), new Vector2(r, hh * 0.75f), new Vector2(0, hh) };
                b.Emissive = 0.15f;
                b.Lathe(prof, 6, new Color(0.6f, 0.85f, 1f));
                b.Emissive = 0f;
                b.Pop();
            }
            b.Gloss = 0f;
        }

        public static void Cactus(MeshBuilder b, Vector3 pos, float h, Rng rng)
        {
            Color g = rng.Jitter(new Color(0.3f, 0.6f, 0.3f), 0.1f);
            b.Gloss = 0.1f;
            b.Tube(new[] { pos, pos + Vector3.up * h }, h * 0.1f, 8, g);
            b.Sphere(pos + Vector3.up * h, h * 0.1f, 8, 5, g);
            for (int s = -1; s <= 1; s += 2)
            {
                if (!rng.Chance(0.75f)) continue;
                float y = h * rng.Range(0.35f, 0.6f);
                Vector3 a = pos + Vector3.up * y;
                Vector3 m = a + new Vector3(s * h * 0.25f, 0, 0);
                Vector3 t = m + Vector3.up * h * 0.3f;
                b.Tube(new[] { a, m, t }, h * 0.07f, 7, g);
                b.Sphere(t, h * 0.07f, 7, 4, g);
            }
            b.Gloss = 0f;
        }

        // ------------------------------------------------------------ track furniture

        public static void LampPost(MeshBuilder b, Vector3 pos, Vector3 towardRoad, Color pole)
        {
            b.Gloss = 0.5f;
            b.Cylinder(pos, pos + Vector3.up * 0.4f, 0.22f, 8, pole * 0.8f);
            b.Cylinder(pos, pos + Vector3.up * 6.2f, 0.1f, 8, pole);
            Vector3 top = pos + Vector3.up * 6.2f;
            Vector3 end = top + towardRoad.normalized * 1.6f + Vector3.up * 0.2f;
            b.Tube(new[] { top, top + towardRoad.normalized * 0.8f + Vector3.up * 0.35f, end }, 0.07f, 6, pole);
            b.RoundedBox(end + Vector3.down * 0.1f, new Vector3(0.6f, 0.2f, 0.6f), 0.08f, pole * 0.7f, 2);
            b.Emissive = 1f;
            b.Box(end + Vector3.down * 0.22f, new Vector3(0.45f, 0.05f, 0.45f), new Color(1f, 0.92f, 0.7f));
            b.Emissive = 0f;
            b.Gloss = 0f;
        }

        public static void TireStack(MeshBuilder b, Vector3 pos, int count, Color band, Rng rng)
        {
            b.Gloss = 0.15f;
            for (int i = 0; i < count; i++)
            {
                Color c = i % 2 == 0 ? new Color(0.1f, 0.1f, 0.11f) : band;
                b.Torus(pos + Vector3.up * (0.17f + i * 0.3f), Quaternion.Euler(0, rng.Range(0f, 90f), 0), 0.34f, 0.15f, 12, 6, c);
            }
            b.Gloss = 0f;
        }

        public static void Cone(MeshBuilder b, Vector3 pos)
        {
            b.Gloss = 0.3f;
            b.Box(pos + Vector3.up * 0.03f, new Vector3(0.5f, 0.06f, 0.5f), new Color(1f, 0.4f, 0.05f));
            var prof = new[] { new Vector2(0.2f, 0.06f), new Vector2(0.05f, 0.7f), new Vector2(0, 0.72f) };
            b.Push(pos);
            b.Lathe(prof, 10, (p, n) => (p.y > 0.3f && p.y < 0.45f) ? Color.white : new Color(1f, 0.4f, 0.05f));
            b.Pop();
            b.Gloss = 0f;
        }

        public static void ChevronSign(MeshBuilder signs, MeshBuilder posts, Vector3 pos, Vector3 facing, float width)
        {
            Vector3 f = new Vector3(facing.x, 0, facing.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, f);
            float h0 = 1.1f, h1 = 2.3f;
            posts.Gloss = 0.4f;
            posts.Cylinder(pos - side * width * 0.4f, pos - side * width * 0.4f + Vector3.up * h1, 0.07f, 6, new Color(0.7f, 0.7f, 0.72f));
            posts.Cylinder(pos + side * width * 0.4f, pos + side * width * 0.4f + Vector3.up * h1, 0.07f, 6, new Color(0.7f, 0.7f, 0.72f));
            posts.Gloss = 0f;
            Vector3 a = pos - side * width * 0.5f + Vector3.up * h0 + f * 0.08f;
            Vector3 c = pos + side * width * 0.5f + Vector3.up * h0 + f * 0.08f;
            // the sign faces toward the road (+f); CCW from the front: bl, br, tr, tl
            signs.Quad(c, a, a + Vector3.up * (h1 - h0), c + Vector3.up * (h1 - h0), Color.white);
            signs.Quad(a - f * 0.02f, c - f * 0.02f, c - f * 0.02f + Vector3.up * (h1 - h0), a - f * 0.02f + Vector3.up * (h1 - h0), new Color(0.3f, 0.3f, 0.32f));
        }

        public static void Billboard(MeshBuilder faces, MeshBuilder frame, Vector3 pos, Vector3 facing, float w, float h)
        {
            Vector3 f = new Vector3(facing.x, 0, facing.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, f);
            frame.Gloss = 0.3f;
            Color steel = new Color(0.55f, 0.57f, 0.6f);
            frame.Cylinder(pos - side * w * 0.42f, pos - side * w * 0.42f + Vector3.up * (h + 1.3f), 0.12f, 8, steel);
            frame.Cylinder(pos + side * w * 0.42f, pos + side * w * 0.42f + Vector3.up * (h + 1.3f), 0.12f, 8, steel);
            frame.Push(pos + Vector3.up * (1.2f + h * 0.5f) - f * 0.1f, Quaternion.LookRotation(f));
            frame.RoundedBox(Vector3.zero, new Vector3(w + 0.3f, h + 0.3f, 0.15f), 0.05f, new Color(0.15f, 0.15f, 0.18f), 1);
            frame.Pop();
            frame.Gloss = 0f;
            Vector3 bl = pos + side * (w * 0.5f) + Vector3.up * 1.2f;
            Vector3 br = pos - side * (w * 0.5f) + Vector3.up * 1.2f;
            faces.Quad(bl, br, br + Vector3.up * h, bl + Vector3.up * h, Color.white);
        }

        public static void FlagPole(MeshBuilder b, Vector3 pos, Color flag, float h = 7f)
        {
            b.Gloss = 0.6f;
            b.Cylinder(pos, pos + Vector3.up * h, 0.06f, 6, new Color(0.85f, 0.85f, 0.88f));
            b.Sphere(pos + Vector3.up * h, 0.1f, 6, 4, new Color(1f, 0.85f, 0.3f));
            b.Gloss = 0.05f;
            Vector3 t = pos + Vector3.up * (h - 0.15f);
            var q1 = new[] { t, t + new Vector3(1.6f, -0.35f, 0.25f), t + new Vector3(0, -1.2f, 0) };
            int i0 = b.Vert(q1[0], Vector3.forward, flag), i1 = b.Vert(q1[1], Vector3.forward, flag), i2 = b.Vert(q1[2], Vector3.forward, flag);
            b.Tri(i0, i1, i2); b.Tri(i0, i2, i1);
            b.Gloss = 0f;
        }

        public static void Grandstand(MeshBuilder b, Vector3 pos, Quaternion rot, float length, Rng rng, Color seatColor)
        {
            b.Push(pos, rot);
            int rows = 6;
            b.Gloss = 0.1f;
            Color concrete = new Color(0.75f, 0.74f, 0.72f);
            for (int r = 0; r < rows; r++)
            {
                float y = r * 0.55f, z = r * 0.9f;
                b.Box(new Vector3(0, y * 0.5f + 0.2f, z), new Vector3(length, y + 0.4f, 0.9f), concrete * (0.95f + r * 0.01f));
                b.Box(new Vector3(0, y + 0.45f, z - 0.15f), new Vector3(length, 0.1f, 0.45f), seatColor);
            }
            // back wall + roof
            b.Box(new Vector3(0, rows * 0.55f * 0.5f + 1.5f, rows * 0.9f), new Vector3(length, rows * 0.55f + 3f, 0.3f), concrete * 0.85f);
            for (float x = -length * 0.5f + 0.5f; x <= length * 0.5f; x += length / 5f)
                b.Cylinder(new Vector3(x, 0, -0.6f), new Vector3(x, 6.4f, -0.6f), 0.12f, 6, new Color(0.9f, 0.9f, 0.92f));
            b.Gloss = 0.3f;
            b.Push(new Vector3(0, 6.6f, rows * 0.45f - 0.3f), Quaternion.Euler(-8f, 0, 0));
            b.RoundedBox(Vector3.zero, new Vector3(length + 1f, 0.25f, rows * 0.9f + 2f), 0.1f, (p, n) =>
                Mathf.Repeat(p.x * 0.25f, 1f) < 0.5f ? new Color(0.95f, 0.25f, 0.2f) : new Color(0.98f, 0.98f, 0.98f), 1);
            b.Pop();
            // crowd
            b.Gloss = 0.05f;
            Color[] shirts =
            {
                new Color(0.95f, 0.3f, 0.25f), new Color(0.2f, 0.55f, 0.95f), new Color(1f, 0.85f, 0.2f),
                new Color(0.3f, 0.8f, 0.4f), new Color(0.95f, 0.95f, 0.95f), new Color(0.7f, 0.35f, 0.85f), new Color(1f, 0.55f, 0.15f),
            };
            Color[] skins = { new Color(1f, 0.85f, 0.7f), new Color(0.85f, 0.65f, 0.5f), new Color(0.6f, 0.42f, 0.3f) };
            for (int r = 0; r < rows; r++)
                for (float x = -length * 0.5f + 0.5f; x < length * 0.5f - 0.3f; x += 0.62f)
                {
                    if (rng.Chance(0.18f)) continue;
                    float y = r * 0.55f + 0.5f, z = r * 0.9f - 0.1f;
                    Vector3 p = new Vector3(x + rng.Range(-0.1f, 0.1f), y, z);
                    Color shirt = rng.Pick(shirts);
                    b.Sphere(p + Vector3.up * 0.3f, new Vector3(0.2f, 0.3f, 0.16f), 6, 4, shirt);
                    b.Sphere(p + Vector3.up * 0.72f, 0.15f, 6, 4, rng.Pick(skins));
                    if (rng.Chance(0.35f))   // cheering arm
                        b.Tube(new[] { p + new Vector3(0.15f, 0.45f, 0), p + new Vector3(0.22f, 0.95f, -0.05f) }, 0.05f, 4, shirt);
                }
            b.Gloss = 0f;
            b.Pop();
        }

        // ------------------------------------------------------------ animated set pieces (own objects)

        public static GameObject Windmill(Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("Windmill");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var b = new MeshBuilder();
            var tower = new[] { new Vector2(3.2f, 0), new Vector2(2.8f, 4), new Vector2(2.1f, 10), new Vector2(1.8f, 12), new Vector2(1.8f, 12) };
            b.Lathe(tower, 16, (p, n) => new Color(0.95f, 0.93f, 0.88f) * (Mathf.Repeat(p.y * 0.8f, 1f) < 0.08f ? 0.85f : 1f));
            var cap = new[] { new Vector2(2.2f, 11.8f), new Vector2(2.3f, 12.2f), new Vector2(1.6f, 13.6f), new Vector2(0.6f, 14.6f), new Vector2(0, 14.8f) };
            b.Gloss = 0.2f;
            b.Lathe(cap, 16, new Color(0.75f, 0.25f, 0.2f));
            b.Gloss = 0f;
            b.Box(new Vector3(0, 1.4f, 2.9f), new Vector3(1.4f, 2.6f, 0.4f), new Color(0.45f, 0.28f, 0.15f));
            b.Box(new Vector3(0, 6.5f, 2.5f), new Vector3(1f, 1.2f, 0.3f), new Color(0.3f, 0.45f, 0.65f));
            b.Build("Tower", root.transform, Mats.VertexLit);

            var hub = new GameObject("Blades");
            hub.transform.SetParent(root.transform, false);
            hub.transform.localPosition = new Vector3(0, 12.4f, 2.6f);
            var bb = new MeshBuilder();
            bb.Gloss = 0.1f;
            bb.Cylinder(new Vector3(0, 0, -0.8f), new Vector3(0, 0, 0.3f), 0.35f, 10, new Color(0.3f, 0.2f, 0.12f));
            for (int i = 0; i < 4; i++)
            {
                bb.Push(Vector3.zero, Quaternion.Euler(0, 0, i * 90f));
                bb.Box(new Vector3(0, 4.2f, 0.1f), new Vector3(0.18f, 8.4f, 0.12f), new Color(0.4f, 0.26f, 0.15f));
                bb.Box(new Vector3(0.7f, 5f, 0.12f), new Vector3(1.3f, 6.2f, 0.05f), new Color(0.96f, 0.94f, 0.88f));
                bb.Pop();
            }
            bb.Build("BladeMesh", hub.transform, Mats.VertexLit);
            hub.AddComponent<Spinner>().speed = new Vector3(0, 0, -35f);
            return root;
        }

        public static GameObject HotAirBalloon(Transform parent, Vector3 pos, Rng rng)
        {
            var go = new GameObject("Balloon");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var b = new MeshBuilder();
            Color a = rng.Pick(new[] { new Color(1f, 0.3f, 0.25f), new Color(0.25f, 0.55f, 1f), new Color(1f, 0.8f, 0.2f), new Color(0.4f, 0.85f, 0.45f) });
            Color c2 = rng.Pick(new[] { Color.white, new Color(1f, 0.55f, 0.15f), new Color(0.7f, 0.35f, 0.9f) });
            var prof = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                float ang = Mathf.Lerp(-70f, 90f, t) * Mathf.Deg2Rad;
                float r = Mathf.Cos(ang) * 4f;
                float y = Mathf.Sin(ang) * 4.4f;
                if (t < 0.25f) r = Mathf.Lerp(1.1f, r, t / 0.25f);
                prof.Add(new Vector2(Mathf.Max(r, 0f), y));
            }
            b.Gloss = 0.35f;
            b.Lathe(prof, 20, (p, n) => Mathf.Repeat(Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) * 10f, 1f) < 0.5f ? a : c2);
            b.Gloss = 0f;
            b.RoundedBox(new Vector3(0, -6.2f, 0), new Vector3(1.4f, 1f, 1.4f), 0.1f, new Color(0.55f, 0.38f, 0.2f), 1);
            for (int k = 0; k < 4; k++)
            {
                float ang = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                b.Cylinder(new Vector3(Mathf.Cos(ang) * 0.6f, -5.7f, Mathf.Sin(ang) * 0.6f),
                           new Vector3(Mathf.Cos(ang) * 1.1f, -4.1f, Mathf.Sin(ang) * 1.1f), 0.03f, 4, new Color(0.3f, 0.25f, 0.2f), false);
            }
            b.Build("BalloonMesh", go.transform, Mats.VertexLit);
            var bob = go.AddComponent<Bobber>();
            bob.amplitude = 1.2f; bob.speed = rng.Range(0.2f, 0.4f); bob.drift = rng.Range(-1f, 1f);
            return go;
        }
    }

    /// <summary>Constant rotation (windmills, item boxes).</summary>
    public class Spinner : MonoBehaviour
    {
        public Vector3 speed;
        void Update() => transform.Rotate(speed * Time.deltaTime, Space.Self);
    }

    /// <summary>Gentle floating motion.</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 0.5f, speed = 1f, drift;
        Vector3 origin;
        float phase;
        void Start() { origin = transform.position; phase = Random.value * 10f; }
        void Update()
        {
            float t = Time.time * speed + phase;
            transform.position = origin + new Vector3(Mathf.Sin(t * 0.3f) * drift * 3f, Mathf.Sin(t) * amplitude, 0f);
            transform.Rotate(0f, 3f * Time.deltaTime, 0f);
        }
    }
}
