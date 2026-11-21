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
        private float _sway;

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
            Stop();
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
            Current = Phase.Idle;
            avatar.SetGlow(new Color(0.2f, 0.9f, 1f));
            GuardPose(3f);
        }

        // ---------------------------------------------------------------- кадр: держать дистанцию

        private void Update()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null) return;

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
        }

        // ---------------------------------------------------------------- поведение

        private IEnumerator Brain()
        {
            while (true)
            {
                // 1. Стойка.
                Current = Phase.Guard;
                GuardPose(3f);
                float wait = Rival.Pick(_level, 2.6f, 1.9f, 1.4f) / _round * Random.Range(0.7f, 1.3f);
                yield return new WaitForSeconds(wait);

                // 2. Замах: рука отводится назад, глаза краснеют — сигнал игроку.
                Current = Phase.Windup;
                var side = Random.value < 0.5f ? AvatarSide.Left : AvatarSide.Right;
                float sx = side == AvatarSide.Left ? -1f : 1f;
                avatar.SetGlow(new Color(1f, 0.15f, 0.1f), 3f);
                avatar.SetHandLocal(side, new Vector3(sx * 0.2f, headHeight - 0.1f, -0.05f), 1.2f);
                yield return new WaitForSeconds(Rival.Pick(_level, 0.85f, 0.65f, 0.48f) / Mathf.Sqrt(_round));

                // 3. Удар — прямой в голову (в точку, где голова была в начале удара).
                Current = Phase.Strike;
                Vector3 aim = _head.position;
                Vector3 from = avatar.Hand(side).position;
                avatar.SetHandWorld(side, aim, 6.5f);
                float t0 = Time.time;
                while (!avatar.HandArrived(side) && Time.time - t0 < 0.35f) yield return null;
                ResolveStrike(aim, from);
                avatar.SetGlow(new Color(0.2f, 0.9f, 1f));

                // 4. Раскрылся: перчатки опущены — окно для контратаки игрока.
                Current = Phase.Open;
                avatar.SetGlow(new Color(1f, 0.85f, 0.2f), 2.5f);
                avatar.SetHandLocal(AvatarSide.Left, new Vector3(-0.22f, headHeight - 0.5f, 0.12f), 2.5f);
                avatar.SetHandLocal(AvatarSide.Right, new Vector3(0.22f, headHeight - 0.5f, 0.12f), 2.5f);
                yield return new WaitForSeconds(Rival.Pick(_level, 1.2f, 0.85f, 0.55f));
                avatar.SetGlow(new Color(0.2f, 0.9f, 1f));
            }
        }

        /// <summary>Стойка: перчатки перед лицом. У лёгкого робота — ниже и шире (дыры в защите).</summary>
        private void GuardPose(float speed)
        {
            float x = Rival.Pick(_level, 0.14f, 0.11f, 0.085f);
            float y = headHeight - Rival.Pick(_level, 0.17f, 0.1f, 0.05f);
            avatar.SetHandLocal(AvatarSide.Left, new Vector3(-x, y, 0.2f), speed);
            avatar.SetHandLocal(AvatarSide.Right, new Vector3(x, y, 0.2f), speed);
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
        }

        // ---------------------------------------------------------------- геометрия

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private Vector3 Clamp(Vector3 p) => new Vector3(
            Mathf.Clamp(p.x, ringCenter.x - ringHalf.x, ringCenter.x + ringHalf.x), 0f,
            Mathf.Clamp(p.z, ringCenter.z - ringHalf.y, ringCenter.z + ringHalf.y));
    }
}
