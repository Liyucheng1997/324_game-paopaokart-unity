using System;
using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    /// <summary>Procedurally painted textures: road, curbs, ground, signs, particles, icons, UI.</summary>
    public static class ProcTex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Make(string name, int w, int h, Func<float, float, Color> px,
                                     bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat, bool cached = true)
        {
            if (cached && cache.TryGetValue(name, out var t) && t != null) return t;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = name, wrapMode = wrap };
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    data[y * w + x] = px((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels32(data);
            tex.Apply(mips, false);
            if (cached) cache[name] = tex;
            return tex;
        }

        static float Grain(float u, float v, int period, int seed) => Noise.TiledFbm(u * period, v * period, period, 4, seed);

        // ------------------------------------------------------------ track surfaces

        public static Texture2D Road(Color asphalt, Color line)
        {
            return Make("Road" + asphalt, 512, 512, (u, v) =>
            {
                float n = Grain(u, v, 8, 3) * 0.6f + Grain(u, v, 64, 5) * 0.4f;
                float speck = Noise.Tiled2(u * 256f, v * 256f, 256, 9);
                Color c = asphalt * (0.86f + n * 0.28f);
                if (speck > 0.93f) c *= 1.18f;
                else if (speck < 0.06f) c *= 0.82f;
                // worn racing lines
                float wear = Mathf.Exp(-Mathf.Pow((u - 0.33f) / 0.07f, 2f)) + Mathf.Exp(-Mathf.Pow((u - 0.67f) / 0.07f, 2f));
                c *= 1f - wear * 0.10f;
                // edge lines
                if ((u > 0.022f && u < 0.040f) || (u > 0.960f && u < 0.978f)) c = Color.Lerp(c, line, 0.92f);
                // dashed center line
                if (Mathf.Abs(u - 0.5f) < 0.007f && (v % 0.5f) < 0.26f) c = Color.Lerp(c, line, 0.75f);
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Curb(Color a, Color b)
        {
            return Make("Curb" + a + b, 64, 128, (u, v) =>
            {
                Color c = (Mathf.FloorToInt(v * 2f) % 2 == 0) ? a : b;
                float edge = Mathf.Min(u, 1f - u);
                c *= 0.8f + Mathf.Clamp01(edge * 8f) * 0.2f;
                c *= 0.95f + Grain(u, v, 16, 2) * 0.1f;
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Ground(string key, Color baseCol, Color alt, float blades)
        {
            return Make("Ground" + key, 256, 256, (u, v) =>
            {
                float n = Grain(u, v, 4, 11);
                float m = Grain(u, v, 32, 12);
                Color c = Color.Lerp(baseCol, alt, Mathf.SmoothStep(0.3f, 0.7f, n));
                c *= 0.88f + m * 0.24f;
                float b = Noise.Tiled2(u * 128f, v * 128f, 128, 13);
                if (b > 1f - blades) c *= 1.12f;
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Checker()
        {
            return Make("Checker", 64, 64, (u, v) =>
                ((Mathf.FloorToInt(u * 2) + Mathf.FloorToInt(v * 2)) % 2 == 0) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.08f, 0.08f, 0.1f),
                true, TextureWrapMode.Repeat);
        }

        public static Texture2D BoostPad()
        {
            return Make("BoostPad", 128, 256, (u, v) =>
            {
                float x = Mathf.Abs(u - 0.5f) * 2f;
                float chev = Mathf.Repeat(v * 3f - x * 0.55f, 1f);
                bool arrow = chev > 0.12f && chev < 0.52f && x < 0.86f;
                Color bg = Color.Lerp(new Color(1f, 0.35f, 0.02f), new Color(0.9f, 0.1f, 0.05f), x);
                Color fg = Color.Lerp(new Color(1f, 0.95f, 0.45f), new Color(1f, 0.75f, 0.15f), x);
                Color c = arrow ? fg : bg;
                if (x > 0.92f) c = new Color(1f, 0.95f, 0.8f);
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Chevron()
        {
            return Make("Chevron", 256, 64, (u, v) =>
            {
                float t = Mathf.Repeat(u * 4f + Mathf.Abs(v - 0.5f) * 1.2f, 1f);
                bool dark = t < 0.45f;
                Color c = dark ? new Color(0.08f, 0.08f, 0.1f) : new Color(1f, 0.82f, 0.1f);
                if (v < 0.07f || v > 0.93f) c = new Color(0.95f, 0.95f, 0.95f);
                return c;
            });
        }

        public static Texture2D Banner(Color a, Color b)
        {
            return Make("Banner" + a + b, 256, 64, (u, v) =>
            {
                float d = Mathf.Repeat(u * 6f + v * 1.2f, 1f);
                Color c = d < 0.5f ? a : Color.Lerp(a, b, 0.85f);
                if (v < 0.12f || v > 0.88f) c = b;
                float star = Mathf.Repeat(u * 6f, 1f);
                if (Mathf.Abs(star - 0.5f) < 0.08f && Mathf.Abs(v - 0.5f) < 0.18f) c = Color.white;
                return c;
            });
        }

        public static Texture2D QuestionBox()
        {
            return Make("QuestionBox", 128, 128, (u, v) =>
            {
                float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                Color bg = Color.Lerp(new Color(0.15f, 0.55f, 1f), new Color(0.6f, 0.2f, 1f), u * 0.5f + v * 0.5f);
                if (edge < 0.06f) bg = new Color(1f, 1f, 1f);
                float q = QuestionMark(u, v);
                Color c = Color.Lerp(bg, new Color(1f, 0.95f, 0.3f), q);
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp);
        }

        static float QuestionMark(float u, float v)
        {
            Vector2 p = new Vector2(u, v);
            // hook: ring arc centred at (0.5, 0.64)
            Vector2 c = new Vector2(0.5f, 0.64f);
            float r = (p - c).magnitude;
            float ang = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg;
            float ring = Mathf.Abs(r - 0.17f) - 0.06f;
            if (ang < -60f && ang > -180f) ring = 1f;   // open at the lower left
            float stem = SegDist(p, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.36f)) - 0.06f;
            float dot = (p - new Vector2(0.5f, 0.2f)).magnitude - 0.07f;
            float d = Mathf.Min(ring, Mathf.Min(stem, dot));
            return Mathf.Clamp01(0.5f - d * 128f);
        }

        public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        // ------------------------------------------------------------ particles

        public static Texture2D SoftDot()
        {
            return Make("SoftDot", 64, 64, (u, v) =>
            {
                float d = Mathf.Clamp01(1f - new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f);
                return new Color(1, 1, 1, d * d);
            }, true, TextureWrapMode.Clamp);
        }

        public static Texture2D Smoke()
        {
            return Make("Smoke", 128, 128, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float n = Noise.Fbm2(u * 6f, v * 6f, 4, 21);
                float a = Mathf.Clamp01(1f - d * (0.8f + n * 0.5f));
                return new Color(1, 1, 1, Mathf.SmoothStep(0, 1, a) * 0.9f);
            }, true, TextureWrapMode.Clamp);
        }

        public static Texture2D Streak()
        {
            return Make("Streak", 32, 128, (u, v) =>
            {
                float x = 1f - Mathf.Abs(u - 0.5f) * 2f;
                float y = Mathf.Sin(v * Mathf.PI);
                return new Color(1, 1, 1, Mathf.Pow(x, 2f) * y);
            }, true, TextureWrapMode.Clamp);
        }

        public static Texture2D Flame()
        {
            // teardrop flame, bright core; v = 0 at the nozzle
            return Make("Flame", 64, 128, (u, v) =>
            {
                float x = (u - 0.5f) * 2f;
                float w = Mathf.Lerp(0.95f, 0.05f, Mathf.Pow(v, 0.8f));
                float body = Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(w, 0.01f));
                float a = Mathf.Pow(body, 1.3f) * Mathf.Clamp01(v * 12f) * (1f - v * 0.6f);
                float core = Mathf.Pow(body, 4f) * (1f - v);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a + core));
            }, true, TextureWrapMode.Clamp);
        }

        public static Texture2D Ring()
        {
            return Make("Ring", 128, 128, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 8f);
                return new Color(1, 1, 1, a);
            }, true, TextureWrapMode.Clamp);
        }

        public static Texture2D Bubble()
        {
            return Make("Bubble", 128, 128, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                if (d > 1f) return new Color(1, 1, 1, 0);
                float rim = Mathf.Pow(d, 5f);
                float hl = Mathf.Clamp01(1f - new Vector2(u - 0.36f, v - 0.66f).magnitude * 7f);
                return new Color(0.75f + hl * 0.25f, 0.9f + hl * 0.1f, 1f, Mathf.Clamp01(0.15f + rim * 0.8f + hl));
            }, true, TextureWrapMode.Clamp);
        }

        // ------------------------------------------------------------ UI

        public static Texture2D RoundRect(string key, int size, float radius, Color fill, Color border, float borderW, Color? fillBottom = null)
        {
            return Make("RR" + key, size, size, (u, v) =>
            {
                float px = u * size, py = v * size;
                float r = radius;
                float dx = Mathf.Max(Mathf.Max(r - px, px - (size - r)), 0f);
                float dy = Mathf.Max(Mathf.Max(r - py, py - (size - r)), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;   // <= 0 inside
                float alpha = Mathf.Clamp01(0.5f - d);
                Color f = fillBottom.HasValue ? Color.Lerp(fillBottom.Value, fill, v) : fill;
                Color c = f;
                if (borderW > 0f)
                {
                    float bw = Mathf.Clamp01(0.5f - (-d - borderW));
                    c = Color.Lerp(f, border, bw);
                }
                c.a *= alpha;
                return c;
            }, false, TextureWrapMode.Clamp);
        }

        public static Texture2D Solid(Color c)
        {
            return Make("Solid" + c, 4, 4, (u, v) => c, false, TextureWrapMode.Clamp);
        }

        public static Texture2D VerticalGradient(string key, Color bottom, Color top)
        {
            return Make("VG" + key, 4, 64, (u, v) => Color.Lerp(bottom, top, v), false, TextureWrapMode.Clamp);
        }

        public static Texture2D HorizontalFade(string key, Color c)
        {
            return Make("HF" + key, 128, 4, (u, v) => new Color(c.r, c.g, c.b, c.a * Mathf.SmoothStep(1f, 0f, u)), false, TextureWrapMode.Clamp);
        }

        public static Texture2D Circle(string key, Color c, float soft = 1.5f)
        {
            const int S = 64;
            return Make("Circle" + key, S, S, (u, v) =>
            {
                float d = (new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f - 1f) * S * 0.5f;
                return new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(0.5f - d / soft));
            }, false, TextureWrapMode.Clamp);
        }

        // ------------------------------------------------------------ item icons

        public static Texture2D ItemIcon(ItemType type)
        {
            return Make("Icon" + type, 128, 128, (u, v) =>
            {
                Vector2 p = new Vector2(u, v);
                Color bg = new Color(0, 0, 0, 0);
                Color c = bg;
                switch (type)
                {
                    case ItemType.Booster:
                    {
                        // blue flame over a double chevron
                        float d1 = Chevron(p, 0.42f), d2 = Chevron(p, 0.68f);
                        float d = Mathf.Min(d1, d2);
                        c = Paint(c, new Color(0.3f, 0.8f, 1f), d, 0.02f);
                        c = Paint(c, new Color(0.85f, 0.97f, 1f), d + 0.03f, 0.02f);
                        break;
                    }
                    case ItemType.Missile:
                    {
                        Vector2 a = new Vector2(0.25f, 0.25f), b = new Vector2(0.72f, 0.72f);
                        float body = SegDist(p, a, b) - 0.1f;
                        float nose = (p - new Vector2(0.74f, 0.74f)).magnitude - 0.09f;
                        float fin1 = SegDist(p, new Vector2(0.28f, 0.28f), new Vector2(0.12f, 0.36f)) - 0.05f;
                        float fin2 = SegDist(p, new Vector2(0.28f, 0.28f), new Vector2(0.36f, 0.12f)) - 0.05f;
                        c = Paint(c, new Color(0.25f, 0.25f, 0.3f), Mathf.Min(fin1, fin2), 0.01f);
                        c = Paint(c, new Color(0.95f, 0.2f, 0.15f), Mathf.Min(body, nose), 0.01f);
                        c = Paint(c, new Color(1f, 0.85f, 0.85f), SegDist(p, new Vector2(0.4f, 0.46f), new Vector2(0.6f, 0.66f)) - 0.025f, 0.01f);
                        float flame = (p - new Vector2(0.17f, 0.17f)).magnitude - 0.08f;
                        c = Paint(c, new Color(1f, 0.7f, 0.1f), flame, 0.02f);
                        break;
                    }
                    case ItemType.WaterBomb:
                    {
                        float ball = (p - new Vector2(0.5f, 0.45f)).magnitude - 0.3f;
                        float tip = SegDist(p, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.85f)) - 0.05f;
                        c = Paint(c, new Color(0.15f, 0.55f, 1f), Mathf.Min(ball, tip), 0.01f);
                        c = Paint(c, new Color(0.8f, 0.95f, 1f), (p - new Vector2(0.4f, 0.55f)).magnitude - 0.08f, 0.02f);
                        break;
                    }
                    case ItemType.Banana:
                    {
                        Vector2 cc = new Vector2(0.62f, 0.62f);
                        float r = (p - cc).magnitude;
                        float ang = Mathf.Atan2(p.y - cc.y, p.x - cc.x) * Mathf.Rad2Deg;
                        float d = Mathf.Abs(r - 0.34f) - 0.1f * Mathf.Clamp01(1f - Mathf.Abs(ang + 135f) / 70f);
                        if (ang > -60f || ang < -210f) d = Mathf.Max(d, 0.1f);
                        c = Paint(c, new Color(1f, 0.85f, 0.15f), d, 0.01f);
                        c = Paint(c, new Color(0.45f, 0.3f, 0.1f), (p - new Vector2(0.84f, 0.38f)).magnitude - 0.04f, 0.01f);
                        break;
                    }
                    case ItemType.Shield:
                    {
                        float ring = Mathf.Abs((p - new Vector2(0.5f, 0.5f)).magnitude - 0.3f) - 0.06f;
                        float halo = Mathf.Abs(new Vector2((p.x - 0.5f) / 1.6f, p.y - 0.86f).magnitude - 0.08f) - 0.02f;
                        c = Paint(c, new Color(1f, 0.85f, 0.3f), ring, 0.01f);
                        c = Paint(c, new Color(1f, 1f, 0.7f), halo, 0.01f);
                        c = Paint(c, new Color(1f, 0.95f, 0.6f, 0.35f), (p - new Vector2(0.5f, 0.5f)).magnitude - 0.24f, 0.01f);
                        break;
                    }
                    case ItemType.Magnet:
                    {
                        Vector2 cc = new Vector2(0.5f, 0.55f);
                        float r = (p - cc).magnitude;
                        float arc = Mathf.Abs(r - 0.24f) - 0.09f;
                        if (p.y < cc.y) arc = Mathf.Max(arc, SegDist(p, new Vector2(0.26f, 0.55f), new Vector2(0.26f, 0.2f)) - 0.09f);
                        float legs = Mathf.Min(SegDist(p, new Vector2(0.26f, 0.55f), new Vector2(0.26f, 0.22f)),
                                               SegDist(p, new Vector2(0.74f, 0.55f), new Vector2(0.74f, 0.22f))) - 0.09f;
                        float shape = p.y >= cc.y ? arc : legs;
                        c = Paint(c, new Color(0.9f, 0.15f, 0.2f), shape, 0.01f);
                        float tips = Mathf.Min(SegDist(p, new Vector2(0.26f, 0.28f), new Vector2(0.26f, 0.2f)),
                                               SegDist(p, new Vector2(0.74f, 0.28f), new Vector2(0.74f, 0.2f))) - 0.09f;
                        c = Paint(c, new Color(0.9f, 0.9f, 0.95f), tips, 0.01f);
                        break;
                    }
                }
                return c;
            }, true, TextureWrapMode.Clamp);
        }

        static float Chevron(Vector2 p, float y)
        {
            float a = SegDist(p, new Vector2(0.22f, y - 0.2f), new Vector2(0.5f, y));
            float b = SegDist(p, new Vector2(0.78f, y - 0.2f), new Vector2(0.5f, y));
            return Mathf.Min(a, b) - 0.07f;
        }

        static Color Paint(Color under, Color col, float dist, float aa)
        {
            float a = Mathf.Clamp01(0.5f - dist / aa) * col.a;
            Color c = Color.Lerp(under, new Color(col.r, col.g, col.b, 1f), a);
            c.a = Mathf.Max(under.a, a);
            return c;
        }
    }
}
