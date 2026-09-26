using System.Collections;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 戦闘の入力一式。敵は events.json ではなくシーンの EventTrigger の Enemy スロットに
    /// 配線した Prefab を使い、トリガー位置(または子の SpawnPoint)へ出す。
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
    /// battle ステップの seam(実体は戦闘班)。敵 Prefab をトリガー位置に出し、勝利まで待つコルーチン。
    /// 敗北時はシーン再読込で再開されるので完了しない想定。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(BattleSetup setup);
    }

    /// <summary>
    /// 実行時の IBattleRunner を登録する seam。Core は Gameplay を参照できず drag 配線もできないため、
    /// Title フローで登録し、EventPlayer は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class BattleRunnerService
    {
        public static IBattleRunner Current { get; set; }
    }
}
