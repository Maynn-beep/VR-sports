using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSports.Modes.Basketball
{
    /// <summary>
    /// Баскетбольный мяч (FIBA №7: диаметр 24 см, масса 0.62 кг).
    ///
    /// Кроме гравитации и отскоков (их считает PhysX) добавлены две аэродинамические силы:
    ///  • Сопротивление воздуха:  F = −k_d·|v|·v,  k_d = ½·ρ·C_d·A
    ///  • Эффект Магнуса:         F =  k_m·(ω × v), k_m = ½·ρ·A·r
    ///    Вращающийся мяч «закручивает» воздух вокруг себя, и появляется сила поперёк движения.
    ///    Обратное вращение (backspin) при броске кистью чуть приподнимает мяч,
    ///    а при ударе о щит или обод он сильнее гасит скорость.
    /// ρ = 1.225 кг/м³ (воздух), C_d = 0.47 (шар), A = π·r² — площадь сечения.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class BasketballBall : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Material fireMaterial;
        [SerializeField] private TrailRenderer fireTrail;
        [Tooltip("Мяч робота-соперника: игрок его не берёт, очки идут боту.")]
        [SerializeField] private bool isRival;
        [Tooltip("Кольцо — для доводки броска в казуальной сложности.")]
        [SerializeField] private Hoop hoop;

        [Header("Казуальная сложность: доводка броска")]
        [Tooltip("Насколько скорость броска подтягивается к идеальной (0 — нет помощи, 1 — точно в кольцо).")]
        [SerializeField] private float shotAssist = 0.9f;
        [Tooltip("Помощь только если бросок примерно в сторону кольца (градусы).")]
        [SerializeField] private float assistMaxYaw = 30f;

        [Header("Аэродинамика")]
        [SerializeField] private float dragK = 0.0130f;    // ½·1.225·0.47·π·0.12²
        [SerializeField] private float magnusK = 0.0033f;  // ½·1.225·π·0.12²·0.12
        [SerializeField] private float maxSpin = 40f;
        [Tooltip("Минимальное обратное вращение после броска, рад/с (~2 оборота в секунду, как у настоящего броска).")]
        [SerializeField] private float assistBackspin = 14f;

        [Header("Возврат на подставку")]
        [Tooltip("Сколько секунд мяч должен пролежать/прокатиться по полу, прежде чем вернуться.")]
        [SerializeField] private float floorRespawnDelay = 1.5f;

        /// <summary>Бросок не попал: мяч коснулся пола или его поймали раньше, чем он попал в кольцо.</summary>
        public event Action<BasketballBall> ShotMissed;

        public bool ShotActive { get; private set; }
        public Vector3 ReleasePosition { get; private set; }
        public InteractorHandedness LastHand { get; private set; }
        public bool IsRival => isRival;
        public float DragK => dragK;
        public float MagnusK => magnusK;
        public float AssistBackspin => assistBackspin;
        public float Mass => _rb.mass;

        /// <summary>Когда мяч последний раз прошёл верхний датчик кольца (см. Hoop).</summary>
        internal float TopSensorTime = -10f;

        private Rigidbody _rb;
        private XRGrabInteractable _grab;
        private MeshRenderer _renderer;
        private Material[] _normalMaterials;
        private float _lastFloorContact = -10f;
        private float _floorTimer;

        public void Setup(Transform spawn, Material fire, TrailRenderer trail, Hoop h, bool rival = false)
        {
            spawnPoint = spawn; fireMaterial = fire; fireTrail = trail; hoop = h; isRival = rival;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grab = GetComponent<XRGrabInteractable>();
            _renderer = GetComponent<MeshRenderer>();
            _normalMaterials = _renderer.sharedMaterials;
            _rb.maxAngularVelocity = maxSpin;
            if (isRival) _grab.enabled = false; // мяч робота игроку не взять
        }

        /// <summary>Робот держит мяч: мяч кинематический и следует за руками.</summary>
        public void BotHold(Vector3 position)
        {
            ShotActive = false;
            _floorTimer = 0f;
            _rb.isKinematic = true;
            _rb.position = position;
            transform.position = position;
        }

        /// <summary>Бросок робота: та же физика, что после броска игрока (сопротивление, Магнус, отскоки).</summary>
        public void BotLaunch(Vector3 position, Vector3 velocity, Vector3 angular)
        {
            _rb.isKinematic = false;
            _rb.position = position;
            transform.position = position;
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = angular;
            ReleasePosition = position;
            TopSensorTime = -10f;
            ShotActive = true;
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

        private void OnGrab(SelectEnterEventArgs args)
        {
            LastHand = args.interactorObject.handedness;
            if (ShotActive) Miss(); // поймали мяч до того, как он попал, — промах
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            ReleasePosition = transform.position;
            StartCoroutine(CheckShot());
        }

        private IEnumerator CheckShot()
        {
            // XRI задаёт скорость броска в следующем физическом шаге — ждём его.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            // Медленно отпущенный мяч — не бросок, а просто «положили».
            ShotActive = _rb.linearVelocity.magnitude > 2f;
            if (ShotActive) ApplyBackspinAssist();
            if (ShotActive && !isRival && VRSports.Core.GameSettings.Casual) ApplyShotAssist();
        }

        /// <summary>
        /// Помощь закрутке. Щелчок кистью с контроллером в руке почти не закручивает мяч,
        /// а без обратного вращения он отскакивает от щита «зеркально» — назад, а не вниз в кольцо.
        ///
        /// Ось обратного вращения: axis = d × up (d — горизонтальное направление полёта).
        /// Проверка: при полёте по +Z ось = (0,0,1)×(0,1,0) = (−1,0,0); верх мяча тогда
        /// движется назад — это и есть backspin. Если игрок закрутил сам сильнее — не трогаем.
        /// </summary>
        private void ApplyBackspinAssist()
        {
            Vector3 d = _rb.linearVelocity;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return; // бросок строго вверх — направление не определено
            Vector3 axis = Vector3.Cross(d.normalized, Vector3.up);

            Vector3 w = _rb.angularVelocity;
            float current = Vector3.Dot(w, axis);
            if (current < assistBackspin)
                _rb.angularVelocity = w + axis * (assistBackspin - current);
        }

        /// <summary>
        /// Доводка броска (казуальная сложность). Если игрок бросил примерно в сторону кольца,
        /// бот-«тренер» решает обратную задачу баллистики для ТОГО ЖЕ угла вылета, что у игрока,
        /// и подтягивает скорость мяча к идеальной на долю shotAssist:
        ///   v' = v + k·(v_идеал − v),  k = 0,9.
        /// Мяч дальше летит по обычной физике; 10% собственной ошибки игрока остаётся,
        /// поэтому промахи всё ещё возможны, но бросок «в сторону кольца» почти всегда близок к цели.
        /// </summary>
        private void ApplyShotAssist()
        {
            if (hoop == null) return;
            Vector3 v = _rb.linearVelocity;
            Vector3 p = _rb.position;
            Vector3 flatV = new Vector3(v.x, 0f, v.z);
            Vector3 toHoop = hoop.RimCenter - p;
            toHoop.y = 0f;
            if (flatV.sqrMagnitude < 0.25f || Vector3.Angle(flatV, toHoop) > assistMaxYaw) return; // бросок не в кольцо

            // Угол вылета игрока, но не положе 52°: при пологой дуге мяч цепляет передний обод (см. тесты бота).
            float angle = Mathf.Clamp(Mathf.Atan2(v.y, flatV.magnitude) * Mathf.Rad2Deg, 52f, 65f);
            if (!BasketballBallistics.SolveToHoop(this, hoop, p, angle, 0.07f, out Vector3 ideal)) return;

            Vector3 assisted = Vector3.Lerp(v, ideal, shotAssist);
            _rb.linearVelocity = assisted;
            _rb.angularVelocity = BasketballBallistics.Backspin(this, assisted);
        }

        private void FixedUpdate()
        {
            if (_grab.isSelected || _rb.isKinematic) return;

            Vector3 v = _rb.linearVelocity;
            Vector3 drag = -dragK * v.magnitude * v;
            Vector3 magnus = magnusK * Vector3.Cross(_rb.angularVelocity, v);
            _rb.AddForce(drag + magnus, ForceMode.Force);
        }

        private void OnCollisionEnter(Collision c)
        {
            if (c.collider.GetComponent<BasketballFloor>() == null) return;
            _lastFloorContact = Time.time;
            if (ShotActive) Miss();
        }

        private void OnCollisionStay(Collision c)
        {
            if (c.collider.GetComponent<BasketballFloor>() != null) _lastFloorContact = Time.time;
        }

        private void Update()
        {
            if (_grab.isSelected || _rb.isKinematic) { _floorTimer = 0f; return; }

            // Аркадный возврат: мяч, который катается по полу, сам возвращается на подставку.
            bool onFloor = Time.time - _lastFloorContact < 0.3f;
            _floorTimer = onFloor ? _floorTimer + Time.deltaTime : 0f;

            bool lost = transform.position.y < -2f || transform.position.sqrMagnitude > 30f * 30f;
            if (_floorTimer > floorRespawnDelay || lost) ReturnToRack();
        }

        private void Miss()
        {
            ShotActive = false;
            ShotMissed?.Invoke(this);
        }

        /// <summary>Попадание засчитано — бросок завершён, промаха уже не будет.</summary>
        public void ResolveShot() => ShotActive = false;

        /// <summary>Горизонтальное расстояние от точки броска до кольца (для 3-очковых).</summary>
        public float ReleaseDistanceTo(Vector3 point)
        {
            Vector3 d = point - ReleasePosition;
            d.y = 0f;
            return d.magnitude;
        }

        public void ReturnToRack()
        {
            if (_grab.isSelected || spawnPoint == null) return;
            _rb.isKinematic = false;
            ShotActive = false;
            _floorTimer = 0f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = spawnPoint.position;
            transform.position = spawnPoint.position;
            if (fireTrail != null) fireTrail.Clear();
        }

        /// <summary>«Огненный мяч» в последние секунды: другой материал и огненный шлейф.</summary>
        public void SetFire(bool on)
        {
            if (fireMaterial != null)
            {
                var mats = (Material[])_normalMaterials.Clone();
                if (on) mats[0] = fireMaterial;
                _renderer.sharedMaterials = mats;
            }
            if (fireTrail != null) fireTrail.emitting = on;
        }
    }
}
