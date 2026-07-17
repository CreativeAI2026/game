using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.CraftingUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "RecipeCraftPanel"
    )]
    public partial class RecipeCraftPanelController : MonoBehaviour
    {
        private const string NoRecipeLabel = "\uFF08\u30EC\u30B7\u30D4\u4E0D\u6240\u6301\uFF09";
        private const float _gearRotationSpeed = 180f;
        private const float _dialogAnimationDuration = 0.22f;
        private const float _dialogStartScale = 0.82f;

        [Header("Shared Data")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private CraftPanelController _craftPanel;

        [Header("Main UI Roots")]
        [SerializeField]
        private RecipeListView _recipeListView;

        [Header("Recipe Tabs")]
        [FormerlySerializedAs("_recipeTabGroup")]
        [SerializeField]
        private TabGroup _categoryTabGroup;

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private RecipeMaterialListView _materialListView;

        [Header("Quantity Dialog")]
        [SerializeField]
        private CraftQuantityDialog _quantityDialogController;

        private readonly HashSet<string> _warnedMissingRequiredReferences = new();
        private RecipeBookManager _subscribedRecipeBook;
        private CraftRecipeData _selectedRecipe;
        private CraftRecipeData _craftedRecipeForResult;
        private int _craftedQuantityForResult = 1;
        private int _quantity = 1;
        private bool _isCrafting;
        private bool _warnedMissingRecipeDB;
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
            BindRecipeListView();
            PrepareInitialHiddenTemplates();
            ValidateSetup();
            SubscribeRecipeBookChanges();
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
            UnsubscribeRecipeBookChanges();

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
            UnbindRecipeListView();
            UnsubscribeRecipeBookChanges();
        }

        private void Update()
        {
            if (_isCrafting)
                _craftPanel?.RotateLoadingGear(_gearRotationSpeed);

            UpdateQuantityDialogKeyboardControls();
        }

        private CraftPanelController GetCraftPanel() => _craftPanel;

        private IEnumerator InitializeViewRoutine()
        {
            yield return null;

            if (!HasRequiredReferences())
            {
                _initializeRoutine = null;
                yield break;
            }
            ValidateSetup();
            SubscribeRecipeBookChanges();
            BuildRecipeList();
            BindDialog();
            ResetView();

            yield return null;

            _recipeListView?.RefreshSlots();

            SelectInitialRecipe();
            ForceRebuildLayouts();
            _initializeRoutine = null;
        }

        private void ForceRebuildLayouts()
        {
            Canvas.ForceUpdateCanvases();

            _recipeListView?.RebuildLayout();

            _materialListView?.RebuildLayout();
        }

        private void ResetView()
        {
            _selectedRecipe = null;
            _quantity = 1;

            _recipeListView?.SelectRecipe(null);

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
                    $"{nameof(RecipeCraftPanelController)} on {name}: CraftRecipeDB が未設定です。CraftPanelController と同じ CraftRecipeDB を Inspector から指定してください。",
                    this
                );
                _warnedMissingRecipeDB = true;
            }

            if (_recipeListView == null)
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanelController)} on {name}: RecipeListView が見つかりません。",
                    this
                );

            if (_materialListView == null)
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanelController)} on {name}: MaterialList が見つかりません。",
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
            valid &= ValidateRequiredReference(_recipeListView, nameof(_recipeListView));
            if (_recipeListView != null && !_recipeListView.HasRequiredReferences)
            {
                ValidateRequiredReference(null, $"{nameof(_recipeListView)} references");
                valid = false;
            }
            valid &= ValidateRequiredReference(_categoryTabGroup, nameof(_categoryTabGroup));
            valid &= ValidateRequiredReference(_detailPanel, nameof(_detailPanel));
            valid &= ValidateRequiredReference(_materialListView, nameof(_materialListView));
            if (_materialListView != null && !_materialListView.HasRequiredReferences)
            {
                ValidateRequiredReference(null, $"{nameof(_materialListView)}._rows");
                valid = false;
            }
            valid &= ValidateRequiredReference(
                _quantityDialogController,
                nameof(_quantityDialogController)
            );
            return valid;
        }

        private bool ValidateRequiredReference(Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            if (_warnedMissingRequiredReferences.Add(fieldName))
            {
                Debug.LogWarning(
                    $"{nameof(RecipeCraftPanelController)} '{UIHierarchyPathUtility.GetPath(transform)}' requires Inspector reference '{fieldName}'. RecipeCraft initialization was stopped.",
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
                $"{nameof(RecipeCraftPanelController)} on {name}: 必須参照 '{referenceName}' が未設定です。Inspectorで設定してください。該当UI処理を中止します。",
                this
            );
            flag = true;
        }
    }
}
