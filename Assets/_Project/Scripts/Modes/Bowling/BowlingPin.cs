using UnityEngine;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Кегля: физическое тело массой 1.5 кг.
    /// Центр масс задан вручную и опущен вниз, как у настоящей кегли: дно у неё
    /// тяжелее. Поэтому кегля устойчиво стоит и падает правдоподобно.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingPin : MonoBehaviour
    {
        [Tooltip("Кегля считается сбитой, если наклонена сильнее этого угла.")]
        [SerializeField] private float fallenAngle = 20f;
        [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.14f, 0f);

        private Rigidbody _rb;
        private Vector3 _spawnPos;
        private Quaternion _spawnRot;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = centerOfMass;
            _rb.solverIterations = 12; // точнее расчёт контактов — кегли не «дрожат»
            _spawnPos = transform.position;
            _spawnRot = transform.rotation;
        }

        /// <summary>Стоит ли кегля (по правилам наклонённая или упавшая считается сбитой).</summary>
        public bool IsStanding =>
            gameObject.activeSelf &&
            Vector3.Angle(transform.up, Vector3.up) < fallenAngle &&
            transform.position.y > -0.03f; // не упала в жёлоб или яму

        public bool IsAsleep => !gameObject.activeSelf || _rb.IsSleeping();

        /// <summary>Поставить на исходную точку (работа «пинсеттера»).</summary>
        public void ResetPin()
        {
            gameObject.SetActive(true);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPos, _spawnRot);
            _rb.position = _spawnPos;
            _rb.rotation = _spawnRot;
            _rb.Sleep(); // «усыпляем» тело: стоящая кегля не тратит время физики
        }

        /// <summary>Убрать сбитую кеглю с площадки (подметание).</summary>
        public void Remove() => gameObject.SetActive(false);
    }
}
