using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Переключатель сложности в хабе: кнопка меняет «Реалистичная» ↔ «Казуальная».
    /// Подпись объясняет, что даёт казуальная сложность.
    /// </summary>
    public class DifficultyToggle : MonoBehaviour
    {
        [SerializeField] private VRButton button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text hint;

        public void Setup(VRButton b, TMP_Text l, TMP_Text h)
        {
            button = b; label = l; hint = h;
        }

        private void OnEnable()
        {
            if (button != null) button.Pressed += Toggle;
            GameSettings.DifficultyChanged += Refresh;
        }

        private void OnDisable()
        {
            if (button != null) button.Pressed -= Toggle;
            GameSettings.DifficultyChanged -= Refresh;
        }

        private void Start() => Refresh(GameSettings.Casual);

        private void Toggle() => GameSettings.Casual = !GameSettings.Casual;

        private void Refresh(bool casual)
        {
            label.text = $"Сложность:\n<b>{GameSettings.DifficultyName}</b>";
            label.color = casual ? new Color(0.5f, 1f, 0.6f) : Color.white;
            hint.text = casual
                ? "Бортики в боулинге, доводка броска в баскетболе,\nтраектория стрелы и крупные мишени, мягче бокс"
                : "Честная физика без подсказок";
        }
    }
}
