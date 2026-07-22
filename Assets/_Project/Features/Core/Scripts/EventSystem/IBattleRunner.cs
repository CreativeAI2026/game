using System.Collections;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 戦闘の入力一式。敵は events.json ではなくシーンの EventTrigger の Enemy スロットに
    /// 配線した Prefab を使い、トリガー位置(または子の SpawnPoint)へ出す
    /// (documents/EventImplementation.md「敵はトリガーに配線」)。
    /// </summary>
    public readonly struct BattleSetup
    {
        public readonly GameObject EnemyPrefab;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public BattleSetup(GameObject enemyPrefab, Vector3 position, Quaternion rotation)
        {
            EnemyPrefab = enemyPrefab;
            Position = position;
            Rotation = rotation;
        }

        /// <summary>敵 Prefab が配線されているか。false ならこの戦闘は警告してスキップする。</summary>
        public bool HasEnemy => EnemyPrefab != null;
    }

    /// <summary>
    /// battle ステップの seam。実体は戦闘班が実装し、EventPlayer に注入する。
    /// 配線された敵 Prefab をトリガー位置に出し、勝利して決着するまで待つ(コルーチン)。敗北時は
    /// 直近セーブから再開(シーン再読込)されるため、このコルーチンは完了しない想定
    /// (戦闘は勝敗を記録しない・documents/Specification.md §4, §6)。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(BattleSetup setup);
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
