using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel
    {
        private void ResolveQuantityDialogReferences()
        {
            _quantityDialogPanel ??= FindGameObjectIn(transform, "CQD-Panel");
            _quantityDialog ??= FindQuantityDialog();

            if (_quantityDialog != null)
                PrepareQuantityDialog();

            if (_quantityDialogPanel != null)
                PrepareQuantityDialogPanel();

            if (_quantityDialog == null)
                return;

            var dialogRoot = _quantityDialog.transform;
            _dialogItemImage ??= FindComponentIn<Image>(dialogRoot, "ItemImage");
            _dialogItemName ??= FindComponentIn<TMP_Text>(dialogRoot, "ItemName");
            _dialogCounts ??= FindComponentIn<TMP_Text>(dialogRoot, "Counts");
            _dialogCounts ??= FindComponentIn<TMP_Text>(dialogRoot, "CountLabel");
            _dialogCounts ??= FindComponentIn<TMP_Text>(dialogRoot, "QuantityLabel");
            _quantityInput ??= FindComponentIn<TMP_InputField>(dialogRoot, "InputField");
            _minButton ??= FindButton(dialogRoot, "MIN");
            _minusButton ??= FindButton(dialogRoot, "-");
            _plusButton ??= FindButton(dialogRoot, "+");
            _maxButton ??= FindButton(dialogRoot, "MAX");
            _cancelButton ??= FindButton(dialogRoot, "CancelButton");
            _dialogCraftButton ??= FindButton(dialogRoot, "CraftButton");

            UIButtonHoverScaleUtility.ApplyTo(_minButton);
            UIButtonHoverScaleUtility.ApplyTo(_minusButton);
            UIButtonHoverScaleUtility.ApplyTo(_plusButton);
            UIButtonHoverScaleUtility.ApplyTo(_maxButton);
            UIButtonHoverScaleUtility.ApplyTo(_cancelButton);
            UIButtonHoverScaleUtility.ApplyTo(_dialogCraftButton);
        }

        private GameObject FindQuantityDialog()
        {
            var dialogTransform =
                _quantityDialogPanel != null
                    ? FindIn(_quantityDialogPanel.transform, "CraftQuantityDialog")
                    : Find("CraftQuantityDialog");

            return dialogTransform != null ? dialogTransform.gameObject : null;
        }

        private void PrepareQuantityDialog()
        {
            _quantityDialogRect ??= _quantityDialog.GetComponent<RectTransform>();
            _quantityDialogCanvasGroup ??= CraftQuantityDialogUtility.PrepareDialog(
                _quantityDialog
            );
        }

        private void PrepareQuantityDialogPanel()
        {
            _quantityDialogPanelClickCatcher ??= CraftQuantityDialogUtility.PrepareBackground(
                _quantityDialogPanel,
                CloseQuantityDialog
            );
        }

        private void BindDialog()
        {
            ResolveQuantityDialogReferences();

            CraftQuantityDialogUtility.BindButton(_minButton, SetMinimum);
            CraftQuantityDialogUtility.BindButton(_minusButton, Decrease);
            CraftQuantityDialogUtility.BindButton(_plusButton, Increase);
            CraftQuantityDialogUtility.BindButton(_maxButton, SetMaximum);
            CraftQuantityDialogUtility.BindButton(_cancelButton, CloseQuantityDialog);
            CraftQuantityDialogUtility.BindButton(_dialogCraftButton, StartCraft);

            if (_quantityInput != null)
            {
                _quantityInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                _quantityInput.onEndEdit.RemoveListener(OnQuantityInput);
                _quantityInput.onEndEdit.AddListener(OnQuantityInput);
            }
        }

        private void OpenQuantityDialog()
        {
            ResolveAllReferences();
            int max = GetMaximumCraftable();
            if (_selectedRecipe == null)
                return;

            if (max <= 0)
            {
                PlayMissingMaterialsWarning();
                return;
            }

            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, max));
            RefreshQuantityDialog();
            PlayDialogOpenAnimation();
        }

        private void CloseQuantityDialog()
        {
            if (_quantityDialog == null)
            {
                SetCloseButtonVisible(true);
                return;
            }

            PlayDialogCloseAnimation();
        }

        private void CloseQuantityDialogImmediately()
        {
            KillDialogAnimation();
            CraftQuantityDialogUtility.HideImmediately(_quantityDialogPanel, _quantityDialog);
            SetCloseButtonVisible(true);
        }

        private void PlayDialogOpenAnimation()
        {
            if (_quantityDialog == null)
                return;

            SetCloseButtonVisible(false);
            CraftQuantityDialogAnimation.PlayOpen(
                _quantityDialogPanel,
                _quantityDialog,
                _quantityDialogRect,
                _quantityDialogCanvasGroup,
                _dialogStartScale,
                _dialogAnimationDuration
            );
        }

        private void PlayDialogCloseAnimation()
        {
            CraftQuantityDialogAnimation.PlayClose(
                _quantityDialogPanel,
                _quantityDialog,
                _quantityDialogRect,
                _quantityDialogCanvasGroup,
                _dialogAnimationDuration,
                () =>
                {
                    if (!_isCrafting)
                        SetCloseButtonVisible(true);
                }
            );
        }

        private void KillDialogAnimation()
        {
            CraftQuantityDialogAnimation.Kill(_quantityDialogRect, _quantityDialogCanvasGroup);
        }

        private void SetMinimum()
        {
            if (_quantity <= 1)
            {
                PlayQuantityLimitWarning();
                return;
            }

            SetQuantity(1);
        }

        private void Decrease()
        {
            if (_quantity <= 1)
            {
                PlayQuantityLimitWarning();
                return;
            }

            SetQuantity(_quantity - 1);
        }

        private void Increase()
        {
            if (_quantity >= GetMaximumCraftable())
            {
                PlayQuantityLimitWarning();
                return;
            }

            SetQuantity(_quantity + 1);
        }

        private void SetMaximum()
        {
            int max = GetMaximumCraftable();
            if (_quantity >= max)
            {
                PlayQuantityLimitWarning();
                return;
            }

            SetQuantity(max);
        }

        private void OnQuantityInput(string value)
        {
            SetQuantity(int.TryParse(value, out int parsed) ? parsed : 1);
        }

        private void SetQuantity(int quantity)
        {
            _quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, GetMaximumCraftable()));
            RefreshQuantityDialog();
        }

        private void PlayQuantityLimitWarning()
        {
            ResolveQuantityDialogReferences();
            CraftUIAnimationUtility.PlayTextLimitWarning(_dialogCounts);
        }

        private void RefreshQuantityDialog()
        {
            ResolveQuantityDialogReferences();
            int max = GetMaximumCraftable();

            CraftQuantityDialogUtility.RefreshItem(
                _dialogItemImage,
                _dialogItemName,
                _selectedRecipe?.resultItem
            );
            CraftQuantityDialogUtility.RefreshQuantity(
                _dialogCounts,
                _quantityInput,
                _dialogCraftButton,
                _quantity,
                max,
                _isCrafting
            );
        }

        private int GetMaximumCraftable()
        {
            if (_selectedRecipe == null)
                return 0;

            if (HasEquippedRecipeMaterial())
                return 0;

            int max = int.MaxValue;
            var materials = _selectedRecipe.Materials.ToList();
            if (materials.Count != 2)
                return 0;

            foreach (var group in materials.GroupBy(material => material))
            {
                if (group.Key == null)
                    return 0;

                int required = group.Count();
                int owned = InventoryManager.Instance?.GetItemCount(group.Key) ?? 0;
                max = Mathf.Min(max, owned / required);
            }

            return Mathf.Max(0, max);
        }
    }
}
