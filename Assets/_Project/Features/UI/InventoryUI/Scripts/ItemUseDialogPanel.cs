using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    [RequireComponent(typeof(Image))]
    public class ItemUseDialogPanel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private Image _itemIconImage;

        [SerializeField]
        private TMP_Text _itemNameText;

        [SerializeField]
        private TMP_Text _itemEffectText;

        [SerializeField]
        private TMP_Text _messageText;

        [SerializeField]
        private Button _useButton;

        private RectTransform _dialogRoot;
        private ItemStack _targetStack;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        public void Show(ItemStack stack)
        {
            EnsureInitialized();

            if (stack?.Data is not FoodData)
            {
                Hide();
                return;
            }

            _targetStack = stack;
            Refresh(stack);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _targetStack = null;
            gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (IsOutsideDialog(eventData))
                Hide();
        }

        private void UseCurrentItem()
        {
            var stack = _targetStack;
            Hide();

            if (stack?.Data is not FoodData)
                return;

            InventoryManager.Instance?.TryUse(stack);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            EnsureBackgroundCanReceiveClicks();
            ResolveReferences();
            BindButtons();
            _initialized = true;
        }

        private void EnsureBackgroundCanReceiveClicks()
        {
            var background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
                background.color = Color.clear;
            }

            if (background != null)
                background.raycastTarget = true;
        }

        private void ResolveReferences()
        {
            _dialogRoot ??= FindChild("ItemUseDialog") as RectTransform;
            _itemIconImage ??= FindComponentInChildren<Image>("ItemIcon");
            _itemNameText ??= FindComponentInChildren<TMP_Text>("ItemName");
            _itemEffectText ??= FindComponentInChildren<TMP_Text>("ItemEffect");
            _messageText ??= FindComponentInChildren<TMP_Text>("Message");
            _useButton ??= FindButton("UseButton");
            _useButton ??= FindButton("YesButton");
        }

        private bool IsOutsideDialog(PointerEventData eventData)
        {
            if (_dialogRoot == null)
                return true;

            return !RectTransformUtility.RectangleContainsScreenPoint(
                _dialogRoot,
                eventData.position,
                eventData.pressEventCamera
            );
        }

        private void BindButtons()
        {
            if (_useButton == null)
                return;

            _useButton.onClick.RemoveListener(UseCurrentItem);
            _useButton.onClick.AddListener(UseCurrentItem);
        }

        private void UnbindButtons()
        {
            if (_useButton != null)
                _useButton.onClick.RemoveListener(UseCurrentItem);
        }

        private void Refresh(ItemStack stack)
        {
            var item = stack.Data;
            string itemName = GetItemName(stack);
            string effectText = GetEffectText(item);
            string message = $"{itemName}\u3092\u4f7f\u7528\u3057\u307e\u3059\u304b\uff1f";

            if (_itemIconImage != null)
            {
                _itemIconImage.sprite = item.icon;
                _itemIconImage.gameObject.SetActive(item.icon != null);
            }

            SetText(_itemNameText, itemName);
            SetText(_itemEffectText, effectText);
            SetText(_messageText, message);
        }

        private static void SetText(TMP_Text tmpText, string text)
        {
            if (tmpText != null)
                tmpText.text = text;
        }

        private string GetItemName(ItemStack stack)
        {
            if (!string.IsNullOrWhiteSpace(stack.Data.itemName))
                return stack.Data.itemName;

            return stack.Data.name;
        }

        private string GetEffectText(ItemData item)
        {
            if (!string.IsNullOrWhiteSpace(item.effect))
                return item.effect;
            if (!string.IsNullOrWhiteSpace(item.description))
                return item.description;

            return string.Empty;
        }

        private Button FindButton(string objectName)
        {
            var child = FindChild(objectName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private T FindComponentInChildren<T>(string objectName)
            where T : Component
        {
            var child = FindChild(objectName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private Transform FindChild(string objectName)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect != null && rect.name == objectName)
                    return rect;
            }

            return null;
        }
    }
}
