using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 鎌の連撃ステート。進行は Animator クリップに任せ、リーチ伸縮を AnimationEvent で制御する（振りの回数だけ1クリップ内で複数回呼ばれる前提）。
    ///   OnAttack2ReachExtend: 伸ばし始める（振りの直前） / OnAttack2ReachRetract: 戻し始める（振り切った直後） / OnAttack2ReachReset: 即戻す（終端の保険）
    /// 伸長はアニメ自体の伸びに加算される。向き直りはリーチを伸ばしていない間だけ行う（UpdateHoming）。判定・ダメージ・ガードは EnemyMeleeHitbox に委譲し、
    /// ここは OnHitLanded で少し引き寄せる演出のみ。伸縮は SpecialAttackState 同様 AnimationRigging ではなく Transform の直接上書きで行う。
    /// </summary>
    public class TBossNormalAttack2State : TBossBaseState
    {
        // Attack2ステートに入れないまま経過したら、トリガー取りこぼしとみなして復帰する
        private const float AttackStateTimeout = 3f;

        private float _attackStateTimer;

        // リーチ延長中（Animator低速/停止中）かどうか。実時間で管理するためAnimator.speedの影響を受けない。
        private bool _isReachHolding;
        private float _reachHoldTimer;

        // 鎌の伸縮量。Enter時は0、OnReachExtendでAttack2ReachExtensionへ、OnReachRetractで0へ向けて
        // Attack2ReachLerpDurationをかけて滑らかに変化させる。
        private float _currentExtension;
        private float _targetExtension;

        private bool _isPulling;
        private float _pullTimer;
        private Vector3 _pullStart;
        private Vector3 _pullEnd;
        private bool _movementSuppressed;
        private bool _collisionIgnored;

        private PlayerController _playerController;
        private CharacterController _playerCC;

        public TBossNormalAttack2State(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 通常攻撃2ステート開始");

            _attackStateTimer = 0f;
            _isReachHolding = false;
            _reachHoldTimer = 0f;
            _currentExtension = 0f;
            _targetExtension = 0f;
            _isPulling = false;
            _movementSuppressed = false;
            _collisionIgnored = false;

            if (boss.Player != null)
            {
                _playerController = boss.Player.GetComponent<PlayerController>();
                _playerCC = boss.Player.GetComponent<CharacterController>();
            }

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Attack2");
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

            if (boss.MeleeHitbox != null)
            {
                boss.MeleeHitbox.OnHitLanded += HandlePlayerHitLanded;
            }

            // 前回の攻撃で残ったバネの速度・姿勢を破棄し、開始フレームのガクつきを防ぐ
            boss.KamaChainDriver?.ResetState();
        }

        public override void Update()
        {
            UpdateReachHold();
            UpdateExtensionLerp();
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

            // リーチ延長中はAnimator.speedを落としているため、normalizedTimeもその分だけ遅く進む
            // （＝停止中は進まない）。追加のタイマー管理をせずクリップの進行だけで完了判定できる。
            if (stateInfo.normalizedTime >= 1.0f)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        public override void LateUpdate()
        {
            ApplyKamaTransform();
        }

        public override void Exit()
        {
            if (boss.MeleeHitbox != null)
            {
                boss.MeleeHitbox.OnHitLanded -= HandlePlayerHitLanded;
            }

            EndPull();

            // 連鎖へ分配した伸長は誰も書き戻さないため、明示的に取り消す
            boss.KamaChainDriver?.RestoreExtension();

            // 中断（怯み等）でリーチが伸びたまま残らないよう、初期姿勢へ戻す
            if (boss.KamaBone != null)
            {
                boss.KamaBone.localPosition = boss.InitialKamaBoneLocalPosition;
                boss.KamaBone.localRotation = boss.InitialKamaBoneLocalRotation;
            }

            // OnAttack2ReachExtendで落としたアニメーション再生速度を必ず戻す
            if (boss.Animator != null)
            {
                boss.Animator.speed = 1f;
            }

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }
        }

        /// <summary>
        /// AnimationEvent(OnAttack2ReachExtend) から呼ばれ、鎌のリーチを伸ばし始める（1クリップ内で何度呼んでもよい）。
        /// Attack2ReachHoldDuration > 0 ならその時間だけアニメを低速/停止して伸びる瞬間を見せる。
        /// </summary>
        public void OnReachExtend()
        {
            _targetExtension = boss.Attack2ReachExtension;

            if (boss.Attack2ReachHoldDuration <= 0f)
            {
                return;
            }

            _isReachHolding = true;
            _reachHoldTimer = 0f;

            if (boss.Animator != null)
            {
                boss.Animator.speed = boss.Attack2ReachHoldAnimatorSpeed;
            }
        }

        /// <summary>
        /// AnimationEvent(OnAttack2ReachRetract)から呼ばれる。鎌のリーチを元の長さへ戻し始める。
        /// Attack2ReachLerpDurationをかけて滑らかに縮む。
        /// OnReachExtendと対で、1クリップ内で何度呼んでもよい。
        /// </summary>
        public void OnReachRetract()
        {
            _targetExtension = 0f;
        }

        /// <summary>
        /// AnimationEvent(OnAttack2ReachReset)から呼ばれる。鎌のリーチを即座に元の長さへ戻す。
        /// 補間を待たないため、クリップ終端で「確実に元の長さに戻す」用途に使う。
        /// </summary>
        public void OnReachReset()
        {
            _targetExtension = 0f;
            _currentExtension = 0f;
        }

        private void UpdateReachHold()
        {
            if (!_isReachHolding)
            {
                return;
            }

            // Animator.speedを落としていてもTime.deltaTime自体は通常通り進むため、実時間で計測できる
            _reachHoldTimer += Time.deltaTime;
            if (_reachHoldTimer >= boss.Attack2ReachHoldDuration)
            {
                _isReachHolding = false;
                if (boss.Animator != null)
                {
                    boss.Animator.speed = 1f;
                }
            }
        }

        /// <summary>
        /// 振りに入る前だけプレイヤーへ向き直る。2振り目は縦振りで Enter 時の向きだけでは当たらない一方、振り中も追尾すると回避不能になるため、
        /// リーチを伸ばし始めた時点で向きを固定し、戻した後に再び向き直る。
        /// </summary>
        private void UpdateHoming()
        {
            if (boss.Player == null || boss.Attack2HomingRotateSpeed <= 0f)
            {
                return;
            }

            // 伸ばし始めたら向きを固定する（_targetExtensionはイベントの瞬間に切り替わる）
            if (_targetExtension > 0f)
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

        private void UpdateExtensionLerp()
        {
            float lerpDuration = Mathf.Max(0.01f, boss.Attack2ReachLerpDuration);
            float maxDelta = Mathf.Max(0.01f, boss.Attack2ReachExtension) / lerpDuration;
            _currentExtension = Mathf.MoveTowards(
                _currentExtension,
                _targetExtension,
                maxDelta * Time.deltaTime
            );
        }

        /// <summary>
        /// 鎌の伸縮・角度補正を LateUpdate の Transform 直接上書きで適用する（SpecialAttackState.ApplyWireTransform と同様。Animation Rigging では詰まったため）。
        /// 1本のボーンでは硬く見えるため、連鎖全体へ分配する TBossLimbChainDriver に委譲する。
        /// </summary>
        private void ApplyKamaTransform()
        {
            ApplyChainFlex();

            // 連鎖が未設定の場合は、鎌ボーン1本だけを伸ばすフォールバック。
            // 棒が生えるような硬い見た目になるため、Inspectorでの連鎖設定を推奨する。
            if (!IsChainConfigured() && boss.KamaBone != null && _currentExtension > 0.0001f)
            {
                Vector3 localAxis =
                    boss.KamaExtendLocalAxis.sqrMagnitude > 0.0001f
                        ? boss.KamaExtendLocalAxis.normalized
                        : Vector3.up;
                Vector3 extendOffset =
                    boss.KamaBone.localRotation * (localAxis * _currentExtension);
                boss.KamaBone.localPosition += extendOffset;
            }
        }

        private bool IsChainConfigured()
        {
            return boss.KamaChainDriver != null && boss.KamaChainDriver.IsValid;
        }

        /// <summary>
        /// 腕の連鎖に「しなり」を適用する。
        /// 波とドループの強さは伸長の進捗に連動させ、伸ばしていない間は揺れないようにする。
        /// </summary>
        private void ApplyChainFlex()
        {
            if (!IsChainConfigured())
            {
                return;
            }

            float reach = Mathf.Max(0.0001f, boss.Attack2ReachExtension);
            float wavePower = Mathf.Clamp01(_currentExtension / reach);
            boss.KamaChainDriver.Apply(_currentExtension, wavePower, Time.deltaTime);
        }

        /// <summary>
        /// 武器が実際にプレイヤーへ触れた瞬間（EnemyMeleeHitbox.OnHitLanded）に呼ばれる。
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

            // 密着・すり抜けを避けるため、通常攻撃の間合いより内側へは詰めない
            float pullAmount = Mathf.Min(
                boss.Attack2PullDistance,
                Mathf.Max(0f, currentDistance - boss.AttackRange)
            );
            Vector3 pullDir =
                currentDistance > 0.0001f ? toPlayer.normalized : boss.transform.forward;
            _pullEnd = _pullStart - pullDir * pullAmount;

            if (_playerController != null)
            {
                _playerController.CanMove = false;
                _movementSuppressed = true;
            }

            // 引き寄せ中にボスと押し合いになるのを防ぐ
            if (boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, true);
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

                // 怯み・掴みが同時に成立している場合はそちら側が操作を管理するため、ここでは戻さない
                if (!_playerController.IsGrabbed && !_playerController.IsFlinching)
                {
                    _playerController.CanMove = true;
                }
            }

            if (_collisionIgnored && boss.EnemyCollider != null && boss.PlayerCollider != null)
            {
                Physics.IgnoreCollision(boss.EnemyCollider, boss.PlayerCollider, false);
                _collisionIgnored = false;
            }
        }
    }
}
