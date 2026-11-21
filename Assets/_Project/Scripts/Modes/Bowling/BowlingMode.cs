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
    ///
    /// С соперником игроки бросают по очереди целыми фреймами (как в клубе): фрейм игрока →
    /// фрейм бота → ... Бот (BowlingBot) берёт тот же шар, а его бросок проходит через ту же
    /// корутину ThrowRoutine — подсчёт идёт в его собственный протокол BowlingScore.
    /// </summary>
    public class BowlingMode : SportModeBase
    {
        [SerializeField] private BowlingBall ball;
        [SerializeField] private BowlingPin[] pins;
        [SerializeField] private BowlingScoreSheet sheet;
        [SerializeField] private BowlingBot bot;

        [Header("Тайминги броска")]
        [SerializeField] private float rollTimeout = 12f;
        [SerializeField] private float settleMin = 1.5f;
        [SerializeField] private float settleMax = 4f;

        public override string ModeId => "bowling";
        public override string DisplayName => "Боулинг";
        public override bool SupportsRival => bot != null;

        private readonly BowlingScore _score = new BowlingScore();
        private readonly BowlingScore _rivalScore = new BowlingScore();
        private bool _botTurn;

        /// <summary>Протокол того, кто сейчас бросает.</summary>
        private BowlingScore Current => _botTurn ? _rivalScore : _score;
        private Coroutine _throwRoutine;
        private bool _ballInPit;
        private int _strikeStreak;

        public void Setup(BowlingBall b, BowlingPin[] p, BowlingScoreSheet s, BowlingBot rival)
        {
            ball = b; pins = p; sheet = s; bot = rival;
        }

        private void OnEnable()
        {
            if (ball != null) ball.Thrown += OnBallThrown;
        }

        private void OnDisable()
        {
            if (ball != null) ball.Thrown -= OnBallThrown;
        }

        private void Start()
        {
            ResetRack();
            ball.ReturnToRack();
            OnRivalChanged();
        }

        protected override void OnRivalChanged()
        {
            if (bot != null) bot.Show(HasRival);
            RefreshSheet();
        }

        private void RefreshSheet() => sheet.Refresh(_score, HasRival ? _rivalScore : null, _botTurn);

        // ---------------------------------------------------------------- хуки SportModeBase

        protected override void OnPrepareRound()
        {
            StopThrow();
            _score.Reset();
            _rivalScore.Reset();
            _botTurn = false;
            _strikeStreak = 0;
            if (bot != null && HasRival) bot.Park();
            ResetRack();
            ball.ReturnToRack();
            ball.SetGrabbable(true);
            RefreshSheet();
        }

        protected override void OnGameStarted() => ShowMessage("Фрейм 1 — бросайте!");

        protected override void OnGameFinished()
        {
            StopThrow();
            if (bot != null && HasRival) bot.Park();
            _botTurn = false;
            ball.ReturnToRack();
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
            var score = Current;
            int frameBefore = score.CurrentFrame;

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
            score.Roll(knocked);
            if (_botTurn) SetRivalScore(_rivalScore.Total);
            else SetScore(_score.Total);
            RefreshSheet();
            Announce(knocked, standingBefore, standing);

            yield return new WaitForSeconds(1.2f);

            _throwRoutine = null;
            bool frameDone = score.CurrentFrame != frameBefore;
            bool allDone = _score.IsGameOver && (!HasRival || _rivalScore.IsGameOver);
            if (allDone)
            {
                ball.ReturnToRack();
                FinishGame();
                yield break;
            }

            // 5. Уборка: новый фрейм или все сбиты → полная расстановка.
            if (frameDone || standing == 0) ResetRack();
            else RemoveFallenPins();

            // Смена очереди: фрейм закончился, и у другого ещё есть фреймы.
            if (HasRival && frameDone)
            {
                var other = _botTurn ? _score : _rivalScore;
                if (!other.IsGameOver) _botTurn = !_botTurn;
                _strikeStreak = 0;
                RefreshSheet();
            }

            // 6. Возврат шара.
            ball.ReturnToRack();
            if (_botTurn)
            {
                ShowMessage($"Бросает бот — фрейм {_rivalScore.CurrentFrame + 1}");
                // Первый бросок фрейма (или после страйка в 10-м) — по полной расстановке.
                bot.TakeTurn(Rival, frameDone || standing == 0, _rivalScore.CurrentFrame);
            }
            else
            {
                ball.SetGrabbable(true);
                ShowMessage(HasRival ? $"Ваш фрейм {_score.CurrentFrame + 1}" : $"Фрейм {_score.CurrentFrame + 1}");
            }
        }

        private void Announce(int knocked, int standingBefore, int standingNow)
        {
            if (_botTurn)
            {
                ShowMessage(standingBefore == 10 && standingNow == 0 ? "Бот: страйк!"
                    : standingNow == 0 ? "Бот: спэр" : knocked == 0 ? "Бот промахнулся" : $"Бот сбил {knocked}");
                return;
            }
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
