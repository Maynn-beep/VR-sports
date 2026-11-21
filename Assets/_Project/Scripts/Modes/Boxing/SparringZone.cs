using UnityEngine;

namespace VRSports.Modes.Boxing
{
    public enum ZoneKind { Head, Body, Guard }

    /// <summary>
    /// Зона попадания на роботе-спарринг-партнёре: голова, корпус или перчатка (блок).
    /// Это триггер; перчатка игрока при входе в него сообщает скорость удара.
    /// Удар засчитывается, если кулак летит В зону (та же проверка, что у лап: v·n ≥ 0.5·|v|).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SparringZone : MonoBehaviour
    {
        [SerializeField] private SparringBot bot;
        [SerializeField] private ZoneKind kind;

        public ZoneKind Kind => kind;

        public void Setup(SparringBot b, ZoneKind k)
        {
            bot = b; kind = k;
        }

        /// <returns>true — удар принят (перчатке включается пауза между ударами).</returns>
        public bool Receive(BoxingGlove glove, Vector3 velocity, Vector3 point)
        {
            Vector3 n = (transform.position - glove.transform.position);
            if (n.sqrMagnitude < 1e-6f) n = velocity;
            n.Normalize();
            float into = Vector3.Dot(velocity, n);
            if (into < 1.5f || into < 0.5f * velocity.magnitude) return false;
            bot.ReceivePunch(this, glove, into, point);
            return true;
        }
    }
}
