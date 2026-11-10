using TMPro;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Станция в хабе: табличка с названием, описанием и рекордом + кнопка «Играть».
    /// Данные берутся из SportCatalog по modeId.
    /// </summary>
    public class HubStation : MonoBehaviour
    {
        [SerializeField] private string modeId;
        [SerializeField] private VRButton playButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text bestText;

        private string _scene;

        public void Setup(string id, VRButton play, TMP_Text title, TMP_Text desc, TMP_Text best)
        {
            modeId = id; playButton = play; titleText = title; descriptionText = desc; bestText = best;
        }

        private void Start()
        {
            foreach (var e in SportCatalog.All)
            {
                if (e.Id != modeId) continue;
                _scene = e.Scene;
                titleText.text = e.Title;
                descriptionText.text = e.Description;
            }
            bestText.text = SaveSystem.TryGetBest(modeId, out int best) ? $"Рекорд: {best}" : "Рекорда пока нет";
        }

        private void OnEnable()
        {
            if (playButton != null) playButton.Pressed += Play;
        }

        private void OnDisable()
        {
            if (playButton != null) playButton.Pressed -= Play;
        }

        private void Play()
        {
            if (!string.IsNullOrEmpty(_scene)) SceneLoader.Load(_scene);
        }
    }
}
