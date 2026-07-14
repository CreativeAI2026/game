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
}
