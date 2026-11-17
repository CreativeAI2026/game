using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// useItem の持ち物ピッカー。持ち物の一覧(アイコン・名前・所持数)と「やめる」を、実行時にコードで組んだ
    /// スクロール付きのパネルとして出す。Prefab は持たない。選択はマウスと、上下 + 決定 / キャンセルの入力。
    /// </summary>
    internal sealed class DialogueItemPickerPresenter
    {
        private const float PanelWidth = 640f;
        private const float PanelHeight = 600f;
        private const float Padding = 24f;
        private const float PromptHeight = 64f;
        private const float RowHeight = 64f;
        private const float RowSpacing = 8f;
        private const float ViewportHeight = PanelHeight - Padding * 3f - PromptHeight;
        private const float IconSize = 48f;
        private const string DefaultPrompt = "何を使う?";
        private const string CancelLabel = "やめる";

        private static readonly Color DimColor = new(0f, 0f, 0f, 0.5f);
        private static readonly Color PanelColor = new(0.05f, 0.07f, 0.1f, 0.92f);
        private static readonly Color RowColor = new(1f, 1f, 1f, 0.1f);
        private static readonly Color RowFocusColor = new(0.55f, 0.75f, 1f, 0.4f);

        private readonly Transform _parent;
        private readonly TMP_FontAsset _font;
        private readonly List<Image> _rowBackgrounds = new();
        private readonly List<Action> _rowActions = new();
        private GameObject _root;
        private RectTransform _contentRect;
        private int _selectedIndex = -1;

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

            var promptText = CreateText(
                "Prompt",
                panel.transform,
                string.IsNullOrWhiteSpace(prompt) ? DefaultPrompt : prompt,
                30f,
                TextAlignmentOptions.MidlineLeft
            );
            var promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0f, 1f);
            promptRect.anchorMax = new Vector2(1f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.offsetMin = new Vector2(Padding, -Padding - PromptHeight);
            promptRect.offsetMax = new Vector2(-Padding, -Padding);

            var viewport = CreateRect("Viewport", panel.transform);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(Padding, Padding);
            viewportRect.offsetMax = new Vector2(-Padding, -Padding * 2f - PromptHeight);
            viewport.AddComponent<RectMask2D>();

            var content = CreateRect("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
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

            // 呼び出し元の再入を防ぐ: 一度選んだら以降の選択は無視する。
            bool picked = false;
            void Pick(string value)
            {
                if (picked)
                    return;
                picked = true;
                onPicked?.Invoke(value);
            }

            foreach (var entry in entries ?? Array.Empty<ItemPickerEntries.Entry>())
            {
                var captured = entry;
                AddRow(
                    content.transform,
                    captured.Icon,
                    $"{captured.Name}  ×{captured.Count}",
                    () => Pick(captured.ItemKey)
                );
            }
            AddRow(content.transform, null, CancelLabel, () => Pick(null));

            Select(0);
        }

        /// <summary>上下で選択を動かし、決定で選び、キャンセルで「やめる」を選ぶ。毎フレーム呼ぶ。</summary>
        public void HandleKeyboardInput()
        {
            if (_root == null || _rowActions.Count == 0)
                return;

            if (UiInputActionMap.WasCancelPressedThisFrame())
            {
                _rowActions[^1].Invoke();
                return;
            }
            if (UiInputActionMap.TryReadDiscreteMove(out Vector2Int step))
            {
                if (step.y > 0)
                    Select(Mathf.Max(0, _selectedIndex - 1));
                else if (step.y < 0)
                    Select(Mathf.Min(_rowActions.Count - 1, _selectedIndex + 1));
            }
            if (UiInputActionMap.WasSubmitPressedThisFrame() && _selectedIndex >= 0)
                _rowActions[_selectedIndex].Invoke();
        }

        /// <summary>パネルを片付ける。</summary>
        public void Clear()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _contentRect = null;
            _rowBackgrounds.Clear();
            _rowActions.Clear();
            _selectedIndex = -1;
        }

        private void AddRow(Transform parent, Sprite icon, string label, Action onClick)
        {
            int index = _rowActions.Count;
            var row = CreateRect($"Row{index}", parent);
            var background = row.AddComponent<Image>();
            background.color = RowColor;
            row.AddComponent<LayoutElement>().preferredHeight = RowHeight;

            var button = row.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick());

            // マウスを乗せたら、その行を選択する(キーボードの選択と同じ見た目にそろえる)。
            // 色だけ変える。スクロール位置は動かさない(動かすと、止めたマウスの下に別の行が来て連鎖する)。
            row.AddComponent<ItemPickerRowHover>().Configure(() => Select(index, false));

            float textLeft = Padding / 2f;
            if (icon != null)
            {
                var iconObject = CreateRect("Icon", row.transform);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(Padding / 2f, 0f);
                iconRect.sizeDelta = new Vector2(IconSize, IconSize);
                var image = iconObject.AddComponent<Image>();
                image.sprite = icon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                textLeft += IconSize + Padding / 2f;
            }

            var text = CreateText(
                "Label",
                row.transform,
                label,
                28f,
                TextAlignmentOptions.MidlineLeft
            );
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(textLeft, 0f);
            text.rectTransform.offsetMax = new Vector2(-Padding / 2f, 0f);

            _rowBackgrounds.Add(background);
            _rowActions.Add(onClick);
        }

        // 選択行を変える。scroll が true のときだけ、選んだ行が見える範囲に入るよう最小限スクロールする
        // (キーボード用。マウスのホバーでは色だけ変える)。
        private void Select(int index, bool scroll = true)
        {
            if (_rowBackgrounds.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, _rowBackgrounds.Count - 1);
            _selectedIndex = index;
            for (int i = 0; i < _rowBackgrounds.Count; i++)
                _rowBackgrounds[i].color = i == index ? RowFocusColor : RowColor;

            if (scroll)
                ScrollToKeepVisible(index);
        }

        private void ScrollToKeepVisible(int index)
        {
            if (_contentRect == null)
                return;

            float pitch = RowHeight + RowSpacing;
            float rowTop = index * pitch;
            float rowBottom = rowTop + RowHeight;
            float contentHeight = _rowBackgrounds.Count * pitch - RowSpacing;
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
