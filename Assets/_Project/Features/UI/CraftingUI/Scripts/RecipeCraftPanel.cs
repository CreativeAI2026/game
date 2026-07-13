using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class RecipeCraftPanel : MonoBehaviour
    {
        private const string NoRecipeLabel = "\uFF08\u30EC\u30B7\u30D4\u4E0D\u6240\u6301\uFF09";
        private const float _testCraftDuration = 5f;
        private const float _gearRotationSpeed = 180f;
        private const float _dialogAnimationDuration = 0.22f;
        private const float _dialogStartScale = 0.82f;

        [Header("Shared Data")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private CraftPanel _craftPanel;

        [Header("Prefabs / Templates")]
        [SerializeField]
        private GameObject _recipeSlotPrefab;

        [Header("Main UI Roots")]
        [SerializeField]
        private Transform _recipeList;

        [SerializeField]
        private Transform _recipeContent;

        [Header("Recipe Tabs")]
        [SerializeField]
        private TabGroup _recipeTabGroup;

        [SerializeField]
        private List<ItemCategory> _recipeCategories = new()
        {
            ItemCategory.Equipment,
            ItemCategory.Food,
        };

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private Transform _materialList;

        [Header("Quantity Dialog Roots")]
        [SerializeField]
        private GameObject _quantityDialogPanel;

        [SerializeField]
        private GameObject _quantityDialog;

        [SerializeField]
        private CraftQuantityDialog _quantityDialogController;

        private readonly List<RecipeSlot> _slots = new();
        private readonly List<RecipeMaterialRow> _materialRows = new();
        private readonly List<ItemCategory> _activeRecipeCategories = new();
        private CraftRecipeDB _subscribedRecipeDB;
        private CraftRecipeData _selectedRecipe;
        private CraftRecipeData _craftedRecipeForResult;
        private int _craftedQuantityForResult = 1;
        private int _quantity = 1;
        private bool _isCrafting;
        private bool _warnedMissingRecipeDB;
        private bool _warnedMissingRecipeSlotPrefab;
        private bool _warnedMissingMaterialRows;
        private bool _warnedMissingQuantityDialogPanel;
        private bool _warnedMissingQuantityDialog;
        private bool _warnedMissingQuantityDialogController;
        private Coroutine _craftRoutine;
        private Coroutine _initializeRoutine;

        private void Awake()
        {
            ResolveAllReferences();
            BindRecipeTabs();
            PrepareInitialHiddenTemplates();
            ValidateSetup();
            SubscribeRecipeDBChanges();
            BindDialog();
            ResetView();
        }

        private void OnEnable()
        {
            if (_initializeRoutine != null)
                StopCoroutine(_initializeRoutine);

            _initializeRoutine = StartCoroutine(InitializeViewRoutine());
        }

        private void OnDisable()
        {
            StopCraftRoutine();
            UnsubscribeRecipeDBChanges();

            if (_initializeRoutine != null)
            {
                StopCoroutine(_initializeRoutine);
                _initializeRoutine = null;
            }

            _isCrafting = false;
            HideWarningImmediately();
            CloseQuantityDialogImmediately();
            _craftPanel?.HideLoadingAndResult();
        }

        private void OnDestroy()
        {
            UnbindRecipeTabs();
            UnsubscribeRecipeDBChanges();
        }

        private void Update()
        {
            if (_isCrafting)
                _craftPanel?.RotateLoadingGear(_gearRotationSpeed);

            UpdateQuantityDialogKeyboardControls();
        }

        private void ResolveAllReferences()
        {
            ResolveCraftPanelReference();
            ResolveRecipeDB();
            ResolveMainReferences();
            ResolveQuantityDialogReferences();
        }

        private void ResolveCraftPanelReference()
        {
            _craftPanel ??= GetComponentInParent<CraftPanel>(true);
        }

        private void ResolveRecipeDB()
        {
            if (_recipeDB != null)
                return;

            ResolveCraftPanelReference();
            _recipeDB = _craftPanel != null ? _craftPanel.RecipeDB : null;
        }

        private void ResolveMainReferences()
        {
            _recipeList ??= Find("RecipeList");
            _recipeContent ??= FindRecipeContent();
            _recipeTabGroup ??= GetComponentInChildren<TabGroup>(true);
            _detailPanel ??= FindDetailPanel();
            _materialList ??= Find("MaterialList");
        }

        private CraftPanel GetCraftPanel()
        {
            ResolveCraftPanelReference();
            return _craftPanel;
        }

        private IEnumerator InitializeViewRoutine()
        {
            yield return null;

            ResolveAllReferences();
            ValidateSetup();
            SubscribeRecipeDBChanges();
            BuildRecipeList();
            BindDialog();
            ResetView();

            yield return null;

            foreach (var slot in _slots)
                slot?.RefreshDisplay();

            SelectInitialRecipe();
            ForceRebuildLayouts();
            _initializeRoutine = null;
        }

        private void ForceRebuildLayouts()
        {
            Canvas.ForceUpdateCanvases();

            if (_recipeContent is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (_materialList is RectTransform materialRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(materialRect);
        }

        private void ResetView()
        {
            _selectedRecipe = null;
            _quantity = 1;

            foreach (var slot in _slots)
                slot?.SetSelected(false);

            _detailPanel?.Clear();
            RebuildMaterialRows();
            HideWarningImmediately();
            CloseQuantityDialogImmediately();

            _craftPanel?.HideLoadingAndResult();
        }

        private void ValidateSetup()
        {
            if (_recipeDB == null && !_warnedMissingRecipeDB)
            {
                Debug.LogError(
                    $"{nameof(RecipeCraftPanel)} on {name}: CraftRecipeDB が未設定です。CraftPanel と同じ CraftRecipeDB を Inspector から指定してください。",
                    this
                );
                _warnedMissingRecipeDB = true;
            }

            if (_recipeContent == null)
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanel)} on {name}: RecipeContent が見つかりません。",
                    this
                );

            if (_materialList == null)
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanel)} on {name}: MaterialList が見つかりません。",
                    this
                );
        }

        private void WarnMissingReferenceOnce(ref bool flag, string referenceName)
        {
            if (flag)
                return;

            Debug.LogWarning(
                $"{nameof(RecipeCraftPanel)} on {name}: {referenceName} が見つかりません。Inspector参照を設定するか、Prefab上の名前を確認してください。",
                this
            );
            flag = true;
        }
    }
}
