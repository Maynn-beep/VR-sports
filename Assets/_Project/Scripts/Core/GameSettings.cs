using System;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Глобальные настройки игры. Сейчас одна — сложность: «Реалистичная» или «Казуальная».
    ///
    /// Казуальная сложность НЕ отключает физику. Она добавляет «помощь игроку» (assist), как в
    /// спортивных симуляторах: бортики в боулинге, доводка броска в баскетболе, траектория-подсказка
    /// в стрельбе из лука, мягче требования в боксе. Все броски по-прежнему летят по законам механики.
    ///
    /// Выбор хранится в PlayerPrefs и переживает перезапуск игры.
    /// Рекорды для двух сложностей ведутся раздельно (RecordKey).
    /// </summary>
    public static class GameSettings
    {
        private const string Key = "difficulty_casual";
        private static bool? _casual;

        public static event Action<bool> DifficultyChanged;

        public static bool Casual
        {
            get
            {
                _casual ??= PlayerPrefs.GetInt(Key, 0) == 1;
                return _casual.Value;
            }
            set
            {
                if (Casual == value) return;
                _casual = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                DifficultyChanged?.Invoke(value);
            }
        }

        public static string DifficultyName => Casual ? "Казуальная" : "Реалистичная";

        /// <summary>Значение параметра для текущей сложности.</summary>
        public static float Pick(float realistic, float casual) => Casual ? casual : realistic;

        /// <summary>Ключ рекорда: у казуальной сложности свой рекорд, чтобы не смешивать результаты.</summary>
        public static string RecordKey(string modeId) => Casual ? modeId + "_casual" : modeId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => _casual = null;
    }
}
