using System.Collections;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// battle ステップの実体(IBattleRunner)。EventTrigger.Enemy に配線された敵 Prefab(events.json ではなくこのスロットで解決)を1体トリガー位置に出し、倒されるまで待って EventPlayer に制御を返す。
    /// 状態もコルーチンも持たないので MonoBehaviour にしない。Title フローが1つ生成し、Core から参照できるよう static seam の BattleRunnerService.Current に登録する。
    /// TODO(戦闘班): カメラ・アリーナ・敗北時は非戦闘時と共通で確定済み(その場戦闘・追従カメラ・直近セーブ再開)。
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
            // このコルーチンは完了しない想定。
            while (!defeated && enemy != null)
                yield return null;

            status.OnDeathTriggered -= OnDeath;
            // 死亡演出後の破棄は EnemyStatus.Die() 側が担う(Destroy(gameObject, 5f))。
        }
    }
}
