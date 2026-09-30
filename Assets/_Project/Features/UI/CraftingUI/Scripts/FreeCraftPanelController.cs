using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class FreeCraftPanelController : MonoBehaviour
    {
        public const int MaterialSlotCount = 2;

        [SerializeField]
        private CraftPanelController _craftPanel;

        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private MaterialSlot[] _materialSlots = new MaterialSlot[MaterialSlotCount];

        [SerializeField]
        private SlotIconView _craftedItemSlot;

        [Header("Craft Flow")]
        [SerializeField]
        private Button _craftButton;

        private readonly ItemStack[] _materials = new ItemStack[MaterialSlotCount];
        private readonly HashSet<string> _warnedMessages = new();
        private InventoryManager _subscribedInventoryManager;
        private int _selectedMaterialSlotIndex = -1;
        private bool _isSubscribed;
        private bool _isCrafting;
        private bool _ownsCraftFlow;
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private ItemData _previewedCraftedItem;
        private bool _previewedCraftedItemKnown;
        private TMP_Text _unknownCraftedItemLabel;

        private CraftRecipeDB RecipeDB => _craftPanel != null ? _craftPanel.RecipeDB : null;

        private bool IsCraftInteractionLocked =>
            _isCrafting || (_craftPanel?.IsCraftFlowRunning ?? false);

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanelController>(true);
            _inventory ??= GetComponentInChildren<InventoryView>(true);
            if (_materialSlots.Any(slot => slot == null))
                _materialSlots = GetComponentsInChildren<MaterialSlot>(true)
                    .OrderBy(slot => slot.transform.GetSiblingIndex())
                    .Take(MaterialSlotCount)
                    .ToArray();
            _craftButton ??= UIChildFinder.FindButton(transform, "CraftButton");
        }
