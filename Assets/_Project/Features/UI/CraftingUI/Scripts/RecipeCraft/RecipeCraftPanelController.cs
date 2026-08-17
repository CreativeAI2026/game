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
        [FormerlySerializedAs("_materialListView")]
        [FormerlySerializedAs("_materialList")]
        private RecipeCraftMaterialRowsView _materialRowsView;

        [Header("Quantity Dialog")]
        [SerializeField]
        private CraftQuantityDialog _quantityDialogController;

        private readonly HashSet<string> _warnedMissingRequiredReferences = new();
        private readonly RecipeCraftSelectionState _selectionState = new();
        private readonly RecipeCraftAvailabilityCalculator _availabilityCalculator = new();
        private RecipeBookManager _subscribedRecipeBook;
        private InventoryManager _subscribedInventoryManager;
        private CraftRecipeData _craftedRecipeForResult;
        private int _craftedQuantityForResult = 1;
        private bool _isCrafting;
        private bool _ownsCraftFlow;
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

            SubscribeCraftInteraction();
            SubscribeInventoryChanges();
            if (_initializeRoutine != null)
                StopCoroutine(_initializeRoutine);

            _initializeRoutine = StartCoroutine(InitializeViewRoutine());
        }

        private void OnDisable()
        {
            UnsubscribeCraftInteraction();
            UnsubscribeInventoryChanges();
            StopCraftRoutine();
            if (_ownsCraftFlow)
                _craftPanel?.CancelCraftFlow();
            _ownsCraftFlow = false;
            SetCraftInteractionEnabled(true);
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
            UnsubscribeCraftInteraction();
            UnsubscribeInventoryChanges();
            UnbindCategoryTabs();
            UnbindRecipeListView();
            UnbindDialog();
            UnsubscribeRecipeBookChanges();
        }

        private void Update()
        {
            if (_isCrafting)
                _craftPanel?.RotateLoadingGear(_gearRotationSpeed);
        }

        private bool IsCraftInteractionLocked =>
            _isCrafting || (_craftPanel?.IsCraftFlowRunning ?? false);

        private void SubscribeCraftInteraction()
        {
            if (_craftPanel == null)
                return;

            _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
            _craftPanel.CraftInteractionChanged += SetCraftInteractionEnabled;
            SetCraftInteractionEnabled(!_craftPanel.IsCraftFlowRunning);
        }

        private void UnsubscribeCraftInteraction()
        {
            if (_craftPanel != null)
                _craftPanel.CraftInteractionChanged -= SetCraftInteractionEnabled;
        }

        private void SetCraftInteractionEnabled(bool enabled)
        {
            _recipeListView?.SetInteractionEnabled(enabled);
            _categoryTabGroup?.SetInteractionEnabled(enabled);
            _quantityDialogController?.SetInteractionEnabled(enabled);
        }

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

        private IReadOnlyList<ItemStack> GetInventorySnapshot()
        {
            return _subscribedInventoryManager != null
                ? _subscribedInventoryManager.GetAllItems()
                : System.Array.Empty<ItemStack>();
        }

        private IReadOnlyList<ItemStack> GetQuickFoodSnapshot()
        {
            return _subscribedInventoryManager != null
                ? _subscribedInventoryManager.GetQuickFoodSlots()
                : System.Array.Empty<ItemStack>();
        }

        private void OnInventoryChanged()
        {
            if (isActiveAndEnabled)
                RefreshMaterialRows();
        }

        private void PlayMissingMaterialsWarning()
        {
            _craftPanel?.ShowWarning(CraftWarningKind.MissingMaterials);
        }

        private void PlayEquippedMaterialWarning()
        {
            _craftPanel?.ShowWarning(CraftWarningKind.EquippedMaterial);
        }

        private void PlayQuickFoodMaterialWarning()
        {
            _craftPanel?.ShowWarning(CraftWarningKind.QuickFoodMaterial);
        }

        private void HideWarningImmediately()
        {
            _craftPanel?.HideWarning();
        }

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

            _materialRowsView?.RebuildLayout();
        }

        private void ResetView()
        {
            _selectionState.Reset();

            _recipeListView?.SelectRecipe(null);

            _detailPanel?.Clear();
            RefreshMaterialRows();
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

            if (_materialRowsView == null)
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
            valid &= ValidateRequiredReference(_materialRowsView, nameof(_materialRowsView));
            if (_materialRowsView != null && !_materialRowsView.HasRequiredReferences)
            {
                ValidateRequiredReference(null, $"{nameof(_materialRowsView)}._rows");
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
