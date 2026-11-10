using System.Collections.Generic;
using System.Text;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Подсчёт очков в боулинге по официальным правилам. Чистая логика без Unity:
    /// получает только «сколько кеглей сбито» за каждый бросок.
    ///
    /// Правила:
    ///  • 10 фреймов, в каждом до 2 бросков.
    ///  • Страйк (все 10 первым броском): 10 + очки двух следующих бросков.
    ///  • Спэр (все 10 за два броска): 10 + очки одного следующего броска.
    ///  • Открытый фрейм: просто сумма двух бросков.
    ///  • В 10-м фрейме за страйк или спэр даются дополнительные броски (всего до 3).
    ///  • Максимум — 300 (12 страйков подряд).
    /// </summary>
    public class BowlingScore
    {
        public const int Frames = 10;
        private readonly List<int> _rolls = new List<int>();

        public IReadOnlyList<int> Rolls => _rolls;

        public void Reset() => _rolls.Clear();

        public void Roll(int pins)
        {
            if (IsGameOver) return;
            _rolls.Add(pins);
        }

        /// <summary>Индекс первого броска каждого начатого фрейма.</summary>
        private List<int> FrameStarts()
        {
            var starts = new List<int>();
            int i = 0;
            for (int f = 0; f < Frames && i < _rolls.Count; f++)
            {
                starts.Add(i);
                i += _rolls[i] == 10 ? 1 : 2; // страйк занимает один бросок
            }
            return starts;
        }

        /// <summary>Номер текущего фрейма (0..9). Если игра окончена — 10.</summary>
        public int CurrentFrame
        {
            get
            {
                int i = 0;
                for (int f = 0; f < Frames - 1; f++)
                {
                    if (i >= _rolls.Count) return f;            // фрейм ещё не начат
                    if (_rolls[i] == 10) { i += 1; continue; }  // страйк — фрейм закрыт
                    if (i + 1 >= _rolls.Count) return f;        // ждём второй бросок
                    i += 2;
                }
                return IsTenthComplete(i) ? Frames : Frames - 1;
            }
        }

        public bool IsGameOver => CurrentFrame >= Frames;

        private bool IsTenthComplete(int start)
        {
            int n = _rolls.Count - start;
            if (n < 2) return false;
            if (n >= 3) return true;
            // Два броска: доп. бросок положен только за страйк или спэр.
            return _rolls[start] + _rolls[start + 1] < 10;
        }

        /// <summary>
        /// Накопленный счёт после фрейма f, или null, если он ещё не известен
        /// (например, за страйк не сделаны два следующих броска).
        /// </summary>
        public int? FrameTotal(int f)
        {
            var starts = FrameStarts();
            int total = 0;
            for (int k = 0; k <= f; k++)
            {
                if (k >= starts.Count) return null;
                int? s = FrameScore(starts[k]);
                if (s == null) return null;
                total += s.Value;
            }
            return total;
        }

        private int? FrameScore(int i)
        {
            int Get(int idx) => _rolls[idx];
            bool Has(int idx) => idx < _rolls.Count;

            if (Get(i) == 10)                                  // страйк
                return Has(i + 2) ? 10 + Get(i + 1) + Get(i + 2) : (int?)null;
            if (!Has(i + 1)) return null;
            if (Get(i) + Get(i + 1) == 10)                     // спэр
                return Has(i + 2) ? 10 + Get(i + 2) : (int?)null;
            return Get(i) + Get(i + 1);                        // открытый фрейм
        }

        /// <summary>Счёт по последнему завершённому фрейму.</summary>
        public int Total
        {
            get
            {
                int best = 0;
                for (int f = 0; f < Frames; f++)
                {
                    int? t = FrameTotal(f);
                    if (t == null) break;
                    best = t.Value;
                }
                return best;
            }
        }

        /// <summary>
        /// Отметки фрейма, как на табло боулинга: X — страйк, / — спэр, − — промах.
        /// </summary>
        public string FrameMarks(int f)
        {
            var starts = FrameStarts();
            if (f >= starts.Count) return "";
            int start = starts[f];
            int end = f < Frames - 1
                ? (f + 1 < starts.Count ? starts[f + 1] : _rolls.Count)
                : _rolls.Count;
            if (f < Frames - 1 && end - start > 2) end = start + 2;

            var sb = new StringBuilder();
            int standing = 10; // сколько кеглей стоит перед броском
            for (int i = start; i < end; i++)
            {
                int r = _rolls[i];
                if (standing == 10 && r == 10) { sb.Append('X'); standing = 10; }
                else if (r == standing) { sb.Append('/'); standing = 10; }
                else
                {
                    sb.Append(r == 0 ? '-' : (char)('0' + r));
                    standing -= r;
                }
            }
            return sb.ToString();
        }
    }
}
