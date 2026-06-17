using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// エイム中にAim専用カメラの優先度を上げるコンポーネント。
    /// 弓装備中（weaponIndex == bowWeaponIndex）のみ切り替える。
    /// 剣の防御（subAction）では切り替わらない。
    /// </summary>
    public class AimSystem : MonoBehaviour
    {
        [SerializeField]
        private CinemachineCamera _aimCam;

        [Tooltip(
            "弓に割り当てられているWeaponManagerのインデックス（BowControllerのweaponIndexと合わせる）"
        )]
        [SerializeField]
        private int _bowWeaponIndex = 1;

        private PlayerInputHandler _input;
        private WeaponManager _weaponManager;
        private PlayerController _playerController;

        void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _weaponManager = GetComponent<WeaponManager>();
            _playerController = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (_aimCam == null)
                return;

            // 弓装備中かつ IsAiming（BowControllerが立てるフラグ）のときだけカメラを切り替える
            bool isBowEquipped =
                _weaponManager == null || _weaponManager.CurrentWeaponIndex == _bowWeaponIndex;

            bool shouldAim =
                isBowEquipped && _playerController != null && _playerController.IsAiming;

            _aimCam.Priority = shouldAim ? 20 : 0;
        }
    }
}
