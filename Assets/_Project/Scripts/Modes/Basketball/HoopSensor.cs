using UnityEngine;

namespace VRSports.Modes.Basketball
{
    /// <summary>Датчик-триггер кольца: сообщает Hoop, что через него прошёл мяч.</summary>
    [RequireComponent(typeof(Collider))]
    public class HoopSensor : MonoBehaviour
    {
        [SerializeField] private Hoop hoop;
        [SerializeField] private bool isTop;

        public void Setup(Hoop h, bool top)
        {
            hoop = h; isTop = top;
        }

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            var ball = rb.GetComponent<BasketballBall>();
            if (ball == null) return;

            if (isTop) hoop.OnTopSensor(ball);
            else hoop.OnBottomSensor(ball, rb);
        }
    }
}
