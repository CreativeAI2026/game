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
        }
#endif

        public void Show(Sprite icon, string itemName, Action onClosed) =>
            Show(icon, itemName, false, onClosed);

        public void Show(Sprite icon, string itemName, bool showNewBadge, Action onClosed)
        {
            _closedAction = onClosed;
            RefreshContent(icon, itemName, showNewBadge);
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

        private void RefreshContent(Sprite icon, string itemName, bool showNewBadge)
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = icon;
                _itemImage.color = icon != null ? Color.white : Color.clear;
                _itemImage.gameObject.SetActive(icon != null);
            }

            if (_itemName != null)
                _itemName.text = itemName ?? string.Empty;

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

            SetNewBadgeVisible(false);
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
