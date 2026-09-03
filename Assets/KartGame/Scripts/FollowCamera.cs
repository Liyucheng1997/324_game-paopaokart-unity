using UnityEngine;

namespace KartGame
{
    /// <summary>Smooth chase camera with speed-based FOV kick.</summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 3.2f, -6.5f);
        public float followLerp = 6f;
        public float lookHeight = 1.2f;

        Camera cam;
        KartController kart;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (target != null)
            {
                kart = target.GetComponent<KartController>();
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.TransformPoint(offset);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.TransformPoint(offset);
            // keep camera above ground
            if (desired.y < target.position.y + 1.2f)
                desired.y = target.position.y + 1.2f;

            transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
            Quaternion look = Quaternion.LookRotation(target.position + Vector3.up * lookHeight - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, followLerp * 1.4f * Time.deltaTime);

            if (cam != null && kart != null)
            {
                float t = Mathf.Clamp01(Mathf.Abs(kart.CurrentSpeed) / kart.boostSpeed);
                float targetFov = Mathf.Lerp(60f, 74f, t);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 4f);
            }
        }
    }
}
