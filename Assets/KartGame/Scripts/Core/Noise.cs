using UnityEngine;

namespace KartGame
{
    /// <summary>Deterministic hash-based value noise (2D/3D, optional tiling) and fBm helpers.</summary>
    public static class Noise
    {
        static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static float S(float t) => t * t * (3f - 2f * t);

        public static float Value3(Vector3 p)
        {
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = S(p.x - x0), fy = S(p.y - y0), fz = S(p.z - z0);
            float a = Mathf.Lerp(Hash(x0, y0, z0), Hash(x0 + 1, y0, z0), fx);
            float b = Mathf.Lerp(Hash(x0, y0 + 1, z0), Hash(x0 + 1, y0 + 1, z0), fx);
            float c = Mathf.Lerp(Hash(x0, y0, z0 + 1), Hash(x0 + 1, y0, z0 + 1), fx);
            float d = Mathf.Lerp(Hash(x0, y0 + 1, z0 + 1), Hash(x0 + 1, y0 + 1, z0 + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        public static float Value2(float x, float y, int seed = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = S(x - x0), fy = S(y - y0);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), fx),
                Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), fx), fy);
        }

        /// <summary>Value noise that tiles with the given integer period.</summary>
        public static float Tiled2(float x, float y, int period, int seed = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = S(x - x0), fy = S(y - y0);
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period);
            int ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx),
                Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx), fy);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        public static float Fbm2(float x, float y, int octaves, int seed = 0)
        {
            float v = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                v += Value2(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return v / norm;
        }

        public static float TiledFbm(float x, float y, int period, int octaves, int seed = 0)
        {
            float v = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                v += Tiled2(x, y, period, seed + i * 17) * amp;
                norm += amp;
                x *= 2f; y *= 2f; period *= 2; amp *= 0.5f;
            }
            return v / norm;
        }
    }
}
