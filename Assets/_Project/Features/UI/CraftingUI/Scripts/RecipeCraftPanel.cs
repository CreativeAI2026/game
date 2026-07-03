using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using TMPro;
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

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [SerializeField]
        private Transform _materialList;

        [Header("Quantity Dialog Roots")]
        [SerializeField]
        private GameObject _quantityDialogPanel;

        [SerializeField]
        private GameObject _quantityDialog;

        [Header("Craft Flow Roots")]
        [SerializeField]
        private GameObject _loadingPanel;

        [SerializeField]
        private GameObject _resultPanel;

        [SerializeField]
        private GameObject _closeButton;

        private RectTransform _quantityDialogRect;
        private CanvasGroup _quantityDialogCanvasGroup;
        private Image _dialogItemImage;
        private TMP_Text _dialogItemName;
        private TMP_Text _dialogCounts;
        private TMP_InputField _quantityInput;
        private Button _minButton;
        private Button _minusButton;
        private Button _plusButton;
        private Button _maxButton;
        private Button _cancelButton;
        private Button _dialogCraftButton;

        private RectTransform _loadingGear;
        private Image _resultItemImage;
        private TMP_Text _resultItemName;
        private ResultPanelClickCatcher _resultClickCatcher;
        private ResultPanelClickCatcher _quantityDialogPanelClickCatcher;

        private readonly List<RecipeSlot> _slots = new();
        private readonly List<RecipeMaterialRow> _materialRows = new();
        private CraftRecipeDB _subscribedRecipeDB;
        private CraftRecipeData _selectedRecipe;
        private CraftRecipeData _craftedRecipeForResult;
        private int _craftedQuantityForResult = 1;
        private int _quantity = 1;
        private bool _isCrafting;
        private bool _warnedMissingRecipeDB;
        private bool _warnedMissingRecipeSlotPrefab;
        private bool _warnedMissingMaterialRows;
        private Coroutine _craftRoutine;
        private Coroutine _initializeRoutine;

        private void Awake()
        {
            ResolveAllReferences();
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
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);
            SetCloseButtonVisible(true);
        }

        private void Update()
        {
            if (_isCrafting && _loadingGear != null)
                _loadingGear.Rotate(0f, 0f, -_gearRotationSpeed * Time.unscaledDeltaTime);
        }

        private void ResolveAllReferences()
        {
            ResolveCraftPanelReference();
            ResolveRecipeDB();
            ResolveMainReferences();
            ResolveQuantityDialogReferences();
            ResolveCraftFlowReferences();
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

            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);

            SetCloseButtonVisible(true);
        }

        private void SetCloseButtonVisible(bool visible)
        {
            if (_closeButton == null)
                ResolveCraftFlowReferences();

            CraftFlowViewUtility.SetCloseButtonVisible(_closeButton, visible);
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
    }
}
