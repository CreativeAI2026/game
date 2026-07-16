using System.Collections;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// battle ステップの実体(IBattleRunner)。EventTrigger に配線された敵 Prefab を1体トリガー位置に出して
    /// 倒されるまで待ち、決着したら EventPlayer に制御を返す(spec: 1戦闘につき1体)。敵の解決は
    /// events.json ではなくシーンの EventTrigger.Enemy スロットが担う(documents/EventImplementation.md)。
    ///
    /// 状態を持たず、コルーチンも EventPlayer 側が回すため MonoBehaviour である必要がない
    /// (常駐 GameObject にはしない)。Title フローが1つ生成し BattleRunnerService.Current に登録する。
    /// static seam が参照を保持するのでシーンをまたいでも常に呼べる(Core は Gameplay を参照できないため
    /// EventPlayer はこの seam 経由で受け取る)。
    ///
    /// TODO(戦闘班): カメラ・アリーナ・敗北時は非戦闘時と共通で確定済み
    /// (その場戦闘・追従カメラ・直近セーブ再開。spec 参照)。
    /// </summary>
    public sealed class BattleRunner : IBattleRunner
    {
        public IEnumerator Run(BattleSetup setup)
        {
            if (!setup.HasEnemy)
            {
                Debug.LogWarning("[BattleRunner] 敵 Prefab が未配線のため戦闘をスキップします。");
                yield break;
            }

            var enemy = Object.Instantiate(setup.EnemyPrefab, setup.Position, setup.Rotation);

            var status = enemy.GetComponentInChildren<EnemyStatus>();
            if (status == null)
            {
                Debug.LogWarning(
                    $"[BattleRunner] '{setup.EnemyPrefab.name}' の Prefab に EnemyStatus が無く決着を検知できません。即時終了。"
                );
                yield break;
            }

            bool defeated = false;
            void OnDeath() => defeated = true;
            status.OnDeathTriggered += OnDeath;

            // 倒される(= OnDeathTriggered)まで待つ。敗北時はセーブ再開でシーンごと作り直されるため
            // このコルーチンは完了しない想定(spec §4, §6)。
            while (!defeated && enemy != null)
                yield return null;

            status.OnDeathTriggered -= OnDeath;
            // 死亡演出後の破棄は EnemyStatus.Die() 側が担う(Destroy(gameObject, 5f))。
        }
    }
}
