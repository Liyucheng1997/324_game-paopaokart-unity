using System;
using UnityEngine;

namespace KartGame
{
    public enum BoostKind { None, Nitro, Mini, Pad, Start, Item }
    public enum HitKind { Spin, Trap, Launch }

    /// <summary>
    /// Arcade kart physics shared by the player and AI.
    ///  - sphere collider rolls on the road mesh; root stays upright (yaw only),
    ///    the visual aligns to the ground normal, banking and slopes work
    ///  - KartRider / QQ飞车 drift: Shift + direction, hold direction (or Shift) to keep
    ///    sliding, counter-steer to snap out; charges persistent nitro (2.4 s / tank, max 2)
    ///  - 小喷: re-press throttle right after a drift (or a landing) for a mini boost
    ///  - Ctrl fires one nitro tank (speed mode) or uses the item (item mode)
    ///  - start boost when throttle is pressed right as the lights go green
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        public const float SphereR = 0.42f;
        const float Gravity = 22f;
        const float NormalGrip = 13f;
        const float ReverseMax = 9f;
        const float BrakeDecel = 32f;
        const float CoastDecel = 3.2f;

        [Header("Setup")]
        public bool isPlayer;
        public string racerName = "Racer";
        public KartStats stats = KartStats.All[0];
        public bool chargeEnabled = true;           // false in item mode (nitro comes from items)
        public float nitroChargeTime = 2.4f;
        public int maxNitroTanks = 2;
        public float nitroDuration = 1.6f;

        public KartRig rig;

        // ---- runtime state (read by AI / HUD / effects)
        public bool ControlEnabled { get; set; }
        public bool AIControlled { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;   // rubber-banding / difficulty
        public bool Grounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool IsDrifting { get; private set; }
        public int DriftDir { get; private set; }
        public float DriftTime { get; private set; }
        public float SlipAngle { get; private set; }
        public float NitroCharge01 { get; private set; }
        public int NitroTanks { get; private set; }
        public float BoostTimer { get; private set; }
        public BoostKind Boost { get; private set; }
        public float MiniWindow { get; private set; }     // > 0 while a 小喷 can be triggered
        public float AirTime { get; private set; }
        public bool OffRoad { get; set; }
        public float ShieldTimer { get; private set; }
        public float StunTimer { get; private set; }
        public HitKind StunKind { get; private set; }
        public float Invulnerable { get; private set; }
        public float SteerInput => steer;
        public float ThrottleInput => throttle;
        public Rigidbody Body => rb;
        public float ForwardSpeed { get; private set; }
        public float Speed => rb == null ? 0f : rb.linearVelocity.magnitude;
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 7.2f;   // gamey readout (~200 km/h at top speed)
        public int DriftLevel => NitroTanks >= maxNitroTanks ? 2 : (NitroCharge01 > 0.5f ? 1 : 0);
        public KartItems Items { get; private set; }

        // telemetry (tuning / tests)
        public int StatDrifts, StatMiniBoosts, StatNitros, StatWallHits, StatPads;

        // events for effects, audio and HUD popups
        public event Action<BoostKind> OnBoost;
        public event Action<float, Vector3> OnWallHit;
        public event Action<float> OnLand;
        public event Action OnDriftStart;
        public event Action<HitKind> OnHit;
        public event Action OnShieldBlock;
        public event Action OnTankFilled;

        Rigidbody rb;
        float yaw, yawVel;
        float throttle, steer;
        bool driftHeld, driftPressed, throttlePressed, actionPressed;
        float gripBlend = 1f;
        float boostPower = 1f;
        float spinAngle, spinSpeed;
        float visualLift, landSquash;
        float wheelSpin;
        float lastThrottleDownTime = -99f;
        float lastPadTime = -99f;
        Vector3 smoothNormal = Vector3.up;
        float pitchVis, rollVis;
        float driftVisualYaw;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.mass = 150f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            yaw = transform.eulerAngles.y;
            Items = GetComponent<KartItems>();
        }

