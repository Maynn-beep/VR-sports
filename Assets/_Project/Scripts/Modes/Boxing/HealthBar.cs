using UnityEngine;

namespace VRSports.Modes.Boxing
{
    /// <summary>
    /// Полоска здоровья из двух кубов: фон и заливка. Заливка масштабируется по X
    /// и сдвигается так, чтобы её левый край оставался на месте.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Transform fill;
        [SerializeField] private float width = 0.6f;

        private float _shown = 1f, _target = 1f;

        public void Setup(Transform f, float w)
        {
            fill = f; width = w;
        }

        public void Set(float value01, bool instant = false)
        {
            _target = Mathf.Clamp01(value01);
            if (instant) { _shown = _target; Apply(); }
        }

        private void Update()
        {
            if (Mathf.Approximately(_shown, _target)) return;
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime * 1.5f);
            Apply();
        }

        private void Apply()
        {
            var s = fill.localScale;
            fill.localScale = new Vector3(Mathf.Max(0.0001f, width * _shown), s.y, s.z);
            fill.localPosition = new Vector3(-width * (1f - _shown) / 2f, 0f, fill.localPosition.z);
            // При нуле прячем заливку целиком: иначе остаётся тонкая полоска, похожая на «1 HP».
            fill.gameObject.SetActive(_shown > 0.002f);
        }
    }
}
