using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel : MonoBehaviour
    {
        private const string EmptyMaterialLabel = "\uFF08\u672A\u9078\u629E\uFF09";
        private const float WarningFadeDelay = 0.8f;
        private const float WarningFadeDuration = 0.6f;

        [Header("Shared Data")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [Header("Shared Flow")]
        [SerializeField]
        private GameObject _loadingPanel;

        [SerializeField]
        private RectTransform _loadingGear;

        [SerializeField]
        private GameObject _resultPanel;

        [SerializeField]
        private GameObject _resultPanelBackground;

        [SerializeField]
        private TMP_Text _resultPanelTitle;

        [SerializeField]
        private Image _resultItemImage;

        [SerializeField]
        private TMP_Text _resultItemName;

        [SerializeField]
        private GameObject _closeButton;

        [SerializeField]
        private Button _closeButtonButton;

        [Header("Warning")]
        [SerializeField]
        private TMP_Text _warningText;

        [SerializeField, FormerlySerializedAs("_warningTextCanvasGroup")]
        private CanvasGroup _warningCanvasGroup;

        [SerializeField]
        private string _categoryMismatchMessage =
            "\u540C\u3058\u30AB\u30C6\u30B4\u30EA\u30FC\u306E\u7D20\u6750\u3092\u9078\u629E\u3057\u3066\u304F\u3060\u3055\u3044";

        [SerializeField]
        private string _equippedMaterialMessage =
            "\u88C5\u5099\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093";

        [SerializeField]
        private string _quickFoodMaterialMessage =
            "\u5373\u6642\u4F7F\u7528\u306B\u30BB\u30C3\u30C8\u4E2D\u306E\u30A2\u30A4\u30C6\u30E0\u306F\u7D20\u6750\u306B\u3067\u304D\u307E\u305B\u3093";

        [SerializeField]
        private string _missingMaterialsMessage =
            "\u7D20\u6750\u304C\u8DB3\u308A\u307E\u305B\u3093\uFF01";

        private Vector2 _warningTextBasePosition;
        private bool _hasWarningTextBasePosition;
        private Coroutine _warningRoutine;

        private CloseOnSelfClick _resultCloseOnSelfClick;
        private System.Action _resultClosedAction;
        private readonly HashSet<string> _warnedMissingReferences = new();

        public CraftRecipeDB RecipeDB => _recipeDB;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            ResetSharedFlow();
        }

        private void OnDisable()
        {
            HideWarning();
        }

        private void Initialize()
        {
            ValidateCraftFlowReferences();
            ResolveWarningReferences();
            BindCraftFlow();
        }

        private bool ResolveWarningReferences()
        {
            if (!ValidateRequiredReference(_warningText, nameof(_warningText)))
                return false;

            RectTransform warningTextRect = _warningText.rectTransform;
            if (!_hasWarningTextBasePosition && warningTextRect != null)
            {
                _warningTextBasePosition = warningTextRect.anchoredPosition;
                _hasWarningTextBasePosition = true;
            }

            if (!ValidateRequiredReference(_warningCanvasGroup, nameof(_warningCanvasGroup)))
                return false;

            return true;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Warning References")]
        private void AutoAssignWarningReferences()
        {
            if (_warningText != null)
                _warningCanvasGroup ??= _warningText.GetComponent<CanvasGroup>();
        }
#endif

        private bool ValidateRequiredReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            if (_warnedMissingReferences.Add(fieldName))
            {
                Debug.LogWarning(
                    $"{nameof(CraftPanel)} on {name}: 必須参照 '{fieldName}' が未設定です。Inspectorで設定してください。該当UI処理を中止します。",
                    this
                );
            }

            return false;
        }
    }
}
