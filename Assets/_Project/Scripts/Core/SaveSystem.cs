using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VRSports.Core
{
    /// <summary>
    /// Сохранение рекордов между запусками игры.
    /// Данные хранятся в JSON-файле в Application.persistentDataPath
    /// (на Quest это внутренняя память приложения, на ПК — AppData/LocalLow).
    /// </summary>
    public static class SaveSystem
    {
        [Serializable]
        private class RecordEntry
        {
            public string modeId;
            public int best;
        }

        [Serializable]
        private class SaveData
        {
            // JsonUtility не умеет сериализовать Dictionary, поэтому храним список пар.
            public List<RecordEntry> records = new List<RecordEntry>();
        }

        private const string FileName = "vrsports_save.json";
        private static SaveData _data;

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private static SaveData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        private static void Load()
        {
            try
            {
                _data = File.Exists(FilePath)
                    ? JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath))
                    : new SaveData();
            }
            catch (Exception e)
            {
                // Повреждённый файл не должен ломать игру — начинаем с чистых рекордов.
                Debug.LogWarning($"[SaveSystem] Не удалось прочитать сохранение: {e.Message}");
                _data = new SaveData();
            }
            if (_data == null) _data = new SaveData();
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Не удалось записать сохранение: {e.Message}");
            }
        }

        /// <summary>Есть ли рекорд для режима, и какой.</summary>
        public static bool TryGetBest(string modeId, out int best)
        {
            var entry = Data.records.Find(r => r.modeId == modeId);
            best = entry?.best ?? 0;
            return entry != null;
        }

        /// <summary>
        /// Отправить результат. Возвращает true, если это новый рекорд.
        /// higherIsBetter = false для режимов, где меньше — лучше (мини-гольф: число ударов).
        /// </summary>
        public static bool SubmitScore(string modeId, int score, bool higherIsBetter = true)
        {
            var entry = Data.records.Find(r => r.modeId == modeId);
            if (entry == null)
            {
                Data.records.Add(new RecordEntry { modeId = modeId, best = score });
                Save();
                return true;
            }

            bool isRecord = higherIsBetter ? score > entry.best : score < entry.best;
            if (isRecord)
            {
                entry.best = score;
                Save();
            }
            return isRecord;
        }
    }
}
