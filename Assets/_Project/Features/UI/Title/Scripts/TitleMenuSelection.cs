using System.Collections.Generic;

namespace CreativeAI.UI
{
    /// <summary>
    /// タイトルの項目の選び方(押せない項目を飛ばして上下に動かす)。画面や入力から切り離した計算だけを持つ。
    /// </summary>
    public static class TitleMenuSelection
    {
        /// <summary>
        /// <paramref name="current"/> から <paramref name="step"/>(+1 で下、-1 で上)の向きに、押せる項目へ動かした番号を返す。
        /// 端を越えたら反対側へ回る。他に押せる項目が無ければ <paramref name="current"/> のまま。
        /// 押せる項目が1つも無ければ -1。
        /// </summary>
        public static int Move(IReadOnlyList<bool> interactable, int current, int step)
        {
            int count = interactable.Count;
            for (int i = 1; i <= count; i++)
            {
                int candidate = ((current + step * i) % count + count) % count;
                if (interactable[candidate])
                    return candidate;
            }
            return -1;
        }
    }
}
