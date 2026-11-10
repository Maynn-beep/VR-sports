using UnityEngine;

namespace VRSports.Core
{
    /// <summary>Пульт в сцене режима: кнопки «Старт/Заново» и «В хаб».</summary>
    public class ModeControls : MonoBehaviour
    {
        [SerializeField] private SportModeBase mode;
        [SerializeField] private VRButton startButton;
        [SerializeField] private VRButton hubButton;

        public void Setup(SportModeBase m, VRButton start, VRButton hub)
        {
            mode = m; startButton = start; hubButton = hub;
        }

        private void OnEnable()
        {
            if (startButton != null) startButton.Pressed += OnStart;
            if (hubButton != null) hubButton.Pressed += SceneLoader.LoadHub;
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.Pressed -= OnStart;
            if (hubButton != null) hubButton.Pressed -= SceneLoader.LoadHub;
        }

        private void OnStart() => mode.StartGame();
    }
}
