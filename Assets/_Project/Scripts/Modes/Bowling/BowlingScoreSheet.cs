using System.Text;
using TMPro;
using UnityEngine;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Табло фреймов над дорожкой, как в настоящем боулинг-клубе:
    ///   |  1|  2|  3| ...
    ///   | X |9 /|8 1| ...
    ///   | 20| 38| 47| ...
    /// С соперником — две пары строк: «Вы» и «Бот».
    /// Используется моноширинный режим TextMeshPro (тег mspace), чтобы колонки ровно совпадали.
    /// </summary>
    public class BowlingScoreSheet : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        public void Setup(TMP_Text t) => text = t;

        /// <param name="rival">Счёт бота; null — одиночная игра.</param>
        /// <param name="rivalTurn">Сейчас бросает бот — его номер фрейма подсвечивается.</param>
        public void Refresh(BowlingScore score, BowlingScore rival = null, bool rivalTurn = false)
        {
            bool duel = rival != null;
            int current = rivalTurn && duel ? rival.CurrentFrame : score.CurrentFrame;
            var header = new StringBuilder(duel ? "   |" : "|");
            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                string num = (f + 1).ToString();
                header.Append(f == current ? $"<color=#FFD54A>{num,3}</color>|" : $"{num,3}|");
            }

            var sb = new StringBuilder(header.ToString());
            AppendRows(sb, score, duel ? (rivalTurn ? "Вы " : "<color=#7CFF8A>Вы </color>") : "");
            if (duel) AppendRows(sb, rival, rivalTurn ? "<color=#FF9A4D>Бот</color>" : "Бот");
            text.text = $"<mspace=0.62em>{sb}</mspace>";
        }

        private static void AppendRows(StringBuilder sb, BowlingScore score, string prefix)
        {
            var marks = new StringBuilder(prefix + "|");
            var totals = new StringBuilder((prefix.Length > 0 ? "   " : "") + "|");
            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                string m = score.FrameMarks(f);
                if (f < BowlingScore.Frames - 1 && m.Length == 2) m = $"{m[0]} {m[1]}"; // «9 /»
                else if (m == "X" && f < BowlingScore.Frames - 1) m = " X ";
                marks.Append($"{m,-3}|");

                int? t = score.FrameTotal(f);
                totals.Append(t.HasValue ? $"{t.Value,3}|" : "   |");
            }
            sb.Append('\n').Append(marks).Append('\n').Append(totals);
        }
    }
}
