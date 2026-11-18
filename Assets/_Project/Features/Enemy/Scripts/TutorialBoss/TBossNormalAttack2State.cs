using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 腕を鞭のように伸ばす二段攻撃のステート（左から右への払い → 振り下ろして地面に刺し、火花を散らして引きずり戻す）。
    /// 腕の伸びと動きはクリップ側に焼いてあり、進行は Animator に任せる。ここは AnimationEvent を受けて、向きの固定・地面の火花・ヒット時の引き寄せだけを行う。
    ///   OnAttack2ReachExtend: 振りに入る（向きを固定） / OnAttack2ReachRetract: 腕を戻し終えた（向き直りを再開）
    /// 判定・ダメージ・ガードは腕全体を掃く TBossArmSweepHitbox に委譲する。
    /// </summary>
    public class TBossNormalAttack2State : TBossBaseState
    {
        private static readonly int Attack2TriggerHash = Animator.StringToHash("Attack2");

        // Attack2ステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float AttackStateTimeout = 3f;

        // 引き寄せた後に、体が触れる距離からさらに空けておく間隔（m）。0だと引き寄せ直後に押し合いになる
        private const float PullContactMargin = 0.5f;

        private float _attackStateTimer;

        // 振っている間は向きを固定する。振り中も追尾すると回避できなくなるため
        private bool _isAimLocked;

        private bool _isPulling;
        private float _pullTimer;
        private Vector3 _pullStart;
        private Vector3 _pullEnd;
        private bool _movementSuppressed;
        private bool _collisionIgnored;

        private PlayerController _playerController;
        private CharacterController _playerCC;

        private TBossMeleeApproach _approach;
        private bool _isApproaching;

        // 走り込みを終えて振り始めたか。Exit で、登録していないイベントを外さないために持つ
        private bool _isSwinging;

        public TBossNormalAttack2State(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            DevLog.Log("[TutorialBoss] 通常攻撃2ステート開始");

            _attackStateTimer = 0f;
            _isAimLocked = false;
            _isPulling = false;
            _movementSuppressed = false;
            _collisionIgnored = false;
            _isSwinging = false;

            if (boss.Player != null)
            {
                _playerController = boss.Player.GetComponent<PlayerController>();
                _playerCC = boss.Player.GetComponent<CharacterController>();
            }

            // 様子見の間合いの外から選ばれた場合は、振れる間合いまで走り込んでから振る
            _approach = new TBossMeleeApproach(boss, boss.NormalAttack2Range);
            if (boss.Player != null && !_approach.CanStrikeNow())
            {
                _isApproaching = true;
                _approach.Begin();
                return;
            }

            StartSwing();
        }

        /// <summary>立ち止まってプレイヤーへ向き、鎌の連撃を始める。</summary>
        private void StartSwing()
        {
            _isApproaching = false;

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.SetAgentStopped(true);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger(Attack2TriggerHash);
            }

            // プレイヤー方向へ向く
            if (boss.Player != null)
            {
                Vector3 dir = boss.Player.transform.position - boss.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    boss.transform.rotation = Quaternion.LookRotation(dir.normalized);
                }
            }

            boss.ArmSweepHitbox.OnHitLanded += HandlePlayerHitLanded;
            _isSwinging = true;
        }

        public override void Update()
        {
            if (_isApproaching)
            {
                UpdateApproach();
                return;
            }

            UpdateHoming();

            if (_isPulling)
            {
                UpdatePull();
            }

            if (boss.Animator == null)
            {
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName("Attack2"))
            {
                // Attack2トリガーの取りこぼしや遷移の中断で無限待機するのを防ぐ
                _attackStateTimer += Time.deltaTime;
                if (_attackStateTimer >= AttackStateTimeout)
                {
                    Debug.LogWarning(
                        "[TutorialBoss] Attack2ステートに入れませんでした。様子見へ復帰します。"
                    );
                    boss.ChangeState(new TBossWatchState(boss));
                }
                return;
            }

            if (stateInfo.normalizedTime >= 1.0f)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        private void UpdateApproach()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            switch (_approach.Tick(Time.deltaTime))
            {
                case TBossMeleeApproach.Result.Arrived:
                    StartSwing();
                    break;
                case TBossMeleeApproach.Result.Unreachable:
                    // 到達できない位置にいて近づけない場合は、走り続けず様子見で仕切り直す
                    boss.ChangeState(new TBossWatchState(boss));
                    break;
                case TBossMeleeApproach.Result.TimedOut:
                    boss.ChangeState(new TBossChaseState(boss));
                    break;
            }
        }

        public override void Exit()
        {
            if (_isSwinging)
            {
                boss.ArmSweepHitbox.OnHitLanded -= HandlePlayerHitLanded;
            }

            // 怯み・パリィなどで振りの途中に抜けた場合でも、攻撃判定・軌跡・火花を残さない
            boss.StopSwingEffects();
            EndPull();

            if (boss.Agent != null)
            {
                boss.SetAgentStopped(false);
            }
        }

        /// <summary>
        /// AnimationEvent(OnAttack2ReachExtend) から呼ばれる。腕を伸ばして振りに入るので、向きを固定する（振りの回数だけ呼ばれる）。
        /// </summary>
        public void OnReachExtend()
        {
            _isAimLocked = true;
            boss.Sounds.PlayAttack2ReachExtend();
        }

        /// <summary>
        /// AnimationEvent(OnAttack2ReachRetract) から呼ばれる。腕を戻し終えたので、次の振りへ向けて向き直りを再開する。
        /// </summary>
        public void OnReachRetract()
        {
            _isAimLocked = false;
            boss.Sounds.PlayAttack2ReachRetract();
        }

        /// <summary>
        /// 振りに入る前だけプレイヤーへ向き直る。2振り目は縦振りで Enter 時の向きだけでは当たらない一方、振り中も追尾すると回避不能になるため、
        /// 腕を伸ばし始めた時点で向きを固定し、戻した後に再び向き直る。
        /// </summary>
        private void UpdateHoming()
        {
            if (boss.Player == null || boss.Attack2HomingRotateSpeed <= 0f)
            {
                return;
            }

            if (_isAimLocked)
            {
                return;
            }

            Vector3 dirToPlayer = boss.Player.transform.position - boss.transform.position;
            dirToPlayer.y = 0f;
            if (dirToPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(dirToPlayer.normalized),
                Time.deltaTime * boss.Attack2HomingRotateSpeed
            );
        }

        /// <summary>
        /// 腕が実際にプレイヤーへ触れた瞬間（TBossArmSweepHitbox.OnHitLanded）に呼ばれる。
        /// ガードされていてもここは発火するため、「防御されても少しだけ引き寄せられる」演出になる。
        /// </summary>
        private void HandlePlayerHitLanded(Vector3 hitPoint)
        {
            BeginPull();
        }

        /// <summary>
        /// プレイヤーを少しだけボス方向へ引き寄せる。
        /// プレイヤーはRigidbodyを持たないため、入力を一時的に切ってCharacterControllerを直接動かす。
        /// </summary>
        private void BeginPull()
        {
            if (_playerCC == null || !_playerCC.enabled || boss.Player == null)
            {
                return;
            }

            _isPulling = true;
            _pullTimer = 0f;
            _pullStart = boss.Player.transform.position;

            Vector3 toPlayer = _pullStart - boss.transform.position;
            toPlayer.y = 0f;
            float currentDistance = toPlayer.magnitude;

            // 密着・すり抜けを避けるため、体が触れる距離より内側へは詰めない
            float pullAmount = Mathf.Min(
                boss.Attack2PullDistance,
                Mathf.Max(0f, currentDistance - (boss.BodyContactDistance + PullContactMargin))
            );
            Vector3 pullDir =
                currentDistance > 0.0001f ? toPlayer.normalized : boss.transform.forward;
            _pullEnd = _pullStart - pullDir * pullAmount;

            if (_playerController != null)
            {
                _playerController.Locks.Set(this, PlayerLock.Move);
                _movementSuppressed = true;
            }

            // 引き寄せ中にボスと押し合いになるのを防ぐ
            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                boss.IgnorePlayerCollision();
                _collisionIgnored = true;
            }
        }

        private void UpdatePull()
        {
            if (_playerCC == null || !_playerCC.enabled)
            {
                EndPull();
                return;
            }

            _pullTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(_pullTimer / boss.Attack2PullDuration)
            );

            Vector3 target = Vector3.Lerp(_pullStart, _pullEnd, t);
            _playerCC.Move(target - _playerCC.transform.position);

            if (_pullTimer >= boss.Attack2PullDuration)
            {
                EndPull();
            }
        }

        private void EndPull()
        {
            _isPulling = false;

            if (_movementSuppressed && _playerController != null)
            {
                _movementSuppressed = false;

                // 自分が掛けた分だけを外す。怯み・掴みが同時に成立していても、そちらの制限は残る
                _playerController.Locks.Release(this);
            }

            if (_collisionIgnored)
            {
                // 重なったまま衝突を戻すとプレイヤーが押し出されて上へ飛ぶため、離れてから戻す
                boss.RestorePlayerCollisionWhenSeparated();
                _collisionIgnored = false;
            }
        }
    }
}
