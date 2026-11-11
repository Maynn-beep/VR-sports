using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRSports.Core;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Лук. Двуручное взаимодействие:
    ///  • одна рука держит лук за рукоять (XRGrabInteractable на корне лука);
    ///  • другая берёт тетиву (XRSimpleInteractable на «хвостовике» — Nock) и тянет назад.
    ///
    /// Натяжение = проекция смещения руки на ось лука (назад), ограниченная maxDraw.
    /// Лук — это пружина: энергия E = ½·k·x², она переходит в кинетическую энергию стрелы
    /// ½·m·v², отсюда v = x·√(k/m) — скорость стрелы ПРОПОРЦИОНАЛЬНА натяжению.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Bow : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable nock;
        [SerializeField] private LineRenderer stringLine;
        [SerializeField] private Transform topTip;
        [SerializeField] private Transform bottomTip;
        [SerializeField] private Transform stringRest;   // положение тетивы в покое
        [SerializeField] private Arrow arrowTemplate;    // неактивный образец стрелы
        [SerializeField] private Transform rack;         // подставка, куда лук возвращается
        [SerializeField] private TMP_Text ammoText;

        [Header("Физика лука")]
        [Tooltip("Макс. натяжение, м. Меньше длины стрелы (0.85 м), чтобы наконечник не уходил за рукоять.")]
        [SerializeField] private float maxDraw = 0.55f;
        [Tooltip("Скорость при полном натяжении, м/с. 26.5 м/с — под 45° стрела летит ~64 м (чуть дальше края поля 60 м) " +
                 "с учётом сопротивления воздуха (подобрано численным моделированием).")]
        [SerializeField] private float maxArrowSpeed = 26.5f;
        [SerializeField] private float minDrawToShoot = 0.08f;

        /// <summary>Сколько стрел осталось. −1 — без ограничения (тренировка).</summary>
        public int ArrowsLeft { get; set; } = -1;

        /// <summary>Выпущена стрела (для подсчёта в режиме).</summary>
        public event Action<Arrow> ArrowShot;

        public float Draw { get; private set; }
        public bool IsHeld => _grab.isSelected;

        private XRGrabInteractable _grab;
        private Rigidbody _rb;
        private IXRSelectInteractor _drawHand;
        private Arrow _nocked;
        private float _hapticTimer;
        private float _releaseAnim;   // тетива «звенит» после выстрела
        private float _awayTimer;

        public void Setup(XRSimpleInteractable n, LineRenderer line, Transform top, Transform bottom, Transform rest,
            Arrow template, Transform rackPoint, TMP_Text ammo)
        {
            nock = n; stringLine = line; topTip = top; bottomTip = bottom; stringRest = rest;
            arrowTemplate = template; rack = rackPoint; ammoText = ammo;
        }

        private void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            nock.selectEntered.AddListener(OnStringGrab);
            nock.selectExited.AddListener(OnStringRelease);
        }

        private void OnDisable()
        {
            nock.selectEntered.RemoveListener(OnStringGrab);
            nock.selectExited.RemoveListener(OnStringRelease);
        }

        // ---------------------------------------------------------------- тетива

        private void OnStringGrab(SelectEnterEventArgs args)
        {
            if (!IsHeld || ArrowsLeft == 0) return; // тянуть тетиву можно, только держа лук
            _drawHand = args.interactorObject;

            // Накладываем стрелу на тетиву (автоматически — колчан не нужен).
            _nocked = Instantiate(arrowTemplate, transform);
            _nocked.gameObject.SetActive(true);
            _nocked.name = "Arrow";
        }

        private void OnStringRelease(SelectExitEventArgs args)
        {
            if (_drawHand == null) return;
            var hand = _drawHand;
            _drawHand = null;

            if (_nocked != null)
            {
                if (Draw >= minDrawToShoot && IsHeld) Shoot(hand);
                else Destroy(_nocked.gameObject); // не дотянули — стрела «снимается»
                _nocked = null;
            }
            Draw = 0f;
            nock.transform.position = stringRest.position;
        }

        private void Shoot(IXRSelectInteractor hand)
        {
            Release(_nocked, Draw);
            Haptics.Pulse(hand.handedness, 0.5f, 0.06f);
            if (_grab.firstInteractorSelecting is IXRInteractor bowHand)
                Haptics.Pulse(bowHand.handedness, 0.8f, 0.12f); // отдача в руку с луком
        }

        private void Release(Arrow arrow, float draw)
        {
            // Скорость пропорциональна натяжению (модель пружины, см. описание класса).
            float speed = maxArrowSpeed * (draw / maxDraw);
            arrow.transform.SetParent(null, true);
            arrow.Launch(transform.forward * speed);

            if (ArrowsLeft > 0) ArrowsLeft--;
            UpdateAmmoText();
            ArrowShot?.Invoke(arrow);
            _releaseAnim = 1f;
        }

        /// <summary>
        /// Выстрел без рук — для автоматических проверок в редакторе (через Unity MCP).
        /// Идёт по тому же пути, что и настоящий: Release → событие ArrowShot → подсчёт в режиме.
        /// </summary>
        public Arrow TestShoot(float draw01, Quaternion aim)
        {
            if (ArrowsLeft == 0) return null;
            transform.rotation = aim;
            var arrow = Instantiate(arrowTemplate, transform);
            arrow.gameObject.SetActive(true);
            arrow.name = "Arrow";
            float draw = Mathf.Clamp01(draw01) * maxDraw;
            arrow.transform.SetPositionAndRotation(transform.TransformPoint(stringRest.localPosition - Vector3.forward * draw),
                transform.rotation);
            Release(arrow, draw);
            return arrow;
        }

        private void Update()
        {
            if (_drawHand != null && !IsHeld) // уронили лук во время натяжения
            {
                if (_nocked != null) Destroy(_nocked.gameObject);
                _nocked = null;
                _drawHand = null;
                Draw = 0f;
            }

            Vector3 nockLocal = transform.InverseTransformPoint(stringRest.position);
            if (_drawHand != null)
            {
                // Натяжение: насколько рука ушла назад вдоль оси лука (локальная −Z).
                Vector3 hand = transform.InverseTransformPoint(_drawHand.transform.position);
                Draw = Mathf.Clamp(nockLocal.z - hand.z, 0f, maxDraw);
                nockLocal.z -= Draw;
                DrawHaptics();
            }

            Vector3 nockWorld = transform.TransformPoint(nockLocal);
            if (_drawHand == null) nock.transform.position = nockWorld;
            if (_nocked != null)
            {
                _nocked.transform.SetPositionAndRotation(nockWorld, transform.rotation);
            }

            UpdateString(nockLocal);
            ReturnIfDropped();
        }

        /// <summary>Вибрация нарастает с натяжением — игрок «чувствует» силу тетивы.</summary>
        private void DrawHaptics()
        {
            _hapticTimer -= Time.deltaTime;
            if (_hapticTimer > 0f) return;
            _hapticTimer = 0.07f;
            float t = Draw / maxDraw;
            Haptics.Pulse(_drawHand.handedness, 0.04f + 0.3f * t * t, 0.05f);
        }

        private void UpdateString(Vector3 nockLocal)
        {
            // После выстрела тетива колеблется вокруг положения покоя и быстро затухает.
            if (_releaseAnim > 0f)
            {
                _releaseAnim = Mathf.MoveTowards(_releaseAnim, 0f, Time.deltaTime * 4f);
                nockLocal.z += Mathf.Sin(_releaseAnim * 40f) * 0.03f * _releaseAnim;
            }
            stringLine.SetPosition(0, transform.InverseTransformPoint(topTip.position));
            stringLine.SetPosition(1, nockLocal);
            stringLine.SetPosition(2, transform.InverseTransformPoint(bottomTip.position));
        }

        private void ReturnIfDropped()
        {
            // Лук — кинематическое тело: отпущенный, он «зависает» в воздухе
            // и через 1.5 с сам возвращается на подставку.
            bool away = !IsHeld && (transform.position - rack.position).sqrMagnitude > 0.0025f;
            _awayTimer = away ? _awayTimer + Time.deltaTime : 0f;
            if (_awayTimer > 1.5f) ReturnToRack();
        }

        public void ReturnToRack()
        {
            if (IsHeld) return;
            _awayTimer = 0f;
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.position = rack.position;
                _rb.rotation = rack.rotation;
            }
            transform.SetPositionAndRotation(rack.position, rack.rotation);
        }

        public void UpdateAmmoText()
        {
            if (ammoText != null) ammoText.text = ArrowsLeft < 0 ? "" : $"Стрелы: {ArrowsLeft}";
        }
    }
}
