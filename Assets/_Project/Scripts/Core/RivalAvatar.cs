using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    public enum AvatarSide { Left, Right }

    /// <summary>
    /// Робот-соперник: тело на «воздушной подушке», голова, две руки.
    /// Модели частей — из Blender-скрипта Tools/Blender/robot.py.
    ///
    /// Руки — процедурная анимация без аниматора:
    ///  • ботам достаточно задать, ГДЕ должна быть кисть (SetHandLocal / SetHandWorld);
    ///  • кисть плавно едет к цели с заданной скоростью;
    ///  • локоть находится обратной кинематикой (IK) двухзвенной руки: плечо и предплечье одной длины L,
    ///    расстояние плечо–кисть d, значит локоть лежит на расстоянии d/2 вдоль руки
    ///    и h = √(L² − (d/2)²) в сторону «полюса» (вниз-наружу, как у человека).
    /// </summary>
    public class RivalAvatar : MonoBehaviour
    {
        [SerializeField] private Transform visual;        // всё тело — качается на подушке
        [SerializeField] private Transform head;
        [SerializeField] private Transform[] shoulders = new Transform[2];
        [SerializeField] private Transform[] hands = new Transform[2];
        [SerializeField] private Transform[] upperArms = new Transform[2];   // цилиндры
        [SerializeField] private Transform[] foreArms = new Transform[2];
        [SerializeField] private Renderer glowRenderer;  // глаза и огни
        [SerializeField] private int glowSlot = 2;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float segment = 0.27f;  // длина плеча и предплечья
        [SerializeField] private float turnSpeed = 240f;  // град/с

        public Transform Head => head;
        public Transform Hand(AvatarSide s) => hands[(int)s];
        public Vector3 Forward => transform.forward;

        private readonly Vector3[] _handLocal = new Vector3[2];
        private readonly Vector3[] _handTarget = new Vector3[2];
        private readonly float[] _handSpeed = new float[2];
        private readonly Vector3[] _restLocal = new Vector3[2];
        private Vector3 _moveTarget;
        private float _moveSpeed;
        private Vector3? _lookAt;
        private float _bobPhase;
        private Vector3 _knock;       // смещение тела от пропущенного удара (затухает)
        private Vector2 _slump, _slumpTarget;   // (опускание, наклон) при нокауте
        private MaterialPropertyBlock _mpb;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void Setup(Transform vis, Transform h, Transform[] sh, Transform[] hd, Transform[] up, Transform[] fore,
            Renderer glow, TMP_Text text)
        {
            visual = vis; head = h; shoulders = sh; hands = hd; upperArms = up; foreArms = fore;
            glowRenderer = glow; label = text;
        }

        /// <summary>Длина звена руки (в боксе руки длиннее — нужен «размах»).</summary>
        public void SetArmSegment(float length) => segment = length;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < 2; i++)
            {
                _restLocal[i] = transform.InverseTransformPoint(hands[i].position);
                _handLocal[i] = _handTarget[i] = _restLocal[i];
            }
            _moveTarget = transform.position;
            _bobPhase = Random.value * 10f;
        }

        // ---------------------------------------------------------------- управление

        /// <summary>Кисть в локальных координатах робота. speed = 0 — мгновенно.</summary>
        public void SetHandLocal(AvatarSide s, Vector3 local, float speed = 2.5f)
        {
            _handTarget[(int)s] = local;
            _handSpeed[(int)s] = speed;
            if (speed <= 0f) _handLocal[(int)s] = local;
        }

        public void SetHandWorld(AvatarSide s, Vector3 world, float speed = 2.5f) =>
            SetHandLocal(s, transform.InverseTransformPoint(world), speed);

        public void RestHands(float speed = 2f)
        {
            SetHandLocal(AvatarSide.Left, _restLocal[0], speed);
            SetHandLocal(AvatarSide.Right, _restLocal[1], speed);
        }

        public bool HandArrived(AvatarSide s) => (_handLocal[(int)s] - _handTarget[(int)s]).sqrMagnitude < 1e-5f;

        /// <summary>Плавно переехать в точку (по полу).</summary>
        public void MoveTo(Vector3 position, float speed = 1.5f)
        {
            _moveTarget = position;
            _moveSpeed = speed;
        }

        public void Teleport(Vector3 position)
        {
            transform.position = _moveTarget = position;
        }

        public bool Arrived => (transform.position - _moveTarget).sqrMagnitude < 1e-4f;

        /// <summary>Повернуться лицом к точке (только вокруг вертикали). null — не поворачиваться.</summary>
        public void LookAt(Vector3? point) => _lookAt = point;

        /// <summary>Отбросить тело (в мировых координатах) — реакция на пропущенный удар.</summary>
        public void Knock(Vector3 worldOffset) => _knock += transform.InverseTransformVector(worldOffset);

        /// <summary>
        /// «Отключение» робота (нокаут): тело опускается на drop метров и наклоняется вперёд на tilt градусов.
        /// SetSlump(0, 0) — вернуть нормальную позу.
        /// </summary>
        public void SetSlump(float drop, float tilt)
        {
            _slumpTarget = new Vector2(drop, tilt);
        }

        public void SetGlow(Color color, float intensity = 2f)
        {
            if (glowRenderer == null) return;
            _mpb ??= new MaterialPropertyBlock();
            glowRenderer.GetPropertyBlock(_mpb, glowSlot);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetColor(EmissionId, color * intensity);
            glowRenderer.SetPropertyBlock(_mpb, glowSlot);
        }

        public void Say(string text)
        {
            if (label != null) label.text = text;
        }

        // ---------------------------------------------------------------- кадр

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, _moveTarget, _moveSpeed * dt);

            if (_lookAt.HasValue)
            {
                Vector3 d = _lookAt.Value - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * dt);
            }

            // Парение: тело слегка покачивается вверх-вниз.
            _bobPhase += dt * 2.2f;
            _knock = Vector3.Lerp(_knock, Vector3.zero, 1f - Mathf.Exp(-dt * 9f));
            _slump = new Vector2(Mathf.MoveTowards(_slump.x, _slumpTarget.x, dt * 1.2f),   // м/с
                                 Mathf.MoveTowards(_slump.y, _slumpTarget.y, dt * 60f));   // град/с
            float bob = _slumpTarget.x > 0f ? 0f : Mathf.Sin(_bobPhase) * 0.015f;   // «выключенный» робот не парит
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, bob - _slump.x, 0f) + _knock;
                visual.localRotation = Quaternion.Euler(_slump.y, 0f, 0f);
            }

            for (int i = 0; i < 2; i++)
            {
                _handLocal[i] = _handSpeed[i] <= 0f
                    ? _handTarget[i]
                    : Vector3.MoveTowards(_handLocal[i], _handTarget[i], _handSpeed[i] * dt);
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < 2; i++) SolveArm(i);
            if (label != null && Camera.main != null)
            {
                // Подпись всегда повёрнута к игроку.
                Vector3 d = label.transform.position - Camera.main.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) label.transform.rotation = Quaternion.LookRotation(d);
            }
        }

        /// <summary>Двухзвенная IK: плечо S, кисть H, локоть E (см. описание класса).</summary>
        private void SolveArm(int i)
        {
            Vector3 s = shoulders[i].position;
            Vector3 target = transform.TransformPoint(_handLocal[i]);
            Vector3 toH = target - s;
            float maxReach = segment * 2f * 0.999f;
            if (toH.magnitude > maxReach) toH = toH.normalized * maxReach;   // рука не растягивается
            Vector3 h = s + toH;

            float d = toH.magnitude;
            Vector3 dir = d > 1e-4f ? toH / d : transform.forward;
            float side = i == 0 ? -1f : 1f;
            // «Полюс» — куда смотрит локоть: вниз, наружу и немного назад.
            Vector3 pole = -transform.up + transform.right * (0.6f * side) - transform.forward * 0.3f;
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir).normalized;
            float bendH = Mathf.Sqrt(Mathf.Max(0f, segment * segment - d * d / 4f));
            Vector3 e = s + dir * (d / 2f) + bend * bendH;

            hands[i].position = h;
            hands[i].rotation = Quaternion.LookRotation(h - e, transform.up);
            Place(upperArms[i], s, e);
            Place(foreArms[i], e, h);
        }

        /// <summary>Цилиндр-примитив (высота 2 по оси Y) от точки a до точки b.</summary>
        private static void Place(Transform cyl, Vector3 a, Vector3 b)
        {
            if (cyl == null) return;
            Vector3 d = b - a;
            cyl.position = (a + b) / 2f;
            if (d.sqrMagnitude > 1e-8f) cyl.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            var sc = cyl.localScale;
            cyl.localScale = new Vector3(sc.x, d.magnitude / 2f, sc.z);
        }
    }
}
