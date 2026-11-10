using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Оценка скорости объекта, который двигает игрок (рука, клюшка).
    ///
    /// Проблема: трекинг контроллера немного «дрожит», и скорость, посчитанная
    /// по двум соседним кадрам, получается шумной. Решение — хранить позиции за
    /// последние ~0.1 с в кольцевом буфере и брать среднюю скорость за это окно:
    ///   v = (p_новая − p_старая) / (t_новая − t_старая)
    /// </summary>
    public class VelocityTracker : MonoBehaviour
    {
        [Tooltip("Окно усреднения в секундах.")]
        [SerializeField] private float window = 0.1f;

        private const int BufferSize = 32;
        private readonly Vector3[] _positions = new Vector3[BufferSize];
        private readonly float[] _times = new float[BufferSize];
        private int _head;
        private int _count;

        /// <summary>Сглаженная скорость в мировых координатах, м/с.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Модуль скорости, м/с.</summary>
        public float Speed => Velocity.magnitude;

        private void OnEnable() => _count = 0;

        private void Update()
        {
            // Записываем текущую позицию в кольцевой буфер.
            _positions[_head] = transform.position;
            _times[_head] = Time.time;
            _head = (_head + 1) % BufferSize;
            if (_count < BufferSize) _count++;

            int newest = (_head - 1 + BufferSize) % BufferSize;

            // Ищем самую старую запись, которая ещё попадает в окно.
            int oldest = newest;
            for (int i = 1; i < _count; i++)
            {
                int idx = (newest - i + BufferSize) % BufferSize;
                if (Time.time - _times[idx] > window) break;
                oldest = idx;
            }

            float dt = _times[newest] - _times[oldest];
            Velocity = dt > 1e-4f ? (_positions[newest] - _positions[oldest]) / dt : Vector3.zero;
        }
    }
}
