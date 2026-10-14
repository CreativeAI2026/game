using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを映すカメラ一式(Main Camera + CinemachineBrain、通常/エイムの CinemachineCamera、画面揺れ)の常駐ルート。
    /// プレイヤーとは別に常駐し、PlayerRegistry から受け取ったプレイヤーの追従点を子の CinemachineCamera に配る。
    /// プレイヤーが居ない間(死亡演出・会話で隠す等)もカメラは残る。
    /// ボス戦の演出カメラもこのリグに足す(ボスはイベントで合図するだけ)。Priority が最も高いカメラが映る。
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "プレイヤーの追従点(PlayerCameraController.CinemachineCameraTarget)を追わせるカメラ"
        )]
        private CinemachineCamera[] _followCameras;

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
        }

        private void Bind(PlayerController player)
        {
            Transform target = null;
            if (
                player != null
                && player.TryGetComponent(out PlayerCameraController cameraController)
            )
                target = cameraController.CinemachineCameraTarget.transform;

            foreach (var cam in _followCameras)
                if (cam != null)
                    cam.Target.TrackingTarget = target;
        }
    }
}
