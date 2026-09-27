using UnityEngine;

namespace KartGame
{
    /// <summary>Per-kart tuning. Bars (1-5) are what the menu shows.</summary>
    public class KartStats
    {
        public string Name;
        public string Blurb;
        public KartDesign Design;
        public float MaxSpeed = 27.5f;      // m/s at full throttle, no boost
        public float Accel = 17f;           // m/s^2 at low speed
        public float TurnRate = 105f;       // deg/s
        public float DriftTurn = 1f;        // multiplier on drift yaw
        public float DriftGrip = 1f;        // higher = tighter, less slide
        public float Charge = 1f;           // nitro charge rate multiplier
        public float BoostPower = 1f;       // multiplier on boost top speed bonus
        public int BarSpeed, BarAccel, BarHandling, BarDrift;

        public static readonly KartStats[] All =
        {
            new KartStats
            {
                Name = "COMET", Blurb = "Balanced formula kart", Design = KartDesign.Comet,
                MaxSpeed = 27.6f, Accel = 17.5f, TurnRate = 106f, DriftTurn = 1f, DriftGrip = 1f, Charge = 1f, BoostPower = 1f,
                BarSpeed = 4, BarAccel = 3, BarHandling = 3, BarDrift = 3,
            },
            new KartStats
            {
                Name = "BUBBLE", Blurb = "Quick off the line, nimble", Design = KartDesign.Bubble,
                MaxSpeed = 26.8f, Accel = 20.5f, TurnRate = 116f, DriftTurn = 1.05f, DriftGrip = 1.15f, Charge = 1.05f, BoostPower = 0.97f,
                BarSpeed = 3, BarAccel = 5, BarHandling = 5, BarDrift = 3,
            },
            new KartStats
            {
                Name = "ARROW", Blurb = "Highest top speed, wide lines", Design = KartDesign.Arrow,
                MaxSpeed = 28.8f, Accel = 15.8f, TurnRate = 98f, DriftTurn = 0.95f, DriftGrip = 0.95f, Charge = 0.95f, BoostPower = 1.05f,
                BarSpeed = 5, BarAccel = 2, BarHandling = 3, BarDrift = 3,
            },
            new KartStats
            {
                Name = "BRICK", Blurb = "Drift king, fills nitro fast", Design = KartDesign.Brick,
                MaxSpeed = 27.2f, Accel = 16.6f, TurnRate = 102f, DriftTurn = 1.15f, DriftGrip = 0.9f, Charge = 1.22f, BoostPower = 1.03f,
                BarSpeed = 3, BarAccel = 3, BarHandling = 3, BarDrift = 5,
            },
        };
    }
}
