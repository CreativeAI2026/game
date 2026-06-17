using System.Collections;
using UnityEngine;

// ■ セットアップ手順
//   1. プレイヤーのルート GameObject（PlayerController と同じ GO）にこのスクリプトをアタッチする
//   2. Animator に "PlayerFlinch" という Trigger パラメータを追加する
//   3. "PlayerFlinch" という名前のアニメーターステートを作成し、怯みクリップを割り当てる
//   4. "Any State" → "PlayerFlinch" へのトランジションを作成し、
//      Trigger: PlayerFlinch で遷移するよう設定する（Has Exit Time: OFF, Interruption: None）
//
// ■ 怯みの安全な終了ロジック
//   PlayerFlinch アニメーションの normalizedTime >= 1.0 を検出して操作を再開します。
//   万が一アニメーターがステートに入れなかった場合は _flinchTimeout 秒後に強制終了します。

namespace CreativeAI.Gameplay
{
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

        // ───── 内部参照 ─────
        private PlayerController _playerController;
        private Animator _animator;
        private SwordController _swordController;
        private BowController _bowController;
        private Coroutine _flinchCoroutine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _animator = GetComponent<Animator>();

            // 武器コントローラーは子 GameObject にある想定
            _swordController = GetComponentInChildren<SwordController>(includeInactive: true);
            _bowController = GetComponentInChildren<BowController>(includeInactive: true);
        }

        /// <summary>
        /// 怯み演出を開始する。PlayerStatus.TakeDamage() から呼び出す。
        /// 怯み中（IsFlinching == true）は再度呼んでも無視される。
        /// </summary>
        public void TriggerFlinch()
        {
            if (_playerController == null)
                return;

            // ─── 怯み中の再トリガー防止 ───
            if (_playerController.IsFlinching)
                return;

            if (_flinchCoroutine != null)
                StopCoroutine(_flinchCoroutine);

            _flinchCoroutine = StartCoroutine(FlinchCoroutine());
        }

        private IEnumerator FlinchCoroutine()
        {
            // ─── 1. フラグをセットして全コントローラーをロック ───
            _playerController.IsFlinching = true;
            _playerController.CanMove = false;
            _playerController.CanChangeWeapon = false;

            // ─── 2. 各武器コントローラーを安全な初期状態にリセット ───
            // SwordController のリセット（コンボ中・ガード中・パリィ中でも安全に抜ける）
            _swordController?.ForceReset();

            // BowController のリセット（エイム中・発射中でも矢の破棄・IsAimingのリセットを行う）
            _bowController?.ForceReset();

            // StateFree.Enter() が CanChangeWeapon = true にセットするため、
            // 怯み中は武器切り替えを禁止するよう再度上書きする
            _playerController.CanChangeWeapon = false;

            // ─── 3. 怯みアニメーションを再生 ───
            if (_animator != null)
                _animator.SetTrigger(_flinchTriggerName);

            // SetTrigger 直後はまだ遷移前のステートにいるため、2フレーム待ってから監視を開始する
            yield return null;
            yield return null;

            // ─── 4. アニメーション完了を待つ ───
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

                // 怯みステートに入ったことを確認
                if (isPlayingFlinch || isTransitioningToFlinch)
                    hasEnteredFlinch = true;

                // 怯みアニメーションが完全に終了し、次のトランジションもない状態になったら終了
                if (
                    hasEnteredFlinch
                    && isPlayingFlinch
                    && stateInfo.normalizedTime >= 1.0f
                    && !_animator.IsInTransition(0)
                )
                    break;

                // フォールバック：一定時間経過しても終わらない場合は強制終了
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

            // ─── 5. 怯み終了：フラグを戻して操作を再開 ───
            _playerController.IsFlinching = false;
            _playerController.CanMove = true;
            _playerController.CanChangeWeapon = true;
            _flinchCoroutine = null;
        }
    }
}
