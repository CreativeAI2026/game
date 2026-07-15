using System.Collections;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// battle ステップの実体(IBattleRunner)。enemyKey で EnemyDB を引き、その Prefab を1体出して
    /// 倒されるまで待ち、決着したら EventPlayer に制御を返す(spec: 1戦闘につき1体)。
    ///
    /// 状態を持たず、コルーチンも EventPlayer 側が回すため MonoBehaviour である必要がない
    /// (常駐 GameObject にはしない)。Title フローが1つ生成し BattleRunnerService.Current に登録する。
    /// static seam が参照を保持するのでシーンをまたいでも常に呼べる(Core は Gameplay を参照できないため
    /// EventPlayer はこの seam 経由で受け取る)。
    ///
    /// TODO(戦闘班): 出現位置は暫定(現状プレイヤー前方)。カメラ・アリーナ・敗北時は非戦闘時と共通で
    /// 確定済み(その場戦闘・追従カメラ・直近セーブ再開。spec 参照)。
    /// </summary>
    public sealed class BattleRunner : IBattleRunner
    {
        public IEnumerator Run(string enemyKey)
        {
            var db = EnemyDB.Instance;
            if (db == null || !db.TryGet(enemyKey, out var prefab))
            {
                Debug.LogWarning(
                    $"[BattleRunner] enemyKey '{enemyKey}' に対応する敵 Prefab が EnemyDB に見つかりません。戦闘をスキップ。"
                );
                yield break;
            }

            var enemy = Object.Instantiate(prefab, SpawnPosition(), Quaternion.identity);

            var status = enemy.GetComponentInChildren<EnemyStatus>();
            if (status == null)
            {
                Debug.LogWarning(
                    $"[BattleRunner] '{enemyKey}' の Prefab に EnemyStatus が無く決着を検知できません。即時終了。"
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

        // 暫定: プレイヤーの少し前方に出す。プレイヤー未検出なら原点。
        private static Vector3 SpawnPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                return Vector3.zero;
            var t = player.transform;
            return t.position + t.forward * 3f;
        }
    }
}
