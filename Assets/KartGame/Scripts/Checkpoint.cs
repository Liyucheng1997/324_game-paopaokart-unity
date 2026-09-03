using UnityEngine;

namespace KartGame
{
    /// <summary>Invisible ordered trigger used for lap validation and ranking.</summary>
    public class Checkpoint : MonoBehaviour
    {
        public int index;

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart != null && RaceManager.Instance != null)
                RaceManager.Instance.OnCheckpointPassed(kart, index);
        }
    }
}
