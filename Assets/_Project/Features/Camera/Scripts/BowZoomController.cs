// ■ セットアップ手順
//   1. AimCamera の GameObject にこのスクリプトをアタッチする
//   2. _aimCamera フィールドは同じ GameObject の CinemachineCamera から自動取得される
//   3. _bowController フィールドにシーン内の BowController を割り当てる
//      （未設定の場合は FindAnyObjectByType で自動検索する）
//
// ■ 動作仕様
//   - エイム中（IsAiming = true）: FOV を _zoomedFov まで徐々にズームイン
//   - 発射時                    : FOV をデフォルトに即座にスナップリセット
//   - エイム解除時              : FOV を滑らかにデフォルトへ戻す

using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class BowZoomController : MonoBehaviour
    {
        [Tooltip("FOVを制御する AimCamera の CinemachineCamera（未設定なら同じ GO から自動取得）")]
        [SerializeField]
        private CinemachineCamera _aimCamera;

        [Tooltip("BowController（未設定なら FindAnyObjectByType で自動検索）")]
        [SerializeField]
        private BowController _bowController;

        [Tooltip("エイム中に到達するズーム後の FOV（度）。小さいほどズームイン。")]
        [SerializeField]
        private float _zoomedFov = 40f;

        [Tooltip("ズームインの速さ（大きいほど速い）")]
        [SerializeField]
        private float _zoomInSpeed = 3f;

        [Tooltip("エイム解除後にデフォルト FOV へ戻す速さ（大きいほど速い）")]
        [SerializeField]
        private float _zoomOutSpeed = 6f;

        // AimCamera に設定されているデフォルト FOV（Awake 時に記録）
        private float _defaultFov;

        // 発射直後フラグ：FOV をスナップリセットするため 1 フレームだけ true
        private bool _snapToDefault = false;

        private void Awake()
        {
            if (_aimCamera == null)
                _aimCamera = GetComponent<CinemachineCamera>();

            if (_bowController == null)
                _bowController = FindAnyObjectByType<BowController>();

            if (_aimCamera != null)
                _defaultFov = _aimCamera.Lens.FieldOfView;

            // 発射イベントを購読
            BowController.OnFired += HandleFired;
        }

        private void OnDestroy()
        {
            BowController.OnFired -= HandleFired;
        }

        private void HandleFired()
        {
            // 発射時は次の LateUpdate でデフォルト FOV にスナップさせる
            _snapToDefault = true;
        }

        private void LateUpdate()
        {
            if (_aimCamera == null || _bowController == null)
                return;

            // 発射直後はスナップリセット
            if (_snapToDefault)
            {
                _aimCamera.Lens.FieldOfView = _defaultFov;
                _snapToDefault = false;
                return;
            }

            bool isAiming =
                _bowController._playerController != null
                && _bowController._playerController.IsAiming;

            float targetFov = isAiming ? _zoomedFov : _defaultFov;
            float speed = isAiming ? _zoomInSpeed : _zoomOutSpeed;

            _aimCamera.Lens.FieldOfView = Mathf.Lerp(
                _aimCamera.Lens.FieldOfView,
                targetFov,
                Time.deltaTime * speed
            );
        }
    }
}
