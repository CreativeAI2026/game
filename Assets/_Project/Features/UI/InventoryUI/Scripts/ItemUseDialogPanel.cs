using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class ItemUseDialogPanel : MonoBehaviour
    {
        [SerializeField]
        private CloseOnSelfClick _closeOnSelfClick;

        [SerializeField]
        private Image _backgroundImage;

        [SerializeField]
        private RectTransform _dialogRoot;

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

        private ItemStack _targetStack;
        private bool _initialized;
        private bool _hasWarnedMissingReferences;

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
            if (stack?.Data is not FoodData)
            {
                Hide();
                return;
            }

            if (!EnsureInitialized())
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

        private void UseCurrentItem()
        {
            var stack = _targetStack;
            Hide();

            if (stack?.Data is not FoodData)
                return;

            InventoryManager.Instance?.TryUse(stack);
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
                return true;

            ResolveReferences();
            if (!ValidateRequiredReferences())
                return false;

            BindButtons();
            _initialized = true;
            return true;
        }

        private void ResolveReferences()
        {
            _backgroundImage ??= FindComponentInChildren<Image>("Background");
            _backgroundImage ??= GetComponent<Image>();
            _closeOnSelfClick ??= GetComponent<CloseOnSelfClick>();
            if (_closeOnSelfClick == null && _backgroundImage != null)
                _closeOnSelfClick = _backgroundImage.GetComponent<CloseOnSelfClick>();
            _dialogRoot ??= FindChild("DialogRoot") as RectTransform;
            _dialogRoot ??= FindChild("ItemUseDialog") as RectTransform;
            _itemIconImage ??= FindComponentInChildren<Image>("ItemIcon");
            _itemNameText ??= FindComponentInChildren<TMP_Text>("ItemName");
            _itemEffectText ??= FindComponentInChildren<TMP_Text>("EffectText");
            _itemEffectText ??= FindComponentInChildren<TMP_Text>("ItemEffect");
            _messageText ??= FindComponentInChildren<TMP_Text>("Message");
            _useButton ??= FindButton("UseButton");
            _useButton ??= FindButton("YesButton");
        }

        private bool ValidateRequiredReferences()
        {
            var missingReferences = new List<string>();
            if (_closeOnSelfClick == null)
                missingReferences.Add(nameof(CloseOnSelfClick));
            if (_backgroundImage == null)
                missingReferences.Add("Background Image");
            else if (!_backgroundImage.raycastTarget)
                missingReferences.Add("Background Image の Raycast Target");
            if (_dialogRoot == null)
                missingReferences.Add("Dialog Root");
            else
            {
                var dialogGraphic = _dialogRoot.GetComponent<Graphic>();
                if (dialogGraphic == null)
                    missingReferences.Add("Dialog Root の Graphic");
                else if (!dialogGraphic.raycastTarget)
                    missingReferences.Add("Dialog Root の Raycast Target");
            }
            if (_itemIconImage == null)
                missingReferences.Add("Item Icon Image");
            if (_itemNameText == null)
                missingReferences.Add("Item Name Text");
            if (_itemEffectText == null)
                missingReferences.Add("Effect Text");
            if (_useButton == null)
                missingReferences.Add("Use Button");

            if (missingReferences.Count == 0)
                return true;

            WarnMissingReferencesOnce(missingReferences);
            return false;
        }

        private void WarnMissingReferencesOnce(IReadOnlyCollection<string> missingReferences)
        {
            if (_hasWarnedMissingReferences)
                return;

            _hasWarnedMissingReferences = true;
            Debug.LogWarning(
                $"{nameof(ItemUseDialogPanel)} '{name}' の必須参照が不足しています: {string.Join(", ", missingReferences)}。PrefabまたはScene上で設定してください。",
                this
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
