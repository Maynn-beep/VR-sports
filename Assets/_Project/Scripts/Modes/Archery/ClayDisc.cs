using System.Collections.Generic;
using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Летящая тарелочка (как в стендовой стрельбе).
    /// Полёт: баллистика с уменьшенной гравитацией — вращающийся диск планирует,
    /// подъёмная сила частично компенсирует вес (упрощённая модель).
    ///
    /// Попадание проверяется аналитически — пересечение отрезка пути наконечника стрелы
    /// со сферой вокруг диска. Так нет задержки синхронизации физики для быстрых объектов.
    /// </summary>
    public class ClayDisc : MonoBehaviour
    {
        public static readonly List<ClayDisc> Active = new List<ClayDisc>();

        [Tooltip("Радиус зоны попадания. Диск увеличен до 22 см (×2 к реальному), зона — 30 см: " +
                 "в VR по настоящей 11-сантиметровой тарелочке стрелой попасть практически невозможно.")]
        [SerializeField] private float hitRadius = 0.3f;
        [SerializeField] private float gravityScale = 0.45f;
        [SerializeField] private float spin = 900f; // град/с — визуальное вращение
        [SerializeField] private ParticleSystem shatterFx;

        private Vector3 _velocity;
        private float _launchTime;

        /// <summary>Сколько секунд диск уже летит.</summary>
        public float Airtime => Time.time - _launchTime;

        /// <summary>Положение диска через dt секунд: x = x₀ + v·t + ½·g·k·t² (k — доля гравитации).</summary>
        public Vector3 Predict(float dt) =>
            transform.position + _velocity * dt + 0.5f * gravityScale * dt * dt * Physics.gravity;

        public System.Action<ClayDisc> Missed;

        public void Setup(ParticleSystem fx) => shatterFx = fx;

        private void Awake()
        {
            // Казуальная сложность: тарелочка и зона попадания в 1,5 раза больше.
            if (VRSports.Core.GameSettings.Casual)
            {
                transform.localScale *= 1.5f;
                hitRadius *= 1.5f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetList() => Active.Clear();

        public void Launch(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            _velocity = velocity;
            _launchTime = Time.time;
            gameObject.SetActive(true);
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void FixedUpdate()
        {
            _velocity += Physics.gravity * (gravityScale * Time.fixedDeltaTime);
            transform.position += _velocity * Time.fixedDeltaTime;
            transform.Rotate(0f, spin * Time.fixedDeltaTime, 0f, Space.Self);
            if (transform.position.y < 0f)
            {
                Missed?.Invoke(this);
                Remove();
            }
        }

        /// <summary>
        /// Пересекает ли отрезок AB сферу (центр — диск, радиус hitRadius):
        /// ищем ближайшую к центру точку отрезка и сравниваем расстояние с радиусом.
        /// </summary>
        public bool IntersectsSegment(Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(transform.position - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (a + ab * t - transform.position).sqrMagnitude <= hitRadius * hitRadius;
        }

        public void Shatter()
        {
            VRSports.Core.Sfx.Play("clay_shatter", transform.position, 1f, 8f);
            if (shatterFx != null)
            {
                shatterFx.transform.position = transform.position;
                shatterFx.Emit(30);
            }
            Remove();
        }

        public void Remove()
        {
            Active.Remove(this);
            gameObject.SetActive(false);
        }
    }
}
