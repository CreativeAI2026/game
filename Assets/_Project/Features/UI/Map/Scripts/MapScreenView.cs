using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 全画面マップ(M で開く)。北を上に固定し、1つの階の全体が画面に収まるように描いて、プレイヤーの現在地とマップピンを重ねる。
    /// 地図は動かさない。右の階ボタン(建物ごとに上の階から並ぶ)で別の階を見られる。
    /// ダブルクリックで閉じる(M・Esc では <see cref="UiRouter"/> 側が閉じる)。
    /// </summary>
    public sealed class MapScreenView
        : MonoBehaviour,
            IPointerClickHandler,
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

        private const float CursorSize = 22f; // 三角のカーソルの大きさ(px)
        private const float CursorGap = 4f; // カーソルと、ずれたボタンの左端のすき間(px)

        /// <summary>階ボタン1つぶんの部品。</summary>
        private sealed class FloorButton
        {
            public Button Button;
            public RectTransform Slot; // レイアウトグループが並べる枠。ボタンはこの中で左右に動かす
            public int Level; // 階の番号
            public float Offset; // ボタンの今の横のずれ(px。左が負)
            public GameObject PinMark; // 「この階にピンがある」黄色い丸
            public GameObject HereMark; // 「プレイヤーが今いる階」の矢印
        }

        private readonly List<FloorButton> _floorButtons = new();
        private RectTransform _floorsRoot; // 階ボタンの枠を並べる親
        private RectTransform _cursor; // 選んでいる階の左に出す右向きの三角。階を替えると上下に動く
        private bool _snapFloorList; // 開いた直後は、動かさずに最終位置へ置く
        private bool[] _levelHasPin;
        private readonly MinimapLayout.Level[] _shown = new MinimapLayout.Level[1];
        private MapPinIconPool _pins;
        private Transform _player;
        private Graphic _playerGraphic;
        private int _levelIndex = -1;
        private Vector2 _center; // 画面の中心に来るワールドの XZ(階の範囲の中心)
        private float _pixelsPerUnit;

        private void Awake()
        {
            _playerGraphic = _playerIcon.GetComponent<Graphic>();
            _pins = new MapPinIconPool(_pinTemplate);
            if (_floorButtonTemplate != null)
                _floorButtonTemplate.gameObject.SetActive(false);
            CreateFloorButtons();
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
            var content = _map.RectTransform;
            content.localScale = new Vector3(_pixelsPerUnit, _pixelsPerUnit, 1f);
            content.anchoredPosition = -_center * _pixelsPerUnit;
            UpdatePlayer();
            _pins.Refresh(_layout, this, _otherFloorAlpha);
            UpdateFloorMarks();
            AnimateFloorList();
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

            FloorButton selected = null;
            foreach (var floor in _floorButtons)
            {
                var isSelected = floor.Level == _levelIndex;
                if (isSelected)
                    selected = floor;
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

        // ピンのある階のボタンに黄色い丸、プレイヤーがいる階のボタンに矢印の印を出す。
        private void UpdateFloorMarks()
        {
            if (_levelHasPin == null)
                return;
            MapPinIconPool.FindLevelsWithPins(_layout, _levelHasPin);
            var playerLevel = _player != null ? _layout.FindLevel(_player.position) : -1;
            foreach (var button in _floorButtons)
            {
                button.PinMark.SetActive(_levelHasPin[button.Level]);
                button.HereMark.SetActive(button.Level == playerLevel);
            }
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

            foreach (var index in order)
            {
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
                button.GetComponentInChildren<TMP_Text>().text = _layout.Levels[index].Title;
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
            _levelIndex = index;
            var level = index >= 0 ? _layout.Levels[index] : null;
            _shown[0] = level;
            _map.Show(level != null ? _shown : null);
            if (level == null)
                return;

            Fit();
            if (_floorTitle != null)
                _floorTitle.text = level.Title;

            foreach (var floor in _floorButtons)
            {
                var selected = floor.Level == index;
                floor.Button.image.color = selected ? _floorButtonSelectedColor : _floorButtonColor;
            }
        }

        /// <summary>今の階の全体が表示範囲に収まるよう、中心と拡大率を決める。</summary>
        private void Fit()
        {
            var bounds = _layout.Levels[_levelIndex].Bounds;
            var size = _viewport.rect.size;
            _center = bounds.center;
            _pixelsPerUnit = Mathf.Min(size.x / bounds.width, size.y / bounds.height) * _fitRatio;
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
