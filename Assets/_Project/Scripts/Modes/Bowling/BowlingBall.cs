using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Шар для боулинга.
    ///
    /// Бросок: XRGrabInteractable при отпускании передаёт шару скорость и угловую скорость
    /// руки (throwOnDetach). Дальше шаром управляет только физика PhysX.
    ///
    /// Хук (закрутка): если в момент броска повернуть кисть, шар получает вращение
    /// вокруг оси движения. На «смазанной» части дорожки трение мало и шар скользит прямо;
    /// на сухой части трение превращает это вращение в боковую силу, и шар уходит в дугу.
    /// Повернуть кисть в VR так же сильно, как в жизни, трудно, поэтому эту составляющую
    /// вращения мы умножаем на spinAssist.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class BowlingBall : MonoBehaviour
    {
        [SerializeField] private Transform rackPoint;
        [SerializeField] private float foulLineZ = 1f;
        [Tooltip("Усиление закрутки вокруг направления броска.")]
        [SerializeField] private float spinAssist = 2f;
        [Tooltip("Макс. угловая скорость, рад/с (по умолчанию в Unity всего 7).")]
        [SerializeField] private float maxSpin = 40f;

        [Header("Зоны трения дорожки")]
        [SerializeField] private float oilEndZ = 13.2f;
        [SerializeField] private float deckStartZ = 18.8f;
        [SerializeField] private PhysicsMaterial approachMat;
        [SerializeField] private PhysicsMaterial oilMat;
        [SerializeField] private PhysicsMaterial dryMat;
        [SerializeField] private PhysicsMaterial deckMat;

        /// <summary>Шар пересёк линию заступа после отпускания — бросок засчитан.</summary>
        public event Action Thrown;

        private Collider _col;
        private Rigidbody _rb;
        private XRGrabInteractable _grab;
        private bool _armed; // отпущен, но ещё не пересёк линию заступа

        public float Speed => _rb.linearVelocity.magnitude;
        public bool IsHeld => _grab.isSelected;

        public void Setup(Transform rack, float foulZ)
        {
            rackPoint = rack;
            foulLineZ = foulZ;
        }

        /// <summary>
        /// Трение по зонам. Дорожка — ОДИН сплошной коллайдер: на стыке отдельных коллайдеров
        /// шар цепляется за ребро следующего и подпрыгивает (internal edge в PhysX).
        /// Поэтому трение зоны меняем не у дорожки, а у самого шара — по его координате Z.
        /// У дорожки трение 1.0 и режим Minimum, так что действует трение шара.
        /// </summary>
        public void SetupZones(float oilEnd, float deckStart, PhysicsMaterial approach, PhysicsMaterial oil,
            PhysicsMaterial dry, PhysicsMaterial deck)
        {
            oilEndZ = oilEnd; deckStartZ = deckStart;
            approachMat = approach; oilMat = oil; dryMat = dry; deckMat = deck;
        }

        private PhysicsMaterial ZoneMaterial(float z)
        {
            if (z < foulLineZ) return approachMat;
            if (z < oilEndZ) return oilMat;
            return z < deckStartZ ? dryMat : deckMat;
        }

        private void Awake()
        {
            _col = GetComponent<Collider>();
            _rb = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
            _rb.maxAngularVelocity = maxSpin;
        }

        private void OnEnable()
        {
            _grab.selectEntered.AddListener(OnGrab);
            _grab.selectExited.AddListener(OnRelease);
        }

        private void OnDisable()
        {
            _grab.selectEntered.RemoveListener(OnGrab);
            _grab.selectExited.RemoveListener(OnRelease);
        }

        private void OnGrab(SelectEnterEventArgs args) => _armed = false;

        private void OnRelease(SelectExitEventArgs args)
        {
            _armed = true;
            StartCoroutine(ApplySpinAssist());
        }

        private IEnumerator ApplySpinAssist()
        {
            // XRI задаёт скорость броска в физическом шаге после отпускания — ждём его.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 v = _rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < 0.04f) yield break; // шар просто уронили

            Vector3 dir = v.normalized;
            Vector3 w = _rb.angularVelocity;
            float along = Vector3.Dot(w, dir);          // вращение вокруг оси движения
            _rb.angularVelocity = w + dir * (along * (spinAssist - 1f));
        }

        private void FixedUpdate()
        {
            var zoneMat = ZoneMaterial(transform.position.z);
            if (zoneMat != null && _col.sharedMaterial != zoneMat) _col.sharedMaterial = zoneMat;

            if (_armed && !IsHeld && transform.position.z > foulLineZ)
            {
                _armed = false;
                Thrown?.Invoke();
            }
        }

        private void Update()
        {
            // Шар укатился за пределы мира — возвращаем на подставку.
            if (!IsHeld && transform.position.y < -2f) ReturnToRack();
        }

        /// <summary>Механизм возврата шара: кладём его на подставку рядом с игроком.</summary>
        public void ReturnToRack()
        {
            if (IsHeld || rackPoint == null) return;
            _armed = false;
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = rackPoint.position;
            transform.position = rackPoint.position;
        }

        /// <summary>Пока шар катится, его нельзя подхватить лучом издалека.</summary>
        public void SetGrabbable(bool value) => _grab.enabled = value;
    }
}
