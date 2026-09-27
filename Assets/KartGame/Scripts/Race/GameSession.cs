using UnityEngine;

namespace KartGame
{
    public enum RaceMode { Speed, Item }
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Menu selections that survive the scene reload into a race.</summary>
    public static class GameSession
    {
        public static bool GoRace;
        public static RaceMode Mode = RaceMode.Speed;
        public static int TrackIndex;
        public static int KartIndex;
        public static int ColorIndex;
        public static Difficulty Difficulty = Difficulty.Normal;
        public static int Laps = 3;

        public static readonly KartLivery[] Liveries =
        {
            new KartLivery(new Color(0.12f, 0.45f, 1f), new Color(1f, 0.82f, 0.12f), Color.white, new Color(0.15f, 0.35f, 0.9f)),
            new KartLivery(new Color(0.95f, 0.18f, 0.12f), new Color(0.12f, 0.12f, 0.14f), new Color(0.95f, 0.2f, 0.12f), new Color(0.22f, 0.22f, 0.25f)),
            new KartLivery(new Color(1f, 0.4f, 0.62f), Color.white, new Color(1f, 0.78f, 0.88f), new Color(0.95f, 0.45f, 0.65f)),
            new KartLivery(new Color(0.3f, 0.78f, 0.32f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.88f, 0.2f), new Color(0.25f, 0.55f, 0.25f)),
            new KartLivery(new Color(1f, 0.72f, 0.1f), new Color(0.2f, 0.25f, 0.6f), new Color(0.2f, 0.25f, 0.6f), new Color(1f, 0.6f, 0.1f)),
            new KartLivery(new Color(0.58f, 0.32f, 0.95f), new Color(0.3f, 0.95f, 0.9f), new Color(0.95f, 0.95f, 1f), new Color(0.45f, 0.25f, 0.8f)),
            new KartLivery(new Color(0.12f, 0.8f, 0.85f), new Color(1f, 1f, 1f), new Color(0.1f, 0.5f, 0.6f), new Color(0.1f, 0.6f, 0.7f)),
            new KartLivery(new Color(0.18f, 0.18f, 0.22f), new Color(0.95f, 0.25f, 0.2f), new Color(0.15f, 0.15f, 0.18f), new Color(0.12f, 0.12f, 0.15f)),
        };

        public static readonly string[] RivalNames = { "Mochi", "Blaze", "Kiko", "Rex", "Luna", "Pip", "Dash", "Nova" };

        public static string BestKey(int track, RaceMode mode) => $"best_{track}_{mode}_{Laps}laps";

        public static float GetBest(int track, RaceMode mode) => PlayerPrefs.GetFloat(BestKey(track, mode), -1f);

        public static bool SubmitBest(int track, RaceMode mode, float time)
        {
            float best = GetBest(track, mode);
            if (best > 0f && best <= time) return false;
            PlayerPrefs.SetFloat(BestKey(track, mode), time);
            PlayerPrefs.Save();
            return true;
        }

        public static string FormatTime(float t)
        {
            if (t < 0f) return "--:--.--";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m}:{s:00.00}";
        }
    }
}
