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
    /// <summary>
    /// レシピ調合の画面。解禁済みレシピの一覧 → 選択レシピの素材表示 → 個数ダイアログ → 調合 を扱う。
    /// </summary>
    public class RecipeCraftPanelController : MonoBehaviour
    {
        private const string NoRecipeLabel = "（レシピ不所持）";
        private static readonly Color SufficientColor = new Color32(67, 73, 91, 255);
        private static readonly Color InsufficientColor = new Color32(180, 55, 55, 255);

        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private CraftPanelController _craftPanel;

        [SerializeField]
        private TabGroup _categoryTabGroup;

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private CraftQuantityDialog _quantityDialogController;

        [Header("Recipe List")]
        [SerializeField]
        private Transform _recipeListContent;

        [SerializeField]
        private GameObject _recipeSlotPrefab;

        [Header("Material Rows")]
        [SerializeField]
        private GameObject _materialRowsRoot;

        [SerializeField]
        private List<GameObject> _materialRows = new();

        private readonly HashSet<string> _warnedMissingRequiredReferences = new();
        private readonly List<RecipeSlot> _recipeSlots = new();
        private RecipeBookManager _subscribedRecipeBook;
        private InventoryManager _subscribedInventoryManager;
        private ItemCategory? _selectedCategory;
        private CraftRecipe _selectedRecipe;
        private int _selectedSlotIndex = -1;
        private int _quantity = 1;
        private bool _recipeListInteractable = true;
        private bool _isCrafting;
        private bool _ownsCraftFlow;
        private bool _warnedInvalidCategoryTab;
        private Coroutine _craftRoutine;
        private Coroutine _initializeRoutine;

        private bool IsCraftInteractionLocked =>
            _isCrafting || (_craftPanel?.IsCraftFlowRunning ?? false);

        private RecipeCraftingService CraftingService =>
            _subscribedInventoryManager != null
                ? _subscribedInventoryManager.RecipeCraftingService
                : null;

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Main References")]
        private void AutoAssignMainReferences()
        {
            _craftPanel ??= GetComponentInParent<CraftPanelController>(true);
            _categoryTabGroup ??= GetComponentInChildren<TabGroup>(true);
            _quantityDialogController ??= GetComponentInChildren<CraftQuantityDialog>(true);
            _detailPanel ??= GetComponentsInChildren<ItemDetailPanel>(true)
                .FirstOrDefault(panel => panel.GetComponentInParent<InventoryView>(true) == null);
        }
#endif

        private void Awake()
        {
            if (!HasRequiredReferences())
                return;

            _categoryTabGroup.OnTabDefinitionSelected -= OnCategoryTabSelected;
            _categoryTabGroup.OnTabDefinitionSelected += OnCategoryTabSelected;
            _quantityDialogController.QuantityChanged -= OnQuantityChanged;
            _quantityDialogController.QuantityChanged += OnQuantityChanged;
            _quantityDialogController.Closed -= OnQuantityDialogClosed;
            _quantityDialogController.Closed += OnQuantityDialogClosed;

            ClearRecipeSlots();
            ClearMaterialRows();
            ResetView();
        }

        private void OnEnable()
        {
            if (!HasRequiredReferences())
                return;

            _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
            _craftPanel.CraftInteractionChanged += SetCraftInteractionEnabled;
            SetCraftInteractionEnabled(!_craftPanel.IsCraftFlowRunning);
            SubscribeInventoryChanges();
            SubscribeRecipeBookChanges();

            if (_initializeRoutine != null)
                StopCoroutine(_initializeRoutine);
            _initializeRoutine = StartCoroutine(InitializeViewRoutine());
        }

        private void OnDisable()
        {
            if (_craftPanel != null)
                _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
            UnsubscribeInventoryChanges();
            UnsubscribeRecipeBookChanges();

            if (_craftRoutine != null)
            {
                StopCoroutine(_craftRoutine);
                _craftRoutine = null;
            }
            if (_initializeRoutine != null)
            {
                StopCoroutine(_initializeRoutine);
                _initializeRoutine = null;
            }

            if (_ownsCraftFlow)
                _craftPanel?.CancelCraftFlow();
            _ownsCraftFlow = false;
            _isCrafting = false;
            SetCraftInteractionEnabled(true);
            _craftPanel?.HideWarning();
            _quantityDialogController?.HideImmediate();
            _craftPanel?.HideLoadingAndResult();
        }

        private void OnDestroy()
        {
            if (_craftPanel != null)
                _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
            UnsubscribeInventoryChanges();
            UnsubscribeRecipeBookChanges();

            if (_categoryTabGroup != null)
                _categoryTabGroup.OnTabDefinitionSelected -= OnCategoryTabSelected;
            if (_quantityDialogController != null)
            {
                _quantityDialogController.QuantityChanged -= OnQuantityChanged;
                _quantityDialogController.Closed -= OnQuantityDialogClosed;
            }
            foreach (var slot in _recipeSlots)
                UnbindSlot(slot);
        }

        private void Update()
        {
            UpdateRecipeListKeyboardNavigation();
        }

        private IEnumerator InitializeViewRoutine()
        {
            yield return null;

            BuildRecipeList();
            ResetView();

            yield return null;

            foreach (var slot in _recipeSlots)
                slot?.RefreshDisplay();
            SelectInitialRecipe();
            ForceRebuildLayouts();
            _initializeRoutine = null;
        }

        private void ResetView()
        {
            _selectedCategory = null;
            _selectedRecipe = null;
            _quantity = 1;

            HighlightRecipeSlot(null);
            _detailPanel.Clear();
            RefreshMaterialRows();
            _craftPanel.HideWarning();
            _quantityDialogController.HideImmediate();
            _craftPanel.HideLoadingAndResult();
        }

        private void ForceRebuildLayouts()
        {
            Canvas.ForceUpdateCanvases();
            RebuildLayout(_recipeListContent);
            RebuildLayout(_materialRowsRoot.transform);
        }

        private static void RebuildLayout(Transform target)
        {
            if (target is RectTransform rect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private void SetCraftInteractionEnabled(bool enabled)
        {
            SetRecipeListInteractable(enabled);
            _categoryTabGroup?.SetInteractionEnabled(enabled);
            _quantityDialogController?.SetInteractionEnabled(enabled);
        }

        private void SetRecipeListInteractable(bool interactable)
        {
            _recipeListInteractable = interactable;
            if (!interactable)
                SlotKeyboardFocus.Release(this);
        }

        // --- 購読(所持品の変化 / レシピ解禁) ---

        private void SubscribeInventoryChanges()
        {
            var inventoryManager = InventoryManager.Instance;
            if (_subscribedInventoryManager == inventoryManager)
                return;

            UnsubscribeInventoryChanges();
            if (inventoryManager == null)
                return;

            inventoryManager.InventoryChanged += OnInventoryChanged;
            _subscribedInventoryManager = inventoryManager;
        }

        private void UnsubscribeInventoryChanges()
        {
            if (_subscribedInventoryManager == null)
                return;

            _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
            _subscribedInventoryManager = null;
        }

        private void OnInventoryChanged()
        {
            if (isActiveAndEnabled)
                RefreshMaterialRows();
        }

        private void SubscribeRecipeBookChanges()
        {
            var recipeBook = RecipeBookManager.Instance;
            if (_subscribedRecipeBook == recipeBook)
                return;

            UnsubscribeRecipeBookChanges();
            if (recipeBook == null)
                return;

            recipeBook.RecipeRevealed += OnRecipeRevealed;
            _subscribedRecipeBook = recipeBook;
        }

        private void UnsubscribeRecipeBookChanges()
        {
            if (_subscribedRecipeBook == null)
                return;

            _subscribedRecipeBook.RecipeRevealed -= OnRecipeRevealed;
            _subscribedRecipeBook = null;
        }

        private void OnRecipeRevealed(CraftRecipe recipe)
        {
            if (!isActiveAndEnabled)
                return;

            BuildRecipeList();
            SelectRecipe(IsRecipeInCurrentTab(recipe) ? recipe : FirstRecipe);
            ForceRebuildLayouts();
        }

        // --- カテゴリタブ ---

        private void OnCategoryTabSelected(int _index, TabDefinition definition)
        {
            if (!isActiveAndEnabled || IsCraftInteractionLocked)
                return;

            _selectedCategory = definition is InventoryTabDefinition inventoryDefinition
                ? inventoryDefinition.Category
                : null;

            BuildRecipeList();
            SelectInitialRecipe(true);
            ForceRebuildLayouts();
        }

        private bool IsRecipeInCurrentTab(CraftRecipe recipe)
        {
            return recipe?.resultItem != null
                && TryGetCurrentCategory(out var category)
                && recipe.resultItem.category == category;
        }

        private bool TryGetCurrentCategory(out ItemCategory category)
        {
            if (_selectedCategory.HasValue)
            {
                category = _selectedCategory.Value;
                return true;
            }

            category = default;
            if (_categoryTabGroup.CurrentIndex < 0)
                return false;

            var definition = _categoryTabGroup.CurrentDefinition;
            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                category = inventoryDefinition.Category;
                _selectedCategory = category;
                return true;
            }

            if (!_warnedInvalidCategoryTab)
            {
                _warnedInvalidCategoryTab = true;
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanelController)} on {name}: カテゴリの TabEntry には {nameof(InventoryTabDefinition)} を設定してください。現在の定義: {(definition != null ? definition.name : "なし")}。レシピ一覧は空のままになります。",
                    this
                );
            }
            return false;
        }

        // --- レシピ一覧 ---

        private CraftRecipe FirstRecipe => _recipeSlots.FirstOrDefault()?.Recipe;

        private void BuildRecipeList()
        {
            ClearRecipeSlots();

            int slotIndex = 0;
            foreach (var recipe in _recipeDB.VisibleRecipes.Where(IsRecipeInCurrentTab))
            {
                var slotObject = Instantiate(_recipeSlotPrefab, _recipeListContent, false);
                slotObject.name = _recipeSlotPrefab.name;
                slotObject.SetActive(true);

                var slot = slotObject.GetComponent<RecipeSlot>();
                slot.SetRecipe(recipe);
                slot.Clicked += OnRecipeSlotClicked;
                slot.DoubleClicked += OnRecipeSlotDoubleClicked;
                _recipeSlots.Add(slot);
                CraftUIAnimationUtility.PlayPopIn(slotObject, slotIndex * 0.04f);
                slotIndex++;
            }

            Canvas.ForceUpdateCanvases();
            RebuildLayout(_recipeListContent);
        }

        private void ClearRecipeSlots()
        {
            foreach (var slot in _recipeSlots)
            {
                if (slot == null)
                    continue;

                UnbindSlot(slot);
                Destroy(slot.gameObject);
            }

            _recipeSlots.Clear();
            _selectedSlotIndex = -1;
            SlotKeyboardFocus.Release(this);
        }

        private void UnbindSlot(RecipeSlot slot)
        {
            if (slot == null)
                return;

            slot.Clicked -= OnRecipeSlotClicked;
            slot.DoubleClicked -= OnRecipeSlotDoubleClicked;
            slot.SetSelected(false);
        }

        private void HighlightRecipeSlot(CraftRecipe recipe)
        {
            _selectedSlotIndex = -1;
            for (int i = 0; i < _recipeSlots.Count; i++)
            {
                bool selected = recipe != null && _recipeSlots[i].Recipe == recipe;
                _recipeSlots[i].SetSelected(selected);
                if (selected)
                    _selectedSlotIndex = i;
            }

            if (_selectedSlotIndex < 0)
            {
                SlotKeyboardFocus.Release(this);
                return;
            }

            SlotKeyboardFocus.Claim(this);
            ScrollToSelectedSlot();
        }

        private void ScrollToSelectedSlot()
        {
            var scrollRect = _recipeListContent.GetComponentInParent<ScrollRect>();
            int columns = GetRecipeListColumnCount();
            int rowCount = Mathf.CeilToInt(_recipeSlots.Count / (float)columns);
            if (scrollRect == null || rowCount <= 1)
                return;

            int selectedRow = _selectedSlotIndex / columns;
            scrollRect.verticalNormalizedPosition = 1f - selectedRow / (float)(rowCount - 1);
        }

        private int GetRecipeListColumnCount()
        {
            if (
                _recipeListContent.TryGetComponent(out GridLayoutGroup grid)
                && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            )
                return Mathf.Max(1, grid.constraintCount);

            return Mathf.Max(1, _recipeSlots.Count);
        }

        private void OnRecipeSlotClicked(RecipeSlot slot)
        {
            if (!_recipeListInteractable || IsCraftInteractionLocked)
                return;

            SlotKeyboardFocus.Claim(this);
            SelectRecipe(slot.Recipe);
        }

        private void OnRecipeSlotDoubleClicked(RecipeSlot slot)
        {
            if (!_recipeListInteractable || IsCraftInteractionLocked)
                return;

            SlotKeyboardFocus.Claim(this);
            OpenQuantityDialog(slot.Recipe);
        }

        private void UpdateRecipeListKeyboardNavigation()
        {
            if (
                !_recipeListInteractable
                || IsCraftInteractionLocked
                || _selectedSlotIndex < 0
                || !SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
            {
                OpenQuantityDialog(_recipeSlots[_selectedSlotIndex].Recipe);
                return;
            }

            int dx =
                keyboard.leftArrowKey.wasPressedThisFrame ? -1
                : keyboard.rightArrowKey.wasPressedThisFrame ? 1
                : 0;
            int dy =
                keyboard.upArrowKey.wasPressedThisFrame ? -1
                : keyboard.downArrowKey.wasPressedThisFrame ? 1
                : 0;
            if (dx == 0 && dy == 0)
                return;

            int next = MoveInGrid(
                _selectedSlotIndex,
                _recipeSlots.Count,
                GetRecipeListColumnCount(),
                dx,
                dy
            );
            if (next != _selectedSlotIndex)
                SelectRecipe(_recipeSlots[next].Recipe);
        }

        // 左右は端で反対側へ回り込む。上下は同じ列の反対端へ回り込む。
        private static int MoveInGrid(int index, int count, int columns, int dx, int dy)
        {
            if (dx != 0)
                return count <= 1 ? index : (index + dx + count) % count;

            if (count <= columns)
                return index;

            int next = index + columns * dy;
            if (next < 0)
            {
                next = index % columns;
                while (next + columns < count)
                    next += columns;
            }
            else if (next >= count)
            {
                next = index % columns;
            }
            return next;
        }

        // --- 選択と素材表示 ---

        private void SelectRecipe(CraftRecipe recipe)
        {
            _selectedRecipe = recipe;
            HighlightRecipeSlot(recipe);
            _detailPanel.Show(recipe?.resultItem, NoRecipeLabel);
            RefreshMaterialRows();
        }

        private void SelectInitialRecipe(bool forceEmptyLabelRefresh = false)
        {
            var firstRecipe = FirstRecipe;
            if (firstRecipe != null)
            {
                SelectRecipe(firstRecipe);
                return;
            }

            _selectedRecipe = null;
            HighlightRecipeSlot(null);
            _detailPanel.Show(null, NoRecipeLabel, forceEmptyLabelRefresh);
            RefreshMaterialRows();
        }

        private void RefreshMaterialRows(bool animate = true)
        {
            if (_selectedRecipe?.resultItem == null || !_recipeDB.IsVisible(_selectedRecipe))
            {
                ClearMaterialRows();
                return;
            }

            var service = CraftingService;
            var materials = _selectedRecipe.Materials.ToList();
            _materialRowsRoot.SetActive(true);
            for (int i = 0; i < _materialRows.Count; i++)
            {
                var row = _materialRows[i];
                bool hasMaterial = i < materials.Count;
                row.SetActive(hasMaterial);
                if (hasMaterial)
                    ShowMaterialRow(row, materials[i], service?.GetOwnedCount(materials[i]) ?? 0);
            }

            Canvas.ForceUpdateCanvases();
            RebuildLayout(_materialRowsRoot.transform);
            if (!animate)
                return;

            for (int i = 0; i < _materialRows.Count; i++)
            {
                if (_materialRows[i].activeSelf)
                    CraftUIAnimationUtility.PlayRowIn(_materialRows[i], i);
            }
        }

        private void ShowMaterialRow(GameObject row, ItemData material, int ownedCount)
        {
            var icon = row.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.name is "Icon" or "Image");
            if (icon != null)
            {
                icon.sprite = material.icon;
                icon.color = Color.white;
                icon.gameObject.SetActive(material.icon != null);
            }

            var text = row.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.name == "Text");
            if (text != null)
            {
                text.text = $"{material.itemName}  {ownedCount} / {_quantity}";
                text.color = ownedCount >= _quantity ? SufficientColor : InsufficientColor;
            }
        }

        private void ClearMaterialRows()
        {
            foreach (var row in _materialRows)
                row.SetActive(false);
            _materialRowsRoot.SetActive(false);
        }

        // --- 個数ダイアログと調合 ---

        private void OpenQuantityDialog(CraftRecipe recipe)
        {
            SelectRecipe(recipe);
            if (ShowWarningIfCannotCraft(1))
            {
                _quantityDialogController.HideImmediate();
                return;
            }

            int max = CraftingService.GetMaximumCraftable(_selectedRecipe);
            _quantity = Mathf.Clamp(_quantity, 1, max);
            bool opened = _quantityDialogController.Show(
                _selectedRecipe.resultItem.icon,
                _selectedRecipe.resultItem.itemName,
                1,
                max,
                _quantity,
                OnQuantityConfirmed
            );
            if (opened)
                SetRecipeListInteractable(false);
        }

        private void OnQuantityChanged(int quantity)
        {
            _quantity = Mathf.Max(1, quantity);
            RefreshMaterialRows(animate: false);
        }

        private void OnQuantityConfirmed(int quantity)
        {
            if (IsCraftInteractionLocked || _selectedRecipe == null)
                return;

            _quantity = Mathf.Max(1, quantity);
            if (ShowWarningIfCannotCraft(_quantity))
            {
                RefreshMaterialRows();
                return;
            }

            _quantityDialogController.Hide();
            _craftPanel.HideWarning();
            _ownsCraftFlow = true;
            _craftRoutine = StartCoroutine(CraftRoutine(_selectedRecipe, _quantity));
        }

        private void OnQuantityDialogClosed()
        {
            bool canInteract = !IsCraftInteractionLocked;
            SetRecipeListInteractable(canInteract);
            if (canInteract)
                HighlightRecipeSlot(_selectedRecipe);
        }

        private bool ShowWarningIfCannotCraft(int quantity)
        {
            var reason =
                CraftingService?.GetBlockReason(_selectedRecipe, quantity)
                ?? CraftBlockReason.MissingMaterials;
            if (reason == CraftBlockReason.None)
                return false;

            _craftPanel.ShowWarning(reason);
            return true;
        }

        private IEnumerator CraftRoutine(CraftRecipe recipe, int quantity)
        {
            _isCrafting = true;
            try
            {
                yield return _craftPanel.RunCraftFlow(
                    () => InventoryManager.Instance?.TryCraft(recipe, quantity) ?? false,
                    recipe.resultItem,
                    quantity,
                    OnResultClosed,
                    () => _craftPanel.ShowWarning(CraftBlockReason.MissingMaterials)
                );
            }
            finally
            {
                _craftRoutine = null;
                _isCrafting = false;
                if (!_craftPanel.IsCraftFlowRunning)
                    _ownsCraftFlow = false;
                RefreshMaterialRows();
            }
        }

        private void OnResultClosed()
        {
            _ownsCraftFlow = false;
            SelectRecipe(_selectedRecipe);
        }

        // --- 参照チェック ---

        private bool HasRequiredReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_recipeDB, nameof(_recipeDB));
            valid &= ValidateRequiredReference(_craftPanel, nameof(_craftPanel));
            valid &= ValidateRequiredReference(_categoryTabGroup, nameof(_categoryTabGroup));
            valid &= ValidateRequiredReference(_detailPanel, nameof(_detailPanel));
            valid &= ValidateRequiredReference(
                _quantityDialogController,
                nameof(_quantityDialogController)
            );
            valid &= ValidateRequiredReference(_recipeListContent, nameof(_recipeListContent));
            valid &= ValidateRequiredReference(_recipeSlotPrefab, nameof(_recipeSlotPrefab));
            if (_recipeSlotPrefab != null && _recipeSlotPrefab.GetComponent<RecipeSlot>() == null)
                valid &= ValidateRequiredReference(null, $"{nameof(_recipeSlotPrefab)}.RecipeSlot");
            valid &= ValidateRequiredReference(_materialRowsRoot, nameof(_materialRowsRoot));
            if (_materialRows.Count == 0 || _materialRows.Any(row => row == null))
                valid &= ValidateRequiredReference(null, nameof(_materialRows));
            return valid;
        }

        private bool ValidateRequiredReference(Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            if (_warnedMissingRequiredReferences.Add(fieldName))
            {
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}': 必須参照 '{fieldName}' が未設定です。Inspectorで設定してください。レシピ調合の初期化を中止します。",
                    this
                );
            }

            return false;
        }
    }
}
