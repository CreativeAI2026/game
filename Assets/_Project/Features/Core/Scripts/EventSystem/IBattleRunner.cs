using System.Collections;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// battle ステップの seam。実体は戦闘班が実装し、EventPlayer に注入する。
    /// 戦闘を実行し、勝利して決着するまで待つ(コルーチン)。敗北時は直近セーブから
    /// 再開(シーン再読込)されるため、このコルーチンは完了しない想定
    /// (戦闘は勝敗を記録しない・documents/Specification.md §4, §6)。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(string enemyKey);
    }

    /// <summary>
    /// 実行時に有効な IBattleRunner を Core 側へ登録する seam。Gameplay の BattleRunner を
    /// Title フローで生成してここに登録し、EventPlayer は Inspector 未配線時のフォールバックとして
    /// ここを見る(ItemGiverService と同じ思想)。BattleRunner は状態を持たない plain class で
    /// シーンから drag 配線できず、かつ Core は Gameplay を参照できないため契約経由で受け取る。
    /// </summary>
    public static class BattleRunnerService
    {
        public static IBattleRunner Current { get; set; }
    }
}
