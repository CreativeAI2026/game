using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被弾時の怯み演出を管理する。怯み中は全操作を制限し、
    /// 装備中の武器を中立状態に戻してから、怯みクリップの ActionEnd イベントまで待つ。
    ///
    /// 怯みの長さは怯みクリップの ActionEnd の位置で決まり、スクリプトは秒数を持たない。
    /// ActionEnd が無い場合は、Animator が怯みステートを抜けた時点で終える(警告を出す)。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(WeaponManager))]
    public class PlayerFlinchHandler : MonoBehaviour
    {
        [Tooltip(
            "Trigger を送ったのに怯みステートへ入れなかった場合(遷移の設定漏れ等)に、怯みを打ち切るまでの猶予（秒）"
        )]
        [SerializeField]
        private float _enterFlinchTimeout = 1.0f;

        private PlayerController _playerController;
        private WeaponManager _weaponManager;
        private Animator _animator;
        private Coroutine _flinchCoroutine;
        private bool _actionEndReceived;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _weaponManager = GetComponent<WeaponManager>();
            _animator = PlayerModel.Of(this).Animator;
        }

        /// <summary>
        /// 怯み演出を開始する。PlayerStatus.TakeDamage() から呼び出される。
        /// 怯み中（IsFlinching == true）に再度呼ばれた場合は無視し、
        /// モーション中にさらにひるむ多重怯みを防ぐ。
        /// </summary>
        public void TriggerFlinch()
        {
            if (_playerController.IsFlinching || _playerController.IsGrabbed)
                return;

            if (_flinchCoroutine != null)
                StopCoroutine(_flinchCoroutine);

            _flinchCoroutine = StartCoroutine(FlinchCoroutine());
        }

        /// <summary>怯みクリップの ActionEnd イベント(PlayerAnimationEventRelay 経由)。</summary>
        public void OnActionEnd(AnimatorStateInfo sourceState)
        {
            if (_flinchCoroutine != null && sourceState.shortNameHash == PlayerAnimatorStates.Flinch)
                _actionEndReceived = true;
        }

        private IEnumerator FlinchCoroutine()
        {
            _playerController.IsFlinching = true;
            _playerController.Locks.Set(this, PlayerLock.All);
            _actionEndReceived = false;

            // コンボ中・ガード中・パリィ中・エイム中でも安全に抜けられるよう、装備中の武器を中立状態に戻す。
            // 武器が自分の制限を外しても、怯みの制限(owner = this)は残る
            _weaponManager.ResetEquippedWeapon();

            // 直前の行動の「移動へ戻る」指示が残っていると、怯みステートに入った直後に抜けてしまう
            _animator.ResetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            _animator.SetTrigger(PlayerAnimatorParams.Flinch);

            yield return WaitForFlinchEnd();

            _animator.SetTrigger(PlayerAnimatorParams.ReturnToLocomotion);
            _playerController.IsFlinching = false;
            _playerController.Locks.Release(this);
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
                bool isPlaying =
                    _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == PlayerAnimatorStates.Flinch;
                bool isTransitioningIn =
                    _animator.IsInTransition(0)
                    && _animator.GetNextAnimatorStateInfo(0).shortNameHash == PlayerAnimatorStates.Flinch;

                if (isPlaying || isTransitioningIn)
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
            _playerController.IsFlinching = false;
            _playerController.Locks.Release(this);
        }
    }
}
