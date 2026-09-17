using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 扇形に液体弾を発射する遠距離攻撃ステート。
    /// 予備動作後、liquidShotCount 本を liquidFanAngle の扇形に均等配置して射出する。
    /// 着弾時に MidBossDamageArea が生成され、一定時間ダメージを与える（ひるみなし）。
    ///
    /// ※予備動作・液体吐きだしのアニメーション導入時は
    ///   TODO コメント箇所に Animator.SetTrigger を追加する。
    /// </summary>
    public class MidBossLiquidAttackState : MidBossBaseState
    {
        private enum Phase
        {
            Windup,
            Fire,
            Done,
        }

        private Phase _phase;
        private float _phaseTimer;

        public MidBossLiquidAttackState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] 遠距離攻撃2（液体）ステート開始");
            _phase = Phase.Windup;
            _phaseTimer = 0f;

            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.isStopped = true;
            }

            // TODO: アニメーション導入時はここで Animator.SetTrigger("LiquidWindup") を追加
        }

        public override void Update()
        {
            _phaseTimer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Windup:
                    // 予備動作中はプレイヤー方向を向く
                    if (con.Player != null)
                    {
                        Vector3 dir = con.Player.transform.position - con.transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            con.transform.rotation = Quaternion.Slerp(
                                con.transform.rotation,
                                Quaternion.LookRotation(dir.normalized),
                                Time.deltaTime * 5f
                            );
                        }
                    }

                    if (_phaseTimer >= con.LiquidWindupDuration)
                    {
                        FireLiquid();
                        _phase = Phase.Fire;
                        _phaseTimer = 0f;
                        // TODO: アニメーション導入時はここで Animator.SetTrigger("LiquidFire") を追加
                    }
                    break;

                case Phase.Fire:
                    // 発射後は少し待ってから終了（エフェクトが見える時間を確保）
                    if (_phaseTimer >= 0.3f)
                    {
                        _phase = Phase.Done;
                    }
                    break;

                case Phase.Done:
                    con.ChangeState(
                        new MidBossWaitState(
                            con,
                            con.WaitAfterAttackDuration,
                            new MidBossStartState(con)
                        )
                    );
                    break;
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
            }
        }

        private float _fallbackTargetDistance = 10f; // プレイヤーがいないときのフォールバック用射出距離
        private float _aimHeightOffset = 0.5f; // プレイヤーを狙う際の、足元からの高さオフセット

        private void FireLiquid()
        {
            Vector3 targetPos =
                con.Player != null
                    ? con.Player.transform.position + Vector3.up * _aimHeightOffset
                    : con.transform.position + con.transform.forward * _fallbackTargetDistance;

            int count = Random.Range(con.LiquidShotMinCount, con.LiquidShotMaxCount + 1);
            float fanAngle = con.LiquidFanAngle;
            // 扇形の左端から右端へ均等配置（count=1 なら正面のみ）
            float startAngle = count > 1 ? -fanAngle * 0.5f : 0f;
            float angleStep = count > 1 ? fanAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * con.transform.forward;
                dir.Normalize();

                Vector3 spawnPos = con.LiquidFirePos.position;

                GameObject obj = Object.Instantiate(
                    con.LiquidPrefab,
                    spawnPos,
                    Quaternion.identity
                );
                var proj = obj.GetComponent<MidBossLiquidProjectile>();
                if (proj == null)
                {
                    proj = obj.AddComponent<MidBossLiquidProjectile>();
                }

                // 各弾の目標は扇形の各方向へ十分遠い地面上の点にする
                // targetPosからの距離ではなく、スポーン位置からdirの方向に伸ばした先を目標にする
                float range = Vector3.Distance(spawnPos, targetPos);
                Vector3 adjustedTarget = spawnPos + dir * range;
                // Y座標はプレイヤーの地面と同じ高さにそろえる（正しい放物線になるよう）
                adjustedTarget.y = targetPos.y;
                proj.Initialize(
                    spawnPos,
                    adjustedTarget,
                    con.LiquidDamage,
                    con.DamageAreaPrefab,
                    con.DamageAreaDuration,
                    con.DamageAreaInterval,
                    con.PoisonDuration,
                    con.PoisonDamagePerTick,
                    con.PoisonTickInterval
                );
            }
        }
    }
}
