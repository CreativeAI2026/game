using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを向かずランダム方向へ移動する。Enter 時にジグザグ（じりじり＋ブレ角度）か大ステップ（速め＋少回数）を選び、
    /// 移動後は距離に応じて攻撃または追跡へ遷移する。移動アニメ導入時は TODO 箇所に Animator 操作を追加する。
    /// </summary>
    public class MidBossRandomMoveState : MidBossBaseState
    {
        private enum MoveStyle
        {
            Zigzag,
            BigStep,
        }

        private MoveStyle _style;

        // 共通
        private Vector3 _targetPos;
        private float _stepTimer;
        private bool _isMoving;

        // スタイルA（ジグザグ）用
        private float _currentBrakeAngle;
        private int _zigzagRemainingSteps;

        // スタイルB（大ステップ）用
        private int _remainingSteps;

        // NavMeshSamplePosition のサーチ半径
        private const float NavSampleRadius = 3f;

        // ランダム方向候補の試行回数
        private const int DirectionCandidates = 12;

        public MidBossRandomMoveState(MidBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[MidBoss] ランダム移動ステート開始");

            // 移動スタイルをランダムに決定
            _style = Random.value < 0.5f ? MoveStyle.Zigzag : MoveStyle.BigStep;
            Debug.Log($"[MidBoss] ランダム移動スタイル: {_style}");

            _stepTimer = 0f;
            _isMoving = true;
            _currentBrakeAngle = 0f;

            // ランダムな移動距離を決定
            float moveDist = Random.Range(con.RandomMoveMinDist, con.RandomMoveMaxDist);

            // 到達可能なランダム方向を探す
            _targetPos = FindRandomTarget(moveDist);

            if (con.Agent != null)
            {
                con.Agent.isStopped = false;
                con.Agent.speed =
                    _style == MoveStyle.Zigzag ? con.ChaseSpeed : con.RandomMoveStepSpeed;
            }

            if (_style == MoveStyle.BigStep)
            {
                _remainingSteps = con.RandomMoveStepCount;
            }
            else
            {
                // ジグザグはステップ数で管理（最初のブレ角度を決定）
                _zigzagRemainingSteps = con.RandomMoveStepCount + Random.Range(0, 3);
                _currentBrakeAngle = Random.Range(
                    -con.RandomMoveZigzagAngle,
                    con.RandomMoveZigzagAngle
                );
            }

            // 最初の目的地をセット
            if (_style == MoveStyle.Zigzag)
                SetNextDestinationZigzag();
            else
                SetNextDestination();

            // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", true) を追加
        }

        public override void Update()
        {
            if (con.Agent == null)
                return;

            // 目的地到達判定
            bool arrived =
                !con.Agent.pathPending
                && con.Agent.remainingDistance < con.Agent.stoppingDistance + 0.1f;

            _stepTimer += UnityEngine.Time.deltaTime;

            switch (_style)
            {
                case MoveStyle.Zigzag:
                    UpdateZigzag(arrived);
                    break;
                case MoveStyle.BigStep:
                    UpdateBigStep(arrived);
                    break;
            }
        }

        public override void Exit()
        {
            if (con.Agent != null)
            {
                con.Agent.ResetPath();
                con.Agent.velocity = Vector3.zero;
                con.Agent.isStopped = true;
            }
            // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", false) を追加
        }

        // ────────────────────────────────────────────
        //  スタイルA: ジグザグ移動
        // ────────────────────────────────────────────

        private void UpdateZigzag(bool arrived)
        {
            if (_isMoving)
            {
                if (arrived || _stepTimer >= con.ChaseStepMoveDuration)
                {
                    _zigzagRemainingSteps--;

                    if (_zigzagRemainingSteps <= 0 || arrived)
                    {
                        // 全ステップ完了 or 目的地到達
                        TransitionByDistance();
                        return;
                    }

                    // 停止フェーズへ
                    _stepTimer = 0f;
                    _isMoving = false;
                    con.Agent.ResetPath();
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", false) を追加
                }
            }
            else
            {
                if (_stepTimer >= con.ChaseStepStopDuration)
                {
                    // 次のブレ角度を決定して移動再開
                    _currentBrakeAngle = Random.Range(
                        -con.RandomMoveZigzagAngle,
                        con.RandomMoveZigzagAngle
                    );
                    _stepTimer = 0f;
                    _isMoving = true;
                    con.Agent.isStopped = false;
                    SetNextDestinationZigzag();
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", true) を追加
                }
            }
        }

        // ────────────────────────────────────────────
        //  スタイルB: 大ステップ移動
        // ────────────────────────────────────────────

        private void UpdateBigStep(bool arrived)
        {
            if (_isMoving)
            {
                if (arrived || _stepTimer >= con.RandomMoveStepMoveDuration)
                {
                    _remainingSteps--;

                    if (_remainingSteps <= 0 || arrived)
                    {
                        // 全ステップ完了
                        TransitionByDistance();
                        return;
                    }

                    // 停止フェーズへ
                    _stepTimer = 0f;
                    _isMoving = false;
                    con.Agent.ResetPath();
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", false) を追加
                }
            }
            else
            {
                if (_stepTimer >= con.RandomMoveStepStopDuration)
                {
                    _stepTimer = 0f;
                    _isMoving = true;
                    SetNextDestination();
                    // TODO: アニメーション導入時はここで Animator.SetBool("IsWalking", true) を追加
                }
            }
        }

        // ────────────────────────────────────────────
        //  ヘルパー
        // ────────────────────────────────────────────

        /// <summary>
        /// ジグザグ移動用の次の目的地をセット（ブレ角度を加味）。
        /// </summary>
        private void SetNextDestinationZigzag()
        {
            if (con.Agent == null)
                return;

            Vector3 dir = (_targetPos - con.transform.position);
            if (dir.sqrMagnitude < 0.01f)
                return;
            dir.y = 0f;
            dir.Normalize();

            // ブレ角度を加えた方向で少し先を目標にする
            Vector3 zigzagDir = Quaternion.Euler(0f, _currentBrakeAngle, 0f) * dir;
            Vector3 stepTarget = con.transform.position + zigzagDir * 2f;

            con.Agent.SetDestination(stepTarget);
        }

        /// <summary>
        /// 大ステップ移動用の次の目的地をセット（targetPosへ直接向かう）。
        /// </summary>
        private void SetNextDestination()
        {
            if (con.Agent == null)
                return;
            con.Agent.SetDestination(_targetPos);
        }

        /// <summary>
        /// 障害物を避けられるランダム方向を探し、目標地点を返す。
        /// 候補が見つからない場合はプレイヤー方向へのフォールバック。
        /// </summary>
        private Vector3 FindRandomTarget(float moveDist)
        {
            float angleStep = 360f / DirectionCandidates;
            float startAngle = Random.Range(0f, 360f);

            for (int i = 0; i < DirectionCandidates; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = con.transform.position + dir * moveDist;

                // NavMesh上に存在するかチェック
                if (
                    !NavMesh.SamplePosition(
                        candidate,
                        out NavMeshHit navHit,
                        NavSampleRadius,
                        NavMesh.AllAreas
                    )
                )
                {
                    continue;
                }

                // 途中に障害物がないかチェック
                Vector3 origin = con.transform.position + Vector3.up * 0.5f;
                if (Physics.SphereCast(origin, 0.3f, dir, out _, moveDist, con.ObstacleLayer))
                {
                    continue;
                }

                return navHit.position;
            }

            // フォールバック：プレイヤーと逆方向に逃げる
            if (con.Player != null)
            {
                Vector3 awayDir = (
                    con.transform.position - con.Player.transform.position
                ).normalized;
                Vector3 fallback = con.transform.position + awayDir * moveDist;
                if (
                    NavMesh.SamplePosition(
                        fallback,
                        out NavMeshHit fbHit,
                        NavSampleRadius,
                        NavMesh.AllAreas
                    )
                )
                {
                    return fbHit.position;
                }
            }

            return con.transform.position;
        }

        /// <summary>
        /// 移動完了後に距離に応じたステートへ遷移する。
        /// </summary>
        private void TransitionByDistance()
        {
            float dist = con.DistanceToPlayer();

            if (dist <= con.AttackRange)
            {
                // 近接範囲内 → 通常攻撃 or 特殊攻撃
                con.TransitionToMeleeAttack();
            }
            else if (dist <= con.RangedAttackRange)
            {
                // 遠距離範囲内 → 遠距離攻撃
                con.TransitionToRangedAttack();
            }
            else
            {
                // 全範囲外 → 追跡
                con.ChangeState(new MidBossChaseState(con));
            }
        }
    }
}
