using UnityEngine;

namespace KartGame
{
    /// <summary>Orange pad that flings karts forward, KartRider booster style.</summary>
    public class SpeedBoostPad : MonoBehaviour
    {
        public float boostSpeed = 32f;
        public float boostDuration = 1.4f;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart != null)
                kart.ApplyBoostPad(boostSpeed, boostDuration);
        }
    }
}
