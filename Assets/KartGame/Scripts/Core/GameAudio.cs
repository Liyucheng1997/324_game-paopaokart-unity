using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    public enum Sfx
    {
        Countdown, Go, ItemPickup, Missile, Explosion, Throw, Splash, Drop, Spin, Magnet, WallHit,
        Boost, MiniBoost, TankFull, Lap, FinalLap, Finish, Click, Land, Shield, Nitro, Warning,
    }

    /// <summary>
    /// Everything audible is synthesized at startup: engine / skid / boost loops,
    /// one-shot effects and a chiptune soundtrack. No audio assets required.
    /// </summary>
    public static class GameAudio
    {
        const int Rate = 22050;
        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static AudioClip engine, skid, roar;
        static readonly Dictionary<int, AudioClip> music = new Dictionary<int, AudioClip>();
        static AudioSource[] pool;
        static AudioSource musicSource;
        static int poolIdx;
        public static float MasterVolume = 0.9f;
        public static float MusicVolume = 0.35f;

        // ------------------------------------------------------------ playback

        static void EnsurePool()
        {
            if (pool != null && pool.Length > 0 && pool[0] != null) return;
            var go = new GameObject("AudioPool");
            Object.DontDestroyOnLoad(go);
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                var child = new GameObject("S" + i);
                child.transform.SetParent(go.transform, false);
                pool[i] = child.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            musicSource = go.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
        }

        public static void Play(Sfx s, float volume = 1f, float pitch = 1f)
        {
            EnsurePool();
            var src = pool[poolIdx++ % pool.Length];
            src.transform.position = Vector3.zero;
            src.spatialBlend = 0f;
            src.pitch = pitch;
            src.PlayOneShot(Get(s), volume * MasterVolume);
        }

        public static void Play3D(Sfx s, Vector3 pos, float volume = 1f)
        {
            EnsurePool();
            var src = pool[poolIdx++ % pool.Length];
            src.transform.position = pos;
            src.spatialBlend = 0.85f;
            src.minDistance = 8f;
            src.maxDistance = 90f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.pitch = Random.Range(0.95f, 1.05f);
            src.PlayOneShot(Get(s), volume * MasterVolume);
        }

        public static void PlayMusic(int variant)
        {
            EnsurePool();
            if (!music.TryGetValue(variant, out var clip) || clip == null)
                music[variant] = clip = MakeMusic(variant);
            if (musicSource.clip == clip && musicSource.isPlaying) return;
            musicSource.clip = clip;
            musicSource.volume = MusicVolume;
            musicSource.Play();
        }

        public static void SetMusicVolume(float v)
        {
            MusicVolume = v;
            if (musicSource != null) musicSource.volume = v;
        }

        public static void StopMusic() { if (musicSource != null) musicSource.Stop(); }

        public static AudioClip Engine => engine != null ? engine : (engine = MakeEngine());
        public static AudioClip Skid => skid != null ? skid : (skid = MakeSkid());
        public static AudioClip Roar => roar != null ? roar : (roar = MakeRoar());

        static AudioClip Get(Sfx s)
        {
            if (clips.TryGetValue(s, out var c) && c != null) return c;
            c = Make(s);
            clips[s] = c;
            return c;
        }

        // ------------------------------------------------------------ synthesis helpers

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Sq(float ph) => Mathf.Repeat(ph, 1f) < 0.5f ? 1f : -1f;
        static float Saw(float ph) => Mathf.Repeat(ph, 1f) * 2f - 1f;
        static float Tri(float ph) => 1f - 4f * Mathf.Abs(Mathf.Repeat(ph + 0.25f, 1f) - 0.5f);
        static float Sin(float ph) => Mathf.Sin(ph * Mathf.PI * 2f);

        static float[] Tone(float seconds, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                d[i] = f(t, t / seconds);
            }
            return d;
        }

        static float Note(int semisFromA4) => 440f * Mathf.Pow(2f, semisFromA4 / 12f);

        static uint noiseState = 2463534242u;
        static float Noise01()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState & 0xFFFFFF) / (float)0x800000 - 1f;
        }

        static AudioClip Make(Sfx s)
        {
            float lp = 0f;
            switch (s)
            {
                case Sfx.Countdown:
                    return Clip("cd", Tone(0.22f, (t, u) => (Sq(t * 523f) * 0.3f + Sin(t * 1046f) * 0.3f) * (1f - u) * Mathf.Min(1f, t * 200f)));
                case Sfx.Go:
                    return Clip("go", Tone(0.7f, (t, u) => (Sq(t * 1046f) * 0.25f + Sin(t * 1568f) * 0.25f) * Mathf.Pow(1f - u, 1.5f) * Mathf.Min(1f, t * 200f)));
                case Sfx.ItemPickup:
                    return Clip("pick", Tone(0.35f, (t, u) =>
                    {
                        int step = Mathf.Min(3, (int)(t / 0.07f));
                        float f = Note(3 + new[] { 0, 4, 7, 12 }[step]);
                        return Sq(t * f) * 0.22f * (1f - u);
                    }));
                case Sfx.Missile:
                    return Clip("missile", Tone(0.9f, (t, u) => { lp += (Noise01() - lp) * 0.3f; return (lp * 0.6f + Saw(t * (200f + u * 500f)) * 0.15f) * Mathf.Sin(u * Mathf.PI); }));
                case Sfx.Explosion:
                    return Clip("boom", Tone(1.1f, (t, u) => { lp += (Noise01() - lp) * (0.25f - u * 0.2f); return (lp * 1.2f + Sin(t * (80f - u * 50f)) * 0.6f) * Mathf.Pow(1f - u, 2.2f); }));
                case Sfx.Throw:
                    return Clip("throw", Tone(0.3f, (t, u) => { lp += (Noise01() - lp) * 0.2f; return lp * 0.8f * Mathf.Sin(u * Mathf.PI); }));
                case Sfx.Splash:
                    return Clip("splash", Tone(0.8f, (t, u) =>
                    {
                        lp += (Noise01() - lp) * 0.35f;
                        float bub = Sin(t * (600f + Mathf.Sin(t * 40f) * 300f)) * 0.15f * (1f - u);
                        return (lp * 0.7f * Mathf.Pow(1f - u, 2f) + bub);
                    }));
                case Sfx.Drop:
                    return Clip("drop", Tone(0.25f, (t, u) => Sin(t * (500f - u * 350f)) * 0.4f * (1f - u)));
                case Sfx.Spin:
                    return Clip("spin", Tone(0.9f, (t, u) => Tri(t * (700f - u * 450f + Mathf.Sin(t * 50f) * 60f)) * 0.3f * (1f - u)));
                case Sfx.Magnet:
                    return Clip("magnet", Tone(0.8f, (t, u) => Sin(t * (300f + Mathf.Sin(t * 30f) * 80f)) * 0.3f * Mathf.Sin(u * Mathf.PI)));
                case Sfx.WallHit:
                    return Clip("hit", Tone(0.25f, (t, u) => { lp += (Noise01() - lp) * 0.3f; return (lp * 0.9f + Sin(t * 90f) * 0.6f) * Mathf.Pow(1f - u, 3f); }));
                case Sfx.Boost:
                    return Clip("boost", Tone(0.8f, (t, u) => { lp += (Noise01() - lp) * (0.08f + u * 0.25f); return (lp * 1.1f + Saw(t * (110f + u * 90f)) * 0.12f) * Mathf.Sin(Mathf.Min(u * 4f, 1f) * Mathf.PI * 0.5f) * (1f - u * 0.7f); }));
                case Sfx.Nitro:
                    return Clip("nitro", Tone(1.0f, (t, u) => { lp += (Noise01() - lp) * (0.12f + u * 0.2f); return (lp * 1.2f + Saw(t * (90f + u * 140f)) * 0.18f + Sq(t * (180f + u * 280f)) * 0.05f) * Mathf.Min(1f, t * 20f) * (1f - u * 0.8f); }));
                case Sfx.MiniBoost:
                    return Clip("mini", Tone(0.45f, (t, u) => { lp += (Noise01() - lp) * 0.3f; return (lp * 0.7f + Sin(t * (900f + u * 600f)) * 0.2f) * (1f - u); }));
                case Sfx.TankFull:
                    return Clip("tank", Tone(0.35f, (t, u) => (Sin(t * Note(u < 0.4f ? 7 : 14)) * 0.35f + Sq(t * Note(u < 0.4f ? 7 : 14)) * 0.08f) * (1f - u)));
                case Sfx.Lap:
                    return Clip("lap", Tone(0.6f, (t, u) => Sq(t * Note(u < 0.3f ? 3 : 10)) * 0.2f * (1f - u)));
                case Sfx.FinalLap:
                    return Clip("final", Tone(1.0f, (t, u) =>
                    {
                        int step = Mathf.Min(3, (int)(t / 0.14f));
                        return (Sq(t * Note(new[] { 3, 7, 10, 15 }[step])) * 0.18f + Sin(t * Note(new[] { 3, 7, 10, 15 }[step]) * 0.5f) * 0.2f) * (1f - u * 0.8f);
                    }));
                case Sfx.Finish:
                    return Clip("finish", Tone(1.8f, (t, u) =>
                    {
                        int step = Mathf.Min(5, (int)(t / 0.13f));
                        float f = Note(new[] { 3, 7, 10, 15, 10, 15 }[step]);
                        return (Sq(t * f) * 0.16f + Tri(t * f * 0.5f) * 0.25f) * Mathf.Pow(1f - u, 1.2f);
                    }));
                case Sfx.Click:
                    return Clip("click", Tone(0.06f, (t, u) => Sq(t * 1400f) * 0.2f * (1f - u)));
                case Sfx.Land:
                    return Clip("land", Tone(0.2f, (t, u) => { lp += (Noise01() - lp) * 0.15f; return (lp + Sin(t * 70f) * 0.5f) * Mathf.Pow(1f - u, 3f); }));
                case Sfx.Shield:
                    return Clip("shield", Tone(0.7f, (t, u) => (Sin(t * 880f) + Sin(t * 1320f + Mathf.Sin(t * 20f))) * 0.15f * Mathf.Sin(u * Mathf.PI)));
                case Sfx.Warning:
                    return Clip("warn", Tone(0.3f, (t, u) => Sq(t * (u < 0.5f ? 1200f : 900f)) * 0.15f));
            }
            return Clip("empty", new float[64]);
        }

        static AudioClip MakeEngine()
        {
            // 1 s loop, 55 Hz fundamental with firing pulses; pitched up with speed
            var d = Tone(1f, (t, u) =>
            {
                float f = 55f;
                float v = 0f;
                for (int k = 1; k <= 8; k++) v += Mathf.Sin(t * f * k * Mathf.PI * 2f) / k * (k % 2 == 0 ? 0.7f : 1f);
                float pulse = 0.65f + 0.35f * Mathf.Sin(t * f * 0.5f * Mathf.PI * 2f);
                return v * pulse * 0.28f;
            });
            return Clip("engine", d);
        }

        static AudioClip MakeSkid()
        {
            float lp = 0f, hp = 0f;
            var d = Tone(0.8f, (t, u) =>
            {
                float nz = Noise01();
                lp += (nz - lp) * 0.45f;
                hp = lp - hp * 0.2f;
                return (hp * 0.5f + Mathf.Sin(t * 1900f * Mathf.PI * 2f + Mathf.Sin(t * 37f) * 2f) * 0.12f);
            });
            return Clip("skid", CrossfadeLoop(d, 0.1f));
        }

        static AudioClip MakeRoar()
        {
            float lp = 0f;
            var d = Tone(1f, (t, u) => { lp += (Noise01() - lp) * 0.12f; return lp * 1.4f + Mathf.Sin(t * 110f * Mathf.PI * 2f) * 0.1f; });
            return Clip("roar", CrossfadeLoop(d, 0.1f));
        }

        static float[] CrossfadeLoop(float[] d, float seconds)
        {
            int n = Mathf.Min(d.Length / 2, (int)(seconds * Rate));
            for (int i = 0; i < n; i++)
            {
                float w = (float)i / n;
                d[i] = d[i] * w + d[d.Length - n + i] * (1f - w);
            }
            System.Array.Resize(ref d, d.Length - n);
            return d;
        }

        // ------------------------------------------------------------ music

        /// <summary>Upbeat chiptune loop: pulse lead, arpeggio, square bass, noise drums.</summary>
        static AudioClip MakeMusic(int variant)
        {
            float bpm = variant == 0 ? 112f : (variant == 1 ? 148f : 140f);
            int key = variant == 2 ? -2 : (variant == 1 ? 0 : -4);   // semitones from C
            int[][] progression = variant == 2
                ? new[] { new[] { 9, 12, 16 }, new[] { 5, 9, 12 }, new[] { 0, 4, 7 }, new[] { 7, 11, 14 } }       // vi IV I V
                : new[] { new[] { 0, 4, 7 }, new[] { 7, 11, 14 }, new[] { 9, 12, 16 }, new[] { 5, 9, 12 } };      // I V vi IV
            int bars = 8;
            float beat = 60f / bpm;
            float step = beat / 4f;
            int steps = bars * 16;
            int total = Mathf.CeilToInt(steps * step * Rate);
            var d = new float[total];
            var rng = new System.Random(variant * 31 + 7);

            // melody: chord tones on strong steps, passing tones otherwise
            var melody = new int[steps];
            int prev = 12;
            for (int i = 0; i < steps; i++)
            {
                var chord = progression[(i / 16) % 4];
                if (i % 16 == 14 || i % 16 == 15 || rng.NextDouble() < 0.3) { melody[i] = -99; continue; }
                if (i % 2 == 1 && rng.NextDouble() < 0.5) { melody[i] = melody[i - 1]; continue; }
                int target = chord[rng.Next(3)] + 12 * (rng.NextDouble() < 0.4 ? 1 : 0);
                if (i % 4 != 0) target = prev + (rng.NextDouble() < 0.5 ? 2 : -1);
                target = Mathf.Clamp(target, 4, 24);
                melody[i] = target;
                prev = target;
            }

            float C4 = 261.63f;
            float Freq(int semis) => C4 * Mathf.Pow(2f, (semis + key) / 12f);
            float lp = 0f;
            for (int i = 0; i < steps; i++)
            {
                var chord = progression[(i / 16) % 4];
                int s0 = Mathf.FloorToInt(i * step * Rate);
                int s1 = Mathf.Min(total, Mathf.FloorToInt((i + 1) * step * Rate));
                bool drop = variant != 0 && (i / 16) >= 4;
                for (int s = s0; s < s1; s++)
                {
                    float t = (float)(s - s0) / Rate;
                    float gt = (float)s / Rate;
                    float env = Mathf.Exp(-t * 9f);
                    float v = 0f;
                    // bass: root, eighth notes with octave bounce
                    int bassNote = chord[0] - 24 + ((i / 2) % 2 == 1 ? 12 : 0);
                    v += Sq(gt * Freq(bassNote)) * 0.11f * Mathf.Exp(-t * 4f);
                    // arpeggio
                    int arp = chord[i % 3] + 12;
                    v += (Mathf.Repeat(gt * Freq(arp), 1f) < 0.25f ? 1f : -1f) * 0.045f * env;
                    // lead
                    if (melody[i] > -99)
                    {
                        float vib = 1f + Mathf.Sin(gt * 30f) * 0.004f;
                        v += (Sq(gt * Freq(melody[i] + 12) * vib) * 0.06f + Tri(gt * Freq(melody[i] + 12)) * 0.08f) * Mathf.Exp(-t * 3f);
                    }
                    // drums
                    int beatStep = i % 16;
                    if (beatStep % 4 == 0) v += Mathf.Sin(t * (140f - t * 900f) * Mathf.PI * 2f) * Mathf.Exp(-t * 18f) * 0.45f;
                    if (beatStep == 4 || beatStep == 12) { lp += (Noise01() - lp) * 0.6f; v += lp * Mathf.Exp(-t * 16f) * 0.3f; }
                    if (drop || variant == 0 ? beatStep % 2 == 0 : true) v += Noise01() * Mathf.Exp(-t * 60f) * 0.06f;
                    d[s] += v;
                }
            }
            return Clip("music" + variant, d);
        }
    }

    /// <summary>Per-kart engine, skid and boost loops plus event sounds.</summary>
    [RequireComponent(typeof(KartController))]
    public class KartAudio : MonoBehaviour
    {
        KartController kart;
        AudioSource engine, skid, roar;
        bool isPlayer;

        void Start()
        {
            kart = GetComponent<KartController>();
            isPlayer = kart.isPlayer;
            engine = Loop(GameAudio.Engine, isPlayer ? 0.32f : 0.22f);
            skid = Loop(GameAudio.Skid, 0f);
            roar = Loop(GameAudio.Roar, 0f);

            kart.OnWallHit += (impact, p) => { if (impact > 4f) GameAudio.Play3D(Sfx.WallHit, p, Mathf.Clamp01(impact / 14f) * (isPlayer ? 1f : 0.6f)); };
            kart.OnLand += impact => { if (impact > 3f) GameAudio.Play3D(Sfx.Land, transform.position, 0.6f); };
            kart.OnBoost += kind =>
            {
                if (!isPlayer && kind != BoostKind.Nitro) return;
                if (kind == BoostKind.Mini) GameAudio.Play(Sfx.MiniBoost, 0.8f);
                else if (kind == BoostKind.Nitro || kind == BoostKind.Item) { if (isPlayer) GameAudio.Play(Sfx.Nitro, 0.9f); else GameAudio.Play3D(Sfx.Nitro, transform.position, 0.6f); }
                else GameAudio.Play(Sfx.Boost, 0.7f);
            };
            if (isPlayer)
            {
                kart.OnTankFilled += () => GameAudio.Play(Sfx.TankFull, 0.7f);
                kart.OnShieldBlock += () => GameAudio.Play(Sfx.Shield, 0.8f);
                kart.OnHit += k => GameAudio.Play(k == HitKind.Trap ? Sfx.Splash : Sfx.Spin, 0.8f);
            }
        }

        AudioSource Loop(AudioClip clip, float vol)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = vol;
            s.spatialBlend = isPlayer ? 0.2f : 1f;
            s.minDistance = 6f;
            s.maxDistance = 70f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = isPlayer ? 0f : 0.4f;
            s.Play();
            return s;
        }

        void Update()
        {
            float spd = Mathf.Abs(kart.ForwardSpeed) / Mathf.Max(kart.stats.MaxSpeed, 1f);
            float gear = spd * 3.2f;
            float gearFrac = gear - Mathf.Floor(Mathf.Min(gear, 2.99f));
            float pitch = 0.75f + spd * 1.1f + gearFrac * 0.18f + kart.ThrottleInput * 0.08f;
            if (kart.BoostTimer > 0f) pitch += 0.2f;
            if (!kart.Grounded) pitch += 0.15f;
            engine.pitch = Mathf.Lerp(engine.pitch, pitch, Time.deltaTime * 8f);
            engine.volume = (isPlayer ? 0.26f : 0.2f) * (0.6f + 0.4f * Mathf.Abs(kart.ThrottleInput)) * GameAudio.MasterVolume;

            bool sliding = kart.Grounded && (kart.IsDrifting || kart.SlipAngle > 14f) && Mathf.Abs(kart.ForwardSpeed) > 6f;
            skid.volume = Mathf.MoveTowards(skid.volume, sliding ? (isPlayer ? 0.22f : 0.12f) * GameAudio.MasterVolume : 0f, Time.deltaTime * 2f);
            skid.pitch = 0.9f + Mathf.Clamp01(kart.SlipAngle / 40f) * 0.25f;
            roar.volume = Mathf.MoveTowards(roar.volume, kart.BoostTimer > 0f ? (isPlayer ? 0.25f : 0.12f) * GameAudio.MasterVolume : 0f, Time.deltaTime * 2.5f);
        }
    }
}
