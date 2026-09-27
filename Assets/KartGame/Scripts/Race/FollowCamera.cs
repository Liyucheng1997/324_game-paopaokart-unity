using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Chase camera: trails between the kart heading and its velocity (so drift
    /// angles read clearly), FOV/radial-blur kick with speed and boosts, shake on
    /// impacts, wall avoidance, speed-line particles and a start-grid flyover.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public KartController target;
        public float distance = 5.6f;
        public float height = 2.25f;

        Camera cam;
        PostFX post;
        Vector3 camDir = Vector3.forward;
        Vector3 smoothPos;
        float shake;
        float fovKick;
        ParticleSystem speedLines;

        // intro
        World introWorld;
        float introLen, introT = -1f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1400f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
        }

        void Start()
        {
            post = GetComponent<PostFX>();
            speedLines = MakeSpeedLines();
            if (target != null)
            {
                target.OnWallHit += (impact, p) => AddShake(Mathf.Clamp01(impact / 18f) * 0.35f);
                target.OnLand += impact => AddShake(Mathf.Clamp01(impact / 12f) * 0.25f);
                target.OnHit += k => AddShake(0.45f);
                target.OnBoost += k => { if (k != BoostKind.Mini) fovKick = 8f; else fovKick = 4f; };
                camDir = target.transform.forward;
                if (introT < 0f) Snap();
            }
        }

        public void AddShake(float s) => shake = Mathf.Max(shake, s);

        public void BeginIntro(World w, float length)
        {
            introWorld = w;
            introLen = length;
            introT = 0f;
        }

        public void Snap()
        {
            if (target == null) return;
            camDir = target.transform.forward;
            smoothPos = Desired();
            transform.position = smoothPos;
            transform.rotation = Quaternion.LookRotation(LookPoint() - smoothPos);
        }

        Vector3 Desired()
        {
            Vector3 flat = camDir; flat.y = 0f; flat.Normalize();
            return target.transform.position - flat * distance + Vector3.up * height;
        }

        Vector3 LookPoint()
        {
            Vector3 flat = camDir; flat.y = 0f; flat.Normalize();
            return target.transform.position + Vector3.up * 1.0f + flat * 2.5f;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (introT >= 0f && introT < introLen)
            {
                Intro(dt);
                return;
            }

            // direction: blend heading with velocity while drifting / sliding
            Vector3 heading = target.transform.forward;
            Vector3 vel = target.Body.linearVelocity; vel.y = 0f;
            Vector3 dir = heading;
            if (vel.magnitude > 4f && target.ForwardSpeed > 0f)
            {
                float w = target.IsDrifting ? 0.55f : 0.25f;
                dir = Vector3.Slerp(heading, vel.normalized, w);
            }
            if (target.StunTimer > 0f) dir = camDir;   // don't spin with the kart
            camDir = Vector3.Slerp(camDir, dir, 1f - Mathf.Exp(-dt * (target.IsDrifting ? 3.2f : 5f)));

            Vector3 desired = Desired();
            float speed01 = Mathf.Clamp01(Mathf.Abs(target.ForwardSpeed) / target.stats.MaxSpeed);
            desired -= camDir.normalized * speed01 * 0.8f;

            // keep the camera out of walls / tunnel ceilings
            Vector3 pivot = target.transform.position + Vector3.up * 1.2f;
            Vector3 toCam = desired - pivot;
            if (Physics.SphereCast(pivot, 0.3f, toCam.normalized, out RaycastHit hit, toCam.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                desired = pivot + toCam.normalized * Mathf.Max(hit.distance - 0.2f, 1.5f);

            smoothPos = Vector3.Lerp(smoothPos, desired, 1f - Mathf.Exp(-dt * 12f));
            smoothPos.y = Mathf.Lerp(transform.position.y, desired.y, 1f - Mathf.Exp(-dt * 6f));

            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
            Vector3 shakeOff = shake > 0f ? new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f, 0f) * shake : Vector3.zero;
            transform.position = smoothPos + transform.rotation * shakeOff;
            Quaternion look = Quaternion.LookRotation(LookPoint() - smoothPos);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-dt * 14f));

            // FOV + blur
            bool boosting = target.BoostTimer > 0f && target.Boost != BoostKind.Mini;
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 10f);
            float fov = Mathf.Lerp(60f, 70f, speed01) + (boosting ? 8f : 0f) + fovKick;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, dt * 4f);
            if (post != null) post.speedBlur = Mathf.Lerp(post.speedBlur, boosting ? 1f : Mathf.Clamp01(speed01 - 0.85f) * 2f, dt * 4f);

            var em = speedLines.emission;
            em.rateOverTime = boosting ? 90f : (speed01 > 0.95f ? 15f : 0f);
        }

        void Intro(float dt)
        {
            introT += dt;
            float u = Mathf.Clamp01(introT / introLen);
            float e = u * u * (3f - 2f * u);
            var t = introWorld.Track;
            Vector3 start = t.Pos[0] + Vector3.up * 1.5f;
            Vector3 fwd = t.Fwd[0]; fwd.y = 0; fwd.Normalize();
            // sweep from high in front of the gate, around, to behind the player
            float ang = Mathf.Lerp(-150f, 0f, e);
            Vector3 center = start - fwd * 14f;
            Vector3 offset = Quaternion.Euler(0f, ang, 0f) * (-fwd) * Mathf.Lerp(28f, distance + 6f, e);
            Vector3 pos = center + offset + Vector3.up * Mathf.Lerp(14f, height + 1f, e);
            Vector3 lookAt = Vector3.Lerp(center + Vector3.up * 2f, LookPoint(), e);
            if (u >= 1f)
            {
                introT = introLen;
                Snap();
                return;
            }
            camDir = target.transform.forward;
            smoothPos = pos;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(lookAt - pos);
            cam.fieldOfView = 55f;
        }

        ParticleSystem MakeSpeedLines()
        {
            var go = new GameObject("SpeedLines");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 9f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 30f;
            main.startLifetime = 0.3f;
            main.startSize = 0.05f;
            main.startColor = new Color(1f, 1f, 1f, 0.5f);
            main.maxParticles = 200;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.5f;
            shape.radiusThickness = 0.3f;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6f;
            r.velocityScale = 0.05f;
            r.material = Mats.Particle(ProcTex.SoftDot(), true, Color.white, 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
