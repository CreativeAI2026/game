using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public abstract partial class BaseItemSlot
    {
        protected void InitializeBase()
        {
            if (_isInitialized)
                return;

            _iconImage ??= GetOrCreateIconImage();
            _countContainer ??= FindCountContainer();
            _countText ??= FindCountText();
            if (_countContainer != null && _countText == null)
                _countText = _countContainer.GetComponentInChildren<TMP_Text>(true);
            if (_countText != null)
                _countContainer ??= GetOrCreateCountContainer();
            EnsureCountCanvasGroup();

            _hoverScale ??= GetComponent<HoverScaleOnPointer>();
            _hoverScale ??= GetComponentInChildren<HoverScaleOnPointer>(true);

            BindHoverTargets();
            _isInitialized = true;
        }

        protected void EnsureCountReferences()
        {
            _countContainer ??= FindCountContainer();
            _countText ??= FindCountText();
            if (_countContainer != null && _countText == null)
                _countText = _countContainer.GetComponentInChildren<TMP_Text>(true);
            if (_countText != null)
                _countContainer ??= GetOrCreateCountContainer();
            if (_countContainer != null)
            {
                _countContainerImage ??= _countContainer.GetComponent<Image>();
                EnsureCountContainerCanvasGroup();
            }
            EnsureCountCanvasGroup();
        }

        protected void BindHoverTargets()
        {
            if (_hoverScale == null)
                return;

            if (_iconImage != null)
                _hoverScale.SetTarget(_iconImage.rectTransform);
            if (_countContainer != null)
            {
                _hoverScale.SetLinkedTargets(_countContainer);
                _hoverScale.SetBounceTarget(null);
            }
            else
            {
                _hoverScale.SetLinkedTargets(null);
                _hoverScale.SetBounceTarget(_countText != null ? _countText.rectTransform : null);
            }
        }

        private void EnsureCountCanvasGroup()
        {
            if (_countText == null)
                return;

            _countCanvasGroup = _countText.GetComponent<CanvasGroup>();
            if (_countCanvasGroup == null)
                _countCanvasGroup = _countText.gameObject.AddComponent<CanvasGroup>();
        }

        private void EnsureCountContainerCanvasGroup()
        {
            if (_countContainer == null)
                return;

            _countContainerCanvasGroup = _countContainer.GetComponent<CanvasGroup>();
            if (_countContainerCanvasGroup == null)
                _countContainerCanvasGroup = _countContainer.gameObject.AddComponent<CanvasGroup>();

            _countContainerCanvasGroup.interactable = false;
            _countContainerCanvasGroup.blocksRaycasts = false;
        }

        private Image GetOrCreateIconImage()
        {
            var iconTransform = transform.Find("VisualRoot/Icon") ?? transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image icon))
                return icon;

            var rootImage = GetComponent<Image>();
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconRect.SetAsFirstSibling();

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            if (rootImage != null)
            {
                iconImage.sprite = rootImage.sprite;
                iconImage.color = rootImage.color;
                rootImage.sprite = null;
                rootImage.color = Color.clear;
            }

            return iconImage;
        }

        private TMP_Text FindCountText()
        {
            var numberSlotTextTransform =
                transform.Find("VisualRoot/numberSlot/Text")
                ?? transform.Find("Icon/numberSlot/Text")
                ?? transform.Find("numberSlot/Text");
            if (
                numberSlotTextTransform != null
                && numberSlotTextTransform.TryGetComponent(out TMP_Text numberSlotText)
            )
                return numberSlotText;

            var countTransform = transform.Find("CountText");
            if (countTransform != null && countTransform.TryGetComponent(out TMP_Text countText))
                return countText;

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (
                    text.transform == transform
                    || text.name == "EmptyText"
                    || text.name == "SlotNumber"
                    || text.transform.parent != null
                        && text.transform.parent.name == "UnusedCountSlot"
                )
                    continue;

                return text;
            }

            return null;
        }

        private RectTransform FindCountContainer()
        {
            var numberSlotTransform =
                transform.Find("VisualRoot/numberSlot")
                ?? transform.Find("Icon/numberSlot")
                ?? transform.Find("numberSlot");
            return numberSlotTransform as RectTransform;
        }

        private RectTransform GetOrCreateCountContainer()
        {
            var numberSlotTransform = FindCountContainer();
            bool createdNumberSlot = numberSlotTransform == null;
            RectTransform numberSlotRect;
            if (!createdNumberSlot)
            {
                numberSlotRect = numberSlotTransform;
                if (_countText == null)
                    _countText = numberSlotTransform.GetComponentInChildren<TMP_Text>(true);
            }
            else
            {
                var numberSlotObject = new GameObject("numberSlot", typeof(RectTransform));
                numberSlotRect = numberSlotObject.GetComponent<RectTransform>();
                numberSlotRect.SetParent(transform, false);
            }

            if (createdNumberSlot && _countText != null)
                CopyRectTransformLayout(_countText.rectTransform, numberSlotRect);
            else
                ConfigureDefaultCountContainerRect(numberSlotRect);

            _countContainerImage = ConfigureCountContainerImage(numberSlotRect);

            if (_countText != null && _countText.transform.parent != numberSlotRect)
            {
                var countRect = _countText.rectTransform;
                countRect.SetParent(numberSlotRect, false);
                countRect.name = "Text";
                ConfigureCountTextRect(countRect);
            }

            if (_countText != null)
                ConfigureCountTextStyle(_countText);

            return numberSlotRect;
        }

        private static void ConfigureDefaultCountContainerRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 40f);
            rect.localScale = Vector3.one;
        }

        private static void CopyRectTransformLayout(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localScale = source.localScale;
        }

        private static void ConfigureCountTextRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        private static Image ConfigureCountContainerImage(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<Image>();

            image.color = new Color(0f, 0f, 0f, CountContainerVisibleAlpha);
            image.enabled = true;
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureCountTextStyle(TMP_Text text)
        {
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSize = 40f;
            text.fontSizeMin = Mathf.Max(text.fontSizeMin, 18f);
            text.fontSizeMax = Mathf.Max(text.fontSizeMax, 48f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
    }
}
