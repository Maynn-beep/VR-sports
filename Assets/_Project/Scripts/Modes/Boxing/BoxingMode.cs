using System.Collections;
using TMPro;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// Бокс: 3 раунда по 30 секунд (общий таймер 90 с), темп растёт с каждым раундом.
    ///
    /// Игровая петля:
    ///  • лапы загораются по одной или сериями (комбинации 2–3 удара);
    ///  • цвет = рука: синий — левая, красный — правая;
    ///  • оценка удара по скорости кулака: &lt;3 м/с «слабо» (1), 3–5 «хорошо» (2), ≥5 «отлично» (3);
    ///  • комбо: каждые 8 точных ударов подряд +1 к множителю (до ×4);
    ///    промах, не та рука или пропущенный «ответный удар» сбрасывают комбо;
    ///  • со 2-го раунда тренажёр бьёт в ответ перекладиной — от неё нужно нырнуть (+5 за уклон).
    /// </summary>
    public class BoxingMode : SportModeBase
    {
        [SerializeField] private BoxingPad[] pads;          // 0 — левый верх, 1 — правый верх, 2 — левый корпус, 3 — правый корпус, 4 — центр
        [SerializeField] private Transform padRig;          // поднимается под рост игрока
        [SerializeField] private DodgeBar dodgeBar;
        [SerializeField] private TMP_Text comboText;

        [Header("Оценка удара, м/с")]
        [SerializeField] private float minSpeed = 1.5f;
        [SerializeField] private float goodSpeed = 3f;
        [SerializeField] private float greatSpeed = 5f;

        public override string ModeId => "boxing";
        public override string DisplayName => "Бокс";

        // Параметры раундов: окно на удар, пауза между сериями, длина серии, период перекладины (0 — нет).
        private static readonly float[] Window = { 1.6f, 1.2f, 0.9f };
        private static readonly float[] Gap = { 0.7f, 0.5f, 0.35f };
        private static readonly int[] ComboLen = { 1, 2, 3 };
        private static readonly float[] DodgeEvery = { 0f, 9f, 6f };

        private const float RoundLength = 30f;
        private int _combo;
        private int _round = -1;
        private float _headY = 1.6f;
        private float _nextDodge;
        private Coroutine _sequence;
        private Transform _head;

        private int Multiplier => Mathf.Min(4, 1 + _combo / 8);

        private void Reset() => RoundDuration = 90f;

        public void Setup(BoxingPad[] p, Transform rig, DodgeBar bar, TMP_Text combo)
        {
            pads = p; padRig = rig; dodgeBar = bar; comboText = combo;
        }

        private void OnEnable()
        {
            if (pads != null)
                foreach (var p in pads) { p.Punched += OnPunched; p.Expired += OnExpired; }
            if (dodgeBar != null) dodgeBar.Resolved += OnDodgeResolved;
        }

        private void OnDisable()
        {
            if (pads != null)
                foreach (var p in pads) { p.Punched -= OnPunched; p.Expired -= OnExpired; }
            if (dodgeBar != null) dodgeBar.Resolved -= OnDodgeResolved;
        }

        private void Start()
        {
            _head = Camera.main != null ? Camera.main.transform : null;
            UpdateComboText();
        }

        // ---------------------------------------------------------------- хуки SportModeBase

        protected override void OnPrepareRound()
        {
            // Не StopAllCoroutines(): этот хук вызывается изнутри корутины отсчёта базового класса.
            StopSequence();
            dodgeBar.Cancel();
            foreach (var p in pads) p.TurnOff();
            _combo = 0;
            _round = -1;
            Calibrate();
            UpdateComboText();
        }

        protected override void OnGameStarted() => ShowMessage("Раунд 1 — бейте по светящимся лапам!");

        protected override void OnGameUpdate()
        {
            int round = Mathf.Min(2, Mathf.FloorToInt((RoundDuration - TimeLeft) / RoundLength));
            if (round != _round)
            {
                _round = round;
                if (round > 0)
                {
                    ShowMessage(round == 1 ? "Раунд 2 — тренажёр бьёт в ответ, ныряйте!" : "Раунд 3 — финальный, максимальный темп!");
                    Haptics.PulseBoth(0.6f, 0.25f);
                }
                _nextDodge = Time.time + 3f;
            }

            // «Ответный удар» — только когда нет активной серии лап.
            if (DodgeEvery[_round] > 0f && Time.time >= _nextDodge && _sequence == null && !dodgeBar.Busy)
            {
                _nextDodge = Time.time + DodgeEvery[_round];
                dodgeBar.Attack(_head, _headY);
                return;
            }

            if (_sequence == null && !dodgeBar.Busy) _sequence = StartCoroutine(Sequence());
        }

        protected override void OnGameFinished()
        {
            StopSequence();
            dodgeBar.Cancel();
            foreach (var p in pads) p.TurnOff();
            _combo = 0;
            UpdateComboText();
        }

        // ---------------------------------------------------------------- серии ударов

        private IEnumerator Sequence()
        {
            yield return new WaitForSeconds(Gap[_round]);
            int len = ComboLen[_round] == 1 ? 1 : Random.Range(ComboLen[_round] - 1, ComboLen[_round] + 1);
            int last = -1;
            for (int i = 0; i < len; i++)
            {
                int idx;
                do idx = Random.Range(0, pads.Length); while (idx == last);
                last = idx;
                var pad = pads[idx];
                pad.Light(HandFor(idx), Window[_round]);
                while (pad.IsLit) yield return null; // ждём удар или истечение окна
                yield return new WaitForSeconds(0.12f);
            }
            _sequence = null;
        }

        /// <summary>Левые лапы — левой рукой, правые — правой, центральная — любой из двух (случайно).</summary>
        private static Hand HandFor(int padIndex) => padIndex switch
        {
            0 or 2 => Hand.Left,
            1 or 3 => Hand.Right,
            _ => Random.value < 0.5f ? Hand.Left : Hand.Right
        };

        private void StopSequence()
        {
            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = null;
        }

        // ---------------------------------------------------------------- события

        private void OnPunched(BoxingPad pad, BoxingGlove glove, float speed, bool correctHand, bool wasLit)
        {
            if (speed < minSpeed) return;
            float strength = Mathf.Clamp01(speed / 7f);
            glove.Feedback(strength);

            if (State != ModeState.Playing || !wasLit) return;
            if (!correctHand)
            {
                ShowMessage("Не та рука!");
                BreakCombo();
                return;
            }

            int rating = speed >= greatSpeed ? 3 : speed >= goodSpeed ? 2 : 1;
            string label = rating == 3 ? "ОТЛИЧНО!" : rating == 2 ? "Хорошо" : "Слабо";
            _combo++;
            int pts = rating * Multiplier;
            AddScore(pts);
            ShowMessage($"{label} {speed:F1} м/с  +{pts}");
            if (_combo % 8 == 0 && Multiplier <= 4) Haptics.PulseBoth(0.5f, 0.15f);
            UpdateComboText();
        }

        private void OnExpired(BoxingPad pad)
        {
            if (State != ModeState.Playing) return;
            ShowMessage("Не успели!");
            BreakCombo();
        }

        private void OnDodgeResolved(bool dodged)
        {
            if (State != ModeState.Playing) return;
            if (dodged)
            {
                AddScore(5);
                ShowMessage("Уклон! +5");
            }
            else
            {
                ShowMessage("Пропущен удар — комбо сброшено");
                Haptics.PulseBoth(1f, 0.35f);
                BreakCombo();
            }
        }

        private void BreakCombo()
        {
            _combo = 0;
            UpdateComboText();
        }

        // ---------------------------------------------------------------- калибровка

        /// <summary>
        /// Рост игрока: высота головы в момент старта. Лапы и перекладина поднимаются под неё,
        /// поэтому играть удобно и высокому, и низкому игроку, и сидя.
        /// </summary>
        private void Calibrate()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            float y = _head != null ? _head.position.y : 1.6f;
            _headY = y > 0.9f ? y : 1.6f; // без шлема камера на полу — берём средний рост
            var p = padRig.position;
            padRig.position = new Vector3(p.x, _headY - 0.1f, p.z);
        }

        private void UpdateComboText()
        {
            if (comboText != null) comboText.text = _combo > 0 ? $"Комбо: {_combo}   ×{Multiplier}" : "";
        }
    }
}
