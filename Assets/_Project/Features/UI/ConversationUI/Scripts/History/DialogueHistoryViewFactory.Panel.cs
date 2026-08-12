using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    internal sealed partial class DialogueHistoryViewFactory
    {
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

            var scrollTrack = CreateRect("ScrollPositionTrack", panel.transform);
            var trackRect = scrollTrack.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0.91f, 0.06f);
            trackRect.anchorMax = new Vector2(0.91f, 0.92f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.sizeDelta = new Vector2(2f, 0f);
            trackRect.anchoredPosition = Vector2.zero;
            scrollTrack.AddComponent<Image>().color = new Color(0.35f, 0.55f, 0.7f, 0.12f);
            var scrollIndicatorObject = CreateRect("Indicator", scrollTrack.transform);
            var scrollIndicator = scrollIndicatorObject.AddComponent<Image>();
            scrollIndicator.color = new Color(0.38f, 0.72f, 0.94f, 0.72f);
            scrollIndicator.raycastTarget = false;
            Stretch(scrollIndicator.rectTransform);

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
                searchField,
                scrollIndicator
            );
        }
    }
}
