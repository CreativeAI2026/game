using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 振り返りで見せる場面を選ぶ。答える人の負担を抑えるため数を絞り、
    /// 焦り度の高い場面を優先しつつ、系統（逃げ腰・硬直など）が偏らないよう順番に1つずつ選ぶ。
    /// 見せる順は起きた順にする（遊んだ流れに沿っている方が思い出しやすいため）。
    /// </summary>
    public static class PanicReviewSelection
    {
        public static List<PanicMoment> Select(IReadOnlyList<PanicMoment> moments, int maxCount)
        {
            List<Queue<PanicMoment>> byCategory = moments
                .GroupBy(m => m.Category)
                .Select(g => new Queue<PanicMoment>(g.OrderByDescending(m => m.PanicScore)))
                .OrderByDescending(q => q.Peek().PanicScore)
                .ToList();

            var selected = new List<PanicMoment>();
            while (selected.Count < maxCount && byCategory.Any(q => q.Count > 0))
            {
                foreach (Queue<PanicMoment> queue in byCategory)
                {
                    if (selected.Count >= maxCount)
                        break;
                    if (queue.Count > 0)
                        selected.Add(queue.Dequeue());
                }
            }

            return selected.OrderBy(m => m.Index).ToList();
        }
    }
}
