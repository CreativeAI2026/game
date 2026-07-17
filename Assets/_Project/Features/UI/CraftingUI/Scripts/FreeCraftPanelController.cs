using System.Collections;
using System.Collections.Generic;
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
        private CraftPanel _craftPanel;

        [SerializeField]
        private Inventory _inventory;

        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Craft Flow")]
        [SerializeField]
        private Button _craftButton;

        [SerializeField]
        private float _testCraftDuration = 5f;

        [SerializeField]
        private float _gearRotationSpeed = 180f;

        private readonly List<MaterialSlot> _slots = new();
        private MaterialSlot _selectedSlot;
        private bool _isSubscribed;
        private bool _isCrafting;
        private CraftRecipeData _lastCraftedRecipe;
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private bool _warnedMissingCraftButton;
        private bool _warnedMissingCraftPanel;
        private bool _warnedMissingInventory;
        private bool _warnedMissingSlotsRoot;
        private bool _warnedMissingDetailPanel;
        private bool _warnedMissingMaterialSlots;
        private bool _warnedInvalidTabDefinition;

        private CraftRecipeDB RecipeDB => _craftPanel != null ? _craftPanel.RecipeDB : null;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!Initialize())
                return;

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
            StopInitialSelectionRoutine();
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
                _slotsRoot,
                ref _warnedMissingSlotsRoot,
                nameof(_slotsRoot)
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
            if (!valid)
                return false;

            _inventory.SetSelectFirstSlotOnRefresh(false);
            _inventory.SetReleaseSelectionOnOutsideClick(false);
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);
            InitializeSlots();
            if (_slots.Count == 0)
            {
                WarnMissingReferenceOnce(ref _warnedMissingMaterialSlots, nameof(MaterialSlot));
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

        private void InitializeSlots()
        {
            if (_slotsRoot == null)
                return;

            UnbindMaterialSlots();
            _slots.Clear();
            _selectedSlot = null;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slotObject = _slotsRoot.GetChild(i).gameObject;
                var slot = slotObject.GetComponent<MaterialSlot>();
                if (slot == null)
                    continue;

                slot.NormalizeVisualState();
                slot.Clicked += OnMaterialSlotClicked;
                slot.DoubleClicked += OnMaterialSlotDoubleClicked;
                _slots.Add(slot);
            }
        }

        private void UnbindMaterialSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                slot.Clicked -= OnMaterialSlotClicked;
                slot.DoubleClicked -= OnMaterialSlotDoubleClicked;
            }
        }

        private void Subscribe()
        {
            if (_inventory == null || _isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
                InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
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

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;

            _isSubscribed = false;
        }

        private void OnInventoryChanged()
        {
            _inventory?.RefreshCurrentTab();
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition definition,
            int tabIndex,
            Inventory.ScrollRefreshMode scrollMode
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
            Inventory.ScrollRefreshMode scrollMode
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
            if (_selectedSlot == null && _slots.Count > 0)
                SelectSlot(_slots[0]);

            ClaimMaterialSlotFocus();
        }

        private IEnumerator EnsureInitialSelectionNextFrame()
        {
            yield return null;

            if (_slots.Count > 0)
                SelectSlot(_slots[0]);

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
            _selectedSlot = null;

            foreach (var slot in _slots)
            {
                slot.Clear();
                slot.SetSelected(false);
            }

            SyncInventoryAssignedColors();
            _inventory?.ResetViewState();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private void SelectSlot(MaterialSlot selectedSlot)
        {
            if (selectedSlot == null)
                return;

            var previousSlot = _selectedSlot;
            previousSlot?.SetSelected(false);

            foreach (var slot in _slots)
                if (slot != null && slot != previousSlot && slot != selectedSlot)
                    slot.SetSelected(false);

            _selectedSlot = selectedSlot;
            selectedSlot.SetSelected(true);

            if (selectedSlot.Stack != null)
                _inventory?.SelectItem(selectedSlot.Stack);
            else
                _inventory?.ClearSelection();

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != selectedSlot
                && previousSlot.Item == null
                && selectedSlot.Item == null;
            _detailPanel?.Show(selectedSlot.Item, EmptyMaterialLabel, changedBetweenEmptySlots);
        }

        private void OnMaterialSlotClicked(MaterialSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            SelectSlot(slot);
        }

        private void OnMaterialSlotDoubleClicked(MaterialSlot slot)
        {
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
            ClearSlot(slot);
        }

        private void ClaimMaterialSlotFocus()
        {
            if (_selectedSlot != null)
                CreativeAI.UI.SlotKeyboardFocus.Claim(this);
        }

        private void ClearSlot(MaterialSlot slot)
        {
            if (slot == null)
                return;

            SelectSlot(slot);

            slot.ClearMaterialAnimated(() =>
            {
                SyncInventoryAssignedColors();
                RefreshDetailFromSelectedCraftSlot();
                UpdateCraftButton();
            });

            _inventory?.ClearSelection();
            _detailPanel?.Clear();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (_selectedSlot == null || stack?.Data == null || stack.Count <= 0)
                return;

            if (stack.IsEquipped)
            {
                _craftPanel?.ShowEquippedMaterialWarning();
                return;
            }

            if (ClearAssignedMaterialSlot(stack.Data))
                return;

            if (!CanAssignMaterial(stack))
                return;

            ClearMaterialFromOtherSlots(stack.Data, _selectedSlot);

            _selectedSlot.SetMaterialAnimated(stack);
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
                _craftPanel?.ShowEquippedMaterialWarning();
                return false;
            }

            if (HasCategoryMismatchWithAssignedMaterials(stack.Data))
            {
                _craftPanel?.ShowCategoryMismatchWarning();
                return false;
            }

            return true;
        }

        private bool HasCategoryMismatchWithAssignedMaterials(ItemData item)
        {
            if (item == null)
                return false;

            foreach (var slot in _slots)
            {
                if (slot == null || slot == _selectedSlot || slot.Stack?.Data == null)
                    continue;

                if (slot.Stack.Data.category != item.category)
                    return true;
            }

            return false;
        }

        private bool ClearAssignedMaterialSlot(ItemData item)
        {
            var assignedSlot = _slots.FirstOrDefault(slot => slot.Item == item);
            if (assignedSlot == null)
                return false;

            SelectSlot(assignedSlot);

            assignedSlot.ClearMaterialAnimated(() =>
            {
                SyncInventoryAssignedColors();
                _inventory?.ClearSelection();
                RefreshDetailFromSelectedCraftSlot();
                UpdateCraftButton();
            });

            _inventory?.ClearSelection();
            _detailPanel?.Clear();

            return true;
        }

        private void SelectNextEmptyCraftSlot()
        {
            if (_selectedSlot == null || _slots.Count <= 1)
                return;

            int selectedIndex = _slots.IndexOf(_selectedSlot);
            if (selectedIndex < 0)
                return;

            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (selectedIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Item != null)
                    continue;

                SelectSlot(_slots[slotIndex]);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedStacks(
                _slots.Where(slot => slot.Stack != null).Select(slot => slot.Stack)
            );
        }

        private void RefreshDetailFromSelectedCraftSlot()
        {
            if (_detailPanel == null || _selectedSlot == null)
                return;

            _detailPanel.Show(_selectedSlot.Item, EmptyMaterialLabel);
        }

        private void ClearMaterialFromOtherSlots(ItemData item, MaterialSlot destinationSlot)
        {
            foreach (var slot in _slots)
            {
                if (slot == destinationSlot || slot.Item != item)
                    continue;

                slot.ClearMaterialAnimated();
            }
        }

        private void UpdateCraftButton()
        {
            bool hasEnoughMaterials = HasEnoughMaterials();
            bool hasCategoryMismatch = HasCategoryMismatch();
            bool hasEquippedMaterial = HasEquippedMaterial();
            bool hasRecipe = FindSelectedRecipe() != null;
            bool canCraft = hasRecipe && CanCraft();

            if (_craftButton != null)
                _craftButton.interactable = !_isCrafting && canCraft;

            if (_isCrafting || canCraft)
                _craftPanel?.HideWarning();
            else if (hasEquippedMaterial)
                _craftPanel?.ShowEquippedMaterialWarning();
            else if (!hasEnoughMaterials)
                _craftPanel?.HideWarning();
            else if (hasCategoryMismatch)
                _craftPanel?.ShowCategoryMismatchWarning();
        }

        private void StartCraft()
        {
            if (_isCrafting)
                return;

            if (!CanCraft())
            {
                if (HasEquippedMaterial())
                    _craftPanel?.ShowEquippedMaterialWarning();
                else if (HasCategoryMismatch())
                    _craftPanel?.ShowCategoryMismatchWarning();

                return;
            }

            StopCraftRoutine();
            _craftRoutine = StartCoroutine(CraftRoutine());
        }

        private bool CanCraft()
        {
            var recipe = FindSelectedRecipe();
            return recipe != null
                && (
                    InventoryManager.Instance?.CanCraft(
                        recipe,
                        GetMaterialStack(0),
                        GetMaterialStack(1)
                    )
                    ?? false
                );
        }

        private bool HasEnoughMaterials()
        {
            return _slots.Count(slot => slot.Stack != null) >= 2;
        }

        private bool HasCategoryMismatch()
        {
            var selectedItems = _slots
                .Where(slot => slot.Stack?.Data != null)
                .Select(slot => slot.Stack.Data)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return false;

            return selectedItems[0].category != selectedItems[1].category;
        }

        private bool HasEquippedMaterial()
        {
            return _slots
                .Where(slot => slot.Stack != null)
                .Select(slot => slot.Stack)
                .Take(2)
                .Any(stack => stack.IsEquipped);
        }

        private IEnumerator CraftRoutine()
        {
            _isCrafting = true;
            _lastCraftedRecipe = FindSelectedRecipe();
            UpdateCraftButton();

            _craftPanel?.HideWarning();

            _craftPanel?.ShowLoading();

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            bool crafted =
                _lastCraftedRecipe != null
                && (
                    InventoryManager.Instance?.TryCraft(
                        _lastCraftedRecipe,
                        GetMaterialStack(0),
                        GetMaterialStack(1)
                    )
                    ?? false
                );
            if (crafted)
                RecipeDB?.RevealRecipe(
                    _lastCraftedRecipe.material1,
                    _lastCraftedRecipe.material2,
                    out _
                );

            CraftFlowViewUtility.CompleteCraftRoutine(ref _craftRoutine, ref _isCrafting);

            _craftPanel?.HideLoading();
            if (!crafted)
            {
                _craftPanel?.HideLoadingAndResult();
                UpdateCraftButton();
                yield break;
            }

            _craftPanel?.ShowResult(_lastCraftedRecipe?.resultItem, 1, CloseResult);

            UpdateCraftButton();
        }

        private CraftRecipeData FindSelectedRecipe()
        {
            if (RecipeDB == null)
                return null;

            var selectedItems = _slots
                .Where(slot => slot.Stack?.Data != null)
                .Select(slot => slot.Stack.Data)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return null;

            return RecipeDB.FindRecipe(selectedItems[0], selectedItems[1]);
        }

        private ItemStack GetMaterialStack(int index)
        {
            return index >= 0 && index < _slots.Count ? _slots[index].Stack : null;
        }

        private void CloseResult()
        {
            ResetSlots();
            SelectFirstSlotIfNeeded();
        }

        private void ResetCraftFlow()
        {
            StopCraftRoutine();
            _lastCraftedRecipe = null;

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
                || _selectedSlot == null
                || _slots.Count <= 0
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
            int currentIndex = _slots.IndexOf(_selectedSlot);
            if (currentIndex < 0)
                return;

            int nextIndex = (currentIndex + offset + _slots.Count) % _slots.Count;
            SelectSlot(_slots[nextIndex]);
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
        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanel>(true);
            _inventory ??= GetComponentInChildren<Inventory>(true);
            _slotsRoot ??= UIChildFinder.Find(transform, "MaterialSlotsRoot");
            _craftButton ??= UIChildFinder.FindButton(transform, "CraftButton");

            if (_detailPanel == null)
            {
                _detailPanel = GetComponentsInChildren<ItemDetailPanel>(true)
                    .FirstOrDefault(panel => panel.GetComponentInParent<Inventory>(true) == null);
            }
        }
#endif
    }
}
