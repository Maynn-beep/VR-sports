using UnityEngine;

namespace VRSports.Modes.Archery
{
    /// <summary>
    /// Текущий ветер на стрельбище (м/с, горизонтальный вектор).
    /// Его читает каждая летящая стрела, а показывает флаг WindFlag.
    /// </summary>
    public static class ArcheryWind
    {
        public static Vector3 Current { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Current = Vector3.zero;
    }
}
