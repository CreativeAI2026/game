using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓エイム時のカメラ切り替えを担当する。
    /// 剣のsubAction（防御）でもカメラが切り替わるのを防ぐため、
    /// 武器インデックスが弓の場合にのみエイムカメラを有効化する設計。
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

            bool isBowEquipped =
                _weaponManager == null || _weaponManager.CurrentWeaponIndex == _bowWeaponIndex;

            bool shouldAim =
                isBowEquipped && _playerController != null && _playerController.IsAiming;

            // 20はデフォルトカメラ(Priority=0)より高い値として設定。Cinemachineは最も高いPriorityのカメラをアクティブにする
            _aimCam.Priority = shouldAim ? 20 : 0;
        }
    }
}
