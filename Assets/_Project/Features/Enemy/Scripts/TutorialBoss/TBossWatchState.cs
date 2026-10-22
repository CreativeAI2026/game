using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 発見後に攻撃の機会を伺う様子見ステート（TestEnemy の StrafeState 相当）。交戦全体の時計（TBossCombatTempo）で攻撃時刻が来たら仕掛ける。
    /// 近すぎれば下がり、遠すぎれば詰め、適正距離ではプレイヤーの視野外側へ回り込む（間合い管理）。
    /// プレイヤーが走って離れたら歩いて追わず、追跡（走り）へ切り替える。
    /// </summary>
    public class TBossWatchState : TBossBaseState
    {
        // プレイヤーへ向き直る補間速度
        private const float RotateSpeed = 8f;

        // プレイヤーが弓を構えている間は、より遠くまで「適正距離」とみなして距離を取る（間合いの外側に対する倍率）
        private const float WatchDistanceMaxScaleWhileAiming = 1.3f;

        // 迷い・硬直の相手へ間合いを詰める時、間合いの内側（WatchDistanceMin）の何倍まで近づくか。
        // TBossDemo の値（内側6m）で3.6m。体同士が触れる距離（中心間 約2m）より遠く、通常攻撃の射程（4m）には入る
        private const float CloseInMinDistanceScale = 0.6f;

        // 間合いの外側のこの倍率より離れたら「振り切られた」とみなし、歩きの様子見をやめて走って追う
        private const float BreakAwayDistanceScale = 1.5f;

        // 間合い調整で1回に動く距離と、壁チェックのレイの高さ（どちらもスケール1換算。BodyScaleを掛けて使う）
        private const float SpacingStepDistance = 2f;
        private const float WallCheckHeight = 1f;

        // 捕捉が切れてから追跡へ戻るまでの猶予（秒）。光の縁でのチラつきで様子見⇔追跡を往復しないための幅
        private const float LostSightGraceBeforeChase = 0.5f;

        private float _strafeDirection;
        private PlayerController _playerController;

        public TBossWatchState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 様子見ステート開始");

            // 攻撃中など見失い判定をしていない間の古い値で、入った直後に追跡へ戻らないようにする
            boss.LostSightTimer = 0f;
            boss.BeginCombatTempo();

            _strafeDirection = DecideInitialStrafeDirection();
            _playerController =
                boss.Player != null ? boss.Player.GetComponent<PlayerController>() : null;

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.StrafeSpeed;
                boss.SetAgentStopped(false);
                // NavMeshAgentの自動回転を切ることで、自前でプレイヤー方向を向きながら移動できる
                boss.Agent.updateRotation = false;
            }
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            // 見失い判定（至近距離なら光の外でも捕捉を維持する）
            if (!boss.CheckPlayerTracked())
            {
                boss.LostSightTimer += Time.deltaTime;
                if (boss.LostSightTimer >= LostSightGraceBeforeChase)
                {
                    // 柱や壁の陰に隠れられた。その場で見つめ続けず、追跡へ戻って最後に見た位置まで回り込む。
                    // 見失い時間は引き継ぎ、追跡側で残りの猶予が切れたら捜索へ移る
                    boss.ChangeState(new TBossChaseState(boss, continueLostSightTimer: true));
                    return;
                }
            }
            else
            {
                boss.LostSightTimer = 0f;
            }

            // 交戦の時計で攻撃時刻が来たら仕掛ける（様子見に入り直しても待ち時間はやり直さない）
            if (boss.IsAttackDue)
            {
                boss.TransitionToAttack();
                return;
            }

            // 走って離れたプレイヤーを歩きの間合い管理で追っても追いつけないため、追跡へ切り替えて走る
            float distance = Vector3.Distance(
                boss.transform.position,
                boss.Player.transform.position
            );
            if (distance > boss.WatchDistanceMax * BreakAwayDistanceScale)
            {
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            // プレイヤーとの間合いを管理しながら移動
            ManageSpacing();
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 様子見ステート終了");

            if (boss.Agent != null)
            {
                if (boss.Agent.isOnNavMesh)
                {
                    boss.Agent.ResetPath();
                }

                // NavMeshから外れた瞬間に抜けても以降のステートで回転が死なないよう、無条件で戻す
                boss.Agent.updateRotation = true;
            }
        }

        /// <summary>
        /// 開始時点でのボスの位置がプレイヤーの正面寄りなら、より外側（視野の端・背後）へ
        /// 回り込む方向を初期ストレイフ方向として選ぶ。プレイヤーの視野外に立つ方が
        /// 「回り込まれている」緊張感を出せるため、単純なランダムより優先する。
        /// </summary>
        private float DecideInitialStrafeDirection()
        {
            if (boss.Player == null)
            {
                return Random.value > 0.5f ? 1f : -1f;
            }

            Vector3 toBossFromPlayer = boss.transform.position - boss.Player.transform.position;
            toBossFromPlayer.y = 0f;
            if (toBossFromPlayer.sqrMagnitude < 0.0001f)
            {
                return Random.value > 0.5f ? 1f : -1f;
            }

            float angle = Vector3.SignedAngle(
                boss.Player.transform.forward,
                toBossFromPlayer.normalized,
                Vector3.up
            );
            return angle >= 0f ? 1f : -1f;
        }

        private void ManageSpacing()
        {
            if (boss.Agent == null || boss.Player == null)
            {
                return;
            }

            // 水平成分だけを見る。y成分を残すとLookRotationでボスが前後に傾いてしまう
            Vector3 dirToPlayer = boss.Player.transform.position - boss.transform.position;
            dirToPlayer.y = 0f;
            float distance = dirToPlayer.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }
            dirToPlayer.Normalize();

            bool playerAiming = _playerController != null && _playerController.IsAiming;

            float tooCloseDistance;
            float tooFarDistance;
            if (boss.PanicPressure.ClosesIn)
            {
                // 迷い・硬直で焦っている相手には、弓を構えられても下がらず、間合いの内側よりさらに詰めて考える時間を与えない
                tooCloseDistance = boss.WatchDistanceMin * CloseInMinDistanceScale;
                tooFarDistance = boss.WatchDistanceMin;
            }
            else
            {
                tooCloseDistance = boss.WatchDistanceMin;
                tooFarDistance =
                    boss.WatchDistanceMax * (playerAiming ? WatchDistanceMaxScaleWhileAiming : 1f);
            }

            // 1回の移動目標までの距離と壁チェック。スケール1の体格で調整した値なので体格に合わせる
            float step = SpacingStepDistance * boss.BodyScale;
            Vector3 targetPos;

            if (distance < tooCloseDistance)
            {
                // 近すぎるので後退
                targetPos = boss.transform.position - dirToPlayer * step;
            }
            else if (distance > tooFarDistance)
            {
                // 遠すぎるので詰める
                targetPos = boss.transform.position + dirToPlayer * step;
            }
            else
            {
                // 適正距離。プレイヤーの視野外側へ回り込むようにストレイフする
                Vector3 strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;

                // 壁、またはNavMeshの端（段差・穴）があれば方向反転。端を見ないと端に押し付けたまま足踏みする
                Vector3 strafeTarget = boss.transform.position + strafeDir * step;
                if (IsStrafeBlocked(strafeDir, step, strafeTarget))
                {
                    _strafeDirection *= -1f;
                    strafeDir = Vector3.Cross(Vector3.up, dirToPlayer) * _strafeDirection;
                    strafeTarget = boss.transform.position + strafeDir * step;
                }

                targetPos = strafeTarget;
            }

            boss.Agent.SetDestination(targetPos);

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(dirToPlayer),
                Time.deltaTime * RotateSpeed
            );
        }

        private bool IsStrafeBlocked(Vector3 strafeDir, float step, Vector3 strafeTarget)
        {
            Vector3 rayStart =
                boss.transform.position + Vector3.up * (WallCheckHeight * boss.BodyScale);
            if (
                Physics.Raycast(
                    rayStart,
                    strafeDir,
                    step,
                    boss.ObstacleLayer,
                    QueryTriggerInteraction.Ignore
                )
            )
            {
                return true;
            }

            // NavMesh.Raycast は途中でNavMeshの端に当たると true を返す
            return NavMesh.Raycast(
                boss.transform.position,
                strafeTarget,
                out NavMeshHit _,
                NavMesh.AllAreas
            );
        }
    }
}
