using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面の隅に出す円形のミニマップ。プレイヤーを中心に、いる階の図を描く(同じ高さの別の建物も並べて描く)。
    /// 既定ではカメラの向きが上になるよう図を回し、北の印を縁に出す。マップピンは範囲の外なら縁に寄せて出す。
    /// Z キー(ゲームパッドは右スティック押し込み)で、広い範囲を見渡す縮小表示と行き来できる。階が替わると、枠の外に階の名前を数秒出す。
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
        private MinimapMap _map;

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

        [
            SerializeField,
            Range(0.2f, 1f),
            Tooltip("縮小表示のときの、ふだんに対する拡大率(小さいほど広く見える)")
        ]
        private float _wideScale = 0.5f;

        [SerializeField, Tooltip("拡大率が切り替わる速さ(大きいほど速い)")]
        private float _zoomSpeed = 10f;

        [SerializeField, Tooltip("階が替わったときの階の名前を、出しておく秒数")]
        private float _bannerSeconds = 2.2f;

        private const float BannerFade = 0.25f; // 階の名前が出入りするときの秒数
        private const float BannerFontSize = 28f;
        private const float BannerGap = 26f; // 枠と階の名前のすき間(px)

        private CanvasGroup _group;
        private bool? _shown;
        private PlayerUIInput _uiInput;
        private Canvas _rootCanvas; // バナーの位置を、画面の上下のどちらかで決めるのに使う
        private bool _wide; // 縮小表示か
        private float _zoomScale = 1f; // 今の拡大率(なめらかに _wide へ向かう)
        private float _scaledPixelsPerUnit;
        private TMP_Text _banner;
        private float _bannerTime = float.MaxValue; // 階の名前を出し始めてからの秒数

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
            _group = ResidentUIVisibility.GetOrAddGroup(gameObject);
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _pins = new MapPinIconPool(_pinTemplate);
            CreateBanner();
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        // 縮小表示の切り替えキーは、プレイヤーの入力の受け口から受け取る(Player マップのときだけ届く)
        private void OnZoomPressed()
        {
            if (_shown == true)
                _wide = !_wide;
        }

        // 階が替わったときに出す、階の名前の札(最初は透明)。枠は円形に切り抜かれるので、枠の外(この画面の直下)に置く。
        private void CreateBanner()
        {
            var go = new GameObject("FloorBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            _banner = go.GetComponent<TextMeshProUGUI>();
            if (_floorLabel != null)
                _banner.font = _floorLabel.font;
            _banner.fontSize = BannerFontSize;
            _banner.alignment = TextAlignmentOptions.Center;
            _banner.textWrappingMode = TextWrappingModes.NoWrap;
            _banner.raycastTarget = false;
            _banner.alpha = 0f;
            _banner.rectTransform.sizeDelta = new Vector2(360f, 40f);
        }

        // 出し始めからの時間で、すっと出て、しばらく止まり、すっと消える。枠の位置に合わせて置く。
        private void UpdateBanner()
        {
            if (_bannerTime >= _bannerSeconds)
                return;
            _bannerTime += Time.unscaledDeltaTime;
            var alpha = Mathf.Min(
                Mathf.Clamp01(_bannerTime / BannerFade),
                Mathf.Clamp01((_bannerSeconds - _bannerTime) / BannerFade)
            );
            _banner.alpha = alpha;

            // 画面の上半分に枠があれば下に、下半分なら上に出す(画面からはみ出さないように)
            var eventCamera =
                _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? _rootCanvas.worldCamera
                    : null;
            var below =
                RectTransformUtility.WorldToScreenPoint(eventCamera, _frame.position).y
                > Screen.height * 0.5f;
            var rect = _frame.rect;
            var y = below ? rect.yMin - BannerGap : rect.yMax + BannerGap;
            _banner.rectTransform.position = _frame.TransformPoint(
                new Vector3(rect.center.x, y, 0f)
            );
        }

        private void UpdateZoom()
        {
            var target = _wide ? _wideScale : 1f;
            _zoomScale = Mathf.Lerp(
                _zoomScale,
                target,
                1f - Mathf.Exp(-_zoomSpeed * Time.unscaledDeltaTime)
            );
            _scaledPixelsPerUnit = _pixelsPerUnit * _zoomScale;
        }

        private void Bind(PlayerController player)
        {
            if (_uiInput != null)
                _uiInput.MinimapZoomPressed -= OnZoomPressed;
            _uiInput = player != null ? player.GetComponent<PlayerUIInput>() : null;
            if (_uiInput != null)
                _uiInput.MinimapZoomPressed += OnZoomPressed;
            _player = player != null ? player.transform : null;
        }

        private void LateUpdate()
        {
            SetShown(
                _layout != null && _player != null && GameModeManager.Current == GameMode.Field
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
            UpdateZoom();
            UpdateBanner();

            var viewYaw =
                _rotateWithCamera && _camera != null ? _camera.transform.eulerAngles.y : 0f;
            _viewRotation = Quaternion.Euler(0f, 0f, viewYaw);

            _rotator.localRotation = _viewRotation;
            _map.RectTransform.localScale = new Vector3(
                _scaledPixelsPerUnit,
                _scaledPixelsPerUnit,
                1f
            );
            _map.RectTransform.anchoredPosition = -_center * _scaledPixelsPerUnit;

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
            var previous = _levelIndex;
            _levelIndex = levelIndex;
            _drawn.Clear();
            var level = levelIndex >= 0 ? _layout.Levels[levelIndex] : null;
            // 最初の階の決定では出さず、階を移ったときだけ名前を出す
            if (previous >= 0 && level != null && _banner != null)
            {
                _banner.text = level.Title;
                _bannerTime = 0f;
            }
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
                _viewRotation * ((new Vector2(world.x, world.z) - _center) * _scaledPixelsPerUnit);
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
