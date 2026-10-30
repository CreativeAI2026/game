using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 手下（MidBossMinionProjectile）を生成してプレイヤーへ向かわせる特殊攻撃。TestEnemyNeedleAttackState ベースで上昇を魚のような揺らぎに変更。
    /// 予備動作アニメ導入時は TODO 箇所に Animator.SetTrigger("Roar") を追加する。
    /// </summary>
    public class MidBossSpecialAttackState : MidBossBaseState
    {
        private float _timer;
        private const float FireInterval = 0.2f;

        public MidBossSpecialAttackState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 特殊攻撃（手下生成）ステート開始");
            _timer = 0f;

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }

            // プレイヤーを向く
            if (con.Player != null)
            {
                Vector3 dir = (con.Player.transform.position - con.transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    con.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            SpawnMinions();

            // TODO: アニメーション導入時はここで Animator.SetTrigger("Roar") を追加
        }

        public override void Update()
        {
            _timer += UnityEngine.Time.deltaTime;

            // 予備演出時間が終わったら待機ステートへ
            if (_timer >= con.SpecialSpawnDuration)
            {
                con.ChangeState(
                    new MidBossWaitState(
                        con,
                        con.WaitAfterAttackDuration,
                        new MidBossStartState(con)
                    )
                );
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }
        }

        private void SpawnMinions()
        {
            if (con.MinionPrefab == null || con.Player == null)
            {
                Debug.LogWarning("[MidBoss] MinionPrefab または Player が未設定です。");
                return;
            }

            int count = con.MinionCount;
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                GameObject obj;
                if (con.MinionPrefab != null)
                {
                    obj = Object.Instantiate(con.MinionPrefab);
                }
                else
                {
                    obj = new GameObject("MidBossMinion");
                }

                var minion = obj.GetComponent<MidBossMinionProjectile>();
                if (minion == null)
                {
                    minion = obj.AddComponent<MidBossMinionProjectile>();
                }

                // TestEnemyと同様に円周状に分散させる（angleStep * i）
                float delay = i * FireInterval;
                minion.Initialize(
                    con.transform,
                    con.Player.transform,
                    angleStep * i,
                    delay,
                    con.MinionDamage
                );
            }
        }
    }
}
