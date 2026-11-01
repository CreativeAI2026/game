using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓エイム時のカメラ切り替えを担当する。**エイム用の CinemachineCamera と同じ GameObject に置く**(シーン側)。
    ///
    /// プレイヤーは Prefab から実行時に生成されるため、プレイヤー側からシーンのカメラを参照せず、
    /// カメラ側が PlayerRegistry からプレイヤーを受け取る。
    /// PlayerController.IsAiming を立てるのは弓だけなので、剣のガード(subAction)でカメラが切り替わることはない。
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class AimSystem : MonoBehaviour
    {
        // 通常カメラ(Priority=0)より高い値。Cinemachineは最も高いPriorityのカメラをアクティブにする
        private const int AimCameraPriority = 20;

        private CinemachineCamera _aimCam;

        void Awake()
        {
            _aimCam = GetComponent<CinemachineCamera>();
        }

        void Update()
        {
            // プレイヤーがまだ生成されていない間、または掴まれている間はエイムカメラにしない
            PlayerController player = PlayerRegistry.Current;
            bool isAiming = player != null && player.IsAiming && !player.IsGrabbed;
            _aimCam.Priority = isAiming ? AimCameraPriority : 0;
        }
    }
}
