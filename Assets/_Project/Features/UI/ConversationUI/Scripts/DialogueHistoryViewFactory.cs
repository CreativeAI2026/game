using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    internal enum DialogueHistoryEntryKind
    {
        Dialogue,
        Choice,
        Narration,
    }

    internal readonly struct DialogueHistoryEntry
    {
        public DialogueHistoryEntry(
            string speaker,
            string body,
            Sprite portrait,
            DialoguePortraitSide side,
            bool portraitObscured,
            int sequence
        )
        {
            Speaker = speaker;
            Body = body;
            Portrait = portrait;
            Side = side;
            PortraitObscured = portraitObscured;
            Kind = string.IsNullOrWhiteSpace(speaker)
                ? DialogueHistoryEntryKind.Narration
                : DialogueHistoryEntryKind.Dialogue;
            Sequence = sequence;
        }

        public DialogueHistoryEntry(string choiceText, int sequence)
        {
            Speaker = string.Empty;
            Body = choiceText;
            Portrait = null;
            Side = DialoguePortraitSide.Left;
            PortraitObscured = false;
            Kind = DialogueHistoryEntryKind.Choice;
            Sequence = sequence;
        }

        public string Speaker { get; }
        public string Body { get; }
        public Sprite Portrait { get; }
        public DialoguePortraitSide Side { get; }
        public bool PortraitObscured { get; }
        public DialogueHistoryEntryKind Kind { get; }
        public int Sequence { get; }
    }

    /// <summary>会話履歴パネルと履歴行のUnity UI階層を構築する。</summary>
    internal sealed class DialogueHistoryViewFactory
    {
        internal readonly struct ViewReferences
        {
            public ViewReferences(
                GameObject panel,
                GameObject openButton,
                RectTransform content,
                ScrollRect scrollRect,
                CanvasGroup panelGroup,
                GameObject latestButton,
                TMP_InputField searchField
            )
            {
                Panel = panel;
                OpenButton = openButton;
                Content = content;
                ScrollRect = scrollRect;
                PanelGroup = panelGroup;
                LatestButton = latestButton;
                SearchField = searchField;
            }

            public GameObject Panel { get; }
            public GameObject OpenButton { get; }
            public RectTransform Content { get; }
            public ScrollRect ScrollRect { get; }
            public CanvasGroup PanelGroup { get; }
            public GameObject LatestButton { get; }
            public TMP_InputField SearchField { get; }
        }

        private readonly TMP_FontAsset _font;

        public DialogueHistoryViewFactory(TMP_FontAsset font) => _font = font;

        public ViewReferences Build(
            RectTransform root,
            float backdropAlpha,
            Action open,
            Action close,
            Action latest,
            Action<string> filter,
            Action<Vector2> scrollChanged
        )
        {
            var openButton = CreateButton(
                "DialogueHistoryButton",
                root,
                "LOG",
                new Vector2(1f, 0f),
                new Vector2(-28f, 10f),
                new Vector2(104f, 36f)
            );
            StyleLogButton(openButton);
            openButton.GetComponent<Button>().onClick.AddListener(() => open());

            var panel = CreateRect("DialogueHistoryPanel", root);
            Stretch(panel.GetComponent<RectTransform>());
            var backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.01f, 0.014f, 0.024f, 1f);
            backdrop.raycastTarget = true;
            var panelGroup = panel.AddComponent<CanvasGroup>();

            var header = CreateRect("ArchiveHeader", panel.transform);
            var headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.025f, 0.032f, 0.05f, 1f);
            headerImage.raycastTarget = true;
            Anchor(
                header.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 66f),
                new Vector2(0.5f, 1f)
            );
            header.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
            header.GetComponent<RectTransform>().anchorMax = Vector2.one;

            var headerLine = CreateRect("HeaderLine", header.transform);
            var headerLineRect = headerLine.GetComponent<RectTransform>();
            headerLineRect.anchorMin = Vector2.zero;
            headerLineRect.anchorMax = Vector2.right;
            headerLineRect.pivot = new Vector2(0.5f, 0f);
            headerLineRect.anchoredPosition = Vector2.zero;
            headerLineRect.sizeDelta = new Vector2(0f, 1f);
            headerLine.AddComponent<Image>().color = new Color(0.28f, 0.52f, 0.68f, 0.34f);
            var closeButton = CreateButton(
                "CloseButton",
                header.transform,
                "CLOSE",
                new Vector2(0.9f, 1f),
                new Vector2(0f, -15f),
                new Vector2(82f, 34f)
            );
            closeButton.GetComponent<Button>().onClick.AddListener(() => close());
            var searchField = CreateSearchField(header.transform, filter);

            var viewportObject = CreateRect("Viewport", panel.transform);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0.1f, 0.06f);
            viewport.anchorMax = new Vector2(0.9f, 0.92f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = CreateRect("Content", viewport);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 18, 32);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;

            var scrollRect = panel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 42f;
            scrollRect.onValueChanged.AddListener(value => scrollChanged(value));

            var latestButton = CreateButton(
                "LatestButton",
                panel.transform,
                "LATEST  ↓",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(132f, 36f)
            );
            latestButton.GetComponent<Button>().onClick.AddListener(() => latest());
            latestButton.SetActive(false);
            panel.SetActive(false);
            return new ViewReferences(
                panel,
                openButton,
                content,
                scrollRect,
                panelGroup,
                latestButton,
                searchField
            );
        }

        public GameObject CreateEntry(RectTransform content, DialogueHistoryEntry entry)
        {
            if (content == null)
                return null;
            var container = CreateRect("DialogueEntry", content);
            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            if (entry.Kind == DialogueHistoryEntryKind.Choice)
                CreateChoice(container.transform, entry);
            else if (entry.Kind == DialogueHistoryEntryKind.Narration)
                CreateNarration(container.transform, entry);
            else
                CreateDialogue(container.transform, entry);
            CreateSeparator(container.transform);
            return container;
        }

        private void CreateDialogue(Transform parent, DialogueHistoryEntry entry)
        {
            var row = CreateRect("Row", parent);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 24f;
            rowLayout.padding = new RectOffset(10, 10, 6, 6);
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            CreateRecordNumber(row.transform, entry.Sequence);
            var icon = CreatePortraitIcon(
                row.transform,
                entry.Portrait,
                entry.Side,
                entry.PortraitObscured
            );
            var column = CreateRect("Text", row.transform);
            var textLayout = column.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 7f;
            textLayout.childControlHeight = textLayout.childControlWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.childForceExpandWidth = true;
            column.AddComponent<LayoutElement>().flexibleWidth = 1f;
            column.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            var name = CreateText("Name", column.transform, entry.Speaker, 25f, FontStyles.Bold);
            name.color = new Color(0.75f, 0.9f, 1f, 1f);
            var body = CreateText("Body", column.transform, entry.Body, 27f, FontStyles.Normal);
            body.textWrappingMode = TextWrappingModes.Normal;
            if (entry.Side == DialoguePortraitSide.Right)
            {
                icon.transform.SetAsLastSibling();
                rowLayout.childAlignment = TextAnchor.UpperRight;
            }
        }

        private void CreateChoice(Transform parent, DialogueHistoryEntry entry)
        {
            var row = CreateRect("ChoiceEntry", parent);
            row.AddComponent<Image>().color = new Color(0.055f, 0.16f, 0.23f, 0.9f);
            var outline = row.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.68f, 0.9f, 0.52f);
            outline.effectDistance = new Vector2(2f, -2f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(22, 22, 14, 14);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            CreateRecordNumber(row.transform, entry.Sequence);
            var badge = CreateText("ChoiceBadge", row.transform, "CHOICE", 18f, FontStyles.Bold);
            badge.alignment = TextAlignmentOptions.Center;
            badge.color = new Color(0.7f, 0.92f, 1f, 1f);
            var badgeLayout = badge.gameObject.AddComponent<LayoutElement>();
            badgeLayout.minWidth = badgeLayout.preferredWidth = 76f;
            var body = CreateText("ChoiceText", row.transform, entry.Body, 26f, FontStyles.Bold);
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void CreateNarration(Transform parent, DialogueHistoryEntry entry)
        {
            var row = CreateRect("NarrationEntry", parent);
            row.AddComponent<Image>().color = new Color(0.045f, 0.05f, 0.065f, 0.82f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter
                .FitMode
                .PreferredSize;
            CreateRecordNumber(row.transform, entry.Sequence);
            var badge = CreateText("NarrationBadge", row.transform, "NOTE", 17f, FontStyles.Bold);
            badge.color = new Color(0.58f, 0.68f, 0.76f, 0.9f);
            var badgeLayout = badge.gameObject.AddComponent<LayoutElement>();
            badgeLayout.minWidth = badgeLayout.preferredWidth = 76f;
            var body = CreateText("Body", row.transform, entry.Body, 25f, FontStyles.Italic);
            body.color = new Color(0.82f, 0.84f, 0.88f, 0.92f);
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private TMP_Text CreateRecordNumber(Transform parent, int sequence)
        {
            var record = CreateText(
                "RecordNumber",
                parent,
                $"{Mathf.Max(1, sequence):000}",
                15f,
                FontStyles.Bold
            );
            record.color = new Color(0.38f, 0.58f, 0.72f, 0.82f);
            record.alignment = TextAlignmentOptions.TopLeft;
            var element = record.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 44f;
            return record;
        }

        private TMP_InputField CreateSearchField(Transform parent, Action<string> filter)
        {
            var target = CreateRect("SearchField", parent);
            Anchor(
                target.GetComponent<RectTransform>(),
                new Vector2(0.1f, 1f),
                new Vector2(0f, -15f),
                new Vector2(256f, 34f),
                new Vector2(0f, 1f)
            );
            target.AddComponent<Image>().color = new Color(0.1f, 0.12f, 0.16f, 0.95f);
            var field = target.AddComponent<TMP_InputField>();
            var text = CreateText("Text", target.transform, string.Empty, 21f, FontStyles.Normal);
            Stretch(text.rectTransform);
            text.margin = new Vector4(14f, 8f, 14f, 8f);
            var placeholder = CreateText(
                "Placeholder",
                target.transform,
                "ログを検索",
                20f,
                FontStyles.Normal
            );
            Stretch(placeholder.rectTransform);
            placeholder.margin = new Vector4(14f, 8f, 14f, 8f);
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            field.textComponent = text;
            field.placeholder = placeholder;
            field.onValueChanged.AddListener(value => filter(value));
            return field;
        }

        private GameObject CreatePortraitIcon(
            Transform parent,
            Sprite portrait,
            DialoguePortraitSide side,
            bool obscured
        )
        {
            var mask = CreateRect("PortraitIcon", parent);
            var element = mask.AddComponent<LayoutElement>();
            element.minWidth =
                element.preferredWidth =
                element.minHeight =
                element.preferredHeight =
                    76f;
            mask.AddComponent<Image>().color = obscured
                ? new Color(0.018f, 0.025f, 0.04f, 1f)
                : new Color(0.055f, 0.075f, 0.1f, 0.98f);
            var iconOutline = mask.AddComponent<Outline>();
            iconOutline.effectColor = obscured
                ? new Color(0.16f, 0.24f, 0.34f, 0.42f)
                : new Color(0.3f, 0.55f, 0.75f, 0.42f);
            iconOutline.effectDistance = new Vector2(1f, -1f);
            mask.AddComponent<Mask>().showMaskGraphic = true;
            if (portrait == null)
                return mask;
            var portraitObject = CreateRect("Portrait", mask.transform);
            var image = portraitObject.AddComponent<Image>();
            image.sprite = portrait;
            image.color = obscured ? new Color(0.045f, 0.06f, 0.085f, 1f) : Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var rect = portraitObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            const float iconZoom = 1.25f;
            rect.localScale = new Vector3(
                (side == DialoguePortraitSide.Left ? 1f : -1f) * iconZoom,
                iconZoom,
                1f
            );
            return mask;
        }

        private void CreateSeparator(Transform parent)
        {
            var separator = CreateRect("Separator", parent);
            separator.AddComponent<Image>().color = new Color(0.48f, 0.68f, 0.82f, 0.12f);
            var layout = separator.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 1f;
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
            var target = CreateRect(name, parent);
            Anchor(target.GetComponent<RectTransform>(), anchor, position, size, anchor);
            var image = target.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.16f, 0.94f);
            target.AddComponent<Button>().targetGraphic = image;
            var text = CreateText("Label", target.transform, label, 21f, FontStyles.Bold);
            Stretch(text.rectTransform);
            return target;
        }

        private void StyleLogButton(GameObject target)
        {
            var image = target.GetComponent<Image>();
            image.color = new Color(0.035f, 0.045f, 0.07f, 0.86f);
            var outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.62f, 0.88f, 0.3f);
            outline.effectDistance = new Vector2(1f, -1f);

            var label = target.GetComponentInChildren<TMP_Text>();
            label.fontSize = 19f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-30f, 0f);

            var key = CreateRect("ShortcutKey", target.transform);
            var keyImage = key.AddComponent<Image>();
            keyImage.color = new Color(1f, 1f, 1f, 0.08f);
            keyImage.raycastTarget = false;
            Anchor(
                key.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(-14f, 0f),
                new Vector2(22f, 22f),
                new Vector2(0.5f, 0.5f)
            );
            var keyLabel = CreateText("Label", key.transform, "D", 14f, FontStyles.Bold);
            keyLabel.alignment = TextAlignmentOptions.Center;
            Stretch(keyLabel.rectTransform);
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            FontStyles style
        )
        {
            var target = CreateRect(name, parent);
            var text = target.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
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

        private static void Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            Vector2 pivot
        )
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
