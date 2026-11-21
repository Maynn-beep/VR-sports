using UnityEngine;

namespace VRSports.Core
{
    /// <summary>Уровень соперника-бота. Off — одиночная игра.</summary>
    public enum RivalLevel { Off, Easy, Normal, Hard }

    /// <summary>
    /// Общие инструменты ботов-соперников.
    ///
    /// Главный принцип: БОТ НЕ ЖУЛЬНИЧАЕТ. Он бросает те же шары и стреляет теми же стрелами,
    /// что и игрок, и их полёт считает та же физика. Бот вычисляет «идеальный» бросок
    /// (решает обратную задачу баллистики), а затем портит его случайной ошибкой —
    /// как у живого человека: рука никогда не повторяет движение точно.
    /// Сложность бота = величина этой ошибки (и скорость реакции).
    /// </summary>
    public static class Rival
    {
        public static string Name(RivalLevel level) => level switch
        {
            RivalLevel.Easy => "лёгкий",
            RivalLevel.Normal => "средний",
            RivalLevel.Hard => "сложный",
            _ => "выкл"
        };

        /// <summary>Выбор параметра по уровню: Easy / Normal / Hard.</summary>
        public static float Pick(RivalLevel level, float easy, float normal, float hard) => level switch
        {
            RivalLevel.Easy => easy,
            RivalLevel.Hard => hard,
            _ => normal
        };

        /// <summary>
        /// Нормальное (гауссово) распределение со средним 0 и отклонением sigma.
        /// Преобразование Бокса–Мюллера: из двух равномерных чисел u1, u2 ∈ (0,1]
        ///   z = √(−2·ln u1) · cos(2π·u2)  — стандартная нормальная величина.
        /// Ошибки человека (разброс попаданий) хорошо описываются именно нормальным законом:
        /// мелкие отклонения часты, крупные — редки (68% попаданий в пределах ±σ).
        /// </summary>
        public static float Gaussian(float sigma)
        {
            float u1 = 1f - Random.value; // (0, 1] — чтобы не взять ln(0)
            float u2 = Random.value;
            return sigma * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        /// <summary>Повернуть направление на случайные малые углы (рысканье и тангаж), градусы σ.</summary>
        public static Vector3 Scatter(Vector3 dir, float sigmaDeg)
        {
            var q = Quaternion.LookRotation(dir);
            return q * Quaternion.Euler(Gaussian(sigmaDeg), Gaussian(sigmaDeg), 0f) * Vector3.forward * dir.magnitude;
        }
    }
}
