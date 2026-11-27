using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被弾時の怯み演出を管理する。怯み中は全操作を制限し(武器はその時点で中立状態へ戻る)、
    /// 怯みクリップの ActionEnd イベントまで待つ。
    ///
    /// 怯みの長さは怯みクリップの ActionEnd の位置で決まり、スクリプトは秒数を持たない。
    /// ActionEnd が無い場合は、Animator が怯みステートを抜けた時点で終える(警告を出す)。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerHitInvincibility))]
    public class PlayerFlinchHandler : MonoBehaviour
    {
        [Tooltip(
            "Trigger を送ったのに怯みステートへ入れなかった場合(遷移の設定漏れ等)に、怯みを打ち切るまでの猶予（秒）"
        )]
        [SerializeField]
        private float _enterFlinchTimeout = 1.0f;

        private PlayerController _playerController;
        private Animator _animator;
        private Coroutine _flinchCoroutine;
        private PlayerHitInvincibility _invincible;
        private bool _actionEndReceived;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _invincible = GetComponent<PlayerHitInvincibility>();
            _animator = PlayerModel.Of(this).Animator;
        }

        /// <summary>
        /// 怯み演出を開始する。PlayerStatus.TakeDamage() から呼び出される。
        /// 怯み中(はじき返されている間を含む)・掴まれ中に呼ばれた場合は無視し、
        /// モーション中にさらにひるむ多重怯みを防ぐ。
        /// </summary>
        public void TriggerFlinch()
        {
            if (_playerController.Conditions.IsFlinching || _playerController.Conditions.IsGrabbed)
                return;

            if (_flinchCoroutine != null)
                StopCoroutine(_flinchCoroutine);

            _flinchCoroutine = StartCoroutine(FlinchCoroutine());
        }

        /// <summary>怯みクリップの ActionEnd イベント(PlayerAnimationEventRelay 経由)。</summary>
        public void OnActionEnd(AnimatorStateInfo sourceState)
        {
            if (
                _flinchCoroutine != null
                && sourceState.shortNameHash == PlayerAnimatorStates.Flinch
            )
                _actionEndReceived = true;
        }

        private IEnumerator FlinchCoroutine()
        {
            _playerController.Conditions.SetFlinching(this, true);
            // 攻撃の制限が掛かった時点で、装備中の武器はコンボ中・ガード中・エイム中でも中立状態へ戻る。
            // 武器が自分の制限を外しても、怯みの制限(owner = this)は残る
            _playerController.Conditions.Set(this, PlayerLock.All);
            _actionEndReceived = false;

            // 直前の行動の「移動へ戻る」指示が残っていると、怯みステートに入った直後に抜けてしまう
            _animator.ResetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            _animator.SetTrigger(PlayerAnimatorParams.Flinch);

            yield return WaitForFlinchEnd();

            _animator.SetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            _playerController.Conditions.SetFlinching(this, false);
            _invincible.StartGrace();
            _playerController.Conditions.Release(this);
            _flinchCoroutine = null;
        }

        /// <summary>
        /// 怯みステートに入り、ActionEnd を受け取るまで待つ。
        /// ActionEnd が無いまま怯みステートを抜けた場合もそこで終える。
        /// </summary>
        private IEnumerator WaitForFlinchEnd()
        {
            float waitedForEnter = 0f;
            bool hasEntered = false;

            while (!_actionEndReceived)
            {
                if (_animator.IsPlayingOrEntering(0, PlayerAnimatorStates.Flinch))
                {
                    hasEntered = true;
                }
                else if (hasEntered)
                {
                    Debug.LogWarning(
                        "[PlayerFlinchHandler] 怯みクリップの ActionEnd イベントを受信しないまま怯みステートを抜けました。"
                            + "クリップに ActionEnd を追加してください。"
                    );
                    yield break;
                }
                else
                {
                    waitedForEnter += Time.deltaTime;
                    if (waitedForEnter >= _enterFlinchTimeout)
                    {
                        Debug.LogWarning(
                            "[PlayerFlinchHandler] 怯みステート「Flinch」に入れませんでした。Animator の遷移とステート名を確認してください。"
                        );
                        _animator.ResetTrigger(PlayerAnimatorParams.Flinch);
                        yield break;
                    }
                }

                yield return null;
            }
        }

        /// <summary>死亡時など、怯みを途中で打ち切る。</summary>
        public void Cancel()
        {
            if (_flinchCoroutine == null)
                return;

            StopCoroutine(_flinchCoroutine);
            _flinchCoroutine = null;
            _playerController.Conditions.SetFlinching(this, false);
            _playerController.Conditions.Release(this);
        }
    }
}
