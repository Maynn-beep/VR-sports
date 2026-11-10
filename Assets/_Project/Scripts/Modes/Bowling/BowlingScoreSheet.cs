using System.Text;
using TMPro;
using UnityEngine;

namespace VRSports.Modes.Bowling
{
    /// <summary>
    /// Табло фреймов над дорожкой, как в настоящем боулинг-клубе:
    ///   | 1 | 2 | 3 | ...
    ///   |X  |9 /|8 1| ...
    ///   | 20| 38| 47| ...
    /// Используется моноширинный режим TextMeshPro (тег mspace), чтобы колонки ровно совпадали.
    /// </summary>
    public class BowlingScoreSheet : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        public void Setup(TMP_Text t) => text = t;

        public void Refresh(BowlingScore score)
        {
            var header = new StringBuilder("|");
            var marks = new StringBuilder("|");
            var totals = new StringBuilder("|");
            int current = score.CurrentFrame;

            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                string num = (f + 1).ToString();
                header.Append(f == current ? $"<color=#FFD54A>{num,3}</color>|" : $"{num,3}|");

                string m = score.FrameMarks(f);
                if (f < BowlingScore.Frames - 1 && m.Length == 2) m = $"{m[0]} {m[1]}"; // «9 /»
                else if (m == "X" && f < BowlingScore.Frames - 1) m = " X ";
                marks.Append($"{m,-3}|");

                int? t = score.FrameTotal(f);
                totals.Append(t.HasValue ? $"{t.Value,3}|" : "   |");
            }

            text.text = $"<mspace=0.62em>{header}\n{marks}\n{totals}</mspace>";
        }
    }
}
