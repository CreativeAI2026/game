using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤー死亡時に操作を止める。PlayerStatus.OnDied を購読する。
    ///
    /// - 入力を無効化し、残っている入力値(移動・構え・攻撃の先行入力)を消す
    /// - 行動制限をすべて掛ける
    /// - 怯みを打ち切り、装備中の武器を中立状態に戻す
    ///
    /// 死亡モーションとゲームオーバーへの進行はここでは扱わない(専用アニメーション・画面フローができてから、
    /// PlayerStatus.OnDied を購読して追加する)。
    /// </summary>
    [RequireComponent(typeof(PlayerStatus))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerInputHandler))]
    [RequireComponent(typeof(PlayerFlinchHandler))]
    [RequireComponent(typeof(WeaponManager))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        private PlayerStatus _status;
        private PlayerController _playerController;
        private PlayerInput _playerInput;
        private PlayerInputHandler _input;
        private PlayerFlinchHandler _flinchHandler;
        private WeaponManager _weaponManager;

        private void Awake()
        {
            _status = GetComponent<PlayerStatus>();
            _playerController = GetComponent<PlayerController>();
            _playerInput = GetComponent<PlayerInput>();
            _input = GetComponent<PlayerInputHandler>();
            _flinchHandler = GetComponent<PlayerFlinchHandler>();
            _weaponManager = GetComponent<WeaponManager>();
        }

        private void OnEnable() => _status.OnDied += HandleDied;

        private void OnDisable() => _status.OnDied -= HandleDied;

        private void HandleDied()
        {
            _playerInput.DeactivateInput();
            _input.ClearAllInput();

            _playerController.Locks.Set(this, PlayerLock.All);
            _flinchHandler.Cancel();
            _weaponManager.ResetEquippedWeapon();
        }
    }
}
