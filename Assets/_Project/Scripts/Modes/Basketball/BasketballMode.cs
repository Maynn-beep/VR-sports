using TMPro;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Basketball
{
    /// <summary>
    /// Баскетбол: аркада на 60 секунд.
    ///
    /// Игровая петля и мотивация игрока:
    ///  • 2 очка за бросок из ближней зоны, 3 очка из-за 3-очковой линии;
    ///  • серия попаданий подряд даёт множитель: 3 подряд — ×2, 6 подряд — ×3; промах сбрасывает серию;
    ///  • набрал 20 очков — кольцо начинает ездить, 50 очков — ездит быстрее;
    ///  • последние 10 секунд — «огненные мячи»: все очки ×2.
    /// Риск и награда: дальний бросок даёт больше очков, но промах сжигает множитель.
    /// </summary>
    public class BasketballMode : SportModeBase
    {
        [SerializeField] private BasketballBall[] balls;
        [SerializeField] private Hoop hoop;
        [SerializeField] private TMP_Text comboText;

        [Header("Правила")]
        [SerializeField] private float threePointRadius = 5.5f;
        [SerializeField] private int moveAtScore = 20;
        [SerializeField] private int fastAtScore = 50;
        [SerializeField] private float fireTime = 10f;

        public override string ModeId => "basketball";
        public override string DisplayName => "Баскетбол";

        private int _streak;
        private bool _fire;
        private int _hoopLevel;

        public float ThreePointRadius => threePointRadius;
        private int Multiplier => _streak >= 6 ? 3 : _streak >= 3 ? 2 : 1;

        private void Reset() => RoundDuration = 60f;

        public void Setup(BasketballBall[] b, Hoop h, TMP_Text combo)
        {
            balls = b; hoop = h; comboText = combo;
        }

        private void OnEnable()
        {
            if (hoop != null) hoop.Scored += OnBasket;
            if (balls != null) foreach (var b in balls) b.ShotMissed += OnMiss;
        }

        private void OnDisable()
        {
            if (hoop != null) hoop.Scored -= OnBasket;
            if (balls != null) foreach (var b in balls) b.ShotMissed -= OnMiss;
        }

        private void Start() => UpdateComboText();

        // ---------------------------------------------------------------- хуки SportModeBase

        protected override void OnPrepareRound()
        {
            _streak = 0;
            SetFire(false);
            SetHoopLevel(0);
            foreach (var b in balls) b.ReturnToRack();
            UpdateComboText();
        }

        protected override void OnGameStarted() => ShowMessage("Бросайте! У вас 60 секунд");

        protected override void OnGameUpdate()
        {
            if (!_fire && TimeLeft <= fireTime)
            {
                SetFire(true);
                ShowMessage("ОГНЕННЫЕ МЯЧИ! Очки ×2");
                Haptics.PulseBoth(0.7f, 0.3f);
            }
        }

        protected override void OnGameFinished()
        {
            SetFire(false);
            SetHoopLevel(0);
            _streak = 0;
            UpdateComboText();
        }

        // ---------------------------------------------------------------- события мяча и кольца

        private void OnBasket(BasketballBall ball)
        {
            ball.ResolveShot();
            Haptics.Pulse(ball.LastHand, 0.9f, 0.2f);

            if (State != ModeState.Playing)
            {
                ShowMessage("Попадание! Нажмите «Старт»");
                return;
            }

            bool three = ball.ReleaseDistanceTo(hoop.HomeRimCenter) >= threePointRadius;
            _streak++;
            int points = (three ? 3 : 2) * Multiplier * (_fire ? 2 : 1);
            AddScore(points);

            string msg = three ? $"Трёхочковый! +{points}" : $"Попадание! +{points}";
            if (_streak == 3) msg += "   Серия: множитель ×2";
            else if (_streak == 6) msg += "   Серия: множитель ×3";
            ShowMessage(msg);

            UpdateComboText();
            UpdateHoopMotion();
        }

        private void OnMiss(BasketballBall ball)
        {
            if (State != ModeState.Playing) return;
            if (_streak >= 3) ShowMessage("Промах — серия прервана");
            _streak = 0;
            UpdateComboText();
        }

        // ---------------------------------------------------------------- усложнение

        private void UpdateHoopMotion()
        {
            int level = Score >= fastAtScore ? 2 : Score >= moveAtScore ? 1 : 0;
            if (level <= _hoopLevel) return;
            SetHoopLevel(level);
            ShowMessage(level == 1 ? "Кольцо начинает двигаться!" : "Кольцо ускоряется!");
        }

        private void SetHoopLevel(int level)
        {
            _hoopLevel = level;
            hoop.SetMotion(level);
        }

        private void SetFire(bool on)
        {
            _fire = on;
            foreach (var b in balls) b.SetFire(on);
        }

        private void UpdateComboText()
        {
            if (comboText == null) return;
            comboText.text = _streak > 0 ? $"Серия: {_streak}   ×{Multiplier}" : "";
        }
    }
}
