using System.Collections.Generic;
using System.Text;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話中に表示済みの行を読み返す、画面全体の履歴パネル。</summary>
    public sealed class DialogueHistoryPanel : MonoBehaviour
    {
        private enum HistoryEntryKind
        {
            Dialogue,
            Choice,
        }

        private readonly struct HistoryEntry
        {
            public HistoryEntry(
                string speaker,
                string body,
                Sprite portrait,
                DialoguePortraitSide side
            )
            {
                Speaker = speaker;
                Body = body;
                Portrait = portrait;
                Side = side;
                Kind = HistoryEntryKind.Dialogue;
            }

            public HistoryEntry(string choiceText)
            {
                Speaker = string.Empty;
                Body = choiceText;
                Portrait = null;
                Side = DialoguePortraitSide.Left;
                Kind = HistoryEntryKind.Choice;
            }

            public string Speaker { get; }
            public string Body { get; }
            public Sprite Portrait { get; }
            public DialoguePortraitSide Side { get; }
            public HistoryEntryKind Kind { get; }
        }

        [SerializeField]
        private int _maxEntries = 200;

        [SerializeField]
        [Range(0f, 1f)]
        private float _backdropAlpha = 0.78f;

        private readonly List<HistoryEntry> _entries = new();
        private TMP_FontAsset _font;
        private GameObject _panel;
        private GameObject _openButton;
        private RectTransform _content;
        private ScrollRect _scrollRect;
        private CanvasGroup _panelGroup;
        private GameObject _latestButton;
        private Coroutine _panelAnimation;
        private bool _isOpen;
        private TMP_InputField _searchField;

        public bool IsOpen => _isOpen;
        public int EntryCount => _entries.Count;

        public void Initialize(TMP_FontAsset font)
        {
            if (_font == null)
                _font = font;
            EnsureView();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.dKey.wasPressedThisFrame)
                SetOpen(!IsOpen);
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
                SetOpen(false);
        }

        public void AddEntry(
            string speaker,
            string body,
            Sprite portrait,
            DialoguePortraitSide side
        )
        {
            bool followLatest = ShouldFollowLatest();
            EnsureView();
            var entry = new HistoryEntry(
                speaker ?? string.Empty,
                body ?? string.Empty,
                portrait,
                side
            );
            _entries.Add(entry);
            CreateEntryView(entry);
            TrimOldEntries();
            if (followLatest)
                ScrollToLatest();
            else
                RefreshLatestButton();
        }

        public void AddChoiceEntry(string choiceText)
        {
            if (string.IsNullOrWhiteSpace(choiceText))
                return;

            bool followLatest = ShouldFollowLatest();
            EnsureView();
            var entry = new HistoryEntry(choiceText);
            _entries.Add(entry);
            CreateEntryView(entry);
            TrimOldEntries();
            if (followLatest)
                ScrollToLatest();
            else
                RefreshLatestButton();
        }

        public void AddChoiceEntry(IReadOnlyList<ChoiceOption> options, string selectedText)
        {
            if (options == null || options.Count == 0)
            {
                AddChoiceEntry(selectedText);
                return;
            }

            var summary = new StringBuilder();
            foreach (var option in options)
            {
                if (option == null)
                    continue;
                if (summary.Length > 0)
                    summary.AppendLine();
                summary.Append(option.Text == selectedText ? "✓ " : "・ ").Append(option.Text);
            }
            AddChoiceEntry(summary.ToString());
        }

        public void SetOpen(bool open)
        {
            EnsureView();
            if (_panel == null)
                return;

            _isOpen = open;
            if (_openButton != null)
                _openButton.SetActive(!open);

            if (open)
            {
                bool wasActive = _panel.activeSelf;
                _panel.SetActive(true);
                if (!wasActive && _panelGroup != null)
                    _panelGroup.alpha = 0f;
                _panel.transform.SetAsLastSibling();
                ScrollToLatest();
                EventSystem.current?.SetSelectedGameObject(null);
            }

            if (!Application.isPlaying)
            {
                _panel.SetActive(open);
                if (_panelGroup != null)
                    _panelGroup.alpha = open ? 1f : 0f;
                return;
            }

            if (_panelAnimation != null)
                StopCoroutine(_panelAnimation);
            _panelAnimation = StartCoroutine(AnimatePanel(open));
        }

        private void EnsureView()
        {
            if (_panel != null)
                return;

            var root = transform as RectTransform;
            if (root == null)
                return;

            _openButton = CreateButton(
                "DialogueHistoryButton",
                root,
                "LOG  [D]",
                new Vector2(1f, 0f),
                new Vector2(-32f, 28f),
                new Vector2(150f, 54f)
            );
            _openButton.GetComponent<Button>().onClick.AddListener(() => SetOpen(true));

            _panel = CreateRect("DialogueHistoryPanel", root);
            Stretch(_panel.GetComponent<RectTransform>());
            var backdrop = _panel.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, _backdropAlpha);
            backdrop.raycastTarget = true;
            _panelGroup = _panel.AddComponent<CanvasGroup>();

            var title = CreateText("Title", _panel.transform, "DIALOGUE LOG", 30f, FontStyles.Bold);
            Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -28f),
                new Vector2(520f, 52f),
                new Vector2(0.5f, 1f)
            );

            var close = CreateButton(
                "CloseButton",
                _panel.transform,
                "CLOSE  [D / ESC]",
                new Vector2(1f, 1f),
                new Vector2(-30f, -24f),
                new Vector2(230f, 52f)
            );
            close.GetComponent<Button>().onClick.AddListener(() => SetOpen(false));

            CreateSearchField(_panel.transform);

            var viewportObject = CreateRect("Viewport", _panel.transform);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0.12f, 0.08f);
            viewport.anchorMax = new Vector2(0.88f, 0.9f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = CreateRect("Content", viewport);
            _content = contentObject.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(12, 12, 12, 28);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect = _panel.AddComponent<ScrollRect>();
            _scrollRect.viewport = viewport;
            _scrollRect.content = _content;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 42f;
            _scrollRect.onValueChanged.AddListener(_ => RefreshLatestButton());

            _latestButton = CreateButton(
                "LatestButton",
                _panel.transform,
                "最新へ戻る",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 22f),
                new Vector2(190f, 48f)
            );
            _latestButton.GetComponent<Button>().onClick.AddListener(ScrollToLatest);
            _latestButton.SetActive(false);
            _panel.SetActive(false);
        }

        private System.Collections.IEnumerator AnimatePanel(bool opening)
        {
            float start = _panelGroup != null ? _panelGroup.alpha : (opening ? 0f : 1f);
            float target = opening ? 1f : 0f;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_panelGroup != null)
                    _panelGroup.alpha = Mathf.Lerp(start, target, t);
                elapsed += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                yield return null;
            }
            if (_panelGroup != null)
                _panelGroup.alpha = target;
            if (!opening)
                _panel.SetActive(false);
            _panelAnimation = null;
        }

        private void RefreshLatestButton()
        {
            if (_latestButton != null)
                _latestButton.SetActive(IsOpen && _scrollRect.verticalNormalizedPosition > 0.03f);
        }

        private bool ShouldFollowLatest() =>
            !IsOpen || _scrollRect == null || _scrollRect.verticalNormalizedPosition <= 0.03f;

        private void CreateSearchField(Transform parent)
        {
            var fieldObject = CreateRect("SearchField", parent);
            Anchor(
                fieldObject.GetComponent<RectTransform>(),
                new Vector2(0f, 1f),
                new Vector2(30f, -24f),
                new Vector2(300f, 48f),
                new Vector2(0f, 1f)
            );
            fieldObject.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.95f);
            _searchField = fieldObject.AddComponent<TMP_InputField>();

            var text = CreateText(
                "Text",
                fieldObject.transform,
                string.Empty,
                21f,
                FontStyles.Normal
            );
            Stretch(text.rectTransform);
            text.margin = new Vector4(14f, 8f, 14f, 8f);
            var placeholder = CreateText(
                "Placeholder",
                fieldObject.transform,
                "本文・話者名で検索",
                20f,
                FontStyles.Normal
            );
            Stretch(placeholder.rectTransform);
            placeholder.margin = new Vector4(14f, 8f, 14f, 8f);
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            _searchField.textComponent = text;
            _searchField.placeholder = placeholder;
            _searchField.onValueChanged.AddListener(ApplyFilter);
        }

        public void ApplyFilter(string query)
        {
            query = query?.Trim() ?? string.Empty;
            for (int i = 0; i < _entries.Count && i < _content.childCount; i++)
            {
                var entry = _entries[i];
                bool visible =
                    query.Length == 0
                    || entry.Speaker.Contains(query, System.StringComparison.OrdinalIgnoreCase)
                    || entry.Body.Contains(query, System.StringComparison.OrdinalIgnoreCase);
                _content.GetChild(i).gameObject.SetActive(visible);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void CreateEntryView(HistoryEntry entry)
        {
            if (_content == null)
                return;

            var container = CreateRect("DialogueEntry", _content);
            var containerLayout = container.AddComponent<VerticalLayoutGroup>();
            containerLayout.spacing = 10f;
            containerLayout.childControlHeight = true;
            containerLayout.childControlWidth = true;
            containerLayout.childForceExpandHeight = false;
            containerLayout.childForceExpandWidth = true;
            var containerFitter = container.AddComponent<ContentSizeFitter>();
            containerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (entry.Kind == HistoryEntryKind.Choice)
            {
                CreateChoiceEntryView(container.transform, entry.Body);
                CreateSeparator(container.transform);
                return;
            }

            var row = CreateRect("Row", container.transform);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24f;
            rowLayout.padding = new RectOffset(10, 10, 6, 6);
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowFitter = row.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var icon = CreatePortraitIcon(row.transform, entry.Portrait, entry.Side);
            var textColumn = CreateRect("Text", row.transform);
            var textLayout = textColumn.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 6f;
            textLayout.childControlHeight = true;
            textLayout.childControlWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.childForceExpandWidth = true;
            var textElement = textColumn.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
            var textFitter = textColumn.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var name = CreateText(
                "Name",
                textColumn.transform,
                entry.Speaker,
                25f,
                FontStyles.Bold
            );
            name.color = new Color(0.75f, 0.9f, 1f, 1f);
            var body = CreateText("Body", textColumn.transform, entry.Body, 29f, FontStyles.Normal);
            body.textWrappingMode = TextWrappingModes.Normal;

            if (entry.Side == DialoguePortraitSide.Right)
            {
                icon.transform.SetAsLastSibling();
                rowLayout.childAlignment = TextAnchor.UpperRight;
            }

            CreateSeparator(container.transform);
        }

        private void CreateChoiceEntryView(Transform parent, string choiceText)
        {
            var row = CreateRect("ChoiceEntry", parent);
            row.AddComponent<Image>().color = new Color(0.12f, 0.28f, 0.38f, 0.72f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(22, 22, 14, 14);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = row.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var badge = CreateText("ChoiceBadge", row.transform, "選択", 22f, FontStyles.Bold);
            badge.alignment = TextAlignmentOptions.Center;
            badge.color = new Color(0.7f, 0.92f, 1f, 1f);
            var badgeLayout = badge.gameObject.AddComponent<LayoutElement>();
            badgeLayout.minWidth = 76f;
            badgeLayout.preferredWidth = 76f;

            var body = CreateText("ChoiceText", row.transform, choiceText, 28f, FontStyles.Bold);
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleWidth = 1f;
        }

        private void CreateSeparator(Transform parent)
        {
            var separator = CreateRect("Separator", parent);
            separator.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
            var separatorLayout = separator.AddComponent<LayoutElement>();
            separatorLayout.minHeight = 1f;
            separatorLayout.preferredHeight = 1f;
        }

        private void TrimOldEntries()
        {
            while (_entries.Count > Mathf.Max(1, _maxEntries))
            {
                _entries.RemoveAt(0);
                if (_content != null && _content.childCount > 0)
                    Destroy(_content.GetChild(0).gameObject);
            }
        }

        private GameObject CreatePortraitIcon(
            Transform parent,
            Sprite portrait,
            DialoguePortraitSide side
        )
        {
            var maskObject = CreateRect("PortraitIcon", parent);
            var element = maskObject.AddComponent<LayoutElement>();
            element.minWidth = 110f;
            element.preferredWidth = 110f;
            element.minHeight = 110f;
            element.preferredHeight = 110f;
            var background = maskObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            maskObject.AddComponent<Mask>().showMaskGraphic = true;

            if (portrait == null)
                return maskObject;

            var portraitObject = CreateRect("Portrait", maskObject.transform);
            var image = portraitObject.AddComponent<Image>();
            image.sprite = portrait;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = portraitObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            rect.localScale = new Vector3(side == DialoguePortraitSide.Left ? 1f : -1f, 1f, 1f);
            return maskObject;
        }

        private void ScrollToLatest()
        {
            if (_scrollRect == null)
                return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scrollRect.verticalNormalizedPosition = 0f;
            RefreshLatestButton();
        }

        private GameObject CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 position,
            Vector2 size
        )
        {
            var buttonObject = CreateRect(name, parent);
            Anchor(buttonObject.GetComponent<RectTransform>(), anchor, position, size, anchor);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.16f, 0.94f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", buttonObject.transform, label, 21f, FontStyles.Bold);
            Stretch(text.rectTransform);
            return buttonObject;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            FontStyles style
        )
        {
            var textObject = CreateRect(name, parent);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            if (_font != null)
                text.font = _font;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            Vector2 pivot
        )
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
