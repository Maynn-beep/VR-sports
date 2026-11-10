using System.Collections;
using UnityEngine;
using VRSports.Core;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Боулинг: 10 фреймов, без таймера.
    ///
    /// Цикл одного броска (корутина ThrowRoutine):
    ///   1. Ожидание: шар на подставке, игрок берёт его и бросает.
    ///   2. Качение: шар пересёк линию заступа; ждём, пока он упадёт в яму или остановится.
    ///   3. Успокоение: ждём, пока кегли перестанут качаться (физика «засыпает»).
    ///   4. Подсчёт: сколько кеглей стояло до броска минус сколько стоит сейчас.
    ///   5. Уборка: сбитые кегли убираем; в новом фрейме ставим все 10 заново.
    ///   6. Шар возвращается на подставку → снова шаг 1.
    /// </summary>
    public class BowlingMode : SportModeBase
    {
        [SerializeField] private BowlingBall ball;
        [SerializeField] private BowlingPin[] pins;
        [SerializeField] private BowlingScoreSheet sheet;

        [Header("Тайминги броска")]
        [SerializeField] private float rollTimeout = 12f;
        [SerializeField] private float settleMin = 1.5f;
        [SerializeField] private float settleMax = 4f;

        public override string ModeId => "bowling";
        public override string DisplayName => "Боулинг";

        private readonly BowlingScore _score = new BowlingScore();
        private Coroutine _throwRoutine;
        private bool _ballInPit;
        private int _strikeStreak;

        public void Setup(BowlingBall b, BowlingPin[] p, BowlingScoreSheet s)
        {
            ball = b; pins = p; sheet = s;
        }

        private void OnEnable() => ball.Thrown += OnBallThrown;
        private void OnDisable() => ball.Thrown -= OnBallThrown;

        private void Start()
        {
            ResetRack();
            ball.ReturnToRack();
            sheet.Refresh(_score);
        }

        // ---------------------------------------------------------------- хуки SportModeBase

        protected override void OnPrepareRound()
        {
            StopThrow();
            _score.Reset();
            _strikeStreak = 0;
            ResetRack();
            ball.ReturnToRack();
            ball.SetGrabbable(true);
            sheet.Refresh(_score);
        }

        protected override void OnGameStarted() => ShowMessage("Фрейм 1 — бросайте!");

        protected override void OnGameFinished()
        {
            StopThrow();
            ball.SetGrabbable(true);
        }

        // ---------------------------------------------------------------- бросок

        private void OnBallThrown()
        {
            if (State != ModeState.Playing || _throwRoutine != null) return;
            _throwRoutine = StartCoroutine(ThrowRoutine());
        }

        public void NotifyBallInPit()
        {
            _ballInPit = true;
            // Вне партии (тренировочные броски) просто возвращаем шар и кегли.
            if (State != ModeState.Playing && _throwRoutine == null) StartCoroutine(PracticeReset());
        }

        private IEnumerator ThrowRoutine()
        {
            ball.SetGrabbable(false);
            _ballInPit = false;
            int standingBefore = CountStanding();
            int frameBefore = _score.CurrentFrame;

            // 2. Качение.
            float t = 0f, slowTime = 0f;
            while (!_ballInPit && t < rollTimeout)
            {
                t += Time.deltaTime;
                slowTime = ball.Speed < 0.05f ? slowTime + Time.deltaTime : 0f;
                if (slowTime > 1.5f) break; // шар остановился на дорожке
                yield return null;
            }

            // 3. Успокоение кеглей.
            float s = 0f;
            while (s < settleMax && (s < settleMin || !AllPinsAsleep()))
            {
                s += Time.deltaTime;
                yield return null;
            }

            // 4. Подсчёт.
            int standing = CountStanding();
            int knocked = Mathf.Max(0, standingBefore - standing);
            _score.Roll(knocked);
            SetScore(_score.Total);
            sheet.Refresh(_score);
            Announce(knocked, standingBefore, standing);

            yield return new WaitForSeconds(1.2f);

            _throwRoutine = null;
            if (_score.IsGameOver)
            {
                ball.ReturnToRack();
                FinishGame();
                yield break;
            }

            // 5. Уборка: новый фрейм или все сбиты → полная расстановка.
            if (_score.CurrentFrame != frameBefore || standing == 0) ResetRack();
            else RemoveFallenPins();

            // 6. Возврат шара.
            ball.ReturnToRack();
            ball.SetGrabbable(true);
            ShowMessage($"Фрейм {_score.CurrentFrame + 1}");
        }

        private void Announce(int knocked, int standingBefore, int standingNow)
        {
            if (standingBefore == 10 && standingNow == 0)
            {
                _strikeStreak++;
                ShowMessage(_strikeStreak switch
                {
                    1 => "СТРАЙК!",
                    2 => "Дубль! Два страйка подряд",
                    3 => "ИНДЮК! Три страйка подряд",
                    _ => $"{_strikeStreak} страйков подряд!"
                });
                Haptics.PulseBoth(1f, 0.4f);
                return;
            }

            _strikeStreak = 0;
            if (standingNow == 0) { ShowMessage("Спэр!"); Haptics.PulseBoth(0.7f, 0.25f); }
            else if (knocked == 0) ShowMessage("Мимо...");
            else ShowMessage($"Сбито кеглей: {knocked}");
        }

        private IEnumerator PracticeReset()
        {
            yield return new WaitForSeconds(2.5f);
            if (State == ModeState.Playing) yield break;
            ResetRack();
            ball.ReturnToRack();
        }

        private void StopThrow()
        {
            if (_throwRoutine != null) StopCoroutine(_throwRoutine);
            _throwRoutine = null;
        }

        // ---------------------------------------------------------------- кегли

        private int CountStanding()
        {
            int n = 0;
            foreach (var p in pins) if (p.IsStanding) n++;
            return n;
        }

        private bool AllPinsAsleep()
        {
            foreach (var p in pins) if (!p.IsAsleep) return false;
            return true;
        }

        private void ResetRack()
        {
            foreach (var p in pins) p.ResetPin();
        }

        private void RemoveFallenPins()
        {
            foreach (var p in pins) if (!p.IsStanding) p.Remove();
        }
    }
}
