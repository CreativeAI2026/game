using CreativeAI.Gameplay;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// TBossCapturedState の各フェーズに合わせて Cinemachine Virtual Camera の Priority を切り替える。
/// GrabEscapeEvents（イベントバス）を購読することで、Gameplay アセンブリへの直接依存なしに動作する。
/// </summary>
public class GrabCameraController : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    [Tooltip("通常のメインカメラ")]
    public CinemachineCamera vcamMain;

    [Tooltip("引き寄せフェーズ用カメラ")]
    public CinemachineCamera vcamPull;

    [Tooltip("電撃ダメージフェーズ用カメラ（顔横アングル等）")]
    public CinemachineCamera vcamDamage;

    [Tooltip("脱出成功フェーズ用カメラ")]
    public CinemachineCamera vcamEscape;

    private void OnEnable()
    {
        GrabEscapeEvents.OnCameraPull += StartPullPhase;
        GrabEscapeEvents.OnCameraDamage += StartDamagePhase;
        GrabEscapeEvents.OnCameraEscape += StartEscapePhase;
        GrabEscapeEvents.OnCameraEnd += EndGrab;
    }

    private void OnDisable()
    {
        GrabEscapeEvents.OnCameraPull -= StartPullPhase;
        GrabEscapeEvents.OnCameraDamage -= StartDamagePhase;
        GrabEscapeEvents.OnCameraEscape -= StartEscapePhase;
        GrabEscapeEvents.OnCameraEnd -= EndGrab;
    }

    private void ResetGrabCameras()
    {
        if (vcamPull != null)
            vcamPull.Priority = 0;
        if (vcamDamage != null)
            vcamDamage.Priority = 0;
        if (vcamEscape != null)
            vcamEscape.Priority = 0;
    }

    public void StartPullPhase()
    {
        ResetGrabCameras();
        if (vcamPull != null)
            vcamPull.Priority = 20;
    }

    public void StartDamagePhase()
    {
        ResetGrabCameras();
        if (vcamDamage != null)
            vcamDamage.Priority = 20;
    }

    public void StartEscapePhase()
    {
        ResetGrabCameras();
        if (vcamEscape != null)
            vcamEscape.Priority = 20;
    }

    public void EndGrab()
    {
        ResetGrabCameras();
    }
}
