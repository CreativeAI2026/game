using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被弾時の怯み演出を管理する。怯み中は全操作をロックし、
    /// 各武器コントローラーを安全な初期状態にリセットしてから
    /// 怯みアニメーションの完了を待つ。
    /// </summary>
    public class PlayerFlinchHandler : MonoBehaviour
    {
        [Tooltip("Animator の怯みTriggerパラメータ名")]
        [SerializeField]
        private string _flinchTriggerName = "PlayerFlinch";

        [Tooltip("Animator の怯みステート名")]
        [SerializeField]
        private string _flinchStateName = "PlayerFlinch";

        [Tooltip("怯みアニメーションステートに入れなかった場合のタイムアウト（秒）")]
        [SerializeField]
        private float _flinchTimeout = 2.0f;

        private PlayerController _playerController;
        private Animator _animator;
        private SwordController _swordController;
        private BowController _bowController;
        private Coroutine _flinchCoroutine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _animator = GetComponent<Animator>();

            _swordController = GetComponentInChildren<SwordController>(includeInactive: true);
            _bowController = GetComponentInChildren<BowController>(includeInactive: true);
        }

        /// <summary>
        /// 怯み演出を開始する。PlayerStatus.TakeDamage() から呼び出される。
        /// 怯み中（IsFlinching == true）に再度呼ばれた場合は無視し、
        /// モーション中にさらにひるむ多重怯みを防ぐ。
        /// </summary>
        public void TriggerFlinch()
        {
            if (_playerController == null)
                return;

            if (_playerController.IsFlinching || _playerController.IsGrabbed)
                return;

            if (_flinchCoroutine != null)
                StopCoroutine(_flinchCoroutine);

            _flinchCoroutine = StartCoroutine(FlinchCoroutine());
        }

        private IEnumerator FlinchCoroutine()
        {
            _playerController.IsFlinching = true;
            _playerController.CanMove = false;
            _playerController.CanChangeWeapon = false;

            // コンボ中・ガード中・パリィ中・エイム中でも安全に抜けられるようリセットする
            _swordController?.ForceReset();
            _bowController?.ForceReset();

            // ForceReset内のStateFree.Enter()がCanChangeWeapon = trueにリセットするため、
            // 怯み中は武器切り替えを禁止するよう再度上書きする
            _playerController.CanChangeWeapon = false;

            if (_animator != null)
                _animator.SetTrigger(_flinchTriggerName);

            // SetTrigger直後はAnimatorがまだ遷移前のステートにいるため、
            // 2フレーム待ってから怯みステートの監視を開始する
            yield return null;
            yield return null;

            float elapsed = 0f;
            bool hasEnteredFlinch = false;

            while (true)
            {
                if (_animator == null)
                    break;

                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                bool isPlayingFlinch = stateInfo.IsName(_flinchStateName);
                bool isTransitioningToFlinch =
                    _animator.IsInTransition(0)
                    && _animator.GetNextAnimatorStateInfo(0).IsName(_flinchStateName);

                if (isPlayingFlinch || isTransitioningToFlinch)
                    hasEnteredFlinch = true;

                if (
                    hasEnteredFlinch
                    && isPlayingFlinch
                    && stateInfo.normalizedTime >= 1.0f
                    && !_animator.IsInTransition(0)
                )
                    break;

                // Animatorの遷移設定ミス等で怯みステートに入れなかった場合の無限ループ防止
                elapsed += Time.deltaTime;
                if (elapsed >= _flinchTimeout)
                {
                    Debug.LogWarning(
                        "[PlayerFlinchHandler] 怯みアニメーションがタイムアウトしました。強制終了します。"
                    );
                    break;
                }

                yield return null;
            }

            _playerController.IsFlinching = false;
            _playerController.CanMove = true;
            _playerController.CanChangeWeapon = true;
            _flinchCoroutine = null;
        }
    }
}