        public void SetupItems(KartItems items)
        {
            Items = items;
            items.Bind(this);
        }

        // ------------------------------------------------------------ input

        public void SetInput(float throttleIn, float steerIn, bool drift)
        {
            throttle = Mathf.Clamp(throttleIn, -1f, 1f);
            steer = Mathf.Clamp(steerIn, -1f, 1f);
            if (drift && !driftHeld) driftPressed = true;
            driftHeld = drift;
        }

        /// <summary>Fresh throttle press (for 小喷 / start boost).</summary>
        public void PressThrottle() { throttlePressed = true; lastThrottleDownTime = Time.time; }

        /// <summary>Ctrl: nitro in speed mode, item in item mode.</summary>
        public void PressAction() { actionPressed = true; }

        void Update()
        {
            if (isPlayer && !AIControlled)
            {
                float th = 0f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) th += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) th -= 1f;
                float st = 0f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) st += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) st -= 1f;
                bool dr = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                // smooth digital steering a little so small taps are possible
                steerSmoothed = Mathf.MoveTowards(steerSmoothed, st, Time.deltaTime * (Mathf.Abs(st) > 0.01f ? 7f : 10f));
                SetInput(th, steerSmoothed, dr);
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) PressThrottle();
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) PressAction();
            }
            AnimateVisual(Time.deltaTime);
        }

        float steerSmoothed;

        // ------------------------------------------------------------ physics

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool stunned = StunTimer > 0f;
            bool control = ControlEnabled && !stunned;
            float th = control ? throttle : 0f;
            float st = control ? steer : 0f;

            // timers
            if (Invulnerable > 0f) Invulnerable -= dt;
            if (ShieldTimer > 0f) ShieldTimer -= dt;
            if (MiniWindow > 0f) MiniWindow -= dt;
            if (StunTimer > 0f) { StunTimer -= dt; if (StunTimer <= 0f) spinSpeed = 0f; }
            if (BoostTimer > 0f) { BoostTimer -= dt; if (BoostTimer <= 0f) { Boost = BoostKind.None; boostPower = 1f; } }

            // ---- ground probe
            Vector3 center = rb.position + Vector3.up * SphereR;
            bool hitGround = Physics.SphereCast(center + Vector3.up * 0.1f, SphereR * 0.85f, Vector3.down, out RaycastHit hit,
                                                 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool wasGrounded = Grounded;
            Grounded = hitGround && hit.normal.y > 0.5f;
            if (Grounded)
            {
                GroundNormal = hit.normal;
                if (!wasGrounded)
                {
                    float impact = -rb.linearVelocity.y;
                    if (AirTime > 0.25f)
                    {
                        OnLand?.Invoke(Mathf.Max(impact, 0f));
                        landSquash = Mathf.Clamp(impact * 0.012f, 0.02f, 0.12f);
                        if (AirTime > 0.45f && control) MiniWindow = 0.5f;   // 落地小喷
                    }
                }
                AirTime = 0f;
            }
            else
            {
                AirTime += dt;
                GroundNormal = Vector3.Slerp(GroundNormal, Vector3.up, dt * 2f);
            }
            smoothNormal = Vector3.Slerp(smoothNormal, GroundNormal, dt * 14f);

            Vector3 n = Grounded ? GroundNormal : Vector3.up;
            Vector3 headingFlat = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 f = Vector3.ProjectOnPlane(headingFlat, n).normalized;
            Vector3 r = Vector3.Cross(n, f).normalized;
            Vector3 vel = rb.linearVelocity;
            float vf = Vector3.Dot(vel, f), vr = Vector3.Dot(vel, r), vn = Vector3.Dot(vel, n);

            // ---- action button (nitro / item)
            if (actionPressed)
            {
                actionPressed = false;
                if (control)
                {
                    if (chargeEnabled) TryFireNitro();
                    else if (Items != null) Items.UseItem();
                }
            }

            // ---- 小喷 (mini boost) on a fresh throttle press inside the window
            if (throttlePressed)
            {
                throttlePressed = false;
                if (control && MiniWindow > 0f && Grounded)
                {
                    MiniWindow = 0f;
                    StartBoost(BoostKind.Mini, 1.14f, 0.7f, 2.2f);
                    StatMiniBoosts++;
                    if (chargeEnabled) AddCharge(0.05f);
                }
            }

            // ---- drift state machine
            UpdateDrift(dt, control, st, vf);

            // ---- yaw
            float speedAbs = Mathf.Abs(vf);
            float targetYawRate;
            if (IsDrifting)
            {
                float inward = (st * DriftDir + 1f) * 0.5f;                      // 0 = counter-steer, 1 = full into the turn
                targetYawRate = DriftDir * Mathf.Lerp(42f, 128f, inward) * stats.DriftTurn * Mathf.Clamp01(speedAbs / 8f);
            }
            else
            {
                targetYawRate = st * MaxGripYawRate(speedAbs) * (vf >= -0.5f ? 1f : -1f);
            }
            if (!Grounded) targetYawRate *= 0.45f;
            if (StunTimer > 0f) targetYawRate = 0f;
            yawVel = Mathf.MoveTowards(yawVel, targetYawRate, 520f * dt);
            yaw += yawVel * dt;
            rb.angularVelocity = Vector3.zero;
            rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            if (Grounded)
            {
                // ---- longitudinal
                float top = stats.MaxSpeed * SpeedMultiplier * (OffRoad ? 0.6f : 1f);
                bool boosting = BoostTimer > 0f;
                float boostTop = stats.MaxSpeed * (1f + (boostPower - 1f) * stats.BoostPower) * SpeedMultiplier;
                if (boosting) top = Mathf.Max(top, boostTop);

                if (th > 0.05f)
                {
                    if (vf < top)
                    {
                        float x = Mathf.Clamp01(vf / top);
                        float curve = Mathf.Max(0.12f, 1.25f - 1.05f * x * x);
                        vf = Mathf.Min(top, vf + stats.Accel * th * curve * dt);
                    }
                }
                else if (th < -0.05f)
                {
                    if (vf > 0.5f) vf = Mathf.Max(0f, vf - BrakeDecel * -th * dt);
                    else if (vf > -ReverseMax) vf -= stats.Accel * 0.55f * -th * dt;
                }
                else
                {
                    vf = Mathf.MoveTowards(vf, 0f, CoastDecel * dt);
                }
                if (boosting && vf < top) vf = Mathf.MoveTowards(vf, top, 34f * dt);
                if (vf > top + 0.05f) vf = Mathf.MoveTowards(vf, top, (OffRoad ? 14f : 7f) * dt);
                if (OffRoad && vf > 4f) vf = Mathf.MoveTowards(vf, 4f, 3f * dt);

                // ---- lateral grip; part of the scrubbed sideways speed carries forward (keeps drifts fast)
                float before = Mathf.Abs(vr);
                if (IsDrifting)
                {
                    vr = Mathf.MoveTowards(vr, 0f, 7.5f * stats.DriftGrip * dt);
                    vr *= Mathf.Exp(-0.9f * stats.DriftGrip * dt);
                    vf -= 1.6f * dt;                                                  // drift costs a bit of speed
                }
                else
                {
                    gripBlend = Mathf.MoveTowards(gripBlend, 1f, dt / 0.35f);
                    float grip = Mathf.Lerp(3f, NormalGrip, gripBlend * gripBlend);
                    vr *= Mathf.Exp(-grip * dt);
                }
                float lost = before - Mathf.Abs(vr);
                if (vf > 1f && vf < top) vf = Mathf.Min(top, vf + lost * (IsDrifting ? 0.42f : 0.25f));

                // cap the slip angle so the kart never swaps ends
                float maxSlip = IsDrifting ? 48f : 30f;
                float maxLat = Mathf.Abs(vf) * Mathf.Tan(maxSlip * Mathf.Deg2Rad);
                vr = Mathf.Clamp(vr, -maxLat - 0.5f, maxLat + 0.5f);

                // parking brake on slopes
                if (Mathf.Abs(th) < 0.05f && Mathf.Abs(vf) < 0.6f && !stunned) { vf = 0f; vr *= 0.5f; }

                if (stunned)
                {
                    float k = StunKind == HitKind.Trap ? 5f : 2.2f;
                    vf *= Mathf.Exp(-k * dt);
                    vr *= Mathf.Exp(-k * dt);
                }

                vn = Mathf.Min(vn, 0f) * 0.3f + Mathf.Max(vn, 0f);                // absorb landing bounce, keep ramp launch
                vel = f * vf + r * vr + n * vn;
                vel -= n * 6f * dt;                                                   // stick to the road over crests
                SlipAngle = vf > 2f ? Mathf.Atan2(Mathf.Abs(vr), vf) * Mathf.Rad2Deg : 0f;
            }
            else
            {
                // light air control, no grip
                SlipAngle = 0f;
                if (BoostTimer > 0f && vf < stats.MaxSpeed * boostPower) vel += f * 10f * dt;
            }

            vel += Vector3.down * Gravity * dt;
            rb.linearVelocity = vel;
            ForwardSpeed = Vector3.Dot(vel, f);

            // nitro charge from drifting (persistent, never cleared)
            if (chargeEnabled && IsDrifting && Grounded && vf > 8f)
            {
                float rate = (0.55f + 0.45f * Mathf.Clamp01(SlipAngle / 32f)) * stats.Charge / nitroChargeTime;
                AddCharge(rate * dt);
            }
        }

        /// <summary>Max non-drift yaw rate (deg/s): turn rate at low speed, lateral-grip limited at speed.
        /// Tight corners at speed therefore need a drift, as in KartRider.</summary>
        public float MaxGripYawRate(float speed)
        {
            float lowSpeed = Mathf.Clamp01(speed / 4.5f);
            float aMax = 19f + (stats.TurnRate - 106f) * 0.2f;
            return Mathf.Min(stats.TurnRate * lowSpeed, aMax / Mathf.Max(speed, 0.1f) * Mathf.Rad2Deg);
        }

        void UpdateDrift(float dt, bool control, float st, float vf)
        {
            if (!IsDrifting)
            {
                bool want = control && (driftPressed || driftHeld) && Mathf.Abs(st) > 0.25f && vf > 9f && Grounded;
                driftPressed = false;
                if (want)
                {
                    IsDrifting = true;
                    DriftDir = st > 0f ? 1 : -1;
                    DriftTime = 0f;
                    gripBlend = 0f;
                    MiniWindow = 0f;
                    // small sideways kick so the tail steps out immediately
                    rb.linearVelocity += -transform.right * DriftDir * 1.6f;
                    StatDrifts++;
                    OnDriftStart?.Invoke();
                }
                return;
            }

            driftPressed = false;
            DriftTime += dt;
            bool holding = driftHeld || st * DriftDir > 0.1f;
            bool counter = st * DriftDir < -0.35f;
            bool end = !control || vf < 6f || !holding || (counter && !driftHeld && DriftTime > 0.12f);
            if (!Grounded && AirTime < 0.4f) end = end && !control;   // hops don't cancel a drift
            if (end) EndDrift();
        }

        void EndDrift()
        {
            if (!IsDrifting) return;
            IsDrifting = false;
            gripBlend = 0.15f;
            if (DriftTime > 0.4f && ControlEnabled) MiniWindow = 0.45f;
            DriftTime = 0f;
        }

        void AddCharge(float amount)
        {
            if (NitroTanks >= maxNitroTanks) { NitroCharge01 = Mathf.Min(1f, NitroCharge01 + amount); return; }
            NitroCharge01 += amount;
            if (NitroCharge01 >= 1f)
            {
                NitroTanks++;
                NitroCharge01 -= 1f;
                OnTankFilled?.Invoke();
                if (NitroTanks >= maxNitroTanks) NitroCharge01 = Mathf.Min(NitroCharge01, 1f);
            }
        }

        // ------------------------------------------------------------ boosts

        public void StartBoost(BoostKind kind, float power, float duration, float kick)
        {
            if (BoostTimer <= 0f || power >= boostPower) { boostPower = power; Boost = kind; }
            BoostTimer = Mathf.Max(BoostTimer, duration);
            Vector3 fwd = transform.forward;
            if (Vector3.Dot(rb.linearVelocity, fwd) < stats.MaxSpeed * power)
                rb.linearVelocity += fwd * kick;
            OnBoost?.Invoke(kind);
        }

        public bool TryFireNitro()
        {
            if (NitroTanks <= 0 || !ControlEnabled || StunTimer > 0f) return false;
            NitroTanks--;
            StatNitros++;
            StartBoost(BoostKind.Nitro, 1.32f, nitroDuration, 3f);
            return true;
        }

        public void GiveNitroTank()
        {
            if (NitroTanks < maxNitroTanks) NitroTanks++;
        }

        public void HitBoostPad()
        {
            if (StunTimer > 0f || Time.time - lastPadTime < 0.5f) return;   // sphere + box both enter the trigger
            lastPadTime = Time.time;
            StatPads++;
            StartBoost(BoostKind.Pad, 1.3f, 1.0f, 4f);
        }

        /// <summary>Called by the race when the lights go green.</summary>
        public void OnRaceStart()
        {
            // start boost: throttle pressed within the last 0.45 s before GO (and not mashed early)
            float since = Time.time - lastThrottleDownTime;
            bool held = throttle > 0.5f;
            if (held && since < 0.45f) StartBoost(BoostKind.Start, 1.25f, 1.3f, 6f);
        }

        public void ForceStartBoost() => StartBoost(BoostKind.Start, 1.25f, 1.3f, 6f);

        public void GiveShield(float seconds) => ShieldTimer = Mathf.Max(ShieldTimer, seconds);

        /// <summary>Item hit. Returns false if blocked by a shield or invulnerability.</summary>
        public bool Hit(HitKind kind)
        {
            if (Invulnerable > 0f) return false;
            if (ShieldTimer > 0f) { ShieldTimer = 0f; Invulnerable = 0.5f; OnShieldBlock?.Invoke(); return false; }
            EndDrift();
            MiniWindow = 0f;
            StunKind = kind;
            switch (kind)
            {
                case HitKind.Spin: StunTimer = 1.3f; spinSpeed = 720f / 1.3f; break;
                case HitKind.Trap: StunTimer = 1.6f; spinSpeed = 90f; break;
                case HitKind.Launch:
                    StunTimer = 1.5f; spinSpeed = 540f;
                    rb.linearVelocity = rb.linearVelocity * 0.35f + Vector3.up * 8f;
                    break;
            }
            BoostTimer = 0f; Boost = BoostKind.None; boostPower = 1f;
            Invulnerable = StunTimer + 0.6f;
            OnHit?.Invoke(kind);
            return true;
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yaw = rotation.eulerAngles.y;
            yawVel = 0f;
            rb.position = position;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            IsDrifting = false;
            BoostTimer = 0f; Boost = BoostKind.None; boostPower = 1f;
            StunTimer = 0f; spinSpeed = 0f; spinAngle = 0f;
            Invulnerable = 1.5f;
            smoothNormal = Vector3.up;
        }

        public void SetYaw(float y) { yaw = y; }

        // ------------------------------------------------------------ collisions

        void OnCollisionEnter(Collision c)
        {
            if (c.contactCount == 0) return;
            var cp = c.GetContact(0);
            if (cp.normal.y > 0.55f) return;   // ground
            var other = c.rigidbody != null ? c.rigidbody.GetComponent<KartController>() : null;
            if (other != null)
            {
                Vector3 push = cp.normal; push.y = 0f;
                rb.linearVelocity += push.normalized * 2.2f;
                OnWallHit?.Invoke(2f, cp.point);
                return;
            }
            float impact = Mathf.Abs(Vector3.Dot(c.relativeVelocity, cp.normal));
            if (impact > 2.5f)
            {
                rb.linearVelocity *= Mathf.Lerp(0.97f, 0.72f, Mathf.InverseLerp(3f, 18f, impact));
                if (impact > 9f) EndDrift();
                if (impact > 6f) StatWallHits++;
                OnWallHit?.Invoke(impact, cp.point);
            }
        }

        // ------------------------------------------------------------ visuals

        void AnimateVisual(float dt)
        {
            if (rig == null || rig.Visual == null) return;
            float v = ForwardSpeed;

            // align to the ground (root is yaw-only)
            Vector3 localN = Quaternion.Inverse(transform.rotation) * smoothNormal;
            Quaternion align = Quaternion.FromToRotation(Vector3.up, localN);

            float accelPitch = Mathf.Clamp(throttle * (BoostTimer > 0f ? -3.5f : -1.5f), -4f, 3f);
            if (!Grounded) accelPitch = Mathf.Clamp(-rb.linearVelocity.y * 1.5f, -12f, 12f);
            pitchVis = Mathf.Lerp(pitchVis, accelPitch, dt * 5f);
            float targetRoll = IsDrifting ? DriftDir * 5f : -steer * Mathf.Clamp01(Mathf.Abs(v) / 15f) * 3f;
            rollVis = Mathf.Lerp(rollVis, targetRoll, dt * 7f);
            float driftYaw = IsDrifting ? DriftDir * Mathf.Clamp(SlipAngle * 0.35f + 6f, 0f, 16f) : 0f;
            driftVisualYaw = Mathf.Lerp(driftVisualYaw, driftYaw, dt * 8f);

            if (StunTimer > 0f) spinAngle += spinSpeed * dt;
            else spinAngle = Mathf.MoveTowardsAngle(spinAngle, 0f, 720f * dt);

            landSquash = Mathf.MoveTowards(landSquash, 0f, dt * 0.6f);
            float lift = StunKind == HitKind.Trap && StunTimer > 0f ? 0.6f + Mathf.Sin(Time.time * 4f) * 0.1f : 0f;
            visualLift = Mathf.Lerp(visualLift, lift, dt * 5f);

            rig.Visual.localRotation = align * Quaternion.Euler(pitchVis, driftVisualYaw + spinAngle, rollVis);
            rig.Visual.localPosition = new Vector3(0f, visualLift - landSquash, 0f);

            // wheels
            wheelSpin += v * dt / 0.28f * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
            {
                if (rig.Wheels[i] == null) continue;
                float mirror = i % 2 == 0 ? 1f : -1f;
                rig.Wheels[i].localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 180f, 0f) * Quaternion.Euler(wheelSpin * mirror, 0f, 0f);
            }
            float steerAngle = IsDrifting ? -DriftDir * 16f + steer * 8f : steer * 24f;
            foreach (var p in rig.SteerPivots)
                if (p != null) p.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
            if (rig.SteeringWheel != null)
                rig.SteeringWheel.localRotation = Quaternion.Euler(-55f, 0f, 0f) * Quaternion.Euler(0f, steerAngle * 3f, 0f);
            if (rig.Driver != null)
                rig.Driver.localRotation = Quaternion.Euler(0f, 0f, -steer * 7f - (IsDrifting ? DriftDir * 5f : 0f));
            if (rig.Head != null)
                rig.Head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9f) * Mathf.Clamp01(Mathf.Abs(v) / 20f) * 1.5f,
                                                          steer * 14f + (IsDrifting ? DriftDir * 12f : 0f), 0f);
        }
    }
}
