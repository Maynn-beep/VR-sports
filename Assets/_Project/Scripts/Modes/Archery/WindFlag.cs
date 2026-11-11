using TMPro;
using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Флаг-индикатор ветра: поворачивается по ветру, «поднимается» тем выше, чем ветер сильнее,
    /// и колышется. Стрелок по нему делает поправку на снос — как в настоящей стрельбе.
    /// </summary>
    public class WindFlag : MonoBehaviour
    {
        [SerializeField] private Transform flag;   // поворотная часть (ось Z — вдоль полотнища)
        [SerializeField] private TMP_Text label;
        [SerializeField] private float maxWind = 6f;

        public void Setup(Transform f, TMP_Text l)
        {
            flag = f; label = l;
        }

        private void Update()
        {
            Vector3 w = ArcheryWind.Current;
            float speed = w.magnitude;
            float t = Mathf.Clamp01(speed / maxWind);

            // Направление: полотнище развевается ПО ветру. Наклон: от «висит» (−80°) до «горизонтально».
            float yaw = speed > 0.05f ? Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg : 0f;
            float droop = Mathf.Lerp(80f, 5f, t) + Mathf.Sin(Time.time * (4f + 6f * t)) * 4f;
            flag.rotation = Quaternion.Euler(droop, yaw, 0f);

            if (label != null)
            {
                string dir = speed < 0.05f ? "" : w.x > 0.05f ? " →" : w.x < -0.05f ? " ←" : "";
                label.text = speed < 0.05f ? "Ветра нет" : $"Ветер {speed:F1} м/с{dir}";
            }
        }
    }
}
