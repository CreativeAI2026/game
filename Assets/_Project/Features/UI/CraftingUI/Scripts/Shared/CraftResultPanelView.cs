using System;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(CloseOnSelfClick))]
    public sealed class CraftResultPanelView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private CloseOnSelfClick _closeOnSelfClick;

        [SerializeField]
        private Graphic _background;

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private Image _itemImage;

        [SerializeField]
        private TMP_Text _itemName;

        [SerializeField]
        private TMP_Text _itemParameters;

        private Action _closedAction;
        private TMP_Text _newBadge;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _closeOnSelfClick ??= GetComponent<CloseOnSelfClick>();
            _background ??=
                UIChildFinder.FindComponent<Graphic>(transform, "Background")
                ?? UIChildFinder.FindComponent<Graphic>(transform, "BackGround");
            _title ??= UIChildFinder.FindComponent<TMP_Text>(transform, "Title");
            _itemImage ??=
                UIChildFinder.FindComponent<Image>(transform, "Icon")
                ?? UIChildFinder.FindComponent<Image>(transform, "ItemImage");
            _itemName ??=
                UIChildFinder.FindComponent<TMP_Text>(transform, "Name")
                ?? UIChildFinder.FindComponent<TMP_Text>(transform, "ItemName");
            _itemParameters ??= UIChildFinder.FindComponent<TMP_Text>(transform, "ItemParameters");
        }
#endif

        public void Show(Sprite icon, string itemName, Action onClosed) =>
            Show(icon, itemName, string.Empty, false, onClosed);

        public void Show(Sprite icon, string itemName, bool showNewBadge, Action onClosed) =>
            Show(icon, itemName, string.Empty, showNewBadge, onClosed);

        public void Show(
            Sprite icon,
            string itemName,
            string itemParameters,
            bool showNewBadge,
            Action onClosed
        )
        {
            _closedAction = onClosed;
            RefreshContent(icon, itemName, itemParameters, showNewBadge);
            _closeOnSelfClick?.SetClickAction(Hide);
            CraftUIAnimationUtility.PlayResultIn(gameObject);
        }

        public void Hide()
        {
            Action closedAction = _closedAction;
            _closedAction = null;
            _closeOnSelfClick?.ClearClickAction(Hide);

            CraftUIAnimationUtility.PlayResultOut(
                gameObject,
                () =>
                {
                    ClearContent();
                    closedAction?.Invoke();
                }
            );
        }

        public void HideImmediate()
        {
            _closedAction = null;
            _closeOnSelfClick?.ClearClickAction(Hide);
            CraftUIAnimationUtility.HideResultImmediately(gameObject);
            ClearContent();
        }

        private void OnDestroy()
        {
            _closeOnSelfClick?.ClearClickAction(Hide);
            _closedAction = null;
        }

        private void RefreshContent(
            Sprite icon,
            string itemName,
            string itemParameters,
            bool showNewBadge
        )
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = icon;
                _itemImage.color = icon != null ? Color.white : Color.clear;
                _itemImage.gameObject.SetActive(icon != null);
            }

            if (_itemName != null)
                _itemName.text = itemName ?? string.Empty;

            SetItemParameters(itemParameters);

            SetNewBadgeVisible(showNewBadge);
        }

        private void ClearContent()
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = null;
                _itemImage.color = Color.clear;
                _itemImage.gameObject.SetActive(false);
            }

            if (_itemName != null)
                _itemName.text = string.Empty;

            SetItemParameters(string.Empty);

            SetNewBadgeVisible(false);
        }

        private void SetItemParameters(string itemParameters)
        {
            bool hasParameters = !string.IsNullOrWhiteSpace(itemParameters);
            if (!hasParameters && _itemParameters == null)
                return;

            EnsureItemParameters();
            _itemParameters.text = itemParameters ?? string.Empty;
            _itemParameters.gameObject.SetActive(hasParameters);
        }

        private void EnsureItemParameters()
        {
            if (_itemParameters != null)
                return;

            var parametersObject = new GameObject(
                "ItemParameters",
                typeof(RectTransform),
                typeof(CanvasRenderer)
            );
            var parametersRect = (RectTransform)parametersObject.transform;
            parametersRect.SetParent(transform, false);
            parametersRect.anchorMin = new Vector2(0.5f, 0.5f);
            parametersRect.anchorMax = new Vector2(0.5f, 0.5f);
            parametersRect.pivot = new Vector2(0.5f, 1f);
            parametersRect.anchoredPosition = new Vector2(0f, -120f);
            parametersRect.sizeDelta = new Vector2(760f, 180f);

            _itemParameters = parametersObject.AddComponent<TextMeshProUGUI>();
            _itemParameters.alignment = TextAlignmentOptions.Top;
            _itemParameters.fontSize = 28f;
            _itemParameters.enableAutoSizing = true;
            _itemParameters.fontSizeMin = 18f;
            _itemParameters.fontSizeMax = 28f;
            _itemParameters.color = Color.white;
            _itemParameters.raycastTarget = false;

            if (_itemName != null)
            {
                _itemParameters.font = _itemName.font;
                _itemParameters.fontSharedMaterial = _itemName.fontSharedMaterial;
                if (_itemName.transform is RectTransform itemNameRect)
                {
                    parametersRect.anchorMin = itemNameRect.anchorMin;
                    parametersRect.anchorMax = itemNameRect.anchorMax;
                    parametersRect.pivot = new Vector2(itemNameRect.pivot.x, 1f);
                    parametersRect.anchoredPosition =
                        itemNameRect.anchoredPosition
                        + new Vector2(0f, -(itemNameRect.rect.height * itemNameRect.pivot.y + 12f));
                }
            }
        }

        private void SetNewBadgeVisible(bool visible)
        {
            if (!visible && _newBadge == null)
                return;

            EnsureNewBadge();
            _newBadge.gameObject.SetActive(visible);
        }

        private void EnsureNewBadge()
        {
            if (_newBadge != null)
                return;

            var badgeObject = new GameObject(
                "NewCraftedItemBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer)
            );
            var badgeRect = (RectTransform)badgeObject.transform;
            badgeRect.SetParent(transform, false);
            badgeRect.anchorMin = Vector2.one;
            badgeRect.anchorMax = Vector2.one;
            badgeRect.pivot = Vector2.one;
            badgeRect.anchoredPosition = new Vector2(-36f, -36f);
            badgeRect.sizeDelta = new Vector2(220f, 70f);
            badgeRect.SetAsLastSibling();

            _newBadge = badgeObject.AddComponent<TextMeshProUGUI>();
            _newBadge.text = "NEW!!";
            _newBadge.alignment = TextAlignmentOptions.TopRight;
            _newBadge.fontSize = 40f;
            _newBadge.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _newBadge.color = new Color(1f, 0.82f, 0.12f, 1f);
            _newBadge.raycastTarget = false;
        }
    }
}
