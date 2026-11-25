using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// useItem の持ち物ピッカー。持ち物をアイコンのグリッドで並べ、選択中の名前と所持数を下に出し、「やめる」を置く。
    /// 実行時にコードで組む(Prefab は持たない)。選択はマウスと、上下左右 + 決定 / キャンセルの入力。
    /// </summary>
    internal sealed class DialogueItemPickerPresenter
    {
        private const float PanelWidth = 640f;
        private const float PanelHeight = 640f;
        private const float Padding = 24f;
        private const float Gap = 12f;
        private const float PromptHeight = 64f;
        private const float NameHeight = 40f;
        private const float CancelHeight = 56f;
        private const float CellSize = 120f;
        private const float CellSpacing = 12f;
        private const int Columns = 4;
        private const float ViewportHeight =
            PanelHeight - Padding * 2f - PromptHeight - Gap * 3f - NameHeight - CancelHeight;
        private const string DefaultPrompt = "何を使う?";
        private const string CancelLabel = "やめる";
        private const string EmptyMessage = "使えそうな持ち物がない。";
        private const string ControlGuide = "SELECT   ARROWS     CONFIRM   ENTER     CANCEL   ESC";

        private static readonly Color DimColor = new(0f, 0f, 0f, 0.5f);
        private static readonly Color PanelColor = new(0.05f, 0.07f, 0.1f, 0.92f);
        private static readonly Color CellColor = new(1f, 1f, 1f, 0.1f);
        private static readonly Color CellFocusColor = new(0.55f, 0.75f, 1f, 0.4f);

        private readonly Transform _parent;
        private readonly TMP_FontAsset _font;
        private readonly List<Image> _backgrounds = new(); // 添字は ItemPickerGridNavigation と同じ(最後が「やめる」)
        private readonly List<Action> _actions = new();
        private readonly List<string> _names = new(); // 持ち物ごとの「名前 ×所持数」。「やめる」は含めない
        private GameObject _root;
        private RectTransform _contentRect;
        private TMP_Text _nameText;
        private int _selectedIndex = -1;
        private string _rememberedKey; // 直前に選んだ itemKey。会話を閉じるまで覚える

        private int ItemCount => _names.Count;

        public DialogueItemPickerPresenter(Transform parent, TMP_FontAsset font)
        {
            _parent = parent;
            _font = font;
        }

        /// <summary>
        /// 一覧を出す。選ぶと選んだ itemKey(キーを持たないアイテムは空文字)、やめるなら null で <paramref name="onPicked"/> を呼ぶ。
        /// 持ち物が無くても「やめる」だけは出す。
        /// </summary>
        public void Show(
            string prompt,
            IReadOnlyList<ItemPickerEntries.Entry> entries,
            Action<string> onPicked
        )
        {
            Clear();
            _root = CreateRect("ItemPicker", _parent);
            Stretch(_root.GetComponent<RectTransform>());
            var dim = _root.AddComponent<Image>();
            dim.color = DimColor; // 背面のクリックを受け止める

            var panel = CreateRect("Panel", _root.transform);
            Anchor(panel.GetComponent<RectTransform>(), new Vector2(PanelWidth, PanelHeight));
            panel.AddComponent<Image>().color = PanelColor;

            BuildPrompt(panel.transform, prompt);
            var content = BuildScrollArea(panel);
            _nameText = BuildNameText(panel.transform);
            BuildControlGuide(_root.transform);

            // 呼び出し元の再入を防ぐ: 一度選んだら以降の選択は無視する。
            bool picked = false;
            void Pick(string value)
            {
                if (picked)
                    return;
                picked = true;
                if (value != null)
                    _rememberedKey = value; // 選び直しのとき、さっき選んだ物にカーソルを合わせる
                onPicked?.Invoke(value);
            }

            foreach (var entry in entries ?? Array.Empty<ItemPickerEntries.Entry>())
            {
                var captured = entry;
                AddCell(content, captured, () => Pick(captured.ItemKey));
            }
            AddCancel(panel.transform, () => Pick(null));

            Select(StartIndex(entries));
        }

        /// <summary>上下左右で選択を動かし、決定で選び、キャンセルで「やめる」を選ぶ。毎フレーム呼ぶ。</summary>
        public void HandleKeyboardInput()
        {
            if (_root == null || _actions.Count == 0)
                return;

            if (UIInputActionMap.WasCancelPressedThisFrame())
            {
                _actions[^1].Invoke();
                return;
            }
            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
                Select(ItemPickerGridNavigation.Move(_selectedIndex, step, ItemCount, Columns));
            if (UIInputActionMap.WasSubmitPressedThisFrame() && _selectedIndex >= 0)
                _actions[_selectedIndex].Invoke();
        }

        /// <summary>パネルを片付け、選んだ位置の記憶も忘れる(会話を閉じるとき)。</summary>
        public void Dismiss()
        {
            Clear();
            _rememberedKey = null;
        }

        // 開いたときにカーソルを置く位置。さっき選んだ物があればそこ、なければ先頭。
        private int StartIndex(IReadOnlyList<ItemPickerEntries.Entry> entries)
        {
            if (_rememberedKey == null || entries == null)
                return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].ItemKey == _rememberedKey)
                    return i;
            }
            return 0;
        }

        /// <summary>パネルを片付ける。</summary>
        public void Clear()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _contentRect = null;
            _nameText = null;
            _backgrounds.Clear();
            _actions.Clear();
            _names.Clear();
            _selectedIndex = -1;
        }

        private void BuildPrompt(Transform panel, string prompt)
        {
            var promptText = CreateText(
                "Prompt",
                panel,
                string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt,
                30f,
                TextAlignmentOptions.MidlineLeft
            );
            var rect = promptText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(Padding, -Padding - PromptHeight);
            rect.offsetMax = new Vector2(-Padding, -Padding);
        }

        // グリッドを入れるスクロール領域を組み、グリッドの親(Content)を返す。
        private Transform BuildScrollArea(GameObject panel)
        {
            var viewport = CreateRect("Viewport", panel.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.offsetMin = new Vector2(
                Padding,
                -(Padding + PromptHeight + Gap + ViewportHeight)
            );
            viewportRect.offsetMax = new Vector2(-Padding, -(Padding + PromptHeight + Gap));
            viewport.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            _contentRect = contentRect;
            var scroll = panel.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content.transform;
        }

        // 画面の左下に、使える操作を 1 行で出す。会話ウィンドウの操作ガイドと同じ位置・書き方にそろえる
        // (ピッカーの暗幕がそちらを覆うので、重ならない)。
        private void BuildControlGuide(Transform screen)
        {
            var text = CreateText(
                "ControlGuide",
                screen,
                ControlGuide,
                20f,
                TextAlignmentOptions.BottomLeft
            );
            text.color = new Color(1f, 1f, 1f, 0.62f);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(26f, 20f);
            rect.sizeDelta = new Vector2(620f, 38f);
        }

        // グリッドの下に、選択中の持ち物の名前と所持数を出す欄。
        private TMP_Text BuildNameText(Transform panel)
        {
            var text = CreateText("Name", panel, string.Empty, 28f, TextAlignmentOptions.Center);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(Padding, Padding + CancelHeight + Gap);
            rect.offsetMax = new Vector2(-Padding, Padding + CancelHeight + Gap + NameHeight);
            return text;
        }

        private void AddCell(Transform parent, ItemPickerEntries.Entry entry, Action onClick)
        {
            var cell = CreateRect($"Cell{_actions.Count}", parent);
            var background = SetUpButton(cell, onClick);

            if (entry.Icon != null)
            {
                var iconObject = CreateRect("Icon", cell.transform);
                Stretch(iconObject.GetComponent<RectTransform>());
                iconObject.GetComponent<RectTransform>().offsetMin = new Vector2(12f, 12f);
                iconObject.GetComponent<RectTransform>().offsetMax = new Vector2(-12f, -12f);
                var image = iconObject.AddComponent<Image>();
                image.sprite = entry.Icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                // 絵の無いアイテムは、マスの中に名前を出して見分けられるようにする。
                var fallback = CreateText(
                    "NameFallback",
                    cell.transform,
                    entry.Name,
                    20f,
                    TextAlignmentOptions.Center
                );
                Stretch(fallback.rectTransform);
            }

            if (entry.Count > 1)
            {
                var count = CreateText(
                    "Count",
                    cell.transform,
                    $"×{entry.Count}",
                    22f,
                    TextAlignmentOptions.BottomRight
                );
                Stretch(count.rectTransform);
                count.rectTransform.offsetMin = new Vector2(6f, 4f);
                count.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }

            _names.Add($"{entry.Name}  ×{entry.Count}");
            _backgrounds.Add(background);
            _actions.Add(onClick);
        }

        private void AddCancel(Transform panel, Action onClick)
        {
            var button = CreateRect("Cancel", panel);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(Padding, Padding);
            rect.offsetMax = new Vector2(-Padding, Padding + CancelHeight);
            var background = SetUpButton(button, onClick);

            var label = CreateText(
                "Label",
                button.transform,
                CancelLabel,
                28f,
                TextAlignmentOptions.Center
            );
            Stretch(label.rectTransform);

            _backgrounds.Add(background);
            _actions.Add(onClick);
        }

        // マス / 「やめる」に共通の、背景・クリック・マウスホバーの設定。背景の Image を返す。
        private Image SetUpButton(GameObject target, Action onClick)
        {
            int index = _actions.Count;
            var background = target.AddComponent<Image>();
            background.color = CellColor;

            var button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick());

            // マウスを乗せたら、その位置を選択する(キーボードの選択と同じ見た目にそろえる)。
            // 色と名前だけ変える。スクロール位置は動かさない(動かすと、止めたマウスの下に別のマスが来て連鎖する)。
            target.AddComponent<ItemPickerRowHover>().Configure(() => Select(index, false));
            return background;
        }

        // 選択位置を変える。scroll が true のときだけ、選んだマスが見える範囲に入るよう最小限スクロールする
        // (キーボード用。マウスのホバーでは色と名前だけ変える)。
        private void Select(int index, bool scroll = true)
        {
            if (_backgrounds.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, _backgrounds.Count - 1);
            _selectedIndex = index;
            for (int i = 0; i < _backgrounds.Count; i++)
                _backgrounds[i].color = i == index ? CellFocusColor : CellColor;
            if (_nameText != null)
                _nameText.text =
                    index < ItemCount
                        ? _names[index]
                        : (ItemCount == 0 ? EmptyMessage : string.Empty);

            if (scroll && index < ItemCount)
                ScrollToKeepVisible(index);
        }

        private void ScrollToKeepVisible(int index)
        {
            if (_contentRect == null)
                return;

            float pitch = CellSize + CellSpacing;
            int rows = (ItemCount + Columns - 1) / Columns;
            float rowTop = index / Columns * pitch;
            float rowBottom = rowTop + CellSize;
            float contentHeight = rows * pitch - CellSpacing;
            float maxOffset = Mathf.Max(0f, contentHeight - ViewportHeight);

            // Content は上端基準で、anchoredPosition.y が下へスクロールした量になる。
            float offset = _contentRect.anchoredPosition.y;
            if (rowTop < offset)
                offset = rowTop;
            else if (rowBottom > offset + ViewportHeight)
                offset = rowBottom - ViewportHeight;
            _contentRect.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, maxOffset));
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            TextAlignmentOptions alignment
        )
        {
            var target = CreateRect(name, parent);
            var text = target.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (_font != null)
                text.font = _font;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            return target;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
