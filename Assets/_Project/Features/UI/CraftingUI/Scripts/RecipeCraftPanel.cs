using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("_recipeTabGroup")]
        [SerializeField]
        private TabGroup _categoryTabGroup;

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
        private readonly List<RecipeSlot> _generatedRecipeSlots = new();

        [SerializeField]
        private List<RecipeMaterialRow> _materialRows = new();
        private readonly HashSet<string> _warnedMissingRequiredReferences = new();
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
        private bool _warnedMissingCategoryTabGroup;
        private bool _warnedInvalidCategoryTab;
        private Coroutine _craftRoutine;
        private Coroutine _initializeRoutine;

        private void Awake()
        {
            if (!HasRequiredReferences())
                return;

            BindCategoryTabs();
            PrepareInitialHiddenTemplates();
            ValidateSetup();
            SubscribeRecipeDBChanges();
            BindDialog();
            ResetView();
        }

        private void OnEnable()
        {
            if (!HasRequiredReferences())
                return;

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
            UnbindCategoryTabs();
            UnsubscribeRecipeDBChanges();
        }

        private void Update()
        {
            if (_isCrafting)
                _craftPanel?.RotateLoadingGear(_gearRotationSpeed);

            UpdateQuantityDialogKeyboardControls();
        }

        private CraftPanel GetCraftPanel() => _craftPanel;

        private IEnumerator InitializeViewRoutine()
        {
            yield return null;

            if (!HasRequiredReferences())
            {
                _initializeRoutine = null;
                yield break;
            }
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

            if (_categoryTabGroup == null)
                WarnMissingReferenceOnce(
                    ref _warnedMissingCategoryTabGroup,
                    nameof(_categoryTabGroup)
                );
        }

        private bool HasRequiredReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_recipeDB, nameof(_recipeDB));
            valid &= ValidateRequiredReference(_craftPanel, nameof(_craftPanel));
            valid &= ValidateRequiredReference(_recipeSlotPrefab, nameof(_recipeSlotPrefab));
            valid &= ValidateRequiredReference(_recipeList, nameof(_recipeList));
            valid &= ValidateRequiredReference(_recipeContent, nameof(_recipeContent));
            valid &= ValidateRequiredReference(_categoryTabGroup, nameof(_categoryTabGroup));
            valid &= ValidateRequiredReference(_detailPanel, nameof(_detailPanel));
            valid &= ValidateRequiredReference(_materialList, nameof(_materialList));
            valid &= ValidateRequiredReference(_quantityDialogPanel, nameof(_quantityDialogPanel));
            valid &= ValidateRequiredReference(_quantityDialog, nameof(_quantityDialog));
            valid &= ValidateRequiredReference(
                _quantityDialogController,
                nameof(_quantityDialogController)
            );
            if (_materialRows.Count == 0 || _materialRows.Exists(row => row == null))
            {
                ValidateRequiredReference(null, nameof(_materialRows));
                valid = false;
            }
            return valid;
        }

        private bool ValidateRequiredReference(Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            if (_warnedMissingRequiredReferences.Add(fieldName))
            {
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanel)} '{UIHierarchyPathUtility.GetPath(transform)}' requires Inspector reference '{fieldName}'. RecipeCraft initialization was stopped.",
                    this
                );
            }

            return false;
        }

        private void WarnMissingReferenceOnce(ref bool flag, string referenceName)
        {
            if (flag)
                return;

            Debug.LogWarning(
                $"{nameof(RecipeCraftPanel)} on {name}: 必須参照 '{referenceName}' が未設定です。Inspectorで設定してください。該当UI処理を中止します。",
                this
            );
            flag = true;
        }
    }
}
