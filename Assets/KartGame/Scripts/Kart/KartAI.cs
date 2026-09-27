using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// AI driver over the sampled track: follows a racing line with lane offsets,
    /// plans corner speed from the kart's own grip model, drifts through tight
    /// corners (with 小喷 on exit), fires nitro on straights, avoids and passes
    /// other karts, uses items tactically and recovers when stuck.
    /// Also used as the player's autopilot after the finish line.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartAI : MonoBehaviour
    {
        [Range(0.5f, 1.15f)] public float skill = 0.9f;
        public TrackData track;

        KartController kart;
        int idx = -1;
        float lane, laneTarget, laneTimer;
        float stuckTimer, reverseTimer, stuckTotal;
        bool driftCommitted;
        int driftSign;
        float miniDelay = -1f;
        float itemHold, itemDelay;
        float noiseSeed;
        float startPressAt = -1f;
        bool startPressed;

        public int TrackIndex => idx;

        void Awake()
        {
            kart = GetComponent<KartController>();
            noiseSeed = Random.value * 100f;
            laneTarget = Random.Range(-1.5f, 1.5f);
        }

        void FixedUpdate()
        {
            if (track == null) return;
            var rm = RaceManager.Instance;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            idx = idx < 0 ? track.FindNearestGlobal(pos) : track.FindNearest(pos, idx, 20);

            if (!kart.ControlEnabled)
            {
                // try for a start boost during the countdown
                kart.SetInput(0f, 0f, false);
                if (rm != null && !rm.RaceStarted && rm.CountdownRemaining > 0f)
                {
                    if (startPressAt < 0f) startPressAt = Random.value < skill * 0.75f ? Random.Range(0.08f, 0.4f) : Random.Range(0.6f, 1.5f);
                    if (startPressed || rm.CountdownRemaining <= startPressAt)
                    {
                        kart.SetInput(1f, 0f, false);
                        if (!startPressed) { kart.PressThrottle(); startPressed = true; }
                    }
                }
                return;
            }

            float speed = kart.ForwardSpeed;
            float hw = track.HalfWidth;

            // ---- lane choice + avoidance
            laneTimer -= dt;
            if (laneTimer <= 0f)
            {
                laneTimer = Random.Range(2f, 5f);
                laneTarget = Random.Range(-1.8f, 1.8f) * (1.2f - skill * 0.5f);
            }
            if (rm != null)
            {
                foreach (var r in rm.Racers)
                {
                    if (r.kart == kart) continue;
                    Vector3 d = r.kart.transform.position - pos;
                    float ahead = Vector3.Dot(d, transform.forward);
                    if (ahead < 0.5f || ahead > 14f) continue;
                    float side = Vector3.Dot(d, transform.right);
                    if (Mathf.Abs(side) < 2.4f && r.kart.ForwardSpeed < speed + 1f)
                    {
                        float myLat = track.Lateral(pos, idx);
                        laneTarget = Mathf.Clamp(myLat + (side > 0f ? -3.2f : 3.2f), -hw + 2f, hw - 2f) - track.RacingLine[idx];
                        laneTimer = 1.5f;
                    }
                }
            }
            lane = Mathf.MoveTowards(lane, laneTarget, dt * 2.5f);

            // ---- steering toward a look-ahead point on the racing line
            float look = Mathf.Clamp(5f + Mathf.Abs(speed) * 0.5f, 7f, 20f);
            int ti = track.Wrap(idx + Mathf.RoundToInt(look / TrackData.Spacing));
            float lat = Mathf.Clamp(track.RacingLine[ti] * Mathf.Lerp(0.5f, 1f, skill) + lane, -hw + 1.3f, hw - 1.3f);
            Vector3 target = track.Surface(ti, lat);
            Vector3 to = target - pos; to.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            float wobble = (Mathf.PerlinNoise(Time.time * 0.7f, noiseSeed) - 0.5f) * (1.15f - skill) * 16f;
            float steer = Mathf.Clamp((angle + wobble) / 20f, -1f, 1f);

            // ---- corner speed planning from the kart's own yaw limits
            float brakeLook = 8f + Mathf.Max(speed, 0f) * 1.4f;
            float curv = track.MaxCurvAhead(idx, 2f, brakeLook) * Mathf.Deg2Rad;
            float curvNear = track.MaxCurvAhead(idx, 0f, 18f) * Mathf.Deg2Rad;
            float vGrip = CornerSpeed(curv, false);
            float vDrift = CornerSpeed(curv, true);
            float turnAhead = track.TurnAhead(idx, 3f, 24f);
            bool wantsDrift = !driftCommitted && Mathf.Abs(turnAhead) > 34f && speed > 14f && vGrip < speed - 1f
                              && curvNear > 0.025f && Random.value < 0.25f + skill * 0.6f;
            if (wantsDrift) { driftCommitted = true; driftSign = turnAhead > 0f ? 1 : -1; }

            float targetSpeed = (driftCommitted ? vDrift : vGrip) * Mathf.Lerp(0.86f, 1.02f, skill);
            targetSpeed = Mathf.Min(targetSpeed, kart.stats.MaxSpeed * 1.5f);
            float throttle;
            if (speed < targetSpeed - 0.5f) throttle = 1f;
            else if (speed > targetSpeed + 4f) throttle = -0.7f;
            else if (speed > targetSpeed + 1.5f) throttle = 0f;
            else throttle = 0.6f;

            // ---- drift execution
            bool driftHeld = false;
            if (driftCommitted)
            {
                float remaining = Mathf.Abs(track.TurnAhead(idx, 0f, 10f));
                float heading = Vector3.SignedAngle(fwd, new Vector3(track.Fwd[ti].x, 0, track.Fwd[ti].z), Vector3.up);
                bool done = kart.IsDrifting && (remaining < 9f || heading * driftSign < -4f) && kart.DriftTime > 0.35f;
                if (done || speed < 9f || (!kart.IsDrifting && kart.DriftTime == 0f && Mathf.Abs(turnAhead) < 15f))
                {
                    driftCommitted = false;
                    if (kart.IsDrifting && Random.value < 0.35f + skill * 0.6f) miniDelay = Random.Range(0.08f, 0.3f);
                }
                else
                {
                    driftHeld = true;
                    if (!kart.IsDrifting) steer = driftSign;                      // initiate with full lock
                    else steer = Mathf.Clamp(steer * 1.3f, -1f, 1f);              // modulate the drift
                    throttle = 1f;
                }
            }
            if (!driftCommitted && kart.IsDrifting)
                steer = -kart.DriftDir * 0.6f;                                   // counter-steer to snap out

            // ---- 小喷 after the drift
            if (miniDelay >= 0f)
            {
                miniDelay -= dt;
                if (miniDelay < 0f && kart.MiniWindow > 0f) kart.PressThrottle();
            }

            // ---- nitro on straights (speed mode)
            if (kart.chargeEnabled && kart.NitroTanks > 0 && !kart.IsDrifting && kart.BoostTimer <= 0f)
            {
                float straight = track.MaxCurvAhead(idx, 0f, 55f);
                bool behind = rm != null && rm.Player != null && rm.Player.kart != kart && rm.Player.Progress > rm.StateOf(kart).Progress;
                if (straight < 1.1f || (kart.NitroTanks >= kart.maxNitroTanks && straight < 1.8f) || (behind && straight < 1.6f))
                    if (Random.value < 0.08f + skill * 0.1f) kart.PressAction();
            }

            // ---- items (item mode)
            if (!kart.chargeEnabled && kart.Items != null && kart.Items.Front != ItemType.None)
                ThinkItem(dt, rm, pos);

            // ---- stuck recovery
            if (Mathf.Abs(speed) < 1.5f && reverseTimer <= 0f && kart.StunTimer <= 0f)
            {
                stuckTimer += dt;
                stuckTotal += dt;
                if (stuckTimer > 1.1f) { reverseTimer = 0.9f; stuckTimer = 0f; }
            }
            else { stuckTimer = 0f; if (Mathf.Abs(speed) > 4f) stuckTotal = 0f; }
            if (stuckTotal > 5f && rm != null) { rm.Respawn(rm.StateOf(kart)); stuckTotal = 0f; }
            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                kart.SetInput(-1f, -Mathf.Sign(angle), false);
                return;
            }

            // wrong way: turn around hard
            float along = Vector3.Dot(fwd.normalized, new Vector3(track.Fwd[idx].x, 0, track.Fwd[idx].z).normalized);
            if (along < -0.2f) { steer = Mathf.Sign(angle); throttle = 0.6f; }

            kart.SetInput(throttle, steer, driftHeld);
        }

        float CornerSpeed(float curvRad, bool drift)
        {
            if (curvRad < 1e-4f) return 99f;
            if (drift) return Mathf.Clamp(1.55f * kart.stats.DriftTurn / curvRad, 12f, 99f);
            // largest v with v * k <= yaw limit (grip limited)
            float v = kart.stats.MaxSpeed * 1.4f;
            while (v > 6f && v * curvRad * Mathf.Rad2Deg > kart.MaxGripYawRate(v) * 0.9f) v -= 0.5f;
            return v;
        }

        void ThinkItem(float dt, RaceManager rm, Vector3 pos)
        {
            itemHold += dt;
            if (itemDelay > 0f) { itemDelay -= dt; return; }
            var type = kart.Items.Front;
            bool use = false;
            float straight = track.MaxCurvAhead(idx, 0f, 40f);
            switch (type)
            {
                case ItemType.Booster: use = straight < 1.5f; break;
                case ItemType.Magnet: use = ItemWorld.FindTargetAhead(kart, 70f) != null; break;
                case ItemType.Missile: use = ItemWorld.FindTargetAhead(kart, 150f) != null || itemHold > 12f; break;
                case ItemType.Shield: use = kart.Items.IncomingMissile > 0f || itemHold > 12f; break;
                case ItemType.WaterBomb:
                {
                    var t = ItemWorld.FindTargetAhead(kart, 35f);
                    use = (t != null && Vector3.Angle(transform.forward, t.transform.position - pos) < 20f) || itemHold > 10f;
                    break;
                }
                case ItemType.Banana:
                {
                    bool someoneBehind = false;
                    if (rm != null)
                        foreach (var r in rm.Racers)
                        {
                            if (r.kart == kart) continue;
                            Vector3 d = r.kart.transform.position - pos;
                            if (Vector3.Dot(d, transform.forward) < -2f && d.magnitude < 22f) someoneBehind = true;
                        }
                    use = someoneBehind || itemHold > 9f;
                    break;
                }
            }
            if (use)
            {
                kart.PressAction();
                itemHold = 0f;
                itemDelay = Random.Range(0.6f, 2.2f) * (1.3f - skill);
            }
        }
    }
}
