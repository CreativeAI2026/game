using System.Collections.Generic;
using CreativeAI.Core;
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
        private MinimapShapeGraphic _map;

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

        private readonly List<Button> _floorButtons = new();
        private readonly List<int> _buttonLevels = new(); // ボタンごとの階の番号
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
            var content = _map.rectTransform;
            content.localScale = new Vector3(_pixelsPerUnit, _pixelsPerUnit, 1f);
            content.anchoredPosition = -_center * _pixelsPerUnit;
            UpdatePlayer();
            _pins.Refresh(_layout, this, _otherFloorAlpha);
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

            foreach (var index in order)
            {
                var button = Instantiate(
                    _floorButtonTemplate,
                    _floorButtonTemplate.transform.parent,
                    false
                );
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>().text = _layout.Levels[index].Title;
                button.onClick.AddListener(() => ShowLevel(index));
                _floorButtons.Add(button);
                _buttonLevels.Add(index);
            }
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

            for (var i = 0; i < _floorButtons.Count; i++)
                _floorButtons[i].image.color =
                    _buttonLevels[i] == index ? _floorButtonSelectedColor : _floorButtonColor;
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
