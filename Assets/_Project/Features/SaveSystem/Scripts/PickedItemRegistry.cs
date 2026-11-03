using System.Collections.Generic;
using System.Linq;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドで拾い済みのアイテム(<see cref="FieldItemPickup.PickupId"/>)の記録。
    /// セッション中はここに溜め、SaveService が保存・復元する。拾い済みのアイテムはフィールドに入ったとき出さない。
    /// 「続きから」「死亡後の再開」はセーブ時点の記録に戻し、「はじめから」は空にする。
    /// </summary>
    public static class PickedItemRegistry
    {
        private static readonly HashSet<string> Ids = new();

        public static bool IsPicked(string id) => !string.IsNullOrEmpty(id) && Ids.Contains(id);

        /// <summary>拾ったことを記録する。識別子が空(未採番)なら何もしない。</summary>
        public static void MarkPicked(string id)
        {
            if (!string.IsNullOrEmpty(id))
                Ids.Add(id);
        }

        /// <summary>セーブ用の写し。並びを固定して差分を安定させる。</summary>
        public static List<string> Snapshot() =>
            Ids.OrderBy(id => id, System.StringComparer.Ordinal).ToList();

        /// <summary>記録をまるごと差し替える(null は空と同じ)。</summary>
        public static void Restore(IEnumerable<string> ids)
        {
            Ids.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
                MarkPicked(id);
        }

        public static void Clear() => Ids.Clear();
    }
}
