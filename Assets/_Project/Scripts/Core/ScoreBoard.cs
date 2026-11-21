using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Табло режима: название, очки, таймер, рекорд, сообщение.
    /// Ничего не знает о конкретном виде спорта — только подписывается
    /// на события SportModeBase (слабая связанность).
    /// </summary>
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] private SportModeBase mode;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text rivalText;

        public void Setup(SportModeBase m, TMP_Text title, TMP_Text score, TMP_Text timer, TMP_Text best, TMP_Text message,
            TMP_Text rival)
        {
            mode = m; titleText = title; scoreText = score; timerText = timer; bestText = best; messageText = message;
            rivalText = rival;
        }

        private void OnEnable()
        {
            if (mode == null) return;
            mode.ScoreChanged += OnScore;
            mode.StateChanged += OnState;
            mode.MessageShown += OnMessage;
            mode.RivalScoreChanged += OnRivalScore;
            mode.RivalChanged += OnRivalChanged;
        }

        private void OnDisable()
        {
            if (mode == null) return;
            mode.ScoreChanged -= OnScore;
            mode.StateChanged -= OnState;
            mode.MessageShown -= OnMessage;
            mode.RivalScoreChanged -= OnRivalScore;
            mode.RivalChanged -= OnRivalChanged;
        }

        private void Start()
        {
            if (mode == null) return;
            titleText.text = GameSettings.Casual ? $"{mode.DisplayName} <size=60%>(казуальная)</size>" : mode.DisplayName;
            OnScore(mode.Score);
            OnMessage("Нажмите «Старт»");
            RefreshBest();
            RefreshTimer();
            OnRivalScore(mode.RivalScore);
        }

        private void Update()
        {
            if (mode != null && mode.HasTimer && mode.State == ModeState.Playing) RefreshTimer();
        }

        private void OnScore(int score) => scoreText.text = mode.HasRival ? $"Вы: {score}" : $"Очки: {score}";

        /// <summary>Строка соперника видна, только когда бот включён.</summary>
        private void OnRivalScore(int score)
        {
            if (rivalText == null) return;
            rivalText.text = mode.HasRival ? $"Бот ({Rival.Name(mode.Rival)}): {score}" : "";
        }

        private void OnRivalChanged(RivalLevel level)
        {
            OnScore(mode.Score);
            OnRivalScore(mode.RivalScore);
            OnMessage(level == RivalLevel.Off ? "Соперник выключен" : $"Соперник: {Rival.Name(level)}. Нажмите «Старт»");
        }

        private void OnState(ModeState state)
        {
            if (state == ModeState.Finished) RefreshBest();
            RefreshTimer();
        }

        private void OnMessage(string msg) => messageText.text = msg;

        private void RefreshBest()
        {
            bestText.text = SaveSystem.TryGetBest(GameSettings.RecordKey(mode.ModeId), out int best) ? $"Рекорд: {best}" : "Рекорд: —";
        }

        private void RefreshTimer()
        {
            if (!mode.HasTimer) { timerText.text = ""; return; }
            float t = mode.State == ModeState.Playing ? mode.TimeLeft : mode.RoundDuration;
            int sec = Mathf.CeilToInt(t);
            timerText.text = $"{sec / 60}:{sec % 60:00}";
            // Последние 10 секунд — красным, чтобы подстегнуть игрока.
            timerText.color = mode.State == ModeState.Playing && t <= 10f ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }
    }
}
