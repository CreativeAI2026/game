using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 全画面マップの右に並べる階ボタン(建物ごとに上の階から)。選んでいる階のボタンを左へずらして三角のカーソルを添え、
    /// ピンのある階・プレイヤーのいる階に印を出す。ボタン・上下キー・ボタンの上のホイールで階を選ばせる。
    /// </summary>
    internal sealed class MapFloorList
    {
        private const float HoverBrighten = 0.12f; // マウスを乗せたとき、ボタンを白へ寄せる割合
        private const float CursorSize = 22f; // 三角のカーソルの大きさ(px)
        private const float CursorGap = 4f; // カーソルと、ずれたボタンの左端のすき間(px)

        /// <summary>見た目の設定。</summary>
        public readonly struct Style
        {
            public readonly Color ButtonColor;
            public readonly Color SelectedColor;
            public readonly Color CursorColor;
            public readonly Color PinColor;
            public readonly Color HereColor;
            public readonly float SelectedPopOut; // 選んでいる階のボタンを、左へずらす幅(px)
            public readonly float SlideSpeed; // ボタンのスライドとカーソルの上下の速さ
            public readonly float BuildingGap; // 建物が替わるところに足す、区切りの高さ(px)

            public Style(
                Color buttonColor,
                Color selectedColor,
                Color cursorColor,
                Color pinColor,
                Color hereColor,
                float selectedPopOut,
                float slideSpeed,
                float buildingGap
            )
            {
                ButtonColor = buttonColor;
                SelectedColor = selectedColor;
                CursorColor = cursorColor;
                PinColor = pinColor;
                HereColor = hereColor;
                SelectedPopOut = selectedPopOut;
                SlideSpeed = slideSpeed;
                BuildingGap = buildingGap;
            }
        }

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

        private readonly List<FloorButton> _buttons = new();
        private readonly Style _style;
        private readonly Func<RectTransform, Vector2, bool> _isPointerOver;
        private readonly Action<int> _select;
        private readonly bool[] _levelHasPin;
        private readonly RectTransform _root; // 階ボタンの枠を並べる親
        private readonly RectTransform _cursor; // 選んでいる階の左に出す右向きの三角
        private bool _snap; // 開いた直後は、動かさずに最終位置へ置く

        /// <param name="template">階ボタンの型。親が並べる枠になる</param>
        /// <param name="isPointerOver">画面上の点が、その範囲の上にあるか</param>
        /// <param name="select">階を選んだときに呼ぶ(階の番号)</param>
        public MapFloorList(
            MinimapLayout layout,
            Button template,
            Style style,
            Func<RectTransform, Vector2, bool> isPointerOver,
            Action<int> select
        )
        {
            _style = style;
            _isPointerOver = isPointerOver;
            _select = select;
            _levelHasPin = new bool[layout.Levels.Count];
            _root = (RectTransform)template.transform.parent;

            var templateLayout = template.GetComponent<LayoutElement>();
            var slotHeight = templateLayout != null ? templateLayout.preferredHeight : 60f;
            string previousBuilding = null;
            foreach (var index in SortedLevels(layout))
            {
                var level = layout.Levels[index];
                if (previousBuilding != null && level.BuildingId != previousBuilding)
                    CreateBuildingDivider();
                previousBuilding = level.BuildingId;
                _buttons.Add(CreateButton(template, slotHeight, level, index));
            }

            // カーソルはレイアウトの対象にしない(並べ方に含めると、ボタンの間に空きができる)
            _cursor = (RectTransform)
                CreateIcon(
                    _root,
                    "Cursor",
                    MinimapIconGraphic.IconShape.Arrow,
                    style.CursorColor,
                    0.5f,
                    0f,
                    CursorSize,
                    -90f // 矢じりは上向きなので、右向きに回す
                ).transform;
            _cursor.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        /// <summary>次の更新で、アニメーションせずに最終位置へ置く(画面を開いたとき)。</summary>
        public void SnapNextUpdate() => _snap = true;

        /// <summary>画面上の点が階ボタンの並びの上にあるか。</summary>
        public bool IsOver(Vector2 screenPoint) => _isPointerOver(_root, screenPoint);

        /// <summary>ピンのある階のボタンに黄色い丸、プレイヤーがいる階のボタンに矢印の印を出す。</summary>
        public void UpdateMarks(MapPinIconPool pins, int playerLevel)
        {
            pins.MarkLevelsWithPins(_levelHasPin);
            foreach (var button in _buttons)
            {
                button.PinMark.SetActive(_levelHasPin[button.Level]);
                button.HereMark.SetActive(button.Level == playerLevel);
            }
        }

        /// <summary>
        /// 選んでいる階のボタンを左へずらし、三角のカーソルをその階の高さへ動かす(どちらもなめらかに)。
        /// マップを開いている間はゲームの時間が止まっていることがあるので、止まらない時間で動かす。
        /// </summary>
        public void Animate(int shownLevel)
        {
            if (_snap)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_root); // 枠の位置を先に確定する
            var blend = _snap ? 1f : 1f - Mathf.Exp(-_style.SlideSpeed * Time.unscaledDeltaTime);

            var mouse = Mouse.current;
            var hasPointer = mouse != null;
            var pointer = hasPointer ? mouse.position.ReadValue() : Vector2.zero;
            var overList = hasPointer && IsOver(pointer);
            HandleInput(shownLevel, overList ? mouse.scroll.ReadValue().y : 0f);

            FloorButton selected = null;
            foreach (var floor in _buttons)
            {
                var isSelected = floor.Level == shownLevel;
                if (isSelected)
                    selected = floor;
                var hovered = overList && _isPointerOver(floor.Slot, pointer);
                floor.Hover = Mathf.Lerp(floor.Hover, hovered ? 1f : 0f, blend);
                floor.Button.image.color = Color.Lerp(
                    isSelected ? _style.SelectedColor : _style.ButtonColor,
                    Color.white,
                    HoverBrighten * floor.Hover
                );
                floor.Offset = Mathf.Lerp(
                    floor.Offset,
                    isSelected ? -_style.SelectedPopOut : 0f,
                    blend
                );
                ((RectTransform)floor.Button.transform).anchoredPosition = new Vector2(
                    floor.Offset,
                    0f
                );
            }

            _cursor.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                var slot = selected.Slot;
                var left = _root.InverseTransformPoint(
                    slot.TransformPoint(new Vector3(slot.rect.xMin, slot.rect.center.y, 0f))
                );
                var x = left.x - _style.SelectedPopOut - CursorGap - CursorSize * 0.5f;
                var y = _snap ? left.y : Mathf.Lerp(_cursor.localPosition.y, left.y, blend);
                _cursor.localPosition = new Vector3(x, y, 0f);
            }
            _snap = false;
        }

        // 上下キー(ゲームパッドの十字・スティックも)と、階ボタンの上でのホイールで、階を1つずつ替える。
        // 一覧は上の階が上なので、上へ動かすと一覧の前の階になる。
        private void HandleInput(int shownLevel, float scroll)
        {
            var step = 0;
            if (UIInputActionMap.TryReadDiscreteMove(out var move) && move.y != 0)
                step = -move.y;
            if (scroll != 0f)
                step = scroll > 0f ? -1 : 1;
            if (step == 0)
                return;

            var current = _buttons.FindIndex(floor => floor.Level == shownLevel);
            if (current < 0)
                return;
            var next = Mathf.Clamp(current + step, 0, _buttons.Count - 1);
            if (next != current)
                _select(_buttons[next].Level);
        }

        // 建物ごと(図の順)に、上の階を上に並べる。
        private static List<int> SortedLevels(MinimapLayout layout)
        {
            var levels = layout.Levels;
            int FirstIndexOf(string buildingId)
            {
                for (var i = 0; i < levels.Count; i++)
                    if (levels[i].BuildingId == buildingId)
                        return i;
                return -1;
            }

            var order = new List<int>();
            for (var i = 0; i < levels.Count; i++)
                order.Add(i);
            order.Sort(
                (a, b) =>
                {
                    var byBuilding = FirstIndexOf(levels[a].BuildingId)
                        .CompareTo(FirstIndexOf(levels[b].BuildingId));
                    return byBuilding != 0
                        ? byBuilding
                        : levels[b].Height.CompareTo(levels[a].Height);
                }
            );
            return order;
        }

        private FloorButton CreateButton(
            Button template,
            float slotHeight,
            MinimapLayout.Level level,
            int index
        )
        {
            // レイアウトグループが並べるのは枠で、ボタンは枠の中を左右に動かす(ボタン自体を動かすと並べ直しと競合する)
            var slotObject = new GameObject(
                "FloorSlot",
                typeof(RectTransform),
                typeof(LayoutElement)
            );
            var slot = (RectTransform)slotObject.transform;
            slot.SetParent(_root, false);
            slotObject.GetComponent<LayoutElement>().preferredHeight = slotHeight;

            var button = UnityEngine.Object.Instantiate(template, slot, false);
            button.gameObject.SetActive(true);
            var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            button.GetComponentInChildren<TMP_Text>().text = ButtonLabel(level);
            button.onClick.AddListener(() => _select(index));
            return new FloorButton
            {
                Button = button,
                Slot = slot,
                Level = index,
                // 右端に、ピンと同じ色の黄色い丸。その左に、プレイヤーと同じ色の矢印
                PinMark = CreateIcon(
                    button.transform,
                    "PinMark",
                    MinimapIconGraphic.IconShape.Dot,
                    _style.PinColor,
                    1f,
                    -20f,
                    24f
                ),
                HereMark = CreateIcon(
                    button.transform,
                    "HereMark",
                    MinimapIconGraphic.IconShape.Arrow,
                    _style.HereColor,
                    1f,
                    -47f,
                    22f
                ),
            };
        }

        // 階の記号(3F など)を大きく、建物の名前を小さくして並べる(リッチテキスト)。
        private static string ButtonLabel(MinimapLayout.Level level) =>
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
            go.GetComponent<LayoutElement>().preferredHeight = _style.BuildingGap;
            go.transform.SetParent(_root, false);

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
    }
}
