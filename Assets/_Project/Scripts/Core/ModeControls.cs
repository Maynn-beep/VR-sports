using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Пульт в сцене режима: кнопки «Старт/Заново», «Соперник» и «В хаб».
    /// Кнопка «Соперник» перебирает уровни: выкл → лёгкий → средний → сложный → выкл.
    /// </summary>
    public class ModeControls : MonoBehaviour
    {
        [SerializeField] private SportModeBase mode;
        [SerializeField] private VRButton startButton;
        [SerializeField] private VRButton hubButton;
        [SerializeField] private VRButton rivalButton;
        [SerializeField] private TMP_Text rivalLabel;

        public void Setup(SportModeBase m, VRButton start, VRButton hub, VRButton rival, TMP_Text label)
        {
            mode = m; startButton = start; hubButton = hub; rivalButton = rival; rivalLabel = label;
        }

        private void OnEnable()
        {
            if (startButton != null) startButton.Pressed += OnStart;
            if (hubButton != null) hubButton.Pressed += SceneLoader.LoadHub;
            if (rivalButton != null) rivalButton.Pressed += OnRival;
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.Pressed -= OnStart;
            if (hubButton != null) hubButton.Pressed -= SceneLoader.LoadHub;
            if (rivalButton != null) rivalButton.Pressed -= OnRival;
        }

        private void Start()
        {
            if (rivalButton != null) rivalButton.gameObject.SetActive(mode.SupportsRival);
            UpdateLabel();
        }

        private void OnStart() => mode.StartGame();

        private void OnRival()
        {
            var next = (RivalLevel)(((int)mode.Rival + 1) % 4);
            if (!mode.SetRival(next)) mode.ShowMessage("Соперника можно сменить между партиями");
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (rivalLabel != null) rivalLabel.text = $"Соперник\n<size=80%>{Rival.Name(mode.Rival)}</size>";
        }
    }
}
