using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Табличка «Как играть» у пульта режима. Текст — из SportCatalog (одно место для всех описаний).
    /// Видна до старта и после партии; во время игры прячется, чтобы не мешать.
    /// Внизу — подсказка о сопернике и, если включена, о казуальной сложности.
    /// </summary>
    public class TutorialPanel : MonoBehaviour
    {
        [SerializeField] private SportModeBase mode;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text text;

        public void Setup(SportModeBase m, GameObject p, TMP_Text t)
        {
            mode = m; panel = p; text = t;
        }

        private void OnEnable()
        {
            if (mode != null) mode.StateChanged += OnState;
        }

        private void OnDisable()
        {
            if (mode != null) mode.StateChanged -= OnState;
        }

        private void Start()
        {
            string steps = "";
            foreach (var e in SportCatalog.All)
                if (e.Id == mode.ModeId) steps = e.Tutorial;

            string extra = "";
            if (mode.SupportsRival) extra += "\n<color=#D9A0FF>Кнопка «Соперник» — игра против робота.</color>";
            if (GameSettings.Casual) extra += "\n<color=#7CFF8A>Казуальная сложность: игра вам помогает.</color>";
            text.text = $"<b>Как играть</b>\n<size=85%>{steps}</size>{extra}\n<size=85%>Нажмите «Старт», чтобы начать.</size>";
            OnState(mode.State);
        }

        private void OnState(ModeState state)
        {
            panel.SetActive(state != ModeState.Playing && state != ModeState.Countdown);
        }
    }
}
