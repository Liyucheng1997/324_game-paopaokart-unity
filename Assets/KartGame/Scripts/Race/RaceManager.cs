using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartGame
{
    public enum RacePhase { Intro, Countdown, Racing, Results }

    /// <summary>
    /// Owns a race: builds the world, spawns karts, runs the intro and countdown
    /// lights, tracks laps by distance along the spline, ranks racers, respawns
    /// stragglers, rubber-bands the AI and handles finish / retire / results.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public class RacerState
        {
            public KartController kart;
            public string name;
            public bool isPlayer;
            public Color color;
            public int trackIndex;
            public int lap;                 // 0 = behind the line at the start, 1..laps racing
            public bool halfway = true;
            public float lapStart;
            public float bestLap = -1f;
            public readonly List<float> lapTimes = new List<float>();
            public float finishTime = -1f;
            public int place;
            public bool retired;
            public float wrongWay;
            public float lateral;
            public float lastRespawn = -99f;
            public float Progress;          // meters along the race (continuous)
            public bool Finished => finishTime >= 0f || retired;
        }

        public World World { get; private set; }
        public TrackData Track => World?.Track;
        public RaceMode Mode { get; private set; }
        public int TotalLaps { get; private set; }
        public RacePhase Phase { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool RaceStarted => Phase == RacePhase.Racing || Phase == RacePhase.Results;
        public float RaceTime { get; private set; }
        public RacerState Player { get; private set; }
        public float RetireCountdown { get; private set; } = -1f;
        public bool Paused { get; private set; }
        public bool NewRecord { get; private set; }
        public float PhaseTime { get; private set; }

        readonly List<RacerState> racers = new List<RacerState>();
        public IReadOnlyList<RacerState> Racers => racers;
        public event System.Action<string, Color> OnMessage;

        int finishedCount;
        float introLength = 3.2f;
        int lastBeep = -1;

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;   // keep simulating when the editor loses focus (MCP tests)
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------ setup

        public void Begin(int trackIndex, RaceMode mode, int laps, Difficulty diff, int kartIndex, int colorIndex)
        {
            Mode = mode;
            TotalLaps = laps;
            var def = Tracks.All[Mathf.Clamp(trackIndex, 0, Tracks.All.Length - 1)];
            World = WorldBuilder.Build(def, mode == RaceMode.Item);

            // player + five rivals
            var rng = new System.Random(System.Environment.TickCount);
            var names = GameSession.RivalNames.OrderBy(_ => rng.Next()).ToList();
            var liveryPool = Enumerable.Range(0, GameSession.Liveries.Length).Where(i => i != colorIndex).OrderBy(_ => rng.Next()).ToList();
            int playerSlot = 4;
            float skillBase = diff == Difficulty.Easy ? 0.74f : (diff == Difficulty.Normal ? 0.88f : 1.0f);
            int ai = 0;
            for (int slot = 0; slot < 6; slot++)
            {
                bool isPlayer = slot == playerSlot;
                var stats = isPlayer ? KartStats.All[kartIndex] : KartStats.All[rng.Next(KartStats.All.Length)];
                var liv = isPlayer ? GameSession.Liveries[colorIndex] : GameSession.Liveries[liveryPool[ai % liveryPool.Count]];
                string name = isPlayer ? "YOU" : names[ai];
                var kart = SpawnKart(name, stats, liv, isPlayer, World.GridPos[slot], World.GridRot);
                var st = new RacerState
                {
                    kart = kart, name = name, isPlayer = isPlayer, color = liv.Primary,
                    trackIndex = Track.FindNearestGlobal(World.GridPos[slot]),
                };
                racers.Add(st);
                if (isPlayer) Player = st;
                else
                {
                    var brain = kart.gameObject.AddComponent<KartAI>();
                    brain.track = Track;
                    brain.skill = Mathf.Clamp(skillBase + (float)(rng.NextDouble() - 0.5) * 0.1f + (5 - ai) * 0.012f, 0.6f, 1.12f);
                    kart.AIControlled = true;
                    ai++;
                }
            }

            var cam = Camera.main;
            var follow = cam.GetComponent<FollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
            follow.target = Player.kart;
            follow.BeginIntro(World, introLength);
            if (cam.GetComponent<PostFX>() == null) cam.gameObject.AddComponent<PostFX>();
            gameObject.AddComponent<RaceHUD>();

            Phase = RacePhase.Intro;
            PhaseTime = 0f;
            CountdownRemaining = 3f;
            World.Lights.Set(0, false);
            GameAudio.PlayMusic(World.Style.Snowy ? 2 : 1);
            GameAudio.SetMusicVolume(0.18f);
        }

        KartController SpawnKart(string name, KartStats stats, KartLivery liv, bool isPlayer, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(isPlayer ? "Player" : "AI_" + name);
            go.layer = 2;   // Ignore Raycast: ground probes never hit karts
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = KartController.SphereR;
            sphere.center = new Vector3(0f, KartController.SphereR, 0f);
            sphere.sharedMaterial = Mats.Slick;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.62f, -0.02f);
            box.size = new Vector3(1.3f, 0.5f, 2.05f);
            box.sharedMaterial = Mats.Slick;

            var items = go.AddComponent<KartItems>();
            var kart = go.AddComponent<KartController>();
            kart.SetupItems(items);
            kart.isPlayer = isPlayer;
            kart.racerName = name;
            kart.stats = stats;
            kart.chargeEnabled = Mode == RaceMode.Speed;
            kart.rig = KartModel.Build(go.transform, stats.Design, liv);
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            go.AddComponent<KartEffects>();
            go.AddComponent<KartAudio>();
            kart.ControlEnabled = false;
            if (isPlayer)
            {
                kart.OnHit += k => Message(k == HitKind.Trap ? "SPLASHED!" : (k == HitKind.Launch ? "HIT BY MISSILE!" : "SPIN OUT!"), new Color(1f, 0.45f, 0.35f));
                kart.OnShieldBlock += () => Message("BLOCKED!", new Color(1f, 0.9f, 0.4f));
                kart.OnBoost += b =>
                {
                    if (b == BoostKind.Mini) Message("BOOST!", new Color(0.45f, 0.85f, 1f));
                    else if (b == BoostKind.Start) Message("PERFECT START!", new Color(1f, 0.85f, 0.2f));
                };
                kart.OnTankFilled += () => Message("NITRO READY", new Color(0.45f, 0.85f, 1f));
            }
            return kart;
        }

        public void Message(string text, Color c) => OnMessage?.Invoke(text, c);

        // ------------------------------------------------------------ loop

        void Update()
        {
            if (World == null) return;
            if (Input.GetKeyDown(KeyCode.Escape) && Phase != RacePhase.Results) SetPaused(!Paused);
            if (Paused) return;

            PhaseTime += Time.deltaTime;
            switch (Phase)
            {
                case RacePhase.Intro:
                    if (PhaseTime >= introLength) { Phase = RacePhase.Countdown; PhaseTime = 0f; }
                    break;
                case RacePhase.Countdown:
                    CountdownRemaining = 3f - PhaseTime;
                    int n = Mathf.CeilToInt(CountdownRemaining);
                    if (n != lastBeep && n > 0) { lastBeep = n; GameAudio.Play(Sfx.Countdown, 0.8f); }
                    World.Lights.Set(4 - Mathf.Clamp(n, 1, 3), false);
                    if (CountdownRemaining <= 0f) StartRace();
                    break;
                case RacePhase.Racing:
                case RacePhase.Results:
                    RaceTime += Time.deltaTime;
                    if (Phase == RacePhase.Racing && PhaseTime > 2f) World.Lights.Set(0, false);
                    Track3();
                    break;
            }

            if (RaceStarted && Input.GetKeyDown(KeyCode.R) && Player != null && !Player.Finished && Time.time - Player.lastRespawn > 1.5f)
                Respawn(Player);
        }

        void StartRace()
        {
            Phase = RacePhase.Racing;
            PhaseTime = 0f;
            RaceTime = 0f;
            CountdownRemaining = 0f;
            World.Lights.Set(0, true);
            GameAudio.Play(Sfx.Go, 0.9f);
            GameAudio.SetMusicVolume(0.3f);
            foreach (var r in racers)
            {
                r.kart.ControlEnabled = true;
                r.kart.OnRaceStart();
                r.lapStart = 0f;
            }
        }

        public void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
        }

        void Track3()
        {
            var t = Track;
            int N = t.Count;
            foreach (var r in racers)
            {
                Vector3 pos = r.kart.transform.position;
                int prev = r.trackIndex;
                int idx = t.FindNearest(pos, prev, 24);
                r.trackIndex = idx;

                if (!r.Finished)
                {
                    if (idx > N * 0.4f && idx < N * 0.6f) r.halfway = true;
                    bool crossedFwd = prev > N - 60 && idx < 60;
                    bool crossedBack = prev < 60 && idx > N - 60;
                    if (crossedFwd && r.halfway)
                    {
                        r.halfway = false;
                        if (r.lap >= 1)
                        {
                            float lapTime = RaceTime - r.lapStart;
                            r.lapTimes.Add(lapTime);
                            if (r.bestLap < 0f || lapTime < r.bestLap) r.bestLap = lapTime;
                        }
                        r.lapStart = RaceTime;
                        r.lap++;
                        if (r.lap > TotalLaps) Finish(r);
                        else if (r.isPlayer && r.lap > 1)
                        {
                            if (r.lap == TotalLaps) { Message("FINAL LAP!", new Color(1f, 0.85f, 0.2f)); GameAudio.Play(Sfx.FinalLap, 0.8f); }
                            else { Message($"LAP {r.lap}", Color.white); GameAudio.Play(Sfx.Lap, 0.7f); }
                        }
                    }
                    else if (crossedBack && r.lap >= 1)
                    {
                        r.lap--;
                        r.halfway = true;
                    }
                }

                float d = t.DistanceOf(idx);
                r.Progress = (r.lap - 1) * t.Length + d;
                if (r.lap == 0 && idx < N / 2) r.Progress = d;   // defensive: never behind the start on lap 0
                if (r.Finished) r.Progress = TotalLaps * t.Length + 1000f - r.place;

                r.lateral = t.Lateral(pos, idx);
                r.kart.OffRoad = Mathf.Abs(r.lateral) > t.HalfWidth + 1.1f && r.kart.Grounded;

                // wrong way (player)
                if (r.isPlayer && !r.Finished)
                {
                    float along = Vector3.Dot(r.kart.Body.linearVelocity, t.Fwd[idx]);
                    r.wrongWay = along < -3f ? r.wrongWay + Time.deltaTime : 0f;
                }

                // fell or escaped the track
                bool lost = pos.y < t.Pos[idx].y - 10f || Mathf.Abs(r.lateral) > t.HalfWidth + 16f;
                if (lost && Time.time - r.lastRespawn > 1f) Respawn(r);
            }

            // rubber banding keeps the pack together
            if (Player != null)
                foreach (var r in racers)
                {
                    if (r.isPlayer || r.Finished) { r.kart.SpeedMultiplier = 1f; continue; }
                    float gap = r.Progress - Player.Progress;
                    float strength = GameSession.Difficulty == Difficulty.Hard ? 0.6f : (GameSession.Difficulty == Difficulty.Easy ? 1.2f : 0.9f);
                    float m = 1f - Mathf.Clamp(gap / 180f, -0.09f, 0.09f) * strength;
                    if (Player.Finished) m = 1f;
                    r.kart.SpeedMultiplier = m;
                }

            // retire timer after the winner crosses the line
            if (RetireCountdown > 0f)
            {
                RetireCountdown -= Time.deltaTime;
                if (RetireCountdown <= 0f)
                {
                    foreach (var r in racers)
                        if (!r.Finished) { r.retired = true; r.place = ++finishedCount; r.kart.ControlEnabled = r.kart.AIControlled; }
                    if (Phase != RacePhase.Results) ShowResults();
                }
            }
        }

        void Finish(RacerState r)
        {
            r.finishTime = RaceTime;
            r.place = ++finishedCount;
            if (finishedCount == 1) RetireCountdown = 20f;
            if (r.isPlayer)
            {
                NewRecord = GameSession.SubmitBest(GameSession.TrackIndex, Mode, RaceTime);
                GameAudio.Play(Sfx.Finish, 0.9f);
                Message(r.place == 1 ? "YOU WIN!" : "FINISH!", new Color(1f, 0.85f, 0.2f));
                // autopilot takes over for the victory lap
                var brain = r.kart.GetComponent<KartAI>();
                if (brain == null) brain = r.kart.gameObject.AddComponent<KartAI>();
                brain.track = Track;
                brain.skill = 0.8f;
                r.kart.AIControlled = true;
                r.kart.SetInput(0f, 0f, false);
                Invoke(nameof(ShowResults), 2.5f);
            }
        }

        void ShowResults()
        {
            if (Phase == RacePhase.Results) return;
            Phase = RacePhase.Results;
            PhaseTime = 0f;
            GameAudio.SetMusicVolume(0.25f);
        }

        public void Respawn(RacerState r)
        {
            if (r == null) return;
            var t = Track;
            int idx = t.Wrap(r.trackIndex - 2);
            float lat = Mathf.Clamp(r.lateral, -3f, 3f);
            Vector3 p = t.Surface(idx, lat) + t.Up[idx] * 0.4f;
            Vector3 f = t.Fwd[idx]; f.y = 0f;
            r.kart.ResetTo(p, Quaternion.LookRotation(f.normalized, Vector3.up));
            r.lastRespawn = Time.time;
            if (r.isPlayer) Message("RESPAWN", Color.white);
        }

        // ------------------------------------------------------------ queries

        public RacerState StateOf(KartController k)
        {
            foreach (var r in racers) if (r.kart == k) return r;
            return null;
        }

        public int GetPosition(RacerState racer)
        {
            int pos = 1;
            foreach (var r in racers)
            {
                if (r == racer) continue;
                if (Ahead(r, racer)) pos++;
            }
            return pos;
        }

        static bool Ahead(RacerState a, RacerState b)
        {
            if (a.Finished && b.Finished) return a.place < b.place;
            if (a.Finished != b.Finished) return a.Finished;
            return a.Progress > b.Progress;
        }

        public List<RacerState> Standings()
        {
            var list = new List<RacerState>(racers);
            list.Sort((a, b) => Ahead(a, b) ? -1 : (Ahead(b, a) ? 1 : 0));
            return list;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSession.GoRace = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void BackToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSession.GoRace = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
