using UnityEngine;
using UnityEngine.XR;
using VRSports.Core;

namespace VRSports.Modes.Boxing
{
    public enum Hand { Left, Right }

    /// <summary>
    /// Боксёрская перчатка, надетая на контроллер.
    ///
    /// В боксе нет предмета, который бросают, — физика здесь в СКОРОСТИ КУЛАКА.
    /// Её считает VelocityTracker (среднее по последним 0.1 с, чтобы убрать дрожание трекинга).
    /// Перчатка — триггер на кинематическом теле: она не толкает предметы физически
    /// (рука игрока бесконечно «тяжёлая» для физики), а сообщает лапам и груше,
    /// с какой скоростью и в какой точке пришёл удар.
    /// </summary>
    [RequireComponent(typeof(VelocityTracker))]
    public class BoxingGlove : MonoBehaviour
    {
        [SerializeField] private Hand hand;
        [Tooltip("Повторный удар той же перчаткой засчитывается не раньше, чем через это время.")]
        [SerializeField] private float cooldown = 0.15f;

        public Hand Hand => hand;
        public Vector3 Velocity => _tracker.Velocity;
        public float Speed => _tracker.Speed;

        private VelocityTracker _tracker;
        private float _lastHit = -10f;

        public void Setup(Hand h) => hand = h;

        private void Awake() => _tracker = GetComponent<VelocityTracker>();

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time - _lastHit < cooldown) return;
            Vector3 point = other.ClosestPoint(transform.position);

            var pad = other.GetComponentInParent<BoxingPad>();
            if (pad != null)
            {
                _lastHit = Time.time;
                pad.ReceivePunch(this, Velocity, point);
                return;
            }

            var bag = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PunchingBag>() : null;
            if (bag != null)
            {
                _lastHit = Time.time;
                bag.ReceivePunch(this, Velocity, point);
            }
        }

        /// <summary>Вибрация пропорциональна силе удара.</summary>
        public void Feedback(float strength01)
        {
            Haptics.Pulse(hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand,
                Mathf.Lerp(0.2f, 1f, strength01), Mathf.Lerp(0.04f, 0.12f, strength01));
        }
    }
}
