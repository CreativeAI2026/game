using System.Collections.Generic;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [MovedFrom(
        true,
        sourceNamespace: "CreativeAI.UI.CraftingUI",
        sourceAssembly: "CreativeAI.UI",
        sourceClassName: "CraftPanel"
    )]
    public partial class CraftPanelController : MonoBehaviour
    {
        private const string EmptyMaterialLabel = "\uFF08\u672A\u9078\u629E\uFF09";

        [Header("Shared Data")]
        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [Header("Shared Views")]
        [SerializeField]
        private CraftLoadingOverlayView _loadingOverlayView;

        [SerializeField]
        private CraftResultPanelView _resultPanelView;

        [SerializeField]
        private CraftWarningToastView _warningToastView;

        [SerializeField]
        private Button _closeButton;

        [Header("Craft Flow")]
        [SerializeField]
        [Min(0f)]
        private float _craftFlowDurationSeconds = 1f;

        private readonly HashSet<string> _warnedMissingReferences = new();
        private bool _isCraftFlowRunning;

        public CraftRecipeDB RecipeDB => _recipeDB;
        public float CraftFlowDurationSeconds => Mathf.Max(0f, _craftFlowDurationSeconds);
        public bool IsCraftFlowRunning => _isCraftFlowRunning;

        public event System.Action<bool> CraftInteractionChanged;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _loadingOverlayView ??= GetComponentInChildren<CraftLoadingOverlayView>(true);
            _resultPanelView ??= GetComponentInChildren<CraftResultPanelView>(true);
            _warningToastView ??= GetComponentInChildren<CraftWarningToastView>(true);
            _closeButton ??= UIChildFinder.FindButton(transform, "CloseButton");
        }
#endif

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
            CancelCraftFlow();
            HideWarning();
        }

        private void Initialize()
        {
            ValidateCraftFlowReferences();
            BindCraftFlow();
        }

        private bool ValidateRequiredReference(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            if (_warnedMissingReferences.Add(fieldName))
            {
                Debug.LogWarning(
                    $"{nameof(CraftPanelController)} on {name}: 必須参照 '{fieldName}' が未設定です。Inspectorで設定してください。該当UI処理を中止します。",
                    this
                );
            }

            return false;
        }
    }
}
