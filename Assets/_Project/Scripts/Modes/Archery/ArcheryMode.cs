using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Стрельба из лука: 4 волны с нарастающей сложностью. На каждую волну даётся
    /// ограниченный запас стрел; волна кончается, когда все стрелы выпущены и упали.
    ///
    ///   1. Разминка     — 3 неподвижные мишени на 12/18/25 м, без ветра.
    ///   2. Движение     — мишени ездят, ветер 1.5 м/с.
    ///   3. Тарелочки    — летящие диски, ветер 2 м/с.
    ///   4. Финал        — движущиеся мишени + тарелочки, ветер 2.5 м/с.
    ///
    /// Очки: кольцо (1–10) + бонус за дальность ⌊расстояние / 10 м⌋; тарелочка — 15.
    /// </summary>
    public class ArcheryMode : SportModeBase
    {
        [SerializeField] private Bow bow;
        [SerializeField] private ArcheryTarget[] targets;
        [SerializeField] private ClayThrower thrower;

        public override string ModeId => "archery";
        public override string DisplayName => "Стрельба из лука";

        private const int DiscPoints = 15;

        private struct TargetSpot
        {
            public Vector3 Pos; public float Amp; public float Speed;
            public TargetSpot(float x, float z, float amp = 0f, float speed = 0f)
            {
                Pos = new Vector3(x, 0f, z); Amp = amp; Speed = speed;
            }
        }

        private class Wave
        {
            public string Name; public int Arrows; public float Wind; public bool Discs;
            public TargetSpot[] Spots;
        }

        private static readonly Wave[] Waves =
        {
            new Wave { Name = "Разминка", Arrows = 6, Wind = 0f,
                Spots = new[] { new TargetSpot(0f, 12f), new TargetSpot(-3f, 18f), new TargetSpot(3.5f, 25f) } },
            new Wave { Name = "Движущиеся мишени", Arrows = 6, Wind = 1.5f,
                Spots = new[] { new TargetSpot(0f, 14f, 3f, 0.9f), new TargetSpot(0f, 22f, 4f, 0.6f), new TargetSpot(-4f, 30f) } },
            new Wave { Name = "Тарелочки", Arrows = 8, Wind = 2f, Discs = true,
                Spots = new TargetSpot[0] },
            new Wave { Name = "Финал", Arrows = 10, Wind = 2.5f, Discs = true,
                Spots = new[] { new TargetSpot(0f, 16f, 3.5f, 1.2f), new TargetSpot(0f, 26f, 4.5f, 0.8f) } },
        };

        private int _wave;
        private int _inFlight;
        private bool _waveEnding;
        private Coroutine _waveRoutine;
        private int _tenStreak;
        private readonly List<Arrow> _arrows = new List<Arrow>();

        public void Setup(Bow b, ArcheryTarget[] t, ClayThrower th)
        {
            bow = b; targets = t; thrower = th;
        }

        private void OnEnable()
        {
            if (bow != null) bow.ArrowShot += OnArrowShot;
        }

        private void OnDisable()
        {
            if (bow != null) bow.ArrowShot -= OnArrowShot;
            ArcheryWind.Current = Vector3.zero;
        }

        private void Start()
        {
            // До старта — тренировка: мишени первой волны, стрелы без ограничения.
            SetupWave(0, practice: true);
        }

        // ---------------------------------------------------------------- хуки SportModeBase

        protected override void OnPrepareRound()
        {
            // ВАЖНО: не StopAllCoroutines()! OnPrepareRound вызывается ИЗНУТРИ корутины отсчёта
            // базового класса — StopAllCoroutines остановил бы и её, и игра навсегда
            // застряла бы в состоянии Countdown (так и было в первой версии).
            if (_waveRoutine != null) StopCoroutine(_waveRoutine);
            _waveRoutine = null;
            _tenStreak = 0;
            _inFlight = 0;
            SetupWave(0, practice: false);
            bow.ArrowsLeft = 0; // во время отсчёта стрелять нельзя
            bow.UpdateAmmoText();
        }

        protected override void OnGameStarted()
        {
            bow.ArrowsLeft = Waves[0].Arrows;
            bow.UpdateAmmoText();
            ShowMessage($"Волна 1: {Waves[0].Name}");
        }

        /// <summary>Подстраховка: конец волны проверяется и каждый кадр, а не только по событию.</summary>
        protected override void OnGameUpdate() => CheckWaveEnd();

        protected override void OnGameFinished()
        {
            if (_waveRoutine != null) StopCoroutine(_waveRoutine);
            _waveRoutine = null;
            thrower.StopAll();
            ArcheryWind.Current = Vector3.zero;
            bow.ArrowsLeft = -1;
            bow.UpdateAmmoText();
        }

        // ---------------------------------------------------------------- волны

        private void SetupWave(int index, bool practice)
        {
            _wave = index;
            _waveEnding = false;
            var w = Waves[index];

            ClearArrows();
            for (int i = 0; i < targets.Length; i++)
            {
                if (i < w.Spots.Length) targets[i].Place(w.Spots[i].Pos, w.Spots[i].Amp, w.Spots[i].Speed);
                else targets[i].Hide();
            }

            // Ветер: случайное направление поперёк линии стрельбы (влево или вправо).
            float side = Random.value < 0.5f ? -1f : 1f;
            ArcheryWind.Current = new Vector3(side * w.Wind, 0f, Random.Range(-0.3f, 0.3f) * w.Wind);

            thrower.StopAll();
            thrower.Running = w.Discs && !practice;
            thrower.Interval = index == 3 ? 2f : 2.6f;

            bow.ArrowsLeft = practice ? -1 : w.Arrows;
            bow.UpdateAmmoText();
        }

        private IEnumerator NextWaveRoutine()
        {
            _waveEnding = true;
            yield return new WaitForSeconds(1.5f);
            if (State != ModeState.Playing) yield break;

            if (_wave + 1 >= Waves.Length)
            {
                FinishGame();
                yield break;
            }
            SetupWave(_wave + 1, practice: false);
            ShowMessage($"Волна {_wave + 1}: {Waves[_wave].Name}");
            Haptics.PulseBoth(0.4f, 0.2f);
        }

        private void ClearArrows()
        {
            foreach (var a in _arrows) if (a != null) Destroy(a.gameObject);
            _arrows.Clear();
        }

        // ---------------------------------------------------------------- стрелы

        private void OnArrowShot(Arrow arrow)
        {
            _arrows.Add(arrow);
            arrow.Landed += OnArrowLanded;
            arrow.DiscHit += OnDiscHit;
            _inFlight++;
        }

        private void OnArrowLanded(Arrow arrow, ArcheryTarget target, Vector3 point)
        {
            _inFlight = Mathf.Max(0, _inFlight - 1);

            if (target != null)
            {
                int ring = target.ScoreFor(point);
                target.Flash();
                if (ring > 0)
                {
                    float dist = Vector3.Distance(arrow.LaunchPosition, point);
                    int bonus = Mathf.FloorToInt(dist / 10f);
                    int pts = ring + bonus;
                    if (State == ModeState.Playing) AddScore(pts);

                    _tenStreak = ring == 10 ? _tenStreak + 1 : 0;
                    string head = ring == 10 ? (_tenStreak >= 2 ? $"Снайпер! {_tenStreak} десятки подряд" : "ДЕСЯТКА!") : $"{ring} очков";
                    ShowMessage($"{head}  +{pts} ({dist:F0} м)");
                    Haptics.PulseBoth(ring >= 9 ? 0.8f : 0.4f, 0.15f);
                }
                else ShowMessage("Край мишени — 0");
            }
            else
            {
                _tenStreak = 0;
                ShowMessage("Мимо");
            }

            CheckWaveEnd();
        }

        private void OnDiscHit(Arrow arrow, ClayDisc disc)
        {
            if (State == ModeState.Playing) AddScore(DiscPoints);
            ShowMessage($"Тарелочка! +{DiscPoints}");
            Haptics.PulseBoth(0.9f, 0.2f);
        }

        private void CheckWaveEnd()
        {
            if (State != ModeState.Playing || _waveEnding) return;
            if (bow.ArrowsLeft == 0 && _inFlight == 0) _waveRoutine = StartCoroutine(NextWaveRoutine());
        }
    }
}
