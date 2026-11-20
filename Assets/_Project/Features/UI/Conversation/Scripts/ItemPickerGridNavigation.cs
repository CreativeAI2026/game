using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// useItem の持ち物ピッカーの選択位置を、上下左右の入力で動かす計算。
    /// 添字 0..itemCount-1 がグリッドの各マス(左上から行ごと)、itemCount が「やめる」。
    /// </summary>
    internal static class ItemPickerGridNavigation
    {
        /// <summary>
        /// 入力 <paramref name="step"/>(y が正で上)で動いた後の添字を返す。動けない向きなら current のまま。
        /// 縦の入力があるときは横を見ない。行をまたぐ回り込みはしない。
        /// </summary>
        public static int Move(int current, Vector2Int step, int itemCount, int columns)
        {
            int cancel = itemCount;
            if (itemCount <= 0 || columns <= 0)
                return cancel;

            if (current >= cancel)
                return step.y > 0 ? itemCount - 1 : cancel;

            int lastRow = (itemCount - 1) / columns;
            int row = current / columns;
            int column = current % columns;

            if (step.y > 0)
                return row > 0 ? current - columns : current;
            if (step.y < 0)
                return row < lastRow ? Mathf.Min(current + columns, itemCount - 1) : cancel;
            if (step.x > 0)
                return column < columns - 1 && current + 1 < itemCount ? current + 1 : current;
            if (step.x < 0)
                return column > 0 ? current - 1 : current;
            return current;
        }
    }
}
