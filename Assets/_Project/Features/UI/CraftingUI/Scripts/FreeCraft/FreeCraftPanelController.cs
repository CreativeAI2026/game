using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class FreeCraftPanelController : MonoBehaviour
    {
        private const string EmptyMaterialLabel = "\uFF08\u672A\u9078\u629E\uFF09";

        [SerializeField]
        private CraftPanelController _craftPanel;

        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private FreeCraftMaterialSlotsView _materialSlotsView;

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Craft Flow")]
        [SerializeField]
        private Button _craftButton;

        [SerializeField]
        private float _gearRotationSpeed = 180f;

        private readonly FreeCraftMaterialAssignmentState _materialAssignmentState = new();
        private FreeCraftRecipeResolver _recipeResolver;
        private InventoryManager _subscribedInventoryManager;
        private int _selectedMaterialSlotIndex = -1;
        private bool _isSubscribed;
        private bool _isCrafting;
        private bool _ownsCraftFlow;
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private bool _warnedMissingCraftButton;
        private bool _warnedMissingCraftPanel;
        private bool _warnedMissingInventory;
        private bool _warnedMissingMaterialSlotsView;
        private bool _warnedMissingDetailPanel;
        private bool _warnedMissingRecipeDatabase;
        private bool _warnedInvalidTabDefinition;
        private bool _warnedMissingRecipeBookManager;

        private CraftRecipeDB RecipeDB => _craftPanel != null ? _craftPanel.RecipeDB : null;

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
            _inventory?.ResetViewState();
            ResetSlots();
            SelectFirstSlotIfNeeded();
            RestartInitialSelectionRoutine();
            ResetCraftFlow();
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
            ResetMaterialAssignments(true);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_isCrafting)
                _craftPanel?.RotateLoadingGear(_gearRotationSpeed);

            UpdateMaterialSlotKeyboardNavigation();
        }

        private bool Initialize()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(
                _craftPanel,
                ref _warnedMissingCraftPanel,
                nameof(_craftPanel)
            );
            valid &= ValidateRequiredReference(
                _inventory,
                ref _warnedMissingInventory,
                nameof(_inventory)
            );
            valid &= ValidateRequiredReference(
                _materialSlotsView,
                ref _warnedMissingMaterialSlotsView,
                nameof(_materialSlotsView)
            );
            valid &= ValidateRequiredReference(
                _detailPanel,
                ref _warnedMissingDetailPanel,
                nameof(_detailPanel)
            );
            valid &= ValidateRequiredReference(
                _craftButton,
                ref _warnedMissingCraftButton,
                nameof(_craftButton)
            );
            valid &= ValidateRequiredReference(
                RecipeDB,
                ref _warnedMissingRecipeDatabase,
                nameof(CraftPanelController.RecipeDB)
            );
            if (!valid)
                return false;

            _recipeResolver ??= new FreeCraftRecipeResolver(RecipeDB);
            _inventory.SetSelectFirstSlotOnRefresh(false);
            _inventory.SetReleaseSelectionOnOutsideClick(false);
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);
            if (
                !_materialSlotsView.HasRequiredReferences
                || _materialSlotsView.SlotCount
                    != FreeCraftMaterialAssignmentState.RequiredSlotCount
            )
            {
                WarnMissingReferenceOnce(
                    ref _warnedMissingMaterialSlotsView,
                    $"{nameof(_materialSlotsView)} slots"
                );
                return false;
            }

            BindCraftFlow();
            return true;
        }

        private void BindCraftFlow()
        {
            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveListener(StartCraft);
                _craftButton.onClick.AddListener(StartCraft);
            }
            else
            {
                WarnMissingReferenceOnce(ref _warnedMissingCraftButton, "CraftButton");
            }
        }

        private void Subscribe()
        {
            if (_inventory == null || _isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
            _materialSlotsView.SlotClicked -= OnMaterialSlotClicked;
            _materialSlotsView.SlotDoubleClicked -= OnMaterialSlotDoubleClicked;
            _materialSlotsView.SlotClicked += OnMaterialSlotClicked;
            _materialSlotsView.SlotDoubleClicked += OnMaterialSlotDoubleClicked;
            _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
            _craftPanel.CraftInteractionChanged += SetCraftInteractionEnabled;
            SetCraftInteractionEnabled(!_craftPanel.IsCraftFlowRunning);
            _subscribedInventoryManager = InventoryManager.Instance;
            if (_subscribedInventoryManager != null)
            {
                _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
                _subscribedInventoryManager.InventoryChanged += OnInventoryChanged;
            }
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (_inventory != null && _isSubscribed)
            {
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
                _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
                _inventory.ItemsRequested -= OnInventoryItemsRequested;
            }

            if (_materialSlotsView != null)
            {
                _materialSlotsView.SlotClicked -= OnMaterialSlotClicked;
                _materialSlotsView.SlotDoubleClicked -= OnMaterialSlotDoubleClicked;
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
            _inventory?.RefreshCurrentTab();
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition definition,
            int tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null)
                return;

            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                _inventory.RequestItems(inventoryDefinition.Category, scrollMode);
                return;
            }

            WarnInvalidTabDefinitionOnce(tabIndex);
            _inventory.SetItems(null, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (_inventory == null)
                return;

            var items = InventoryManager.Instance?.GetItemsByCategory(category);
            _inventory.SetItems(items, scrollMode);
        }

        private void WarnInvalidTabDefinitionOnce(int tabIndex)
        {
            if (_warnedInvalidTabDefinition)
                return;

            _warnedInvalidTabDefinition = true;
            Debug.LogWarning(
                $"{nameof(FreeCraftPanelController)} '{name}' cannot resolve Inventory tab index {tabIndex}. Assign an {nameof(InventoryTabDefinition)} to every Inventory TabEntry.",
                this
            );
        }

        private void SelectFirstSlotIfNeeded()
        {
            if (_selectedMaterialSlotIndex < 0 && _materialSlotsView.SlotCount > 0)
                SelectSlot(0);

            ClaimMaterialSlotFocus();
        }

        private IEnumerator EnsureInitialSelectionNextFrame()
        {
            yield return null;

            if (_materialSlotsView.SlotCount > 0)
                SelectSlot(0);

            ClaimMaterialSlotFocus();

            _initialSelectionRoutine = null;
        }

        private void RestartInitialSelectionRoutine()
        {
            StopInitialSelectionRoutine();
            _initialSelectionRoutine = StartCoroutine(EnsureInitialSelectionNextFrame());
        }

        private void StopInitialSelectionRoutine()
        {
            if (_initialSelectionRoutine == null)
                return;

            StopCoroutine(_initialSelectionRoutine);
            _initialSelectionRoutine = null;
        }

        private void ResetSlots()
        {
            ResetMaterialAssignments(true);
            _inventory?.ResetViewState();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private void ResetMaterialAssignments(bool resetSelection)
        {
            _materialAssignmentState.ClearAll();
            _materialSlotsView?.ResetAll();
            if (resetSelection)
                _selectedMaterialSlotIndex = -1;

            SyncInventoryAssignedColors();
        }

        private void SelectSlot(int selectedIndex)
        {
            if (!_materialSlotsView.IsValidIndex(selectedIndex))
                return;

            int previousIndex = _selectedMaterialSlotIndex;
            ItemData previousItem = GetAssignedItem(previousIndex);
            _selectedMaterialSlotIndex = selectedIndex;
            _materialSlotsView.SetSelectedIndex(selectedIndex);

            var selectedStack = _materialAssignmentState.GetStack(selectedIndex);
            if (selectedStack != null)
                _inventory?.SelectItem(selectedStack);
            else
                _inventory?.ClearSelection();

            bool changedBetweenEmptySlots =
                previousIndex >= 0
                && previousIndex != selectedIndex
                && previousItem == null
                && GetAssignedItem(selectedIndex) == null;
            _detailPanel?.Show(
                GetAssignedItem(selectedIndex),
                EmptyMaterialLabel,
                changedBetweenEmptySlots
            );
        }

        private void OnMaterialSlotClicked(int slotIndex)
        {
            if (IsCraftInteractionLocked)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slotIndex);
        }

        private void OnMaterialSlotDoubleClicked(int slotIndex)
        {
            if (IsCraftInteractionLocked)
                return;

            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            ClearSlot(slotIndex);
        }

        private void ClaimMaterialSlotFocus()
        {
            if (_selectedMaterialSlotIndex >= 0)
                CreativeAI.UI.SlotKeyboardFocus.Claim(this);
        }

        private void ClearSlot(int slotIndex)
        {
            if (!_materialSlotsView.IsValidIndex(slotIndex))
                return;

            SelectSlot(slotIndex);
            _materialAssignmentState.ClearStack(slotIndex);

            _materialSlotsView.ClearMaterial(
                slotIndex,
                true,
                () =>
                {
                    SyncInventoryAssignedColors();
                    RefreshDetailFromSelectedCraftSlot();
                    UpdateCraftButton();
                }
            );

            _inventory?.ClearSelection();
            _detailPanel?.Clear();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (IsCraftInteractionLocked)
                return;

            if (_selectedMaterialSlotIndex < 0 || stack?.Data == null || stack.Count <= 0)
                return;

            if (stack.IsEquipped)
            {
                _craftPanel?.ShowWarning(CraftWarningKind.EquippedMaterial);
                return;
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInQuickFood(stack))
            {
                _craftPanel?.ShowQuickFoodMaterialWarning();
                return;
            }

            if (ClearAssignedMaterialSlot(stack.Data))
                return;

            if (!CanAssignMaterial(stack))
                return;

            ClearMaterialFromOtherSlots(stack.Data, _selectedMaterialSlotIndex);

            _materialAssignmentState.SetStack(_selectedMaterialSlotIndex, stack);
            _materialSlotsView.SetMaterial(_selectedMaterialSlotIndex, stack, true);
            SyncInventoryAssignedColors();
            _detailPanel?.Show(stack.Data);
            SelectNextEmptyCraftSlot();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private bool CanAssignMaterial(ItemStack stack)
        {
            if (stack == null || stack.Data == null || stack.Count <= 0)
                return false;

            if (stack.IsEquipped)
            {
                _craftPanel?.ShowWarning(CraftWarningKind.EquippedMaterial);
                return false;
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInQuickFood(stack))
            {
                _craftPanel?.ShowQuickFoodMaterialWarning();
                return false;
            }

            if (HasCategoryMismatchWithAssignedMaterials(stack.Data))
            {
                _craftPanel?.ShowWarning(CraftWarningKind.CategoryMismatch);
                return false;
            }

            return true;
        }

        private bool HasCategoryMismatchWithAssignedMaterials(ItemData item)
        {
            if (item == null)
                return false;

            for (int i = 0; i < _materialSlotsView.SlotCount; i++)
            {
                var assignedStack = _materialAssignmentState.GetStack(i);
                if (i == _selectedMaterialSlotIndex || assignedStack?.Data == null)
                    continue;

                if (assignedStack.Data.category != item.category)
                    return true;
            }

            return false;
        }

        private bool ClearAssignedMaterialSlot(ItemData item)
        {
            int assignedIndex = FindAssignedMaterialIndex(item);
            if (assignedIndex < 0)
                return false;

            SelectSlot(assignedIndex);
            _materialAssignmentState.ClearStack(assignedIndex);

            _materialSlotsView.ClearMaterial(
                assignedIndex,
                true,
                () =>
                {
                    SyncInventoryAssignedColors();
                    _inventory?.ClearSelection();
                    RefreshDetailFromSelectedCraftSlot();
                    UpdateCraftButton();
                }
            );

            _inventory?.ClearSelection();
            _detailPanel?.Clear();

            return true;
        }

        private int FindAssignedMaterialIndex(ItemData item)
        {
            if (item == null)
                return -1;

            for (int i = 0; i < _materialAssignmentState.SlotCount; i++)
            {
                if (_materialAssignmentState.GetStack(i)?.Data == item)
                    return i;
            }

            return -1;
        }

        private ItemData GetAssignedItem(int index)
        {
            return _materialAssignmentState.IsValidIndex(index)
                ? _materialAssignmentState.GetStack(index)?.Data
                : null;
        }

        private void SelectNextEmptyCraftSlot()
        {
            if (_selectedMaterialSlotIndex < 0 || _materialSlotsView.SlotCount <= 1)
                return;

            for (int offset = 1; offset < _materialSlotsView.SlotCount; offset++)
            {
                int slotIndex =
                    (_selectedMaterialSlotIndex + offset) % _materialSlotsView.SlotCount;
                if (_materialAssignmentState.HasStack(slotIndex))
                    continue;

                SelectSlot(slotIndex);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedStacks(_materialAssignmentState.GetAssignedStacks());
        }

        private void RefreshDetailFromSelectedCraftSlot()
        {
            if (_detailPanel == null || _selectedMaterialSlotIndex < 0)
                return;

            _detailPanel.Show(GetAssignedItem(_selectedMaterialSlotIndex), EmptyMaterialLabel);
        }

        private void ClearMaterialFromOtherSlots(ItemData item, int destinationIndex)
        {
            for (int i = 0; i < _materialSlotsView.SlotCount; i++)
            {
                if (i == destinationIndex || GetAssignedItem(i) != item)
                    continue;

                _materialAssignmentState.ClearStack(i);
                _materialSlotsView.ClearMaterial(i, true);
            }
        }

        private void UpdateCraftButton()
        {
            bool hasEnoughMaterials = HasEnoughMaterials();
            bool hasCategoryMismatch = HasCategoryMismatch();
            bool hasEquippedMaterial = HasEquippedMaterial();
            bool canCraft = TryCreateCraftRequest(out var request) && CanCraft(request);

            if (_craftButton != null)
                _craftButton.interactable = !IsCraftInteractionLocked && canCraft;

            if (IsCraftInteractionLocked || canCraft)
                _craftPanel?.HideWarning();
            else if (hasEquippedMaterial)
                _craftPanel?.ShowWarning(CraftWarningKind.EquippedMaterial);
            else if (!hasEnoughMaterials)
                _craftPanel?.HideWarning();
            else if (hasCategoryMismatch)
                _craftPanel?.ShowWarning(CraftWarningKind.CategoryMismatch);
        }

        private void StartCraft()
        {
            if (_isCrafting || IsCraftInteractionLocked)
                return;

            if (!TryCreateCraftRequest(out var request) || !CanCraft(request))
            {
                if (HasEquippedMaterial())
                    _craftPanel?.ShowWarning(CraftWarningKind.EquippedMaterial);
                else if (HasCategoryMismatch())
                    _craftPanel?.ShowWarning(CraftWarningKind.CategoryMismatch);

                return;
            }

            StopCraftRoutine();
            _ownsCraftFlow = true;
            _craftRoutine = StartCoroutine(CraftRoutine(request));
        }

        private bool CanCraft(FreeCraftRequest request)
        {
            return InventoryManager.Instance?.CanCraft(
                    request.Recipe,
                    request.FirstMaterial,
                    request.SecondMaterial
                ) ?? false;
        }

        private bool HasEnoughMaterials()
        {
            return _materialAssignmentState.GetAssignedStacks().Count
                >= FreeCraftMaterialAssignmentState.RequiredSlotCount;
        }

        private bool HasCategoryMismatch()
        {
            var selectedItems = _materialAssignmentState
                .GetAssignedStacks()
                .Where(stack => stack?.Data != null)
                .Select(stack => stack.Data)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return false;

            return selectedItems[0].category != selectedItems[1].category;
        }

        private bool HasEquippedMaterial()
        {
            return _materialAssignmentState
                .GetAssignedStacks()
                .Take(FreeCraftMaterialAssignmentState.RequiredSlotCount)
                .Any(stack => stack.IsEquipped);
        }

        private IEnumerator CraftRoutine(FreeCraftRequest request)
        {
            _isCrafting = true;
            try
            {
                UpdateCraftButton();
                yield return _craftPanel.RunCraftFlow(
                    () => TryExecuteFreeCraft(request),
                    request.Recipe.resultItem,
                    1,
                    CloseResult
                );
            }
            finally
            {
                CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);
                if (!_craftPanel.IsCraftFlowRunning)
                    _ownsCraftFlow = false;
                UpdateCraftButton();
            }
        }

        private bool TryExecuteFreeCraft(FreeCraftRequest request)
        {
            bool crafted =
                InventoryManager.Instance?.TryCraft(
                    request.Recipe,
                    request.FirstMaterial,
                    request.SecondMaterial
                ) ?? false;
            if (!crafted)
                return false;

            if (RecipeBookManager.Instance == null)
                WarnMissingRecipeBookManagerOnce(request.Recipe);
            else
                RecipeDB?.RevealRecipe(request.Recipe);

            return true;
        }

        private bool TryCreateCraftRequest(out FreeCraftRequest request)
        {
            request = default;
            if (_recipeResolver == null)
                return false;

            var assignedStacks = _materialAssignmentState.GetAssignedStacks();
            var resolution = _recipeResolver.Resolve(assignedStacks);
            if (!resolution.Succeeded)
                return false;

            request = new FreeCraftRequest(resolution.Recipe, assignedStacks[0], assignedStacks[1]);
            return true;
        }

        private void WarnMissingRecipeBookManagerOnce(CraftRecipeData recipe)
        {
            if (_warnedMissingRecipeBookManager)
                return;

            _warnedMissingRecipeBookManager = true;
            Debug.LogWarning(
                $"[RecipeDiscovery] {UIHierarchyPathUtility.GetPath(transform)} cannot reveal recipe '{recipe?.name ?? "<null>"}' because {nameof(RecipeBookManager)}.{nameof(RecipeBookManager.Instance)} is null. Start through the session bootstrap before entering Field_Area01.",
                this
            );
        }

        private void CloseResult()
        {
            _ownsCraftFlow = false;
            ResetSlots();
            SelectFirstSlotIfNeeded();
        }

        private void ResetCraftFlow()
        {
            StopCraftRoutine();

            _craftPanel?.HideLoadingAndResult();
            _craftPanel?.HideWarning();
            UpdateCraftButton();
        }

        private void StopCraftRoutine()
        {
            CraftFlowViewUtility.StopCraftRoutine(this, ref _craftRoutine, ref _isCrafting);
        }

        private void UpdateMaterialSlotKeyboardNavigation()
        {
            if (
                !isActiveAndEnabled
                || IsCraftInteractionLocked
                || _selectedMaterialSlotIndex < 0
                || _materialSlotsView.SlotCount <= 0
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectMaterialSlotByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectMaterialSlotByOffset(1);
        }

        private void SelectMaterialSlotByOffset(int offset)
        {
            int nextIndex =
                (_selectedMaterialSlotIndex + offset + _materialSlotsView.SlotCount)
                % _materialSlotsView.SlotCount;
            SelectSlot(nextIndex);
        }

        private bool IsCraftInteractionLocked =>
            _isCrafting || (_craftPanel?.IsCraftFlowRunning ?? false);

        private void SetCraftInteractionEnabled(bool enabled)
        {
            _inventory?.SetInteractionEnabled(enabled);
            UpdateCraftButton();
        }

        private bool ValidateRequiredReference(
            Object reference,
            ref bool warned,
            string referenceName
        )
        {
            if (reference != null)
                return true;

            WarnMissingReferenceOnce(ref warned, referenceName);
            return false;
        }

        private void WarnMissingReferenceOnce(ref bool flag, string referenceName)
        {
            if (flag)
                return;

            Debug.LogWarning(
                $"{nameof(FreeCraftPanelController)} on {name}: {referenceName} が見つかりません。Inspector参照を設定するか、Prefab上の名前を確認してください。",
                this
            );
            flag = true;
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanelController>(true);
            _inventory ??= GetComponentInChildren<InventoryView>(true);
            _materialSlotsView ??= GetComponentInChildren<FreeCraftMaterialSlotsView>(true);
            _craftButton ??= UIChildFinder.FindButton(transform, "CraftButton");

            if (_detailPanel == null)
            {
                _detailPanel = GetComponentsInChildren<ItemDetailPanel>(true)
                    .FirstOrDefault(panel =>
                        panel.GetComponentInParent<InventoryView>(true) == null
                    );
            }
        }
#endif
    }
}
