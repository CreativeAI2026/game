using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面の隅に出す円形のミニマップ。プレイヤーを中心に、いる階の図を描く(同じ高さの別の建物も並べて描く)。
    /// 既定ではカメラの向きが上になるよう図を回し、北の印を縁に出す。マップピンは範囲の外なら縁に寄せて出す。
    /// 他の HUD と同じく、Field かつ会話イベント中でないときだけ出す(CanvasGroup で隠すだけで自身は動かし続ける)。
    /// </summary>
    public sealed class MinimapView : MonoBehaviour, MapPinIconPool.IPlacement
    {
        [SerializeField]
        private MinimapLayout _layout;

        [SerializeField, Tooltip("円形に切り抜く枠(Mask 付き)")]
        private RectTransform _frame;

        [SerializeField, Tooltip("図を回す親(枠の中心に置く)")]
        private RectTransform _rotator;

        [SerializeField]
        private MinimapShapeGraphic _map;

        [SerializeField]
        private RectTransform _playerIcon;

        [SerializeField, Tooltip("北の印(枠の縁を回る)")]
        private RectTransform _northMarker;

        [SerializeField, Tooltip("ピンの記号の型。実行時に複製して使う")]
        private MinimapIconGraphic _pinTemplate;

        [SerializeField]
        private TMP_Text _floorLabel;

        [SerializeField, Tooltip("1u を何ピクセルで描くか(大きいほど拡大)")]
        private float _pixelsPerUnit = 2.2f;

        [SerializeField, Tooltip("カメラの向きを上にして図を回すか(false なら北が上で固定)")]
        private bool _rotateWithCamera = true;

        [SerializeField, Tooltip("縁に寄せるピンを枠の内側へ下げる量(px)")]
        private float _pinEdgeInset = 12f;

        [SerializeField, Range(0f, 1f), Tooltip("別の階にあるピンの不透明度")]
        private float _otherFloorPinAlpha = 0.45f;

        private CanvasGroup _group;
        private bool? _shown;

        private Transform _player;
        private Camera _camera;
        private readonly List<MinimapLayout.Level> _drawn = new();
        private int _levelIndex = -1;
        private MapPinIconPool _pins;

        // このフレームのピンの置き方(図の中心・回転・縁に寄せる半径)
        private Vector2 _center;
        private Quaternion _viewRotation = Quaternion.identity;
        private float _pinMaxRadius;

        private void Awake()
        {
            _group = ResidentUiVisibility.GetOrAddGroup(gameObject);
            _pins = new MapPinIconPool(_pinTemplate);
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable() => PlayerRegistry.Changed -= Bind;

        private void Bind(PlayerController player) =>
            _player = player != null ? player.transform : null;

        private void LateUpdate()
        {
            var mode =
                GameModeManager.Instance != null
                    ? GameModeManager.Instance.CurrentMode
                    : GameMode.Field;
            SetShown(
                _layout != null
                    && _player != null
                    && mode == GameMode.Field
                    && !EventPlaybackService.IsPlaying
            );
            if (_shown != true)
                return;
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;

            var position = _player.position;
            _center = new Vector2(position.x, position.z);
            var levelIndex = _layout.FindLevel(position);
            if (levelIndex != _levelIndex)
                ChangeLevel(levelIndex);

            var viewYaw =
                _rotateWithCamera && _camera != null ? _camera.transform.eulerAngles.y : 0f;
            _viewRotation = Quaternion.Euler(0f, 0f, viewYaw);

            _rotator.localRotation = _viewRotation;
            _map.rectTransform.localScale = new Vector3(_pixelsPerUnit, _pixelsPerUnit, 1f);
            _map.rectTransform.anchoredPosition = -_center * _pixelsPerUnit;

            var forward = _player.forward;
            var playerYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            _playerIcon.localRotation = Quaternion.Euler(0f, 0f, viewYaw - playerYaw);

            var radius = _frame.rect.width * 0.5f;
            if (_northMarker != null)
                _northMarker.anchoredPosition = _viewRotation * new Vector2(0f, radius);

            _pinMaxRadius = radius - _pinEdgeInset;
            _pins.Refresh(_layout, this, _otherFloorPinAlpha);
        }

        private void SetShown(bool shown)
        {
            if (_shown == shown)
                return;
            _shown = shown;
            _group.SetShown(shown);
        }

        // 階が変わったときだけ、同じ高さの階を集めて描き直し、階の名前を替える
        private void ChangeLevel(int levelIndex)
        {
            _levelIndex = levelIndex;
            _drawn.Clear();
            var level = levelIndex >= 0 ? _layout.Levels[levelIndex] : null;
            if (level != null)
                _layout.LevelsAtHeight(level.Height, _drawn);
            _map.Show(_drawn);
            if (_floorLabel != null)
                _floorLabel.text = level != null ? level.Label : string.Empty;
        }

        Vector2 MapPinIconPool.IPlacement.ToIconPosition(Vector3 world)
        {
            // 範囲の外にあるピンは、枠の縁に寄せて出す
            Vector2 offset =
                _viewRotation * ((new Vector2(world.x, world.z) - _center) * _pixelsPerUnit);
            if (offset.sqrMagnitude > _pinMaxRadius * _pinMaxRadius)
                offset = offset.normalized * _pinMaxRadius;
            return offset;
        }

        // 同じ高さの別の建物も並べて描いているので、階の番号ではなく床面の高さで比べる
        bool MapPinIconPool.IPlacement.IsShownLevel(int levelIndex) =>
            levelIndex >= 0
            && _levelIndex >= 0
            && Mathf.Approximately(
                _layout.Levels[levelIndex].Height,
                _layout.Levels[_levelIndex].Height
            );
    }
}
