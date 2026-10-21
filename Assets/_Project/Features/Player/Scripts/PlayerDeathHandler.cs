using SoundKit;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤー死亡時の反応(死亡モーション・倒れる音・操作停止)を担当する。PlayerStatus.OnDied を購読する。
    ///
    /// - 死亡モーションを再生する。倒れる音はクリップの AnimationEvent(OnFallDown)で鳴らす
    /// - 入力を無効化し、残っている入力値(移動・構え・攻撃の先行入力)を消す
    /// - 行動制限をすべて掛ける
    /// - 怯みを打ち切り、装備中の武器を中立状態に戻す
    /// - 敵への視線追従を止める(頭の回転を上書きし続け、倒れるモーション中も敵を見てしまうため)
    /// </summary>
    [RequireComponent(typeof(PlayerStatus))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerFlinchHandler))]
    [RequireComponent(typeof(WeaponManager))]
    [RequireComponent(typeof(HeadLookController))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        private Animator _animator;
        private PlayerStatus _status;
        private PlayerController _playerController;
        private PlayerInput _playerInput;
        private PlayerInputHandler _input;
        private PlayerFlinchHandler _flinchHandler;
        private WeaponManager _weaponManager;
        private HeadLookController _headLook;

        [Header("Audio")]
        [Tooltip("体が床に着いたときの音。死亡クリップの AnimationEvent「OnFallDown」で鳴らす")]
        [SerializeField]
        private SoundCue _fallDownSound;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _status = GetComponent<PlayerStatus>();
            _playerController = GetComponent<PlayerController>();
            _playerInput = GetComponent<PlayerInput>();
            _input = GetComponent<PlayerInputHandler>();
            _flinchHandler = GetComponent<PlayerFlinchHandler>();
            _weaponManager = GetComponent<WeaponManager>();
            _headLook = GetComponent<HeadLookController>();
        }

        private void OnEnable() => _status.OnDied += HandleDied;

        private void OnDisable() => _status.OnDied -= HandleDied;

        private void HandleDied()
        {
            _animator.SetTrigger(PlayerAnimatorParams.Die);
            _playerInput.DeactivateInput();
            _input.ClearAllInput();

            _playerController.Locks.Set(this, PlayerLock.All);
            _flinchHandler.Cancel();
            _weaponManager.ResetEquippedWeapon();
            _headLook.enabled = false;
        }

        /// <summary>死亡クリップの OnFallDown イベント(PlayerAnimationEventRelay 経由)。</summary>
        public void OnFallDownEvent() => WorldSoundPlayer.Play(_fallDownSound, transform.position, this);
    }
}
