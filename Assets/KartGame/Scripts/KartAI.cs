using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Waypoint-following AI driver: brakes ahead of corners based on track
    /// curvature, probes for walls with a forward raycast, and reverses
    /// briefly when stuck.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartAI : MonoBehaviour
    {
        public Transform[] waypoints;
        public float waypointReachDistance = 8f;
        [Range(0.5f, 1.2f)] public float skill = 0.9f;

        KartController kart;
        int currentIndex;
        float stuckTimer;
        float reverseTimer;

        public int CurrentWaypoint => currentIndex;

        void Awake()
        {
            kart = GetComponent<KartController>();
        }

        void FixedUpdate()
        {
            if (waypoints == null || waypoints.Length == 0 || !kart.ControlEnabled)
                return;

            int n = waypoints.Length;
            Vector3 pos = transform.position;
            if ((waypoints[currentIndex].position - pos).sqrMagnitude < waypointReachDistance * waypointReachDistance)
                currentIndex = (currentIndex + 1) % n;

            Vector3 target = waypoints[currentIndex].position;
            Vector3 toTarget = target - pos;
            toTarget.y = 0f;
            float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
            float steer = Mathf.Clamp(angle / 30f, -1f, 1f);

            // upcoming curvature: how much the track bends over the next stretch
            Vector3 dirNear = (waypoints[(currentIndex + 1) % n].position - target).normalized;
            Vector3 dirFar = (waypoints[(currentIndex + 3) % n].position -
                              waypoints[(currentIndex + 2) % n].position).normalized;
            float bend = Vector3.Angle(dirNear, dirFar) + Mathf.Abs(angle) * 0.5f;

            float throttle = skill;
            if (bend > 50f) throttle = kart.CurrentSpeed > 12f ? -0.4f : 0.35f;   // brake into hairpins
            else if (bend > 28f) throttle = kart.CurrentSpeed > 17f ? 0.1f : 0.55f;

            // wall radar
            if (Physics.Raycast(pos + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, 7f)
                && hit.collider.name.StartsWith("Wall"))
            {
                float side = Vector3.Dot(hit.normal, transform.right) > 0f ? 1f : -1f;
                steer = Mathf.Clamp(steer + side * 0.8f, -1f, 1f);
                if (hit.distance < 4f && kart.CurrentSpeed > 8f) throttle = Mathf.Min(throttle, 0f);
            }

            // stuck detection -> short reverse
            if (Mathf.Abs(kart.CurrentSpeed) < 1.5f && reverseTimer <= 0f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 1.2f) { reverseTimer = 1.1f; stuckTimer = 0f; }
            }
            else stuckTimer = 0f;

            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.fixedDeltaTime;
                kart.SetInput(-0.9f, -steer, false);
                return;
            }

            // drift through medium corners to charge nitro, fire it on straights
            bool drift = bend > 32f && bend < 55f && kart.CurrentSpeed > 13f;
            if (kart.NitroTanks > 0 && bend < 14f && kart.CurrentSpeed > 10f && !kart.IsDrifting)
                kart.TryFireNitro();

            kart.SetInput(throttle, steer, drift);
        }
    }
}
