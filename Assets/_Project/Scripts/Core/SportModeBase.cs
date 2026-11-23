using System;
using System.Collections;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>Состояния любого спортивного режима.</summary>
    public enum ModeState
    {
        Ready,      // ждём нажатия «Старт»
        Countdown,  // обратный отсчёт 3-2-1
        Playing,    // идёт игра
        Finished    // игра окончена, показан итог
    }

    /// <summary>
    /// Базовый класс всех режимов — конечный автомат (state machine):
    /// Ready → Countdown → Playing → Finished → (Старт) → Countdown ...
    ///
    /// Общая логика (таймер, очки, рекорды, сообщения) написана здесь один раз.
    /// Конкретный вид спорта наследуется и переопределяет только «хуки»:
    /// OnGameStarted, OnGameUpdate, OnGameFinished.
    /// </summary>
    public abstract class SportModeBase : MonoBehaviour
    {
        [Header("Общие параметры режима")]
        [Tooltip("Длительность раунда в секундах. 0 — без таймера (игра заканчивается по своим правилам).")]
        [SerializeField] private float roundDuration = 0f;

        [Tooltip("Сколько секунд длится обратный отсчёт перед стартом.")]
        [SerializeField] private int countdownSeconds = 3;

        /// <summary>Уникальный идентификатор режима — ключ для рекордов.</summary>
        public abstract string ModeId { get; }

        /// <summary>Название режима для табло.</summary>
        public abstract string DisplayName { get; }

        /// <summary>true — больше очков лучше; false — меньше лучше (гольф).</summary>
        public virtual bool HigherIsBetter => true;

        public ModeState State { get; private set; } = ModeState.Ready;
        public int Score { get; private set; }
        public float TimeLeft { get; private set; }
        public bool HasTimer => roundDuration > 0f;
        public float RoundDuration
        {
            get => roundDuration;
            protected set => roundDuration = value;
        }

        // События — табло и другие системы подписываются на них
        // и не зависят от конкретного вида спорта (паттерн «Наблюдатель»).
        public event Action<ModeState> StateChanged;
        public event Action<int> ScoreChanged;
        public event Action<string> MessageShown;

        // ---------- Соперник-бот (общая часть для всех видов спорта) ----------

        /// <summary>Поддерживает ли режим соперника (кнопка «Соперник» на пульте).</summary>
        public virtual bool SupportsRival => false;
        public RivalLevel Rival { get; private set; } = RivalLevel.Off;
        public bool HasRival => Rival != RivalLevel.Off;
        public int RivalScore { get; private set; }
        public event Action<int> RivalScoreChanged;
        public event Action<RivalLevel> RivalChanged;

        private Coroutine _countdown;

        protected virtual void Awake()
        {
            // Выбранный уровень соперника запоминается для каждого режима отдельно.
            if (SupportsRival) Rival = (RivalLevel)Mathf.Clamp(PlayerPrefs.GetInt("rival_" + ModeId, 0), 0, 3);
        }

        /// <summary>Сменить соперника (только между партиями).</summary>
        public bool SetRival(RivalLevel level)
        {
            if (!SupportsRival || State == ModeState.Playing || State == ModeState.Countdown) return false;
            Rival = level;
            PlayerPrefs.SetInt("rival_" + ModeId, (int)level);
            SetRivalScore(0);
            RivalChanged?.Invoke(level);
            OnRivalChanged();
            return true;
        }

        public void AddRivalScore(int points)
        {
            if (State != ModeState.Playing) return;
            SetRivalScore(RivalScore + points);
        }

        protected void SetRivalScore(int value)
        {
            RivalScore = value;
            RivalScoreChanged?.Invoke(RivalScore);
        }

        /// <summary>Итог дуэли: 1 — победил игрок, −1 — бот, 0 — ничья. По умолчанию — по очкам.</summary>
        protected virtual int DuelOutcome()
        {
            int diff = HigherIsBetter ? Score - RivalScore : RivalScore - Score;
            return diff > 0 ? 1 : diff < 0 ? -1 : 0;
        }

        /// <summary>Засчитывать ли результат в рекорды (спарринг в боксе считает очки по-другому).</summary>
        protected virtual bool CountsForRecord => true;

        /// <summary>Кнопка «Старт». Во время игры работает как «Заново».</summary>
        public void StartGame()
        {
            if (_countdown != null) StopCoroutine(_countdown);
            if (State == ModeState.Playing) OnGameFinished(); // корректно сворачиваем текущую партию
            _countdown = StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            SetScore(0);
            SetRivalScore(0);
            TimeLeft = roundDuration;
            OnPrepareRound();
            SetState(ModeState.Countdown);

            for (int i = countdownSeconds; i > 0; i--)
            {
                ShowMessage(i.ToString());
                Haptics.PulseBoth(0.2f, 0.05f);
                Sfx.Play2D("countdown", 0.6f);
                yield return new WaitForSeconds(1f);
            }

            ShowMessage("Вперёд!");
            Haptics.PulseBoth(0.6f, 0.15f);
            Sfx.Play2D("go", 0.7f);
            SetState(ModeState.Playing);
            _countdown = null;
            OnGameStarted();
        }

        protected virtual void Update()
        {
            if (State != ModeState.Playing) return;

            if (HasTimer)
            {
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    FinishGame();
                    return;
                }
            }

            OnGameUpdate();
        }

        /// <summary>Завершить партию: сохранить рекорд и показать итог.</summary>
        public void FinishGame()
        {
            if (State != ModeState.Playing) return;

            OnGameFinished();
            bool isRecord = CountsForRecord && SaveSystem.SubmitScore(GameSettings.RecordKey(ModeId), Score, HigherIsBetter);
            SetState(ModeState.Finished);
            if (HasRival)
            {
                // Дуэль: сравниваем счёт игрока и бота.
                int diff = DuelOutcome();
                string verdict = diff > 0 ? "ПОБЕДА!" : diff < 0 ? "Поражение" : "Ничья";
                ShowMessage($"{verdict}  {Score} : {RivalScore}" + (isRecord ? "  Рекорд!" : ""));
                Haptics.PulseBoth(diff > 0 ? 0.9f : 0.4f, 0.3f);
                Sfx.Play2D(diff > 0 ? "record" : diff < 0 ? "fail" : "finish", 0.8f);
                return;
            }
            ShowMessage(isRecord ? $"Новый рекорд: {Score}!" : $"Итог: {Score}");
            Haptics.PulseBoth(isRecord ? 0.8f : 0.4f, 0.3f);
            Sfx.Play2D(isRecord ? "record" : "finish", 0.8f);
        }

        public void AddScore(int points)
        {
            if (State != ModeState.Playing) return;
            SetScore(Score + points);
        }

        protected void SetScore(int value)
        {
            Score = value;
            ScoreChanged?.Invoke(Score);
        }

        public void ShowMessage(string text) => MessageShown?.Invoke(text);

        private void SetState(ModeState newState)
        {
            State = newState;
            StateChanged?.Invoke(State);
        }

        // ---------- Хуки для конкретных видов спорта ----------

        /// <summary>
        /// Перед отсчётом: расставить кегли, вернуть мяч и т.п.
        /// ВНИМАНИЕ: вызывается изнутри корутины отсчёта — не вызывайте здесь StopAllCoroutines(),
        /// иначе отсчёт остановится и режим навсегда останется в состоянии Countdown.
        /// Останавливайте только свои корутины по ссылке (StopCoroutine).
        /// </summary>
        protected virtual void OnPrepareRound() { }

        /// <summary>Сразу после «Вперёд!».</summary>
        protected virtual void OnGameStarted() { }

        /// <summary>Каждый кадр во время игры.</summary>
        protected virtual void OnGameUpdate() { }

        /// <summary>Перед подсчётом итога (остановить спавн мишеней и т.п.).</summary>
        protected virtual void OnGameFinished() { }

        /// <summary>Соперник включён/выключен/сменён (между партиями).</summary>
        protected virtual void OnRivalChanged() { }
    }
}
