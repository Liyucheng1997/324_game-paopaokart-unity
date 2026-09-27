using UnityEngine;

namespace KartGame
{
    /// <summary>Per-theme palette and atmosphere.</summary>
    public class ThemeStyle
    {
        public Color SkyTop, SkyHorizon, SkyBottom, Sun, Fog, AmbSky, AmbEq, AmbGround, CloudShade;
        public float FogStart = 120f, FogEnd = 520f, SunIntensity = 1.1f, CloudAmount = 0.55f;
        public Vector3 SunEuler = new Vector3(48f, -35f, 0f);
        public Color Asphalt, Line, CurbA, CurbB, Grass, GrassAlt, Sand, Rock, Shoulder;
        public Color BarrierA, BarrierB, Leaf, LeafAlt, Water, WaterDeep, Mountain, MountainCap;
        public bool Snowy;

        public static ThemeStyle For(Theme t)
        {
            switch (t)
            {
                case Theme.Snow:
                    return new ThemeStyle
                    {
                        SkyTop = new Color(0.33f, 0.52f, 0.86f), SkyHorizon = new Color(0.86f, 0.91f, 0.98f),
                        SkyBottom = new Color(0.8f, 0.85f, 0.92f), Sun = new Color(1f, 0.96f, 0.9f),
                        Fog = new Color(0.82f, 0.88f, 0.96f), CloudShade = new Color(0.7f, 0.76f, 0.88f),
                        AmbSky = new Color(0.62f, 0.72f, 0.9f), AmbEq = new Color(0.62f, 0.66f, 0.74f), AmbGround = new Color(0.55f, 0.58f, 0.64f),
                        FogStart = 100f, FogEnd = 480f, SunIntensity = 1.0f, CloudAmount = 0.62f, SunEuler = new Vector3(38f, 30f, 0f),
                        Asphalt = new Color(0.29f, 0.3f, 0.35f), Line = new Color(0.95f, 0.95f, 1f),
                        CurbA = new Color(0.2f, 0.45f, 0.95f), CurbB = new Color(0.97f, 0.97f, 1f),
                        Grass = new Color(0.93f, 0.95f, 1f), GrassAlt = new Color(0.82f, 0.87f, 0.97f), Sand = new Color(0.8f, 0.86f, 0.95f),
                        Rock = new Color(0.45f, 0.47f, 0.55f), Shoulder = new Color(0.88f, 0.91f, 0.98f),
                        BarrierA = new Color(0.2f, 0.45f, 0.95f), BarrierB = new Color(0.97f, 0.97f, 1f),
                        Leaf = new Color(0.12f, 0.36f, 0.3f), LeafAlt = new Color(0.16f, 0.42f, 0.34f),
                        Water = new Color(0.55f, 0.82f, 0.95f, 0.85f), WaterDeep = new Color(0.25f, 0.55f, 0.8f, 0.92f),
                        Mountain = new Color(0.46f, 0.5f, 0.62f), MountainCap = new Color(0.96f, 0.98f, 1f),
                        Snowy = true,
                    };
                default:
                    return new ThemeStyle
                    {
                        SkyTop = new Color(0.22f, 0.48f, 0.92f), SkyHorizon = new Color(0.72f, 0.86f, 1f),
                        SkyBottom = new Color(0.55f, 0.65f, 0.7f), Sun = new Color(1f, 0.95f, 0.84f),
                        Fog = new Color(0.72f, 0.84f, 0.96f), CloudShade = new Color(0.72f, 0.78f, 0.9f),
                        AmbSky = new Color(0.55f, 0.66f, 0.88f), AmbEq = new Color(0.52f, 0.58f, 0.58f), AmbGround = new Color(0.36f, 0.38f, 0.3f),
                        Asphalt = new Color(0.25f, 0.25f, 0.28f), Line = new Color(0.95f, 0.95f, 0.92f),
                        CurbA = new Color(0.9f, 0.15f, 0.12f), CurbB = new Color(0.97f, 0.97f, 0.97f),
                        Grass = new Color(0.42f, 0.68f, 0.27f), GrassAlt = new Color(0.52f, 0.74f, 0.3f), Sand = new Color(0.86f, 0.8f, 0.6f),
                        Rock = new Color(0.52f, 0.5f, 0.47f), Shoulder = new Color(0.48f, 0.66f, 0.3f),
                        BarrierA = new Color(0.92f, 0.18f, 0.15f), BarrierB = new Color(0.97f, 0.97f, 0.97f),
                        Leaf = new Color(0.25f, 0.55f, 0.2f), LeafAlt = new Color(0.4f, 0.65f, 0.22f),
                        Water = new Color(0.25f, 0.72f, 0.82f, 0.78f), WaterDeep = new Color(0.06f, 0.36f, 0.6f, 0.9f),
                        Mountain = new Color(0.36f, 0.52f, 0.38f), MountainCap = new Color(0.95f, 0.97f, 1f),
                    };
            }
        }
    }
}
