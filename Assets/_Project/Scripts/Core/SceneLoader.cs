using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRSports.Core
{
    /// <summary>
    /// Переход между сценами: затемнение → асинхронная загрузка → проявление.
    /// Асинхронная загрузка (LoadSceneAsync) не «замораживает» кадр, что в VR критично:
    /// зависшая картинка при поворотах головы сразу вызывает дискомфорт.
    /// </summary>
    public static class SceneLoader
    {
        public const string HubScene = "Hub";

        private static bool _isLoading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _isLoading = false;
            // Корутина живёт на камере СТАРОЙ сцены и уничтожается вместе с ней,
            // поэтому флаг сбрасываем по событию «новая сцена загружена».
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _isLoading = false;

        public static void Load(string sceneName)
        {
            if (_isLoading) return;

            var fader = ScreenFader.Instance;
            if (fader == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            fader.StartCoroutine(LoadRoutine(fader, sceneName));
        }

        public static void LoadHub() => Load(HubScene);

        private static IEnumerator LoadRoutine(ScreenFader fader, string sceneName)
        {
            _isLoading = true;
            yield return fader.FadeTo(1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null) _isLoading = false; // сцены нет в Build Settings
            // Проявление новой сцены делает ScreenFader её собственной камеры (в Start).
        }
    }
}
