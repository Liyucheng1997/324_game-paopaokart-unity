using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    public enum Theme { Forest, Snow, Desert }

    /// <summary>Authoring description of a circuit (control points + features, t = fraction of lap).</summary>
    public class TrackDef
    {
        public string Name;
        public string Subtitle;
        public Theme Theme;
        public Vector3[] Points;                 // x, elevation, z
        public float Width = 14f;
        public float[] BoostPads = new float[0];
        public float[] Jumps = new float[0];
        public float[] ItemRows = new float[0];
        public Vector2[] Tunnels = new Vector2[0]; // (t0, t1)
        public Vector3 LakeCenter;               // xz center, y = water level
        public float LakeRadius;                 // 0 = no lake
        public float MaxBank = 11f;
        public int Seed = 1;
    }

    /// <summary>
    /// Arc-length sampled closed spline (1 m spacing) with banking frames,
    /// curvature, AI racing line and fast local nearest-point queries.
    /// </summary>
    public class TrackData
    {
        public const float Spacing = 1f;

        public TrackDef Def;
        public int Count;
        public float Length;
        public float HalfWidth;
        public Vector3[] Pos;
        public Vector3[] Fwd;       // 3D tangent (includes slope)
        public Vector3[] Right;     // banked right vector
        public Vector3[] Up;        // banked road normal
        public Vector3[] FlatRight; // horizontal right
        public float[] Curv;        // signed yaw change per meter (deg), + = right turn
        public float[] RacingLine;  // lateral offset in meters
        public bool[] Tunnel;
        public bool[] Bridge;

        public TrackData(TrackDef def)
        {
            Def = def;
            HalfWidth = def.Width * 0.5f;
            var dense = SampleDense(def.Points, 60);
            Resample(dense);
            ComputeFrames();
            ComputeRacingLine();
            MarkFeatures();
        }

        // ------------------------------------------------------------ construction

        static List<Vector3> SampleDense(Vector3[] cp, int perSeg)
        {
            int n = cp.Length;
            var list = new List<Vector3>(n * perSeg);
            for (int s = 0; s < n; s++)
            {
                Vector3 p0 = cp[(s - 1 + n) % n], p1 = cp[s], p2 = cp[(s + 1) % n], p3 = cp[(s + 2) % n];
                for (int k = 0; k < perSeg; k++)
                    list.Add(CentripetalCR(p0, p1, p2, p3, (float)k / perSeg));
            }
            return list;
        }

        static Vector3 CentripetalCR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), 0.5f);
            float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), 0.5f);
            float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), 0.5f);
            float tt = Mathf.Lerp(t1, t2, t);
            Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
            return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
        }

        void Resample(List<Vector3> dense)
        {
            int n = dense.Count;
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++)
                cum[i + 1] = cum[i] + Vector3.Distance(dense[i], dense[(i + 1) % n]);
            float total = cum[n];
            Count = Mathf.Max(16, Mathf.RoundToInt(total / Spacing));
            Length = total;
            float step = total / Count;
            Pos = new Vector3[Count];
            int seg = 0;
            for (int i = 0; i < Count; i++)
            {
                float d = i * step;
                while (seg < n - 1 && cum[seg + 1] < d) seg++;
                float segLen = cum[seg + 1] - cum[seg];
                float u = segLen > 1e-5f ? (d - cum[seg]) / segLen : 0f;
                Pos[i] = Vector3.Lerp(dense[seg], dense[(seg + 1) % n], u);
            }
        }

        void ComputeFrames()
        {
            int n = Count;
            Fwd = new Vector3[n];
            FlatRight = new Vector3[n];
            Right = new Vector3[n];
            Up = new Vector3[n];
            Curv = new float[n];
            for (int i = 0; i < n; i++)
            {
                Fwd[i] = (Pos[(i + 1) % n] - Pos[(i - 1 + n) % n]).normalized;
                Vector3 flat = new Vector3(Fwd[i].x, 0f, Fwd[i].z).normalized;
                FlatRight[i] = Vector3.Cross(Vector3.up, flat).normalized;
            }
            const int K = 4;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = Fwd[(i - K + n) % n], b = Fwd[(i + K) % n];
                a.y = 0; b.y = 0;
                Curv[i] = Vector3.SignedAngle(a, b, Vector3.up) / (2f * K * Spacing);
            }
            // bank: smoothed curvature -> roll the road into the corner
            var bank = new float[n];
            const int W = 14;
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int k = -W; k <= W; k++) s += Curv[(i + k + n) % n];
                s /= (2 * W + 1);
                bank[i] = Mathf.Clamp(-s * 3.2f, -Def.MaxBank, Def.MaxBank);
            }
            for (int i = 0; i < n; i++)
            {
                float b = bank[i] * Mathf.Deg2Rad;
                Vector3 r = FlatRight[i] * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                Right[i] = r.normalized;
                Up[i] = Vector3.Cross(Fwd[i], Right[i]).normalized;
                if (Up[i].y < 0f) Up[i] = -Up[i];
            }
        }

        void ComputeRacingLine()
        {
            int n = Count;
            RacingLine = new float[n];
            var raw = new float[n];
            float maxOff = HalfWidth - 2.4f;
            for (int i = 0; i < n; i++)
            {
                // look slightly ahead so the apex comes early
                float s = 0f, wsum = 0f;
                for (int k = -8; k <= 26; k++)
                {
                    float w = 1f - Mathf.Abs(k - 9) / 18f;
                    if (w <= 0f) continue;
                    s += Curv[(i + k + n) % n] * w;
                    wsum += w;
                }
                s /= wsum;
                raw[i] = Mathf.Clamp(s * 0.55f, -1f, 1f) * maxOff;
            }
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int k = -10; k <= 10; k++) s += raw[(i + k + n) % n];
                RacingLine[i] = s / 21f;
            }
        }

        void MarkFeatures()
        {
            Tunnel = new bool[Count];
            Bridge = new bool[Count];
            foreach (var t in Def.Tunnels)
            {
                int a = IndexAtT(t.x), b = IndexAtT(t.y);
                for (int i = a; i != b; i = (i + 1) % Count) Tunnel[i] = true;
            }
            if (Def.LakeRadius > 0f)
            {
                Vector2 lc = new Vector2(Def.LakeCenter.x, Def.LakeCenter.z);
                for (int i = 0; i < Count; i++)
                    if ((new Vector2(Pos[i].x, Pos[i].z) - lc).magnitude < Def.LakeRadius + 6f)
                        Bridge[i] = true;
            }
        }

        // ------------------------------------------------------------ queries

        public int Wrap(int i) => ((i % Count) + Count) % Count;
        public int IndexAtT(float t) => Wrap(Mathf.RoundToInt(Mathf.Repeat(t, 1f) * Count));
        public float DistanceOf(int i) => Wrap(i) * (Length / Count);

        /// <summary>Nearest sample searching a window around a previous index (cheap per-frame tracking).</summary>
        public int FindNearest(Vector3 p, int hint, int window = 24)
        {
            int best = Wrap(hint);
            float bestD = float.MaxValue;
            for (int k = -window; k <= window; k++)
            {
                int i = Wrap(hint + k);
                float d = (Pos[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public int FindNearestGlobal(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < Count; i += 2)
            {
                float d = (Pos[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return FindNearest(p, best, 3);
        }

        public float Lateral(Vector3 p, int i) => Vector3.Dot(p - Pos[i], FlatRight[i]);

        /// <summary>Point on the road surface at sample i with lateral offset (follows banking).</summary>
        public Vector3 Surface(int i, float lateral) => Pos[Wrap(i)] + Right[Wrap(i)] * lateral;

        /// <summary>Accumulated heading change (deg) over the next <paramref name="meters"/>; sign = direction.</summary>
        public float TurnAhead(int i, float startMeters, float meters)
        {
            int a = i + Mathf.RoundToInt(startMeters / Spacing);
            int steps = Mathf.RoundToInt(meters / Spacing);
            float s = 0f;
            for (int k = 0; k < steps; k++) s += Curv[Wrap(a + k)] * Spacing;
            return s;
        }

        public float MaxCurvAhead(int i, float startMeters, float meters)
        {
            int a = i + Mathf.RoundToInt(startMeters / Spacing);
            int steps = Mathf.RoundToInt(meters / Spacing);
            float m = 0f;
            for (int k = 0; k < steps; k++) m = Mathf.Max(m, Mathf.Abs(Curv[Wrap(a + k)]));
            return m;
        }

        public Quaternion RotationAt(int i) => Quaternion.LookRotation(Fwd[Wrap(i)], Up[Wrap(i)]);
    }
}