#endif

        private void Awake()
        {
            if (!Initialize())
                enabled = false;
        }

        private void OnEnable()
        {
            if (!Initialize())
            {
                enabled = false;
                return;
            }

            Subscribe();
            ResetSlots(resetInventoryView: true);
            SelectFirstSlotIfNeeded();
            RestartInitialSelectionRoutine();
            StopCraftRoutine();
            _craftPanel.HideLoadingAndResult();
            _craftPanel.HideWarning();
            UpdateCraftButton();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopCraftRoutine();
            if (_ownsCraftFlow)
                _craftPanel?.CancelCraftFlow();
            _ownsCraftFlow = false;
            SetCraftInteractionEnabled(true);
            StopInitialSelectionRoutine();
            ResetMaterialAssignments(resetSelection: true);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            UpdateMaterialSlotKeyboardNavigation();
        }

        private bool Initialize()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_craftPanel, nameof(_craftPanel));
            valid &= ValidateRequiredReference(_inventory, nameof(_inventory));
            valid &= ValidateRequiredReference(_craftedItemSlot, nameof(_craftedItemSlot));
            valid &= ValidateRequiredReference(_craftButton, nameof(_craftButton));
            valid &= ValidateRequiredReference(RecipeDB, nameof(CraftPanelController.RecipeDB));
            if (!valid)
                return false;

            if (
                _materialSlots.Length != MaterialSlotCount
                || _materialSlots.Any(slot => slot == null)
                || _materialSlots.Distinct().Count() != MaterialSlotCount
            )
            {
                WarnOnce(
                    $"{nameof(FreeCraftPanelController)} on {name}: {nameof(_materialSlots)} に MaterialSlot を{MaterialSlotCount}つ、表示順に設定してください。"
                );
                return false;
            }

            _craftedItemSlot.GetComponent<SlotFrameView>()?.SetRole(SlotFrameRole.ItemSet);
            _inventory.SetSelectFirstSlotOnRefresh(false);
            _inventory.SetReleaseSelectionOnOutsideClick(false);
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);
            _craftButton.onClick.RemoveListener(StartCraft);
            _craftButton.onClick.AddListener(StartCraft);
            return true;
        }

        private void Subscribe()
        {
            if (_isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted += OnInventorySlotSubmitted;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
            foreach (var slot in _materialSlots)
            {
                slot.Clicked += OnMaterialSlotClicked;
                slot.DoubleClicked += OnMaterialSlotDoubleClicked;
                slot.NormalizeVisualState();
            }
            _craftPanel.CraftInteractionChanged += SetCraftInteractionEnabled;
            SetCraftInteractionEnabled(!_craftPanel.IsCraftFlowRunning);
            _subscribedInventoryManager = InventoryManager.Instance;
            if (_subscribedInventoryManager != null)
                _subscribedInventoryManager.InventoryChanged += OnInventoryChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            if (_inventory != null)
            {
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
                _inventory.OnSlotSubmitted -= OnInventorySlotSubmitted;
                _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
                _inventory.ItemsRequested -= OnInventoryItemsRequested;
            }

            foreach (var slot in _materialSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnMaterialSlotClicked;
                slot.DoubleClicked -= OnMaterialSlotDoubleClicked;
            }

            if (_craftPanel != null)
                _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;

            if (_subscribedInventoryManager != null)
            {
                _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
                _subscribedInventoryManager = null;
            }

            _isSubscribed = false;
        }

        private void OnInventoryChanged()
        {
            _inventory.RefreshCurrentTab();
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition definition,
            int tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                _inventory.RequestItems(inventoryDefinition.Category, scrollMode);
                return;
            }

            WarnOnce(
                $"{nameof(FreeCraftPanelController)} on {name}: Inventoryのタブ {tabIndex} を解決できません。すべての Inventory TabEntry に {nameof(InventoryTabDefinition)} を設定してください。"
            );
            _inventory.SetItems(null, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            var items = InventoryManager.Instance?.GetItemsByCategory(category);
            _inventory.SetItems(items, scrollMode);
        }

        private void SelectFirstSlotIfNeeded()
        {
            if (_selectedMaterialSlotIndex < 0)
                SelectSlot(0);

            ClaimMaterialSlotFocus();
        }

        private void RestartInitialSelectionRoutine()
        {
            StopInitialSelectionRoutine();
            _initialSelectionRoutine = StartCoroutine(SelectFirstSlotNextFrame());
        }

        private IEnumerator SelectFirstSlotNextFrame()
        {
            yield return null;

            SelectSlot(0);
            ClaimMaterialSlotFocus();
            _initialSelectionRoutine = null;
        }

        private void StopInitialSelectionRoutine()
        {
            if (_initialSelectionRoutine == null)
                return;

            StopCoroutine(_initialSelectionRoutine);
            _initialSelectionRoutine = null;
        }

        private void ResetSlots(bool resetInventoryView)
        {
            ResetMaterialAssignments(resetSelection: true);
            if (resetInventoryView)
                _inventory.ResetViewState();
            else
                _inventory.RefreshCurrentTab();
            UpdateCraftButton();
        }

        private void ResetMaterialAssignments(bool resetSelection)
        {
            Array.Clear(_materials, 0, _materials.Length);
            foreach (var slot in _materialSlots)
            {
                if (slot == null)
                    continue;
                slot.Clear();
                slot.SetSelected(false);
            }
            _previewedCraftedItem = null;
            _previewedCraftedItemKnown = false;
            _craftedItemSlot?.Clear();
            SetUnknownCraftedItemVisible(false);
            if (resetSelection)
                _selectedMaterialSlotIndex = -1;

            SyncInventoryAssignedColors();
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= MaterialSlotCount)
                return;

            _selectedMaterialSlotIndex = index;
            HighlightSlot(index);

            if (_materials[index] != null)
                _inventory.SelectItem(_materials[index]);
            else
                _inventory.ClearSelection();
        }

        private void OnMaterialSlotClicked(MaterialSlot slot)
        {
            int index = Array.IndexOf(_materialSlots, slot);
            if (IsCraftInteractionLocked)
                return;

            SlotKeyboardFocus.Claim(this);
            SelectSlot(index);
        }

        private void OnMaterialSlotDoubleClicked(MaterialSlot slot)
        {
            int index = Array.IndexOf(_materialSlots, slot);
            if (IsCraftInteractionLocked || index < 0)
                return;

            SlotKeyboardFocus.Claim(this);
            ClearSlotAnimated(index);
        }

        private void HighlightSlot(int index)
        {
            for (int i = 0; i < _materialSlots.Length; i++)
                _materialSlots[i].SetSelected(i == index);
        }

        private void ClaimMaterialSlotFocus()
        {
            if (_selectedMaterialSlotIndex >= 0)
                SlotKeyboardFocus.Claim(this);
        }

        private void ClearSlotAnimated(int index)
        {
            SelectSlot(index);
            _materials[index] = null;
            _materialSlots[index]
                .ClearMaterialAnimated(() =>
                {
                    SyncInventoryAssignedColors();
                    _inventory.ClearSelection();
                    UpdateCraftButton();
                });
            _inventory.ClearSelection();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (IsCraftInteractionLocked || _selectedMaterialSlotIndex < 0 || !IsValidStack(stack))
                return;

            if (ShowWarningIfUnusable(stack))
                return;

            // 割り当て済みの素材をもう一度選んだら外す。
            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                ClearSlotAnimated(assignedIndex);
                return;
            }

            if (HasCategoryMismatchWithOtherSlot(stack.Data))
            {
                _craftPanel.ShowWarning(CraftBlockReason.CategoryMismatch);
                return;
            }

            AssignMaterial(_selectedMaterialSlotIndex, stack);
            SelectNextEmptySlot();
        }

        private void OnInventorySlotSubmitted(ItemStack stack)
        {
            if (IsCraftInteractionLocked || !IsValidStack(stack))
                return;

            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                _selectedMaterialSlotIndex = assignedIndex;
                _materials[assignedIndex] = null;
                HighlightSlot(assignedIndex);
                _materialSlots[assignedIndex].ClearMaterialAnimated();
                SyncInventoryAssignedColors();
                UpdateCraftButton();
                _inventory.SelectItem(stack);
                SlotKeyboardFocus.Claim(_inventory);
                return;
            }

            int destinationIndex = Array.IndexOf(_materials, null);
            if (destinationIndex < 0)
                return;

            _selectedMaterialSlotIndex = destinationIndex;
            HighlightSlot(destinationIndex);
            if (ShowWarningIfUnusable(stack))
                return;

            if (HasCategoryMismatchWithOtherSlot(stack.Data))
            {
                _craftPanel.ShowWarning(CraftBlockReason.CategoryMismatch);
                return;
            }

            AssignMaterial(destinationIndex, stack);
            _inventory.SelectItem(stack);
            SlotKeyboardFocus.Claim(_inventory);
        }

        private static bool IsValidStack(ItemStack stack) => stack?.Data != null && stack.Count > 0;

        private void AssignMaterial(int index, ItemStack stack)
        {
            _materials[index] = stack;
            _materialSlots[index].SetMaterialAnimated(stack);
            SyncInventoryAssignedColors();
            UpdateCraftButton();
        }

        private bool ShowWarningIfUnusable(ItemStack stack)
        {
            if (stack.IsEquipped)
            {
                _craftPanel.ShowWarning(CraftBlockReason.EquippedMaterial);
                return true;
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInQuickFood(stack))
            {
                _craftPanel.ShowWarning(CraftBlockReason.QuickFoodMaterial);
                return true;
            }

            return false;
        }

        private bool HasCategoryMismatchWithOtherSlot(ItemData item)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (i == _selectedMaterialSlotIndex || _materials[i]?.Data == null)
                    continue;

                if (_materials[i].Data.category != item.category)
                    return true;
            }

            return false;
        }

        private int FindAssignedIndex(ItemData item)
        {
            return Array.FindIndex(_materials, stack => stack?.Data == item);
        }

        private void SelectNextEmptySlot()
        {
            for (int offset = 1; offset < _materials.Length; offset++)
            {
                int index = (_selectedMaterialSlotIndex + offset) % _materials.Length;
                if (_materials[index] != null)
                    continue;

                SelectSlot(index);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedStacks(_materials.Where(stack => stack != null).ToList());
        }

        private bool HasAllMaterials() => _materials.All(stack => stack != null);

        private bool HasCategoryMismatch() =>
            HasAllMaterials() && _materials[0].Data.category != _materials[1].Data.category;

        private bool HasEquippedMaterial() =>
            _materials.Any(stack => stack != null && stack.IsEquipped);

        private CraftRecipe FindAssignedRecipe()
        {
            return HasAllMaterials()
                ? RecipeDB?.FindRecipe(_materials[0].Data, _materials[1].Data)
                : null;
        }

        private bool CanCraft(CraftRecipe recipe)
        {
            return recipe != null
                && (
                    InventoryManager.Instance?.CanCraft(recipe, _materials[0], _materials[1])
                    ?? false
                );
        }

        private void UpdateCraftButton()
        {
            var recipe = FindAssignedRecipe();
            bool canCraft = CanCraft(recipe);

            RefreshCraftedItemSlot(recipe);

            if (_craftButton != null)
                _craftButton.interactable = !IsCraftInteractionLocked && canCraft;

            if (IsCraftInteractionLocked || canCraft)
                _craftPanel?.HideWarning();
            else if (HasEquippedMaterial())
                _craftPanel?.ShowWarning(CraftBlockReason.EquippedMaterial);
            else if (!HasAllMaterials())
                _craftPanel?.HideWarning();
            else if (HasCategoryMismatch())
                _craftPanel?.ShowWarning(CraftBlockReason.CategoryMismatch);
        }

        private void RefreshCraftedItemSlot(CraftRecipe recipe)
        {
            ItemData resultItem = recipe != null ? recipe.resultItem : null;
            bool isKnown = IsCraftedItemKnown(recipe);
            if (
                _craftedItemSlot == null
                || (_previewedCraftedItem == resultItem && _previewedCraftedItemKnown == isKnown)
            )
                return;

            _previewedCraftedItem = resultItem;
            _previewedCraftedItemKnown = isKnown;
            if (resultItem != null && isKnown)
            {
                SetUnknownCraftedItemVisible(false);
                _craftedItemSlot.SetIcon(resultItem);
                return;
            }

            _craftedItemSlot.Clear();
            SetUnknownCraftedItemVisible(resultItem != null);
        }

        internal static bool IsCraftedItemKnown(
            CraftRecipe recipe,
            RecipeBookManager recipeBook = null
        )
        {
            if (recipe == null || recipe.resultItem == null)
                return false;

            recipeBook ??= RecipeBookManager.Instance;
            return recipeBook != null && recipeBook.IsRevealed(recipe);
        }

        private void SetUnknownCraftedItemVisible(bool visible)
        {
            if (!visible && _unknownCraftedItemLabel == null)
                return;

            EnsureUnknownCraftedItemLabel();
            _unknownCraftedItemLabel.gameObject.SetActive(visible);
        }

        private void EnsureUnknownCraftedItemLabel()
        {
            if (_unknownCraftedItemLabel != null || _craftedItemSlot == null)
                return;

            var labelObject = new GameObject(
                "UnknownCraftedItemLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer)
            );
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(_craftedItemSlot.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.SetAsLastSibling();

            _unknownCraftedItemLabel = labelObject.AddComponent<TextMeshProUGUI>();
            _unknownCraftedItemLabel.text = "？";
            _unknownCraftedItemLabel.alignment = TextAlignmentOptions.Center;
            _unknownCraftedItemLabel.enableAutoSizing = true;
            _unknownCraftedItemLabel.fontSizeMin = 24f;
            _unknownCraftedItemLabel.fontSizeMax = 96f;
            _unknownCraftedItemLabel.color = Color.white;
            _unknownCraftedItemLabel.raycastTarget = false;
        }

        private void StartCraft()
        {
            if (IsCraftInteractionLocked)
                return;

            var recipe = FindAssignedRecipe();
            if (!CanCraft(recipe))
            {
                if (HasEquippedMaterial())
                    _craftPanel.ShowWarning(CraftBlockReason.EquippedMaterial);
                else if (HasCategoryMismatch())
                    _craftPanel.ShowWarning(CraftBlockReason.CategoryMismatch);

                return;
            }

            StopCraftRoutine();
            _ownsCraftFlow = true;
            _craftRoutine = StartCoroutine(CraftRoutine(recipe, _materials[0], _materials[1]));
        }

        private IEnumerator CraftRoutine(
            CraftRecipe recipe,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        )
        {
            _isCrafting = true;
            bool isNewRecipe = !IsCraftedItemKnown(recipe);
            bool crafted = false;
            try
            {
                UpdateCraftButton();
                yield return _craftPanel.RunCraftFlow(
                    () => crafted = TryCraftAndReveal(recipe, firstMaterial, secondMaterial),
                    recipe.resultItem,
                    1,
                    OnResultClosed,
                    showNewBadge: isNewRecipe
                );

                if (crafted)
                {
                    yield return null;
                    ResetMaterialAssignments(resetSelection: false);
                    _inventory.RefreshCurrentTab();
                }
            }
            finally
            {
                _craftRoutine = null;
                _isCrafting = false;
                if (!_craftPanel.IsCraftFlowRunning)
                    _ownsCraftFlow = false;
                UpdateCraftButton();
            }
        }

        private bool TryCraftAndReveal(
            CraftRecipe recipe,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        )
        {
            bool crafted =
                InventoryManager.Instance?.TryCraft(recipe, firstMaterial, secondMaterial) ?? false;
            if (!crafted)
                return false;

            if (RecipeBookManager.Instance != null)
                RecipeBookManager.Instance.Reveal(recipe);
            else
                WarnOnce(
                    $"[RecipeDiscovery] {UIHierarchyPathUtility.GetPath(transform)}: {nameof(RecipeBookManager)}.{nameof(RecipeBookManager.Instance)} が null のため、レシピ '{recipe.resultItem.itemName}' を解禁できません。Field_Area01 に入る前にセッションのブートストラップを通して起動してください。"
                );

            return true;
        }

        private void OnResultClosed()
        {
            _ownsCraftFlow = false;
            ResetSlots(resetInventoryView: false);
            SelectFirstSlotIfNeeded();
        }

        private void StopCraftRoutine()
        {
            if (_craftRoutine != null)
            {
                StopCoroutine(_craftRoutine);
                _craftRoutine = null;
            }

            _isCrafting = false;
        }

        private void UpdateMaterialSlotKeyboardNavigation()
        {
            if (
                IsCraftInteractionLocked
                || _selectedMaterialSlotIndex < 0
                || !SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            int offset =
                keyboard.leftArrowKey.wasPressedThisFrame ? -1
                : keyboard.rightArrowKey.wasPressedThisFrame ? 1
                : 0;
            if (offset != 0)
                SelectSlot(
                    (_selectedMaterialSlotIndex + offset + MaterialSlotCount) % MaterialSlotCount
                );
        }

        private void SetCraftInteractionEnabled(bool enabled)
        {
            _inventory?.SetInteractionEnabled(enabled);
            UpdateCraftButton();
        }

        private bool ValidateRequiredReference(UnityEngine.Object reference, string referenceName)
        {
            if (reference != null)
                return true;

            WarnOnce(
                $"{nameof(FreeCraftPanelController)} on {name}: {referenceName} が見つかりません。Inspector参照を設定するか、Prefab上の名前を確認してください。"
            );
            return false;
        }

        private void WarnOnce(string message)
        {
            if (_warnedMessages.Add(message))
                Debug.LogWarning(message, this);
        }
    }
}
