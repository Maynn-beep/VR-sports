using UnityEngine;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Яма за кеглями. Это триггер: он не мешает движению, только сообщает,
    /// что в него что-то попало. Попавший шар значит, что бросок окончен.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BowlingPit : MonoBehaviour
    {
        [SerializeField] private BowlingMode mode;

        public void Setup(BowlingMode m) => mode = m;

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb != null && rb.GetComponent<BowlingBall>() != null) mode.NotifyBallInPit();
        }
    }
}
