using System.Collections.Generic;
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
    /// 右の階ボタン(建物ごとに上の階から並ぶ)や上下キーで別の階を見られ、「現在地へ」でいる階と全体表示に戻る。
    /// ホイールで拡大・縮小、ドラッグで移動でき、ピンにマウスを乗せると階と距離を出す。
    /// ダブルクリックで閉じる(M・Esc では <see cref="UiRouter"/> 側が閉じる)。
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
        private const float TooltipFontSize = 22f;
        private const float HoverBrighten = 0.12f; // マウスを乗せたとき、ボタンを白へ寄せる割合
        private const float CursorSize = 22f; // 三角のカーソルの大きさ(px)
        private const float CursorGap = 4f; // カーソルと、ずれたボタンの左端のすき間(px)

        /// <summary>階ボタン1つぶんの部品。</summary>
        private sealed class FloorButton
        {
            public Button Button;
            public RectTransform Slot; // レイアウトグループが並べる枠。ボタンはこの中で左右に動かす
            public int Level; // 階の番号
            public float Offset; // ボタンの今の横のずれ(px。左が負)
            public float Hover; // マウスを乗せている度合い(0〜1。なめらかに変わる)
            public GameObject PinMark; // 「この階にピンがある」黄色い丸
            public GameObject HereMark; // 「プレイヤーが今いる階」の矢印
        }

        private readonly List<FloorButton> _floorButtons = new();
        private RectTransform _floorsRoot; // 階ボタンの枠を並べる親
        private Canvas _rootCanvas; // マウスの位置を、階ボタンの上かどうか調べるのに使う
        private RectTransform _cursor; // 選んでいる階の左に出す右向きの三角。階を替えると上下に動く
        private bool _snapFloorList; // 開いた直後は、動かさずに最終位置へ置く
        private bool[] _levelHasPin;
        private readonly MinimapLayout.Level[] _shown = new MinimapLayout.Level[1];
        private MapPinIconPool _pins;
        private Transform _player;
        private Graphic _playerGraphic;
        private int _levelIndex = -1;
        private Vector2 _center; // 画面の中心に来るワールドの XZ(今見ている範囲の中心)
        private float _pixelsPerUnit;
        private Vector2 _baseCenter; // 階全体を収めるときの中心
        private float _basePixelsPerUnit; // 階全体を収めるときの拡大率
        private float _zoom = 1f; // 階全体を収めた大きさを 1 とした倍率
        private Vector2 _pan; // 階全体の中心から、見ている中心へのずれ(ワールドの XZ)
        private bool _dragging;
        private CanvasGroup _viewGroup; // 階を替えたときのフェードに使う
        private float _fadeTime;
        private Button _returnButton; // 「現在地へ」
        private RectTransform _tooltip; // ピンにマウスを乗せたときの説明
        private TMP_Text _tooltipText;
        private long _tooltipKey = -1; // 説明の文字を作り直すかの判定(階と距離)

        private void Awake()
        {
            _playerGraphic = _playerIcon.GetComponent<Graphic>();
            var canvas = GetComponentInParent<Canvas>();
            _rootCanvas = canvas != null ? canvas.rootCanvas : null;
            _pins = new MapPinIconPool(_pinTemplate);
            _viewGroup = _viewport.GetComponent<CanvasGroup>();
            if (_viewGroup == null)
                _viewGroup = _viewport.gameObject.AddComponent<CanvasGroup>();
            if (_floorButtonTemplate != null)
                _floorButtonTemplate.gameObject.SetActive(false);
            CreateFloorButtons();
            CreateTooltip();
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
            _snapFloorList = true;

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
            content.localScale = new Vector3(_pixelsPerUnit, _pixelsPerUnit, 1f);
            content.anchoredPosition = -_center * _pixelsPerUnit;
            UpdatePlayer();
            _pins.Refresh(_layout, this, _otherFloorAlpha);
            UpdateFloorMarks();
            AnimateFloorList();
            UpdateFade();
            UpdatePinTooltip();
        }

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
            ZoomAt(pointer, scroll > 0f ? _zoomStep : 1f / _zoomStep);
        }

        private void ZoomAt(Vector2 screenPoint, float factor)
        {
            var zoom = Mathf.Clamp(_zoom * factor, 1f, _maxZoom);
            if (Mathf.Approximately(zoom, _zoom))
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _viewport,
                screenPoint,
                EventCamera(),
                out var local
            );
            local -= _viewport.rect.center; // 表示範囲の中心からの位置にそろえる
            var world = _center + local / _pixelsPerUnit; // マウスの下にある地図の点
            _zoom = zoom;
            _pixelsPerUnit = _basePixelsPerUnit * _zoom;
            _pan = world - local / _pixelsPerUnit - _baseCenter; // その点が同じ場所に残るように
            ClampPan();
            _center = _baseCenter + _pan;
        }

        // 見ている中心が、階の範囲から出ないようにする。全体表示のときは動かさない。
        private void ClampPan()
        {
            if (_zoom <= 1.001f)
            {
                _pan = Vector2.zero;
                return;
            }
            var half = _layout.Levels[_levelIndex].Bounds.size * 0.5f;
            _pan = new Vector2(
                Mathf.Clamp(_pan.x, -half.x, half.x),
                Mathf.Clamp(_pan.y, -half.y, half.y)
            );
        }

        private void ResetView()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
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
            _pan -= eventData.delta / scale / _pixelsPerUnit;
            ClampPan();
        }

        // 選んでいる階のボタンを左へずらし、三角のカーソルをその階の高さへ動かす(どちらもなめらかに)。
        // マップを開いている間はゲームの時間が止まっていることがあるので、止まらない時間で動かす。
        private void AnimateFloorList()
        {
            if (_floorsRoot == null || _cursor == null)
                return;
            if (_snapFloorList)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_floorsRoot); // 枠の位置を先に確定する
            var blend = _snapFloorList ? 1f : 1f - Mathf.Exp(-_slideSpeed * Time.unscaledDeltaTime);

            var mouse = Mouse.current;
            var hasPointer = mouse != null;
            var pointer = hasPointer ? mouse.position.ReadValue() : Vector2.zero;
            var overList = hasPointer && IsPointerOver(_floorsRoot, pointer);
            HandleFloorInput(overList ? mouse.scroll.ReadValue().y : 0f);

            FloorButton selected = null;
            foreach (var floor in _floorButtons)
            {
                var isSelected = floor.Level == _levelIndex;
                if (isSelected)
                    selected = floor;
                var hovered = overList && IsPointerOver(floor.Slot, pointer);
                floor.Hover = Mathf.Lerp(floor.Hover, hovered ? 1f : 0f, blend);
                floor.Button.image.color = Color.Lerp(
                    isSelected ? _floorButtonSelectedColor : _floorButtonColor,
                    Color.white,
                    HoverBrighten * floor.Hover
                );
                floor.Offset = Mathf.Lerp(floor.Offset, isSelected ? -_selectedPopOut : 0f, blend);
                ((RectTransform)floor.Button.transform).anchoredPosition = new Vector2(
                    floor.Offset,
                    0f
                );
            }

            _cursor.gameObject.SetActive(selected != null);
            if (selected == null)
                return;
            var slot = selected.Slot;
            var left = _floorsRoot.InverseTransformPoint(
                slot.TransformPoint(new Vector3(slot.rect.xMin, slot.rect.center.y, 0f))
            );
            var x = left.x - _selectedPopOut - CursorGap - CursorSize * 0.5f;
            var y = _snapFloorList ? left.y : Mathf.Lerp(_cursor.localPosition.y, left.y, blend);
            _cursor.localPosition = new Vector3(x, y, 0f);
            _snapFloorList = false;
        }

        // Overlay のキャンバスはカメラを使わない
        private Camera EventCamera() =>
            _rootCanvas != null && _rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _rootCanvas.worldCamera
                : null;

        private bool IsPointerOver(RectTransform rect, Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, EventCamera());

        private bool IsOverFloorList(Vector2 screenPoint) =>
            _floorsRoot != null && IsPointerOver(_floorsRoot, screenPoint);

        // 上下キー(ゲームパッドの十字・スティックも)と、階ボタンの上でのホイールで、階を1つずつ替える。
        // 一覧は上の階が上なので、上へ動かすと一覧の前の階になる。
        private void HandleFloorInput(float scroll)
        {
            var step = 0;
            if (UiInputActionMap.TryReadDiscreteMove(out var move) && move.y != 0)
                step = -move.y;
            if (scroll != 0f)
                step = scroll > 0f ? -1 : 1;
            if (step == 0)
                return;

            var current = _floorButtons.FindIndex(floor => floor.Level == _levelIndex);
            if (current < 0)
                return;
            var next = Mathf.Clamp(current + step, 0, _floorButtons.Count - 1);
            if (next != current)
                ShowLevel(_floorButtons[next].Level);
        }

        // ピンのある階のボタンに黄色い丸、プレイヤーがいる階のボタンに矢印の印を出す。
        private void UpdateFloorMarks()
        {
            if (_levelHasPin == null)
                return;
            _pins.MarkLevelsWithPins(_levelHasPin);
            var playerLevel = _player != null ? _layout.FindLevel(_player.position) : -1;
            foreach (var button in _floorButtons)
            {
                button.PinMark.SetActive(_levelHasPin[button.Level]);
                button.HereMark.SetActive(button.Level == playerLevel);
            }
            // いる階を見ていて、全体表示のままなら、戻る先が無い
            if (_returnButton != null)
                _returnButton.interactable =
                    playerLevel >= 0 && (playerLevel != _levelIndex || _zoom > 1.001f);
        }

        // 今いる階を、全体表示で見せる。
        private void ReturnToPlayer()
        {
            var playerLevel = _player != null ? _layout.FindLevel(_player.position) : -1;
            if (playerLevel < 0)
                return;
            if (playerLevel != _levelIndex)
                ShowLevel(playerLevel);
            else
                ResetView();
        }

        /// <summary>ダブルクリックで閉じる。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
                gameObject.SetActive(false); // UiRouter はパネルのオン・オフの通知を受けるので、ここで閉じてよい
        }

        private void CreateFloorButtons()
        {
            if (_layout == null || _floorButtonTemplate == null)
                return;
            _levelHasPin = new bool[_layout.Levels.Count];
            // 建物ごと(図の順)に、上の階を上に並べる
            var order = new List<int>();
            for (var i = 0; i < _layout.Levels.Count; i++)
                order.Add(i);
            order.Sort(
                (a, b) =>
                {
                    var levels = _layout.Levels;
                    var byBuilding = FirstIndexOf(levels[a].BuildingId)
                        .CompareTo(FirstIndexOf(levels[b].BuildingId));
                    return byBuilding != 0
                        ? byBuilding
                        : levels[b].Height.CompareTo(levels[a].Height);
                }
            );

            _floorsRoot = (RectTransform)_floorButtonTemplate.transform.parent;
            var templateLayout = _floorButtonTemplate.GetComponent<LayoutElement>();
            var slotHeight = templateLayout != null ? templateLayout.preferredHeight : 60f;

            string previousBuilding = null;
            foreach (var index in order)
            {
                var level = _layout.Levels[index];
                if (previousBuilding != null && level.BuildingId != previousBuilding)
                    CreateBuildingDivider();
                previousBuilding = level.BuildingId;

                // レイアウトグループが並べるのは枠で、ボタンは枠の中を左右に動かす(ボタン自体を動かすと並べ直しと競合する)
                var slotObject = new GameObject(
                    "FloorSlot",
                    typeof(RectTransform),
                    typeof(LayoutElement)
                );
                var slot = (RectTransform)slotObject.transform;
                slot.SetParent(_floorsRoot, false);
                slotObject.GetComponent<LayoutElement>().preferredHeight = slotHeight;

                var button = Instantiate(_floorButtonTemplate, slot, false);
                button.gameObject.SetActive(true);
                var buttonRect = (RectTransform)button.transform;
                buttonRect.anchorMin = Vector2.zero;
                buttonRect.anchorMax = Vector2.one;
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;
                button.GetComponentInChildren<TMP_Text>().text = FloorButtonLabel(level);
                button.onClick.AddListener(() => ShowLevel(index));
                _floorButtons.Add(
                    new FloorButton
                    {
                        Button = button,
                        Slot = slot,
                        Level = index,
                        // 右端に、ピンと同じ色の黄色い丸。その左に、プレイヤーと同じ色の矢印
                        PinMark = CreateIcon(
                            button.transform,
                            "PinMark",
                            MinimapIconGraphic.IconShape.Dot,
                            _pinTemplate.color,
                            1f,
                            -20f,
                            24f
                        ),
                        HereMark = CreateIcon(
                            button.transform,
                            "HereMark",
                            MinimapIconGraphic.IconShape.Arrow,
                            _playerGraphic.color,
                            1f,
                            -47f,
                            22f
                        ),
                    }
                );
            }

            // 階の並びの下に、「現在地へ」のボタン(階ボタンと同じ見た目。カーソルや上下キーの対象にはしない)
            CreateBuildingDivider();
            var returnSlot = new GameObject(
                "ReturnSlot",
                typeof(RectTransform),
                typeof(LayoutElement)
            );
            returnSlot.transform.SetParent(_floorsRoot, false);
            returnSlot.GetComponent<LayoutElement>().preferredHeight = slotHeight;
            _returnButton = Instantiate(_floorButtonTemplate, returnSlot.transform, false);
            _returnButton.gameObject.SetActive(true);
            var returnRect = (RectTransform)_returnButton.transform;
            returnRect.anchorMin = Vector2.zero;
            returnRect.anchorMax = Vector2.one;
            returnRect.offsetMin = Vector2.zero;
            returnRect.offsetMax = Vector2.zero;
            _returnButton.image.color = _floorButtonColor;
            _returnButton.GetComponentInChildren<TMP_Text>().text = "現在地へ";
            _returnButton.onClick.AddListener(ReturnToPlayer);

            // カーソルはレイアウトの対象にしない(並べ方に含めると、ボタンの間に空きができる)
            _cursor = (RectTransform)
                CreateIcon(
                    _floorsRoot,
                    "Cursor",
                    MinimapIconGraphic.IconShape.Arrow,
                    _cursorColor,
                    0.5f,
                    0f,
                    CursorSize,
                    -90f // 矢じりは上向きなので、右向きに回す
                ).transform;
            _cursor.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        // ピンにマウスを乗せたときの説明の枠を作る(最初は隠す)。地図の表示範囲で切り抜かれないよう、この画面の直下に置く。
        private void CreateTooltip()
        {
            var go = new GameObject("PinTooltip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var background = go.GetComponent<Image>();
            background.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);
            background.raycastTarget = false;
            _tooltip = (RectTransform)go.transform;
            _tooltip.pivot = new Vector2(0f, 1f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(go.transform, false);
            _tooltipText = textObject.GetComponent<TextMeshProUGUI>();
            if (_floorTitle != null)
                _tooltipText.font = _floorTitle.font;
            _tooltipText.fontSize = TooltipFontSize;
            _tooltipText.color = Color.white;
            _tooltipText.raycastTarget = false;
            _tooltipText.textWrappingMode = TextWrappingModes.NoWrap;
            var textRect = _tooltipText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            go.SetActive(false);
        }

        // ピンの記号の近くにマウスがあれば、そのピンのある階と、現在地からの距離を出す。
        private void UpdatePinTooltip()
        {
            var mouse = Mouse.current;
            if (_tooltip != null && mouse != null)
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
                    ShowTooltip(pointer, anchor, level);
                    return;
                }
            }
            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }

        private void ShowTooltip(Vector2 pointer, Vector3 anchor, int level)
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
            // 階か距離が替わったときだけ、文字を作り直す
            var key = level * 100000L + meters;
            if (key != _tooltipKey || !_tooltip.gameObject.activeSelf)
            {
                _tooltipKey = key;
                var title = level >= 0 ? _layout.Levels[level].Title : "ピン";
                _tooltipText.text = meters >= 0 ? $"{title}\n現在地から {meters}m" : title;
                _tooltip.gameObject.SetActive(true);
                _tooltip.SetAsLastSibling(); // 出すときに1度だけ、他の部品より手前へ
                var size = _tooltipText.GetPreferredValues(_tooltipText.text);
                _tooltip.sizeDelta = new Vector2(size.x + 24f, size.y + 12f);
            }

            var area = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area,
                pointer,
                EventCamera(),
                out var local
            );
            // マウスの右下に出し、画面の端からはみ出すなら内側へ寄せる
            var rect = area.rect;
            var position = local + new Vector2(16f, -16f);
            position.x = Mathf.Min(position.x, rect.xMax - _tooltip.sizeDelta.x);
            position.y = Mathf.Max(position.y, rect.yMin + _tooltip.sizeDelta.y);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            _tooltip.anchoredPosition = position - rect.center; // 中心アンカーからの位置に直す
        }

        // 階の記号(3F など)を大きく、建物の名前を小さくして並べる(リッチテキスト)。
        private static string FloorButtonLabel(MinimapLayout.Level level) =>
            string.IsNullOrEmpty(level.Building)
                ? $"<size=125%>{level.Label}</size>"
                : $"<size=70%>{level.Building}</size> <size=125%>{level.Label}</size>";

        // 建物が替わるところに、空きと細い線を入れる。レイアウトグループが並べるので、高さだけ持つ枠にする。
        private void CreateBuildingDivider()
        {
            var go = new GameObject(
                "BuildingDivider",
                typeof(RectTransform),
                typeof(LayoutElement)
            );
            go.GetComponent<LayoutElement>().preferredHeight = _buildingGap;
            go.transform.SetParent(_floorsRoot, false);

            var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            var image = line.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.18f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.SetParent(go.transform, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-24f, 2f); // 左右に 12px ずつ余白
        }

        // 親の子に、小さな記号を置く(最初は隠す)。
        // anchorX は基準にする端(0 = 左端、1 = 右端)、centerX はその端から記号の中心までの距離(px)。
        private static GameObject CreateIcon(
            Transform parent,
            string name,
            MinimapIconGraphic.IconShape shape,
            Color color,
            float anchorX,
            float centerX,
            float size,
            float rotation = 0f
        )
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(MinimapIconGraphic));
            var icon = go.GetComponent<MinimapIconGraphic>();
            icon.SetShape(shape);
            icon.color = color;
            icon.raycastTarget = false;
            var rect = icon.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f); // 回しても中心が動かないようにする
            rect.anchoredPosition = new Vector2(centerX, 0f);
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.SetActive(false);
            return go;
        }

        private int FirstIndexOf(string buildingId)
        {
            for (var i = 0; i < _layout.Levels.Count; i++)
                if (_layout.Levels[i].BuildingId == buildingId)
                    return i;
            return -1;
        }

        private void ShowLevel(int index)
        {
            var opening = _snapFloorList; // 開いた直後は、拡大を戻すだけでフェードしない
            if (index == _levelIndex && !opening)
                return;
            _levelIndex = index;
            ResetView();
            _fadeTime = opening ? _fadeSeconds : 0f;
            if (opening)
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
        private void Fit()
        {
            var bounds = _layout.Levels[_levelIndex].Bounds;
            var size = _viewport.rect.size;
            _baseCenter = bounds.center;
            _basePixelsPerUnit =
                Mathf.Min(size.x / bounds.width, size.y / bounds.height) * _fitRatio;
            _center = _baseCenter + _pan;
            _pixelsPerUnit = _basePixelsPerUnit * _zoom;
        }

        private Vector2 ToViewport(Vector3 world) =>
            (new Vector2(world.x, world.z) - _center) * _pixelsPerUnit;

        private void UpdatePlayer()
        {
            if (_player == null)
            {
                _playerIcon.gameObject.SetActive(false);
                return;
            }
            _playerIcon.gameObject.SetActive(true);
            var position = _player.position;
            _playerIcon.anchoredPosition = ToViewport(position);
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

        Vector2 MapPinIconPool.IPlacement.ToIconPosition(Vector3 world) => ToViewport(world);

        bool MapPinIconPool.IPlacement.IsShownLevel(int levelIndex) => levelIndex == _levelIndex;

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var tint = graphic.color;
            tint.a = alpha;
            graphic.color = tint;
        }
    }
}
