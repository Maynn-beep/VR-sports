using System;
using System.Collections;
using UnityEngine;

namespace VRSports.Modes.Basketball
{
    /// <summary>
    /// Баскетбольная стойка: щит, обод, сетка и два датчика попадания.
    ///
    /// Как засчитывается попадание: верхний датчик (над ободом) и нижний (внутри сетки).
    /// Мяч должен пройти сначала верхний, потом нижний, двигаясь ВНИЗ, и не дольше чем за 1 с.
    /// Так не засчитываются ни мяч, подброшенный в кольцо снизу, ни мяч, скатившийся по ободу наружу.
    ///
    /// Движение: вся стойка — кинематическое тело (isKinematic). Мы двигаем его через
    /// Rigidbody.MovePosition, и PhysX правильно считает удары мяча о движущийся щит и обод
    /// (у кинематического тела есть скорость, хотя силы на него не действуют).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Hoop : MonoBehaviour
    {
        [SerializeField] private Transform rimCenter;
        [SerializeField] private Transform net;
        [SerializeField] private float amplitude = 1.2f;
        [Tooltip("Угловая частота движения (рад/с) для уровней 0, 1, 2.")]
        [SerializeField] private float[] speeds = { 0f, 0.9f, 1.7f };
        [Tooltip("Сетка гасит скорость мяча (коэффициент, оставляемая доля).")]
        [SerializeField] private float netDamping = 0.55f;

        public event Action<BasketballBall> Scored;

        public Vector3 RimCenter => rimCenter.position;
        /// <summary>Центр кольца в исходном положении — от него размечена 3-очковая линия.</summary>
        public Vector3 HomeRimCenter { get; private set; }

        private Rigidbody _rb;
        private Vector3 _home;
        private int _level;
        private float _phase;
        private Coroutine _swish;

        public void Setup(Transform rim, Transform netTransform)
        {
            rimCenter = rim; net = netTransform;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _home = transform.position;
            HomeRimCenter = rimCenter.position;
        }

        /// <summary>0 — стоит, 1 — медленно ездит, 2 — быстро.</summary>
        public void SetMotion(int level)
        {
            _level = Mathf.Clamp(level, 0, speeds.Length - 1);
            if (_level == 0)
            {
                _phase = 0f;
                _rb.position = _home;
                transform.position = _home;
            }
        }

        private void FixedUpdate()
        {
            if (_level == 0) return;
            _phase += speeds[_level] * Time.fixedDeltaTime;
            _rb.MovePosition(_home + Vector3.right * (amplitude * Mathf.Sin(_phase)));
        }

        internal void OnTopSensor(BasketballBall ball) => ball.TopSensorTime = Time.time;

        internal void OnBottomSensor(BasketballBall ball, Rigidbody rb)
        {
            bool passedTop = Time.time - ball.TopSensorTime < 1f;
            if (!passedTop || rb.linearVelocity.y > 0f) return;

            ball.TopSensorTime = -10f; // одно прохождение — одно попадание
            rb.linearVelocity *= netDamping; // сетка «ловит» мяч
            Scored?.Invoke(ball);

            if (_swish != null) StopCoroutine(_swish);
            _swish = StartCoroutine(Swish());
        }

        /// <summary>Анимация сетки: она вытягивается вниз и пружинит обратно.</summary>
        private IEnumerator Swish()
        {
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / 0.5f * Mathf.PI) * Mathf.Exp(-t * 4f);
                net.localScale = new Vector3(1f - 0.15f * k, 1f + 0.35f * k, 1f - 0.15f * k);
                yield return null;
            }
            net.localScale = Vector3.one;
        }
    }
}
