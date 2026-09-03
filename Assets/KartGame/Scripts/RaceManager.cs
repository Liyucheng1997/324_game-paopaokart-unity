using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Owns the race: countdown, checkpoint/lap validation, live ranking,
    /// out-of-track respawn and the finish state. Singleton per scene.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public int totalLaps = 3;
        public Transform[] waypoints;       // also used for respawn + ranking
        public int checkpointCount;
        public float countdownSeconds = 3f;

        public class RacerState
        {
            public KartController kart;
            public string name;
            public int lap;                 // completed laps
            public int nextCheckpoint;
            public bool crossedStart;       // has passed checkpoint 0 once
            public float finishTime = -1f;
            public bool Finished => finishTime >= 0f;
            public float progress;          // for live ranking
        }

        readonly List<RacerState> racers = new List<RacerState>();
        public IReadOnlyList<RacerState> Racers => racers;

        public float CountdownRemaining { get; private set; }
        public bool RaceStarted { get; private set; }
        public float RaceTime { get; private set; }
        public RacerState Player { get; private set; }

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
        }

        void Start()
        {
            foreach (var kart in FindObjectsByType<KartController>())
            {
                var state = new RacerState { kart = kart, name = kart.name, nextCheckpoint = 0 };
                racers.Add(state);
                if (kart.isPlayer) Player = state;
                kart.ControlEnabled = false;
            }
            CountdownRemaining = countdownSeconds;
        }

        void Update()
        {
            if (!RaceStarted)
            {
                CountdownRemaining -= Time.deltaTime;
                if (CountdownRemaining <= 0f)
                {
                    RaceStarted = true;
                    foreach (var r in racers) r.kart.ControlEnabled = true;
                }
                return;
            }

            RaceTime += Time.deltaTime;
            UpdateProgressAndRespawn();

            if (Input.GetKeyDown(KeyCode.R))
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void UpdateProgressAndRespawn()
        {
            foreach (var r in racers)
            {
                // progress = laps + fraction of checkpoints + closeness to next checkpoint
                float frac = checkpointCount > 0 ? (float)r.nextCheckpoint / checkpointCount : 0f;
                r.progress = r.lap + frac;

                // fell off the world -> respawn at last waypoint
                if (r.kart.transform.position.y < -8f)
                    RespawnAtNearestWaypoint(r);
            }
        }

        void RespawnAtNearestWaypoint(RacerState r)
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Transform best = waypoints[0];
            float bestDist = float.MaxValue;
            Vector3 pos = r.kart.transform.position; pos.y = 0f;
            foreach (var wp in waypoints)
            {
                Vector3 w = wp.position; w.y = 0f;
                float d = (w - pos).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = wp; }
            }
            r.kart.ResetTo(best.position + Vector3.up * 1.0f, best.rotation);
        }

        public void OnCheckpointPassed(KartController kart, int index)
        {
            var r = racers.FirstOrDefault(x => x.kart == kart);
            if (r == null || r.Finished || !RaceStarted) return;

            if (index != r.nextCheckpoint) return;

            // the lap counts when crossing the start/finish line (checkpoint 0)
            if (index == 0 && r.crossedStart)
            {
                r.lap++;
                if (r.lap >= totalLaps)
                {
                    r.finishTime = RaceTime;
                    if (r == Player) r.kart.ControlEnabled = false;
                }
            }
            r.crossedStart = true;
            r.nextCheckpoint = (index + 1) % checkpointCount;
        }

        public int GetPosition(RacerState racer)
        {
            var ordered = racers
                .OrderByDescending(r => r.Finished ? 1000f - r.finishTime * 0.001f : r.progress)
                .ToList();
            return ordered.IndexOf(racer) + 1;
        }
    }
}
