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
        private Button _useButton;

        private ItemStack _targetStack;
        private bool _initialized;
        private bool _hasWarnedMissingReferences;
        private bool _hasWarnedInvalidConfiguration;

        private void Awake()
        {
            if (!EnsureInitialized())
                Hide();
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
            if (!ValidateRequiredReferences())
                return false;

            if (_initialized)
                return true;

            BindButtons();
            _initialized = true;
            return true;
        }

        private bool ValidateRequiredReferences()
        {
            var missingReferences = new List<string>();
            if (_closeOnSelfClick == null)
                missingReferences.Add(nameof(_closeOnSelfClick));
            if (_backgroundImage == null)
                missingReferences.Add(nameof(_backgroundImage));
            if (_dialogRoot == null)
                missingReferences.Add(nameof(_dialogRoot));
            if (_itemIconImage == null)
                missingReferences.Add(nameof(_itemIconImage));
            if (_itemNameText == null)
                missingReferences.Add(nameof(_itemNameText));
            if (_itemEffectText == null)
                missingReferences.Add(nameof(_itemEffectText));
            if (_useButton == null)
                missingReferences.Add(nameof(_useButton));

            if (missingReferences.Count > 0)
            {
                WarnMissingReferencesOnce(missingReferences);
                return false;
            }

            return ValidateRaycastConfiguration();
        }

        private void WarnMissingReferencesOnce(IReadOnlyCollection<string> missingReferences)
        {
            if (_hasWarnedMissingReferences)
                return;

            _hasWarnedMissingReferences = true;
            Debug.LogWarning(
                $"{nameof(ItemUseDialogPanel)} '{name}' Missing references: {string.Join(", ", missingReferences)}。Inspectorで必須参照を設定してください。",
                this
            );
        }

        private bool ValidateRaycastConfiguration()
        {
            var invalidSettings = new List<string>();
            if (!_backgroundImage.raycastTarget)
                invalidSettings.Add("_backgroundImage.raycastTarget");

            var dialogGraphic = _dialogRoot.GetComponent<Graphic>();
            if (dialogGraphic == null)
                invalidSettings.Add("_dialogRoot.Graphic");
            else if (!dialogGraphic.raycastTarget)
                invalidSettings.Add("_dialogRoot.Graphic.raycastTarget");

            if (invalidSettings.Count == 0)
                return true;

            WarnInvalidConfigurationOnce(invalidSettings);
            return false;
        }

        private void WarnInvalidConfigurationOnce(IReadOnlyCollection<string> invalidSettings)
        {
            if (_hasWarnedInvalidConfiguration)
                return;

            _hasWarnedInvalidConfiguration = true;
            Debug.LogWarning(
                $"{nameof(ItemUseDialogPanel)} '{name}' の必須設定が不正です: {string.Join(", ", invalidSettings)}。PrefabまたはScene上で設定してください。",
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

            if (_itemIconImage != null)
            {
                _itemIconImage.sprite = item.icon;
                _itemIconImage.gameObject.SetActive(item.icon != null);
            }

            SetText(_itemNameText, itemName);
            SetText(_itemEffectText, effectText);
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

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
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
            _useButton ??= FindButton("UseButton");
            _useButton ??= FindButton("YesButton");
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
#endif
    }
}
