using System;
using System.Collections;
using UnityEngine;
using VRSports.Core;
using Random = UnityEngine.Random;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// Робот — спарринг-партнёр. Поведение — конечный автомат:
    ///
    ///   Guard (стойка) ──таймер──▶ Windup (замах, глаза краснеют) ──▶ Strike (удар в голову игрока)
    ///        ▲                                                                │
    ///        └──────────── Open (раскрылся после удара, перчатки опущены) ◀───┘
    ///
    /// Игрок:
    ///  • бьёт по голове и корпусу — урон зависит от скорости кулака (как оценка на лапах);
    ///  • в стойке голову прикрывают перчатки робота — удар в перчатку = блок (это физические
    ///    зоны-триггеры, а не случайность: можно обойти защиту хуком или апперкотом);
    ///  • после своего удара робот на время «раскрыт» — лучший момент для контратаки.
    /// Защита игрока от удара робота (проверка в момент, когда кулак робота долетел):
    ///  • уклон — голова ушла от точки прицела дальше 22 см (нырок, шаг в сторону);
    ///  • блок — одна из перчаток игрока рядом с линией удара перед лицом (ближе 16 см).
    /// Уровень сложности меняет: частоту атак, длительность замаха (время на реакцию),
    /// плотность стойки и длительность «раскрытия».
    /// </summary>
    public class SparringBot : MonoBehaviour
    {
        [SerializeField] private RivalAvatar avatar;
        [SerializeField] private HealthBar botBar;
        [SerializeField] private HealthBar playerBar;
        [SerializeField] private float distance = 0.75f;     // от головы игрока до головы робота
        [SerializeField] private float headHeight = 1.52f;   // голова робота над его «полом»
        [SerializeField] private Vector2 ringHalf = new Vector2(2.1f, 2.1f);
        [SerializeField] private Vector3 ringCenter = new Vector3(0f, 0f, 0.8f);

        public enum Phase { Idle, Guard, Windup, Strike, Open }
        public Phase Current { get; private set; } = Phase.Idle;

        public float BotHp { get; private set; }
        public float PlayerHp { get; private set; }

        /// <summary>Игрок попал: урон, зона.</summary>
        public event Action<float, ZoneKind> PlayerLanded;
        /// <summary>Удар в перчатку робота.</summary>
        public event Action Blocked;
        /// <summary>Робот ударил: урон по игроку (0 — увернулся/заблокировал) и как защитился игрок.</summary>
        public event Action<float, string> BotStruck;

        private RivalLevel _level;
        private Transform _head;
        private BoxingGlove[] _gloves;
        private float _headY = 1.6f;
        private float _round = 1f;   // темп растёт от раунда к раунду
        private Coroutine _brain;
        private Coroutine _parry;
        private float _sway;
        private bool _counterRequested;
        private bool _finished;          // бой окончен: робот не двигается и не атакует
        private bool _guardLow;          // стойка опущена: закрыт корпус, открыта голова
        private float _bodyPressure, _headPressure;
        private float _lastCounter = -10f;
        private readonly bool[] _punchSeen = new bool[4];

        private const float MaxHp = 100f;

        public void Setup(RivalAvatar a, HealthBar bot, HealthBar player)
        {
            avatar = a; botBar = bot; playerBar = player;
        }

        public void Show(bool on) => gameObject.SetActive(on);

        private void Start()
        {
            _gloves = FindObjectsByType<BoxingGlove>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        // ---------------------------------------------------------------- управление из BoxingMode

        /// <summary>Встать перед игроком на его рост, восстановить здоровье.</summary>
        public void Prepare(Transform head, float headY)
        {
            _head = head;
            _headY = headY;
            // Перчатки ищем заново перед каждым боем: контроллеры могли переподключиться.
            _gloves = FindObjectsByType<BoxingGlove>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Stop();
            _finished = false;
            avatar.SetSlump(0f, 0f);
            avatar.Say("");
            BotHp = PlayerHp = MaxHp;
            botBar.Set(1f, true);
            playerBar.Set(1f, true);
            Vector3 flat = _head != null ? Flat(_head.position) : Vector3.zero;
            avatar.Teleport(Clamp(flat + Vector3.forward * distance) + Vector3.up * (_headY - headHeight));
        }

        public void Begin(RivalLevel level)
        {
            _level = level;
            _round = 1f;
            if (_brain != null) StopCoroutine(_brain);
            _brain = StartCoroutine(Brain());
        }

        /// <summary>Номер раунда (1..3): ускоряет атаки.</summary>
        public void SetRound(int round) => _round = 1f + 0.18f * (round - 1);

        public void Stop()
        {
            if (_brain != null) StopCoroutine(_brain);
            _brain = null;
            if (_parry != null) StopCoroutine(_parry);
            _parry = null;
            _counterRequested = false;
            _guardLow = false;
            _bodyPressure = _headPressure = 0f;
            Current = Phase.Idle;
            avatar.SetGlow(new Color(0.2f, 0.9f, 1f));
            GuardPose(3f);
        }

        // ---------------------------------------------------------------- кадр: держать дистанцию

        private void Update()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null || _finished) return;

            // Робот держится на дистанции удара перед игроком и смотрит на него; слегка «качает маятник».
            Vector3 headFlat = Flat(_head.position);
            Vector3 botFlat = Flat(avatar.transform.position);
            Vector3 dir = botFlat - headFlat;
            dir = dir.sqrMagnitude < 1e-4f ? Vector3.forward : dir.normalized;
            _sway += Time.deltaTime * 1.7f;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (Mathf.Sin(_sway) * 0.06f);
            Vector3 target = Clamp(headFlat + dir * distance + side) + Vector3.up * (_headY - headHeight);
            avatar.MoveTo(target, Current == Phase.Strike ? 0.3f : 1.2f);
            avatar.LookAt(_head.position);
            UpdateParry();
            // «Память» об ударах игрока затухает: старые удары перестают влиять на стойку.
            _bodyPressure = Mathf.Max(0f, _bodyPressure - Time.deltaTime * 0.3f);
            _headPressure = Mathf.Max(0f, _headPressure - Time.deltaTime * 0.3f);
        }

        // ---------------------------------------------------------------- поведение

        private IEnumerator Brain()
        {
            while (true)
            {
                // 1. Стойка. Ожидание прерывается, если робот решил контратаковать.
                Current = Phase.Guard;
                GuardPose(3f);
                float wait = Rival.Pick(_level, 2.6f, 1.6f, 1.1f) / _round * Random.Range(0.7f, 1.3f);
                bool counter = false;
                for (float t = 0f; t < wait; t += Time.deltaTime)
                {
                    if (_counterRequested) { counter = true; break; }
                    yield return null;
                }
                _counterRequested = false;

                // 2–3. Атака: одиночный удар или серия (у сильного робота — «двойка» обеими руками).
                var side = Random.value < 0.5f ? AvatarSide.Left : AvatarSide.Right;
                int punches = Random.value < Rival.Pick(_level, 0f, 0.25f, 0.45f) ? 2 : 1;
                for (int i = 0; i < punches; i++)
                {
                    // Замах: рука отводится назад, глаза краснеют — сигнал игроку.
                    // Контратака и второй удар серии — с коротким замахом (меньше времени на реакцию).
                    Current = Phase.Windup;
                    float sx = side == AvatarSide.Left ? -1f : 1f;
                    avatar.SetGlow(new Color(1f, 0.15f, 0.1f), 3f);
                    avatar.SetHandLocal(side, new Vector3(sx * 0.2f, headHeight - 0.1f, -0.05f), 1.6f);
                    float windup = Rival.Pick(_level, 0.85f, 0.55f, 0.4f) / Mathf.Sqrt(_round);
                    if (counter || i > 0) windup *= 0.6f;
                    yield return new WaitForSeconds(windup);

                    // Удар — прямой в голову (в точку, где голова была в начале удара).
                    Current = Phase.Strike;
                    Vector3 aim = _head.position;
                    Vector3 from = avatar.Hand(side).position;
                    avatar.SetHandWorld(side, aim, 6.5f);
                    float t0 = Time.time;
                    while (!avatar.HandArrived(side) && Time.time - t0 < 0.35f) yield return null;
                    ResolveStrike(aim, from);
                    avatar.SetGlow(new Color(0.2f, 0.9f, 1f));
                    GuardPose(4f, side);   // ударная рука возвращается в защиту
                    side = side == AvatarSide.Left ? AvatarSide.Right : AvatarSide.Left;
                }

                // 4. Раскрылся: перчатки опущены — окно для контратаки игрока (голова открыта).
                Current = Phase.Open;
                avatar.SetGlow(new Color(1f, 0.85f, 0.2f), 2.5f);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.22f, headHeight - 0.5f, 0.12f), 2.5f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.22f, headHeight - 0.5f, 0.12f), 2.5f);
                yield return new WaitForSeconds(Rival.Pick(_level, 1.2f, 0.7f, 0.45f));
                avatar.SetGlow(new Color(0.2f, 0.9f, 1f));
            }
        }

        /// <summary>
        /// Финал боя — робот показывает результат, чтобы его было видно прямо перед собой:
        ///  • нокаут робота — он «отключается»: глаза гаснут, руки падают, тело оседает на пол;
        ///  • победа робота — поднимает руки вверх;
        ///  • решение по очкам — над головой крупная надпись с итогом.
        /// outcome: 1 — победил игрок, −1 — робот, 0 — ничья.
        /// </summary>
        public void Finish(int outcome, bool knockout, int playerScore, int botScore)
        {
            Stop();
            _finished = true;
            string score = $"{playerScore} : {botScore}";
            if (outcome > 0 && knockout)
            {
                avatar.SetGlow(Color.black, 0f);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.3f, headHeight - 0.95f, 0.15f), 2f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.3f, headHeight - 0.95f, 0.15f), 2f);
                // Нижняя точка робота — на 0,95 м ниже центра головы; опускаем его до пола.
                float bottom = avatar.transform.position.y + headHeight - 0.95f;
                avatar.SetSlump(Mathf.Max(0f, bottom - 0.02f), 25f);
                avatar.Say($"<size=160%>НОКАУТ!</size>\nВы победили  {score}");
            }
            else if (outcome < 0)
            {
                avatar.SetGlow(new Color(1f, 0.15f, 0.1f), 3f);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.28f, headHeight + 0.3f, 0.1f), 2.5f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.28f, headHeight + 0.3f, 0.1f), 2.5f);
                avatar.Say(knockout ? $"<size=160%>Вы в нокауте</size>\nПобеда робота  {score}"
                                    : $"<size=160%>Поражение</size>\nРобот победил по очкам  {score}");
            }
            else if (outcome > 0)
            {
                avatar.SetGlow(new Color(0.2f, 0.9f, 1f), 0.5f);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.22f, headHeight - 0.6f, 0.12f), 2f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.22f, headHeight - 0.6f, 0.12f), 2f);
                avatar.Say($"<size=150%>ПОБЕДА!</size>\nПо очкам  {score}");
            }
            else avatar.Say($"<size=160%>Ничья</size>\n{score}");
        }

        // ---------------------------------------------------------------- защита робота

        /// <summary>
        /// Парирование: робот видит летящий кулак игрока (скорость к роботу больше 2 м/с, ближе 60 см)
        /// и с вероятностью, зависящей от уровня, через время реакции подставляет ближнюю перчатку
        /// на линию удара. Перчатка робота — зона блока, поэтому удар физически упирается в неё.
        /// Каждый удар оценивается один раз: пока кулак не отведён назад, второй попытки нет.
        /// </summary>
        private void UpdateParry()
        {
            if (Current != Phase.Guard || _gloves == null) return;
            Vector3 center = avatar.transform.TransformPoint(new Vector3(0f, headHeight - 0.25f, 0f));
            for (int i = 0; i < _gloves.Length && i < _punchSeen.Length; i++)
            {
                var g = _gloves[i];
                if (g == null || !g.isActiveAndEnabled) continue;
                Vector3 toBot = center - g.transform.position;
                float dist = toBot.magnitude;
                float approach = Vector3.Dot(g.Velocity, toBot / Mathf.Max(dist, 1e-4f));
                if (approach < 0.5f) { _punchSeen[i] = false; continue; }   // рука уходит назад — новый удар
                if (_punchSeen[i] || approach < 2f || dist > 0.6f) continue;
                _punchSeen[i] = true;
                if (_parry != null || Random.value > Rival.Pick(_level, 0.15f, 0.45f, 0.7f)) continue;
                _parry = StartCoroutine(Parry(g));
            }
        }

        private IEnumerator Parry(BoxingGlove glove)
        {
            yield return new WaitForSeconds(Rival.Pick(_level, 0.2f, 0.1f, 0.05f)); // время реакции
            if (Current == Phase.Guard && glove != null)
            {
                // Ближняя к кулаку рука робота перехватывает его в 22 см перед корпусом или головой.
                Vector3 local = avatar.transform.InverseTransformPoint(glove.transform.position);
                var hand = local.x < 0f ? AvatarSide.Left : AvatarSide.Right;
                float y = Mathf.Clamp(local.y, headHeight - 0.55f, headHeight);
                float x = Mathf.Clamp(local.x * 0.5f, -0.15f, 0.15f);
                avatar.SetHandLocal(hand, new Vector3(x, y, 0.22f), 7f);
                yield return new WaitForSeconds(0.35f);
                if (Current == Phase.Guard) GuardPose(4f);
            }
            _parry = null;
        }

        /// <summary>Стойка: перчатки перед лицом. У лёгкого робота — ниже и шире (дыры в защите).</summary>
        private void GuardPose(float speed, AvatarSide? only = null)
        {
            float x = Rival.Pick(_level, 0.14f, 0.11f, 0.085f);
            float y = headHeight - Rival.Pick(_level, 0.17f, 0.1f, 0.05f);
            if (_guardLow)
            {
                // Низкая стойка: перчатки и предплечья перед корпусом, голова открыта.
                x = Rival.Pick(_level, 0.13f, 0.1f, 0.09f);
                y = headHeight - 0.38f;
            }
            if (only != AvatarSide.Right) avatar.SetHandLocal(AvatarSide.Left, new Vector3(-x, y, 0.2f), speed);
            if (only != AvatarSide.Left) avatar.SetHandLocal(AvatarSide.Right, new Vector3(x, y, 0.2f), speed);
        }

        private void ResolveStrike(Vector3 aim, Vector3 from)
        {
            if (_head == null) return;
            // Уклон: голова ушла с линии удара.
            if ((_head.position - aim).magnitude > 0.22f)
            {
                BotStruck?.Invoke(0f, "Уклон!");
                return;
            }
            // Блок: перчатка игрока перед лицом на линии удара.
            Vector3 guardPoint = aim + (from - aim).normalized * 0.12f;
            foreach (var g in _gloves)
            {
                if (g == null || !g.isActiveAndEnabled) continue;
                if ((g.transform.position - guardPoint).magnitude < 0.16f)
                {
                    g.Feedback(0.6f);
                    BotStruck?.Invoke(0f, "Блок!");
                    return;
                }
            }

            float dmg = Rival.Pick(_level, 7f, 10f, 13f);
            PlayerHp = Mathf.Max(0f, PlayerHp - dmg);
            playerBar.Set(PlayerHp / MaxHp);
            Haptics.PulseBoth(1f, 0.25f);
            BotStruck?.Invoke(dmg, "Пропущен удар!");
        }

        /// <summary>Удар игрока по зоне робота (вызывает SparringZone).</summary>
        public void ReceivePunch(SparringZone zone, BoxingGlove glove, float speed, Vector3 point)
        {
            if (Current == Phase.Idle) return;
            float strength = Mathf.Clamp01(speed / 7f);
            glove.Feedback(strength);

            // Контратака: робот отвечает на удары игрока (по защите или нет), если сам в стойке.
            // Это наказывает за «молотилку» — серию ударов без защиты.
            if (Current == Phase.Guard && Time.time - _lastCounter > 1.2f &&
                Random.value < Rival.Pick(_level, 0.1f, 0.35f, 0.6f))
            {
                _counterRequested = true;
                _lastCounter = Time.time;
            }

            if (zone.Kind == ZoneKind.Guard)
            {
                Blocked?.Invoke();
                return;
            }

            // Урон растёт со скоростью кулака; корпус — 60% от головы.
            float dmg = Mathf.Clamp(2f + speed * 1.6f, 3f, 14f) * (zone.Kind == ZoneKind.Head ? 1f : 0.6f);
            BotHp = Mathf.Max(0f, BotHp - dmg);
            botBar.Set(BotHp / MaxHp);
            Vector3 dir = (zone.transform.position - glove.transform.position).normalized;
            Vector3 push = dir * (zone.Kind == ZoneKind.Head ? 0.07f : 0.04f) * strength;
            avatar.Knock(push);
            PlayerLanded?.Invoke(dmg, zone.Kind);
            Adapt(zone.Kind);
        }

        /// <summary>
        /// Адаптация к игроку. Робот «запоминает», куда его бьют: каждое попадание добавляет +1
        /// к «давлению» на голову или корпус, давление затухает на 0,3 в секунду.
        /// Если игрок долбит в одно место (давление ≥ порога), робот перестраивает стойку:
        /// бьют в корпус — опускает перчатки и закрывает корпус (голова открывается),
        /// бьют в голову — поднимает. Порог: лёгкий 4, средний 2, сложный 1 удар.
        /// Так однообразная «молотилка» перестаёт работать — приходится чередовать голову и корпус,
        /// как в настоящем боксе.
        /// </summary>
        private void Adapt(ZoneKind hit)
        {
            float threshold = Rival.Pick(_level, 4f, 2f, 1f);
            if (hit == ZoneKind.Body) _bodyPressure += 1f; else _headPressure += 1f;

            bool wantLow = _guardLow;
            if (!_guardLow && _bodyPressure >= threshold) { wantLow = true; _bodyPressure = 0f; }
            else if (_guardLow && _headPressure >= threshold) { wantLow = false; _headPressure = 0f; }
            if (wantLow == _guardLow) return;

            _guardLow = wantLow;
            if (Current == Phase.Guard) GuardPose(5f);
        }

        // ---------------------------------------------------------------- геометрия

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, ringCenter.x - ringHalf.x, ringCenter.x + ringHalf.x), 0f,
            Mathf.Clamp(p.z, ringCenter.z - ringHalf.y, ringCenter.z + ringHalf.y));
    }
}
