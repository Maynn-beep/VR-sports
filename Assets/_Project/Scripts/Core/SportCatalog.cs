namespace VRSports.Core
{
    /// <summary>
    /// Список всех режимов сборника — единое место, откуда хаб берёт
    /// названия, описания и имена сцен. Добавить новый вид спорта = добавить строку.
    /// </summary>
    public static class SportCatalog
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Scene;
            public readonly string Description;
            public readonly bool HigherIsBetter;

            public Entry(string id, string title, string scene, string description, bool higherIsBetter = true)
            {
                Id = id;
                Title = title;
                Scene = scene;
                Description = description;
                HigherIsBetter = higherIsBetter;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry("bowling", "Боулинг", "Bowling",
                "10 фреймов. Закрутите шар,\nчтобы он ушёл в хук."),
            new Entry("basketball", "Баскетбол", "Basketball",
                "60 секунд. Серия попаданий\nувеличивает множитель."),
            new Entry("archery", "Стрельба из лука", "Archery",
                "Волны мишеней и ветер.\nНатяните тетиву и отпустите."),
            new Entry("boxing", "Бокс", "Boxing",
                "3 раунда. Бейте по светящимся\nлапам нужной рукой."),
        };
    }
}
