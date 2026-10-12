using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ハンマー固有のステートの基底クラス。
    /// Free → Slam → Free の叩きつけ(範囲攻撃)を管理する。攻撃は共通の Dash / Attack を使う。
    /// </summary>
    public abstract class HammerState : MeleeWeaponState
    {
        protected readonly HammerController hammer;

        protected HammerState(HammerController context)
            : base(context)
        {
            hammer = context;
        }
    }

    /// <summary>待機ステート。攻撃入力で接近/コンボ、subAction の押下で叩きつけへ。</summary>
    public class HammerStateFree : HammerState
    {
        public HammerStateFree(HammerController context)
            : base(context) { }

        public override bool IsNeutral => true;

        public override void Enter()
        {
            hammer.ReleaseControlLocks();
        }

        public override void Update()
        {
            // 叩きつけ・攻撃は地上でのみ始める(空中の攻撃入力は消費せず先行入力として残す)
            if (!hammer.CanStartGroundAction)
                return;

            if (hammer.SubActionPressedThisFrame)
            {
                hammer.ChangeState(new HammerStateSlam(hammer));
                return;
            }

            if (hammer.Input.ConsumeAttack())
            {
                hammer.BeginAttack();
            }
        }
    }

    /// <summary>
    /// 叩きつけ(範囲攻撃)のステート。最寄りの敵を向いて Slam モーションを再生し、
    /// クリップの AnimationEvent で進行する:
    /// - ThrustStart         : 噴射エフェクトを出す
    /// - LeapStart           : 敵の手前へ跳んで移動し始める(SlamImpact のフレームで着地点に着くよう、クリップの進みに合わせて移動。
    ///                         見た目は HammerController の跳躍カーブで放物線状に持ち上げる)
    /// - SlamImpact          : 衝撃エフェクトと範囲攻撃判定を出す
    /// - TriggerDisableHitbox: 範囲攻撃判定を消す
    /// - ActionEnd           : 操作を返し、移動モーションへ戻す
    /// </summary>
    public class HammerStateSlam : HammerState
    {
        // Trigger を送ったのに Slam ステートへ入れない(遷移の設定漏れ等)ときに待機へ戻すまでの猶予
        private const float EnterSlamTimeout = 2.0f;

        private static readonly int SlamHash = PlayerAnimatorStates.Slam;

        private Transform _target;
        private float _requestTime;
        private bool _hasStartedAction;
        private bool _isActionEnded;

        private bool _isLeaping;
        private float _leapStartNormalizedTime;
        private float _impactNormalizedTime;
        private Vector3 _leapStart;
        private Vector3 _leapEnd;

        public HammerStateSlam(HammerController context)
            : base(context) { }

        public override void Enter()
        {
            hammer.LockControls();
            hammer.ComboStep = 0;

            _target = hammer.FindNearestEnemy();
            hammer.FaceTowards(_target);

            hammer.StartAnimatorAction(PlayerAnimatorParams.Slam);
            _requestTime = Time.time;
        }

        public override void Exit()
        {
            // 怯み・武器切替等で途中で抜けても、Trigger・エフェクト・判定が残らないようにする
            hammer.Animator.ResetTrigger(PlayerAnimatorParams.Slam);
            hammer.StopThrustEffect();
            hammer.DisableSlamHitbox();
            hammer.ResetLeapHeight();
        }

        public override void OnAnimationEvent(
            WeaponAnimationEventType type,
            AnimatorStateInfo sourceState
        )
        {
            if (sourceState.shortNameHash != SlamHash)
                return;

            switch (type)
            {
                case WeaponAnimationEventType.ThrustStart:
                    hammer.StartThrustEffect();
                    break;
                case WeaponAnimationEventType.LeapStart:
                    BeginLeap(sourceState);
                    break;
                case WeaponAnimationEventType.SlamImpact:
                    // 跳躍が予定より長引いても、叩きつけた位置・地面の高さで止める
                    _isLeaping = false;
                    hammer.ResetLeapHeight();
                    hammer.ApplySlamImpact();
                    break;
                case WeaponAnimationEventType.HitboxOff:
                    hammer.DisableSlamHitbox();
                    break;
                case WeaponAnimationEventType.ActionEnd:
                    _isActionEnded = true;
                    break;
            }
        }

        public override void Update()
        {
            // 叩きつけ中の攻撃入力は捨てる(終わった直後に勝手に攻撃が出ないように)
            hammer.Input.ConsumeAttack();

            AnimatorStateInfo current = hammer.Animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = hammer.Animator.GetNextAnimatorStateInfo(0);
            bool isPlayingSlam = current.shortNameHash == SlamHash;
            bool isTransitioningToSlam =
                hammer.Animator.IsInTransition(0) && next.shortNameHash == SlamHash;

            if (!_hasStartedAction)
            {
                if (isPlayingSlam || isTransitioningToSlam)
                {
                    _hasStartedAction = true;
                }
                else if (Time.time - _requestTime > EnterSlamTimeout)
                {
                    Debug.LogWarning(
                        $"[{hammer.name}] Slam ステートへ遷移できませんでした。Animator の遷移設定を確認してください。"
                    );
                    hammer.ChangeState(hammer.CreateNeutralState());
                }
                return;
            }

            if (_isLeaping && (isPlayingSlam || isTransitioningToSlam))
                UpdateLeap(isPlayingSlam ? current.normalizedTime : next.normalizedTime);

            if (_isActionEnded)
            {
                hammer.ReturnAnimatorToLocomotion();
                hammer.ChangeState(hammer.CreateNeutralState());
                return;
            }

            // ActionEnd を受け取らないまま Slam ステートを抜けた(遷移の割り込み・イベントの付け忘れ)
            if (!isPlayingSlam && !isTransitioningToSlam && !hammer.Animator.IsInTransition(0))
            {
                Debug.LogWarning(
                    $"[{hammer.name}] Slam で ActionEnd イベントを受信しないまま叩きつけが終了しました。"
                        + "クリップに ActionEnd を追加してください。"
                );
                hammer.ChangeState(hammer.CreateNeutralState());
            }
        }

        private void BeginLeap(AnimatorStateInfo sourceState)
        {
            if (!TryGetImpactNormalizedTime(out _impactNormalizedTime))
                return;

            _leapStartNormalizedTime = sourceState.normalizedTime;
            if (_impactNormalizedTime <= _leapStartNormalizedTime)
            {
                Debug.LogError(
                    $"[{hammer.name}] Slam クリップの SlamImpact が LeapStart より前にあります。イベントの順番を確認してください。"
                );
                return;
            }

            // 予備動作の間に敵が撃破された(非アクティブ)場合は狙い直す
            if (_target == null || !_target.gameObject.activeInHierarchy)
                _target = hammer.FindNearestEnemy();

            // 予備動作中に敵が動いた分を追従するため、向きと着地点は跳ぶ瞬間に決め直す
            hammer.FaceTowards(_target);
            _leapStart = hammer.PlayerTransform.position;
            _leapEnd = hammer.ResolveLeapDestination(_target);
            _isLeaping = true;
        }

        /// <summary>
        /// 再生中の Slam クリップから SlamImpact イベントの位置を normalizedTime(0〜1)で求める。
        /// 秒ではなくクリップ上の割合で持つので、ステートやクリップの再生速度を変えても着地が叩きつけのフレームに揃う。
        /// </summary>
        private bool TryGetImpactNormalizedTime(out float impactNormalizedTime)
        {
            impactNormalizedTime = 0f;

            // LeapStart は Slam ステート由来なので、遷移中なら Slam は「次のステート」側にいる
            Animator animator = hammer.Animator;
            AnimatorClipInfo[] clips =
                animator.GetCurrentAnimatorStateInfo(0).shortNameHash == SlamHash
                    ? animator.GetCurrentAnimatorClipInfo(0)
                    : animator.GetNextAnimatorClipInfo(0);

            if (clips.Length > 0)
            {
                AnimationClip clip = clips[0].clip;
                foreach (AnimationEvent evt in clip.events)
                {
                    if (evt.functionName == nameof(PlayerAnimationEventRelay.SlamImpact))
                    {
                        impactNormalizedTime = evt.time / clip.length;
                        return true;
                    }
                }
            }

            Debug.LogError(
                $"[{hammer.name}] Slam クリップに SlamImpact イベントが見つからないため、跳躍できません。クリップのイベントを確認してください。"
            );
            return false;
        }

        /// <summary>
        /// 跳躍先へ水平に移動し、見た目を放物線状に持ち上げる。進み具合は LeapStart〜SlamImpact 区間のクリップの再生位置で決める。
        /// ルート(CharacterController)は水平にしか動かさない。浮かせると PlayerController の重力・接地判定と干渉するため、
        /// 高さは見た目(モデル)だけに付ける。
        /// 壁等に当たった分は CharacterController が止めるので、到達できなくてもその場で叩きつける。
        /// </summary>
        private void UpdateLeap(float slamNormalizedTime)
        {
            float t = Mathf.InverseLerp(_leapStartNormalizedTime, _impactNormalizedTime, slamNormalizedTime);

            Vector3 desired = Vector3.Lerp(_leapStart, _leapEnd, t);
            Vector3 delta = desired - hammer.PlayerTransform.position;
            delta.y = 0f;
            hammer.CharacterController.Move(delta);
            hammer.ApplyLeapHeight(t);

            if (t >= 1f)
                _isLeaping = false;
        }
    }
}
