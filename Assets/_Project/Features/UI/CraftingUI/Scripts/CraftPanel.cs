using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel : MonoBehaviour
    {
        private const string EmptyMaterialLabel = "\uFF08\u672A\u9078\u629E\uFF09";
        private const float WarningShakeDistance = 8f;
        private const float WarningFadeDelay = 0.8f;
        private const float WarningFadeDuration = 0.6f;

        [SerializeField]
        private Inventory _inventory;

        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private ItemDetailPanel _detailPanel;

        [Header("Craft Flow")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private Button _craftButton;

        [SerializeField]
        private GameObject _loadingPanel;

        [SerializeField]
        private RectTransform _loadingGear;

        [SerializeField]
        private GameObject _resultPanel;

        [SerializeField]
        private GameObject _closeButton;

        [SerializeField]
        private float _testCraftDuration = 5f;

        [SerializeField]
        private float _gearRotationSpeed = 180f;

        [Header("Warning")]
        [SerializeField]
        private TMP_Text _warningText;

        [SerializeField]
        private string _notReadyMessage =
            "\u7D20\u6750\u30922\u3064\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044";

        [SerializeField]
        private string _categoryMismatchMessage =
            "\u540C\u3058\u30AB\u30C6\u30B4\u30EA\u30FC\u306E\u7D20\u6750\u3092\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044";

        [SerializeField]
        private string _equippedMaterialMessage =
            "\u88C5\u5099\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093";

        [SerializeField]
        private string _missingMaterialsMessage =
            "\u7D20\u6750\u304C\u8DB3\u308A\u307E\u305B\u3093\uFF01";

        private RectTransform _warningTextRect;
        private CanvasGroup _warningTextCanvasGroup;
        private Vector2 _warningTextBasePosition;
        private bool _hasWarningTextBasePosition;
        private Sequence _warningSequence;
        private string _activeWarningMessage;

        private readonly List<MaterialSlot> _slots = new();
        private MaterialSlot _selectedSlot;
        private bool _isSubscribed;
        private bool _isCrafting;
        private CraftRecipeData _lastCraftedRecipe;
        private Image _resultItemImage;
        private TMP_Text _resultItemName;
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private ResultPanelClickCatcher _resultClickCatcher;

        public CraftRecipeDB RecipeDB
        {
            get
            {
                _recipeDB ??= Resources.Load<CraftRecipeDB>("Crafting/CraftRecipeDB");
                return _recipeDB;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            _inventory?.ResetViewState();
            ResetSlots();
            Subscribe();
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

        private void Update()
        {
            if (_isCrafting && _loadingGear != null)
                _loadingGear.Rotate(0f, 0f, -_gearRotationSpeed * Time.unscaledDeltaTime);
        }

        private void Initialize()
        {
            _inventory ??= GetComponentInChildren<Inventory>(true);
            _inventory?.SetSelectFirstSlotOnRefresh(false);
            _inventory?.SetReleaseSelectionOnOutsideClick(false);
            _detailPanel ??= FindDetailPanel();
            _recipeDB ??= Resources.Load<CraftRecipeDB>("Crafting/CraftRecipeDB");
            FindCraftFlowReferences();
            ResolveWarningReferences();
            InitializeSlots();
            BindCraftFlow();
        }

        private void ResolveWarningReferences()
        {
            if (_warningText == null)
                _warningText = FindDescendant("WarningText")?.GetComponent<TMP_Text>();
            if (_warningText == null)
                return;

            _warningTextRect ??= _warningText.rectTransform;
            if (!_hasWarningTextBasePosition && _warningTextRect != null)
            {
                _warningTextBasePosition = _warningTextRect.anchoredPosition;
                _hasWarningTextBasePosition = true;
            }

            _warningTextCanvasGroup ??= _warningText.GetComponent<CanvasGroup>();
            if (_warningTextCanvasGroup == null)
                _warningTextCanvasGroup = _warningText.gameObject.AddComponent<CanvasGroup>();
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

        private ItemDetailPanel FindDetailPanel()
        {
            foreach (var panel in GetComponentsInChildren<ItemDetailPanel>(true))
            {
                if (panel.GetComponentInParent<Inventory>(true) == null)
                    return panel;
            }

            return GetComponentInChildren<ItemDetailPanel>(true);
        }

        private Transform FindDescendant(string objectName)
        {
            return UIChildFinder.Find(transform, objectName);
        }

        private static T FindComponentIn<T>(Transform root, string objectName)
            where T : Component
        {
            return UIChildFinder.FindComponent<T>(root, objectName);
        }
    }
}
