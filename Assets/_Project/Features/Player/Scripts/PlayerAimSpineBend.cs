using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓を構えている間、カメラの上下角に合わせて背骨を曲げ、上下のエイムを体の姿勢に反映する。
    /// PlayerController から分離した、見た目の補正だけの担当。
    /// アニメーションの後(LateUpdate)に上書きする。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCameraController))]
    public class PlayerAimSpineBend : MonoBehaviour
    {
        [Tooltip("曲げる背骨(Humanoid のボーン。モデルのボーン名には依存しない)")]
        [SerializeField]
        private HumanBodyBones _spineBone = HumanBodyBones.Spine;

        private PlayerController _playerController;
        private PlayerCameraController _camera;
        private Transform _spine;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _camera = GetComponent<PlayerCameraController>();
        }

        private void Start()
        {
            // ボーンの取得は Animator の初期化後(Start)に行う
            _spine = PlayerModel.Of(this).GetBone(_spineBone);
        }

        private void LateUpdate()
        {
            if (!_playerController.IsAiming)
                return;

            // ボーンのローカル軸はモデルごとに違うため、キャラクターの右方向(ワールド)を軸に回す
            _spine.rotation =
                Quaternion.AngleAxis(_camera.Pitch, transform.right) * _spine.rotation;
        }
    }
}
