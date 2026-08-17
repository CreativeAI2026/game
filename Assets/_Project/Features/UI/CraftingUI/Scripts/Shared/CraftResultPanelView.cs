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

        public void Show(Sprite icon, string itemName, Action onClosed)
        {
            _closedAction = onClosed;
            RefreshContent(icon, itemName);
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

        private void RefreshContent(Sprite icon, string itemName)
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = icon;
                _itemImage.color = icon != null ? Color.white : Color.clear;
                _itemImage.gameObject.SetActive(icon != null);
            }

            if (_itemName != null)
                _itemName.text = itemName ?? string.Empty;
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
        }
    }
}
