using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 全画面マップ(M で開く)。北を上に固定し、1つの階の全体が画面に収まるように描いて、プレイヤーの現在地とマップピンを重ねる。
    /// 右の階ボタン(建物ごとに上の階から並ぶ)や上下キーで別の階を見られる(階を替えるたびに全体表示に戻る)。
    /// ホイールで拡大・縮小、ドラッグで移動でき、ピンにマウスを乗せると階と距離を出す。
    /// ダブルクリックで閉じる(M・Esc では <see cref="UIRouter"/> 側が閉じる)。
    /// </summary>
    public sealed class MapScreenView
        : MonoBehaviour,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler,
            MapPinIconPool.IPlacement
    {
        [SerializeField]
        private MinimapLayout _layout;

        [SerializeField, Tooltip("地図を見せる範囲(RectMask2D 付き)")]
        private RectTransform _viewport;

        [SerializeField]
        private MinimapMap _map;

        [SerializeField]
        private RectTransform _playerIcon;

        [SerializeField, Tooltip("ピンの記号の型。実行時に複製して使う")]
        private MinimapIconGraphic _pinTemplate;

        [SerializeField, Tooltip("階ボタンの型。実行時に階の数だけ複製する")]
        private Button _floorButtonTemplate;

        [SerializeField]
        private TMP_Text _floorTitle;

        [SerializeField, Range(0.5f, 1f), Tooltip("階全体を収めるとき、表示範囲のどれだけを使うか")]
        private float _fitRatio = 0.94f;

        [SerializeField, Range(0f, 1f), Tooltip("別の階を見ているときのプレイヤーの不透明度")]
        private float _otherFloorAlpha = 0.35f;

        [SerializeField]
        private Color _floorButtonColor = new(0.12f, 0.15f, 0.2f, 0.95f);

        [SerializeField]
        private Color _floorButtonSelectedColor = new(0.2f, 0.42f, 0.66f, 1f);

        [SerializeField, Tooltip("選んでいる階のボタンを、左へずらす幅(px)")]
        private float _selectedPopOut = 14f;

        [SerializeField, Tooltip("選んでいる階の左に出す、三角のカーソルの色")]
        private Color _cursorColor = new(1f, 0.82f, 0.25f, 1f);

        [SerializeField, Tooltip("ボタンのスライドとカーソルの上下の速さ(大きいほど速い)")]
        private float _slideSpeed = 16f;

        [SerializeField, Tooltip("建物が替わるところに足す、区切りの高さ(px)")]
        private float _buildingGap = 20f;

        [
            SerializeField,
            Range(1.5f, 10f),
            Tooltip("拡大できる上限(階全体が収まる大きさを 1 とした倍率)")
        ]
        private float _maxZoom = 5f;

        [SerializeField, Range(1.05f, 2f), Tooltip("ホイールを1目盛り回したときの拡大・縮小の倍率")]
        private float _zoomStep = 1.25f;

        [
            SerializeField,
            Tooltip("階を替えたとき、地図が薄い状態から戻るまでの秒数(0 でフェードしない)")
        ]
        private float _fadeSeconds = 0.18f;

        [SerializeField, Range(0f, 1f), Tooltip("階を替えた直後の、地図の不透明度")]
        private float _fadeStartAlpha = 0.15f;

        private const float PinHoverRadius = 18f; // ピンの記号にマウスが近いと見なす距離(px)

        private readonly MinimapLayout.Level[] _shown = new MinimapLayout.Level[1];
        private Canvas _rootCanvas; // マウスの位置を、階ボタンの上かどうか調べるのに使う
        private MapPanZoom _view;
        private MapFloorList _floorList; // 階ボタンが作れないときは null
        private MapPinTooltip _tooltip;
        private MapPinIconPool _pins;
        private Transform _player;
        private Graphic _playerGraphic;
        private int _levelIndex = -1;
        private bool _opening; // 開いた直後(拡大を戻すだけでフェードしない)
        private bool _dragging;
        private CanvasGroup _viewGroup; // 階を替えたときのフェードに使う
        private float _fadeTime;

        private void Awake()
        {
            _playerGraphic = _playerIcon.GetComponent<Graphic>();
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _view = new MapPanZoom(_fitRatio, _maxZoom);
            _pins = new MapPinIconPool(_pinTemplate);
            _viewGroup = _viewport.GetComponent<CanvasGroup>();
            if (_viewGroup == null)
                _viewGroup = _viewport.gameObject.AddComponent<CanvasGroup>();
            if (_floorButtonTemplate != null)
            {
                _floorButtonTemplate.gameObject.SetActive(false);
                if (_layout != null)
                    _floorList = new MapFloorList(
                        _layout,
                        _floorButtonTemplate,
                        new MapFloorList.Style(
                            _floorButtonColor,
                            _floorButtonSelectedColor,
                            _cursorColor,
                            _pinTemplate.color,
                            _playerGraphic.color,
                            _selectedPopOut,
                            _slideSpeed,
                            _buildingGap
                        ),
                        IsPointerOver,
                        ShowLevel
                    );
            }
            _tooltip = new MapPinTooltip(
                (RectTransform)transform,
                _floorTitle != null ? _floorTitle.font : null
            );
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
            _opening = true;
            _floorList?.SnapNextUpdate();

            // 開くたびに、いる階を出す
            if (_layout == null)
                ShowLevel(-1);
            else if (_player != null)
                ShowLevel(_layout.FindLevel(_player.position));
            else
                ShowLevel(_layout.Levels.Count > 0 ? 0 : -1);
        }

        private void OnDisable() => PlayerRegistry.Changed -= Bind;

        private void Bind(PlayerController player) =>
            _player = player != null ? player.transform : null;

        private void LateUpdate()
        {
            if (_levelIndex < 0)
                return;
            Fit(); // 画面の大きさが変わっても収まるよう毎回合わせる(計算だけ)
            HandleZoomInput();
            var content = _map.RectTransform;
            content.localScale = new Vector3(_view.PixelsPerUnit, _view.PixelsPerUnit, 1f);
            content.anchoredPosition = -_view.Center * _view.PixelsPerUnit;
            UpdatePlayer();
            _pins.Refresh(_layout, this, _otherFloorAlpha);
            if (_floorList != null)
            {
                _floorList.UpdateMarks(_pins, PlayerLevel);
                _floorList.Animate(_levelIndex);
            }
            UpdateFade();
            UpdatePinTooltip();
            _opening = false;
        }

        private int PlayerLevel => _player != null ? _layout.FindLevel(_player.position) : -1;

        // 階を替えた直後は地図を薄くして、元の濃さへ戻す。止まらない時間で動かす。
        private void UpdateFade()
        {
            if (_fadeTime >= _fadeSeconds)
                return; // 戻りきっている間は何もしない
            _fadeTime = Mathf.Min(_fadeTime + Time.unscaledDeltaTime, _fadeSeconds);
            var t = _fadeSeconds > 0f ? _fadeTime / _fadeSeconds : 1f;
            _viewGroup.alpha = Mathf.Lerp(_fadeStartAlpha, 1f, t);
        }

        // 地図の上でホイールを回すと、マウスの位置を動かさないまま拡大・縮小する(階ボタンの上のホイールは階の切り替え)。
        private void HandleZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var scroll = mouse.scroll.ReadValue().y;
            if (scroll == 0f)
                return;
            var pointer = mouse.position.ReadValue();
            if (IsOverFloorList(pointer) || !IsPointerOver(_viewport, pointer))
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                pointer,
                EventCamera(),
                out var local
            );
            // 表示範囲の中心からの位置にそろえる
            _view.ZoomAt(local - _viewport.rect.center, scroll > 0f ? _zoomStep : 1f / _zoomStep);
        }

        /// <summary>左ボタンのドラッグで地図を動かす(階ボタンの上から始めたものは除く)。</summary>
        public void OnBeginDrag(PointerEventData eventData) =>
            _dragging =
                eventData.button == PointerEventData.InputButton.Left
                && !IsOverFloorList(eventData.position);

        /// <summary>ドラッグを終える。</summary>
        public void OnEndDrag(PointerEventData eventData) => _dragging = false;

        /// <summary>ドラッグした分だけ地図を動かす。</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _levelIndex < 0)
                return;
            var scale = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
            _view.Drag(eventData.delta / scale);
        }

        // Overlay のキャンバスはカメラを使わない
        private Camera EventCamera() =>
            _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;

        private bool IsPointerOver(RectTransform rect, Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, EventCamera());

        private bool IsOverFloorList(Vector2 screenPoint) =>
            _floorList != null && _floorList.IsOver(screenPoint);

        /// <summary>ダブルクリックで閉じる。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
                gameObject.SetActive(false); // UIRouter はパネルのオン・オフの通知を受けるので、ここで閉じてよい
        }

        // ピンの記号の近くにマウスがあれば、そのピンのある階と、現在地からの距離を出す。
        private void UpdatePinTooltip()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                if (
                    IsPointerOver(_viewport, pointer)
                    && !IsOverFloorList(pointer)
                    && _pins.TryFindPinAt(
                        pointer,
                        EventCamera(),
                        PinHoverRadius,
                        out var anchor,
                        out var level
                    )
                )
                {
                    var meters =
                        _player != null
                            ? Mathf.RoundToInt(
                                Vector2.Distance(
                                    new Vector2(_player.position.x, _player.position.z),
                                    new Vector2(anchor.x, anchor.z)
                                )
                            )
                            : -1;
                    var title = level >= 0 ? _layout.Levels[level].Title : "ピン";
                    _tooltip.Show(pointer, EventCamera(), title, meters, level);
                    return;
                }
            }
            _tooltip.Hide();
        }

        private void ShowLevel(int index)
        {
            if (index == _levelIndex && !_opening)
                return;
            _levelIndex = index;
            _view.Reset();
            _fadeTime = _opening ? _fadeSeconds : 0f;
            if (_opening)
                _viewGroup.alpha = 1f; // 前回の途中で閉じても、薄いまま開かない
            var level = index >= 0 ? _layout.Levels[index] : null;
            _shown[0] = level;
            _map.Show(level != null ? _shown : null);
            if (level == null)
                return;

            Fit();
            if (_floorTitle != null)
                _floorTitle.text = level.Title;
        }

        /// <summary>今の階の全体が表示範囲に収まる大きさを基準に、拡大とずれを重ねて、中心と拡大率を決める。</summary>
        private void Fit() => _view.Fit(_layout.Levels[_levelIndex].Bounds, _viewport.rect.size);

        private void UpdatePlayer()
        {
            if (_player == null)
            {
                _playerIcon.gameObject.SetActive(false);
                return;
            }
            _playerIcon.gameObject.SetActive(true);
            var position = _player.position;
            _playerIcon.anchoredPosition = _view.ToViewport(position);
            var forward = _player.forward;
            _playerIcon.localRotation = Quaternion.Euler(
                0f,
                0f,
                -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
            );
            SetAlpha(
                _playerGraphic,
                _layout.FindLevel(position) == _levelIndex ? 1f : _otherFloorAlpha
            );
        }

        Vector2 MapPinIconPool.IPlacement.ToIconPosition(Vector3 world) => _view.ToViewport(world);

        bool MapPinIconPool.IPlacement.IsShownLevel(int levelIndex) => levelIndex == _levelIndex;

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var tint = graphic.color;
            tint.a = alpha;
            graphic.color = tint;
        }
    }
}
