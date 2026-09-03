using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Arcade kart physics: acceleration, speed-sensitive steering, and a
    /// KartRider-style drift that charges a mini-turbo released as a boost.
    /// Works for both the player and AI (AI feeds inputs via SetInput).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        [Header("Movement")]
        public float maxSpeed = 22f;
        public float reverseSpeed = 8f;
        public float acceleration = 30f;
        public float brakeForce = 45f;
        public float steerStrength = 95f;
        public float gripNormal = 8f;
        public float gripDrift = 2.2f;

        [Header("Drift / Nitro")]
        public float driftSteerBonus = 1.45f;
        public float nitroChargeTime = 2.4f; // seconds of drifting to fill one tank
        public int maxNitroTanks = 2;
        public float boostSpeed = 30f;
        public float boostDuration = 1.6f;

        [Header("Setup")]
        public bool isPlayer;
        public Transform[] wheels;          // visual wheels, spun by speed
        public Transform body;              // visual body, tilts while drifting

        Rigidbody rb;
        float throttleInput;
        float steerInput;
        bool driftInput;

        bool grounded;
        bool drifting;
        int driftDir;
        float nitroCharge;                   // 0..1, persists between drifts
        int nitroTanks;
        float boostTimer;

        public float CurrentSpeed => rb == null ? 0f : Vector3.Dot(rb.linearVelocity, transform.forward);
        public bool IsDrifting => drifting;
        public bool IsGrounded => grounded;
        public float NitroCharge01 => nitroCharge;
        public int NitroTanks => nitroTanks;
        // 0/1/2 used by the effects for spark color: charging -> nearly full -> tanks maxed
        public int DriftLevel => nitroTanks >= maxNitroTanks ? 2 : (nitroCharge > 0.5f ? 1 : 0);
        public float BoostTimer => boostTimer;
        public bool ControlEnabled { get; set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = new Vector3(0f, -0.4f, 0f);
        }

        void Update()
        {
            if (isPlayer && ControlEnabled)
            {
                SetInput(Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"),
                         Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
                    TryFireNitro();
            }
            AnimateVisuals();
        }

        public void SetInput(float throttle, float steer, bool drift)
        {
            throttleInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steer, -1f, 1f);
            driftInput = drift;
        }

        void FixedUpdate()
        {
            grounded = Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, 0.9f);
            if (!ControlEnabled)
            {
                throttleInput = 0f; steerInput = 0f; driftInput = false;
            }

            HandleDrift();

            float forwardSpeed = CurrentSpeed;
            float targetMax = boostTimer > 0f ? boostSpeed : maxSpeed;
            boostTimer = Mathf.Max(0f, boostTimer - Time.fixedDeltaTime);

            if (grounded)
            {
                // throttle / brake
                if (throttleInput > 0.01f && forwardSpeed < targetMax)
                    rb.AddForce(transform.forward * (throttleInput * acceleration), ForceMode.Acceleration);
                else if (throttleInput < -0.01f)
                {
                    if (forwardSpeed > 0.5f)
                        rb.AddForce(transform.forward * (throttleInput * brakeForce), ForceMode.Acceleration);
                    else if (forwardSpeed > -reverseSpeed)
                        rb.AddForce(transform.forward * (throttleInput * acceleration * 0.6f), ForceMode.Acceleration);
                }

                if (boostTimer > 0f && forwardSpeed < boostSpeed)
                    rb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);

                // colliders are frictionless; this is the rolling resistance
                if (Mathf.Abs(throttleInput) < 0.01f)
                    rb.AddForce(-Vector3.Project(rb.linearVelocity, transform.forward) * 2.5f, ForceMode.Acceleration);

                // steering (speed sensitive, reversed when reversing)
                float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 6f);
                float steer = steerInput;
                if (drifting) steer = Mathf.Lerp(driftDir * 0.55f, driftDir * driftSteerBonus, (steerInput * driftDir + 1f) * 0.5f);
                float yaw = steer * steerStrength * speedFactor * Mathf.Sign(forwardSpeed >= 0 ? 1f : -1f);
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw * Time.fixedDeltaTime, 0f));

                // lateral grip
                Vector3 vel = rb.linearVelocity;
                Vector3 lateral = Vector3.Project(vel, transform.right);
                float grip = drifting ? gripDrift : gripNormal;
                rb.AddForce(-lateral * grip, ForceMode.Acceleration);

                // mild extra downforce for stability
                rb.AddForce(Vector3.down * 12f, ForceMode.Acceleration);
            }
        }

        void HandleDrift()
        {
            bool wantDrift = driftInput && grounded && CurrentSpeed > 8f && Mathf.Abs(steerInput) > 0.15f;
            if (!drifting && wantDrift)
            {
                drifting = true;
                driftDir = steerInput > 0f ? 1 : -1;
            }
            else if (drifting)
            {
                drifting = driftInput && grounded && CurrentSpeed > 5f;
            }

            // charge persists between drifts; a full meter becomes one nitro tank
            if (drifting)
            {
                nitroCharge += Time.fixedDeltaTime / nitroChargeTime;
                if (nitroCharge >= 1f)
                {
                    if (nitroTanks < maxNitroTanks) { nitroTanks++; nitroCharge -= 1f; }
                    else nitroCharge = 1f;   // tanks full: meter stays capped
                }
            }
        }

        /// <summary>Spend one stored nitro tank for a boost (QQ飞车 style, CTRL).</summary>
        public bool TryFireNitro()
        {
            if (nitroTanks <= 0 || !ControlEnabled) return false;
            nitroTanks--;
            boostTimer = boostDuration;
            return true;
        }

        void AnimateVisuals()
        {
            if (wheels != null)
            {
                float spin = CurrentSpeed * 120f * Time.deltaTime;
                foreach (var w in wheels)
                    if (w != null) w.Rotate(spin, 0f, 0f, Space.Self);
            }
            if (body != null)
            {
                float targetRoll = drifting ? -driftDir * 8f : -steerInput * 4f;
                var e = body.localEulerAngles;
                float roll = Mathf.LerpAngle(e.z, targetRoll, Time.deltaTime * 6f);
                body.localEulerAngles = new Vector3(e.x, e.y, roll);
            }
        }

        public void ApplyBoostPad(float speed, float duration)
        {
            boostTimer = Mathf.Max(boostTimer, duration);
            if (CurrentSpeed < speed)
                rb.AddForce(transform.forward * (speed - CurrentSpeed), ForceMode.VelocityChange);
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            boostTimer = 0f; drifting = false;   // stored nitro survives a respawn
        }
    }
}
