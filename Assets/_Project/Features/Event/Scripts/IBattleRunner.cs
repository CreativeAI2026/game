using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
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
    /// battle ステップの実行役。敵 Prefab をトリガー位置に出し、勝利まで待つコルーチン。
    /// 実体は BattleRunner で、プレビュー・テストは差し替える。敗北時はシーン再読込で再開されるので完了しない想定。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(BattleSetup setup);
    }
}
