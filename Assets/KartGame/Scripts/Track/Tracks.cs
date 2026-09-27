using UnityEngine;

namespace KartGame
{
    /// <summary>Circuit catalogue. Layouts were checked offline for clearance (&gt; 50 m) and hairpin radius (&gt; 17 m).</summary>
    public static class Tracks
    {
        /// <summary>Uniform layout scale (authoring units were a bit tight for ~40 s laps).</summary>
        const float S = 1.2f;

        static Vector3[] Scale(Vector3[] pts)
        {
            for (int i = 0; i < pts.Length; i++) pts[i] *= S;
            return pts;
        }

        public static readonly TrackDef[] All =
        {
            new TrackDef
            {
                Name = "FOREST VILLAGE",
                Subtitle = "Rolling hills, a lake bridge, a hillside tunnel and a big jump",
                Theme = Theme.Forest,
                Seed = 11,
                Points = Scale(new[]
                {
                    new Vector3(-40, 0, -100), new Vector3(40, 0, -100), new Vector3(95, 0.5f, -90),
                    new Vector3(125, 2.5f, -55), new Vector3(125, 5.5f, -15), new Vector3(105, 8, 20),
                    new Vector3(100, 8.5f, 55), new Vector3(120, 6.5f, 90), new Vector3(110, 4.5f, 125),
                    new Vector3(75, 3, 140), new Vector3(45, 2, 120), new Vector3(40, 3, 85),
                    new Vector3(15, 4, 55), new Vector3(-20, 4, 40), new Vector3(-55, 3, 55),
                    new Vector3(-85, 2, 85), new Vector3(-120, 1.5f, 80), new Vector3(-130, 1.5f, 45),
                    new Vector3(-105, 2.5f, 15), new Vector3(-100, 4, -20), new Vector3(-115, 0.8f, -60),
                    new Vector3(-95, 0, -95),
                }),
                BoostPads = new[] { 0.06f, 0.46f, 0.80f, 0.845f },
                Jumps = new[] { 0.865f },
                ItemRows = new[] { 0.13f, 0.38f, 0.62f, 0.92f },
                Tunnels = new[] { new Vector2(0.27f, 0.33f) },
                LakeCenter = new Vector3(5, 0.2f, 55) * S,
                LakeRadius = 24f * S,
            },
            new TrackDef
            {
                Name = "SNOW VALLEY",
                Subtitle = "Icy peaks, a frozen lake crossing, an ice cave and tight hairpins",
                Theme = Theme.Snow,
                Seed = 23,
                Points = Scale(new[]
                {
                    new Vector3(-112, 0, -40), new Vector3(-110, 0.5f, 30), new Vector3(-95, 2, 55),
                    new Vector3(-60, 4.5f, 75), new Vector3(-25, 7, 60), new Vector3(0, 9.5f, 30),
                    new Vector3(30, 10, 30), new Vector3(55, 6, 55), new Vector3(60, 4.5f, 95),
                    new Vector3(95, 3.5f, 115), new Vector3(130, 3, 95), new Vector3(125, 2.5f, 55),
                    new Vector3(95, 2, 20), new Vector3(100, 2.5f, -20), new Vector3(130, 3, -50),
                    new Vector3(120, 3.5f, -95), new Vector3(80, 4, -110), new Vector3(40, 3.5f, -85),
                    new Vector3(0, 2.5f, -95), new Vector3(-35, 1, -120), new Vector3(-80, 0.5f, -115),
                    new Vector3(-108, 0, -85),
                }),
                BoostPads = new[] { 0.03f, 0.42f, 0.72f },
                Jumps = new[] { 0.075f },
                ItemRows = new[] { 0.15f, 0.40f, 0.64f, 0.88f },
                Tunnels = new[] { new Vector2(0.55f, 0.61f) },
                LakeCenter = new Vector3(30, 0.6f, -75) * S,
                LakeRadius = 20f * S,
            },
        };
    }
}
