using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 調合画面の共通部分。レシピ調合・自由調合の両方から使う
    /// 「ローディング → 結果パネル」の実行フローと、警告トーストを持つ。
    /// </summary>
    public class CraftPanelController : MonoBehaviour
    {
        private const float GearRotationSpeed = 180f;
        private const float WarningShakeDistance = 12f;
        private const float WarningShakeDuration = 0.6f;
        private const float WarningShakeFrequency = 5f;
        private const float WarningFadeDelay = 0.8f;
        private const float WarningFadeDuration = 0.6f;

        [SerializeField]
        private CraftRecipeDB _recipeDB;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        [Min(0f)]
        private float _craftFlowDurationSeconds = 1f;

        [Header("Loading")]
        [SerializeField]
        private GameObject _loadingRoot;

        [SerializeField]
        private RectTransform _loadingGear;

        [Header("Result")]
        [SerializeField]
        private CanvasGroup _resultCanvasGroup;

        [SerializeField]
        private CloseOnSelfClick _resultCloseOnClick;

        [SerializeField]
        private Image _resultItemImage;

        [SerializeField]
        private TMP_Text _resultItemName;

        [SerializeField]
        private TMP_Text _resultItemParameters;

        [Header("Warning")]
        [SerializeField]
        private TMP_Text _warningText;

        [SerializeField]
        private CanvasGroup _warningCanvasGroup;

        private readonly HashSet<string> _warnedMissingReferences = new();
        private bool _isCraftFlowRunning;
        private Action _resultClosedAction;
        private TMP_Text _resultNewBadge;
        private Coroutine _warningRoutine;
        private Vector2 _warningBasePosition;
        private bool _hasWarningBasePosition;

        public CraftRecipeDB RecipeDB => _recipeDB;
        public bool IsCraftFlowRunning => _isCraftFlowRunning;

        public event Action<bool> CraftInteractionChanged;

        private GameObject ResultRoot => _resultCanvasGroup.gameObject;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            CancelCraftFlow();
            HideWarning();
        }

        private void OnDisable()
        {
            CancelCraftFlow();
            HideWarning();
        }

        private void OnDestroy()
        {
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.ClearClickAction(HideResult);
            _resultClosedAction = null;
        }

        private void Update()
        {
            if (_loadingGear != null && _loadingGear.gameObject.activeInHierarchy)
                _loadingGear.Rotate(0f, 0f, -GearRotationSpeed * Time.unscaledDeltaTime);
        }

        private void Initialize()
        {
            if (!HasRequiredReferences())
                return;

            UIButtonHoverScaleUtility.ApplyTo(_closeButton);
            _closeButton.onClick.RemoveListener(HideResult);
            _closeButton.onClick.AddListener(HideResult);

            if (!_hasWarningBasePosition)
            {
                _warningBasePosition = _warningText.rectTransform.anchoredPosition;
                _hasWarningBasePosition = true;
            }
        }

        // --- 実行フロー ---

        public IEnumerator RunCraftFlow(
            Func<bool> craftAction,
            ItemData resultItem,
            int resultCount,
            Action onResultClosed,
            Action onFailed = null,
            bool showNewBadge = false
        )
        {
            if (craftAction == null || _isCraftFlowRunning || !HasRequiredReferences())
                yield break;

            bool awaitingResultClose = false;
            try
            {
                SetCraftFlowRunning(true);
                HideWarning();
                HideResultImmediately();
                ShowLoading();
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, _craftFlowDurationSeconds));

                bool crafted = craftAction();
                HideLoading();
                if (!crafted)
                {
                    HideResultImmediately();
                    onFailed?.Invoke();
                    yield break;
                }

                bool resultClosed = false;
                void CompleteResult()
                {
                    if (resultClosed)
                        return;

                    resultClosed = true;
                    try
                    {
                        onResultClosed?.Invoke();
                    }
                    finally
                    {
                        SetCraftFlowRunning(false);
                    }
                }

                ShowResult(resultItem, resultCount, CompleteResult, showNewBadge);
                awaitingResultClose = true;
            }
            finally
            {
                HideLoading();
                if (!awaitingResultClose)
                    SetCraftFlowRunning(false);
            }
        }

        public void CancelCraftFlow()
        {
            HideLoadingAndResult();
            SetCraftFlowRunning(false);
        }

        public void HideLoadingAndResult()
        {
            HideLoading();
            HideResultImmediately();
        }

        private void SetCraftFlowRunning(bool running)
        {
            if (_isCraftFlowRunning == running)
                return;

            _isCraftFlowRunning = running;
            CraftInteractionChanged?.Invoke(!running);
        }

        // --- ローディング ---

        private void ShowLoading()
        {
            _loadingRoot.SetActive(true);
            _loadingGear.localRotation = Quaternion.identity;
            _loadingGear.gameObject.SetActive(true);
        }

        private void HideLoading()
        {
            if (_loadingGear != null)
            {
                _loadingGear.localRotation = Quaternion.identity;
                _loadingGear.gameObject.SetActive(false);
            }
            if (_loadingRoot != null)
                _loadingRoot.SetActive(false);
        }

        // --- 結果パネル ---

        public void ShowResult(
            ItemData resultItem,
            int count,
            Action closeAction,
            bool showNewBadge = false
        )
        {
            if (!ValidateRequiredReference(_resultCanvasGroup, nameof(_resultCanvasGroup)))
                return;

            HideWarning();
            int safeCount = Mathf.Max(1, count);
            string itemName =
                resultItem == null ? string.Empty
                : safeCount > 1 ? $"{resultItem.itemName} ×{safeCount}"
                : resultItem.itemName;

            _resultClosedAction = closeAction;
            SetResultContent(
                resultItem != null ? resultItem.icon : null,
                itemName,
                ItemStatTextFormatter.BuildStatsText(resultItem),
                showNewBadge
            );
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.SetClickAction(HideResult);
            CraftUIAnimationUtility.PlayResultIn(ResultRoot);
        }

        // 閉じるボタン・結果パネルのクリックから呼ばれる。
        private void HideResult()
        {
            HideWarning();
            if (_resultCanvasGroup == null)
                return;

            Action closedAction = _resultClosedAction;
            _resultClosedAction = null;
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.ClearClickAction(HideResult);
            CraftUIAnimationUtility.PlayResultOut(
                ResultRoot,
                () =>
                {
                    SetResultContent(null, string.Empty, string.Empty, false);
                    closedAction?.Invoke();
                }
            );
        }

        private void HideResultImmediately()
        {
            if (_resultCanvasGroup == null)
                return;

            _resultClosedAction = null;
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.ClearClickAction(HideResult);
            CraftUIAnimationUtility.HideResultImmediately(ResultRoot);
            SetResultContent(null, string.Empty, string.Empty, false);
        }

        private void SetResultContent(
            Sprite icon,
            string itemName,
            string itemParameters,
            bool showNewBadge
        )
        {
            if (_resultItemImage != null)
            {
                _resultItemImage.sprite = icon;
                _resultItemImage.color = icon != null ? Color.white : Color.clear;
                _resultItemImage.gameObject.SetActive(icon != null);
            }

            if (_resultItemName != null)
                _resultItemName.text = itemName ?? string.Empty;

            if (_resultItemParameters != null)
            {
                _resultItemParameters.text = itemParameters ?? string.Empty;
                _resultItemParameters.gameObject.SetActive(
                    !string.IsNullOrWhiteSpace(itemParameters)
                );
            }

            if (showNewBadge || _resultNewBadge != null)
                GetOrCreateNewBadge().gameObject.SetActive(showNewBadge);
        }

        private TMP_Text GetOrCreateNewBadge()
        {
            if (_resultNewBadge != null)
                return _resultNewBadge;

            var badgeObject = new GameObject(
                "NewCraftedItemBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer)
            );
            var badgeRect = (RectTransform)badgeObject.transform;
            badgeRect.SetParent(ResultRoot.transform, false);
            badgeRect.anchorMin = Vector2.one;
            badgeRect.anchorMax = Vector2.one;
            badgeRect.pivot = Vector2.one;
            badgeRect.anchoredPosition = new Vector2(-36f, -36f);
            badgeRect.sizeDelta = new Vector2(220f, 70f);
            badgeRect.SetAsLastSibling();

            _resultNewBadge = badgeObject.AddComponent<TextMeshProUGUI>();
            _resultNewBadge.text = "NEW!!";
            _resultNewBadge.alignment = TextAlignmentOptions.TopRight;
            _resultNewBadge.fontSize = 40f;
            _resultNewBadge.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _resultNewBadge.color = new Color(1f, 0.82f, 0.12f, 1f);
            _resultNewBadge.raycastTarget = false;
            return _resultNewBadge;
        }

        // --- 警告トースト(横に揺れてからフェードアウト) ---

        public void ShowWarning(CraftBlockReason reason)
        {
            if (
                !ValidateRequiredReference(_warningText, nameof(_warningText))
                || !ValidateRequiredReference(_warningCanvasGroup, nameof(_warningCanvasGroup))
            )
                return;

            StopWarningAnimation();
            _warningText.text = GetWarningMessage(reason);
            _warningText.gameObject.SetActive(true);
            _warningCanvasGroup.alpha = 1f;
            _warningRoutine = StartCoroutine(PlayWarningRoutine());
        }

        public void HideWarning()
        {
            StopWarningAnimation();
            if (_warningCanvasGroup != null)
                _warningCanvasGroup.alpha = 0f;
            if (_warningText != null)
                _warningText.gameObject.SetActive(false);
        }

        private static string GetWarningMessage(CraftBlockReason reason)
        {
            return reason switch
            {
                CraftBlockReason.CategoryMismatch => "同じカテゴリーの素材を選択してください",
                CraftBlockReason.EquippedMaterial => "装備中のアイテムは素材にできません",
                CraftBlockReason.MissingMaterials => "素材が足りません！",
                CraftBlockReason.QuickFoodMaterial =>
                    "即時使用にセット中のアイテムは素材にできません",
                _ => string.Empty,
            };
        }

        private IEnumerator PlayWarningRoutine()
        {
            var rect = _warningText.rectTransform;
            float elapsed = 0f;
            while (elapsed < WarningShakeDuration)
            {
                float damping = 1f - Mathf.Clamp01(elapsed / WarningShakeDuration);
                float offsetX =
                    Mathf.Sin(elapsed * Mathf.PI * 2f * WarningShakeFrequency)
                    * WarningShakeDistance
                    * damping;
                rect.anchoredPosition = _warningBasePosition + new Vector2(offsetX, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            rect.anchoredPosition = _warningBasePosition;
            yield return new WaitForSecondsRealtime(WarningFadeDelay);

            elapsed = 0f;
            while (elapsed < WarningFadeDuration)
            {
                _warningCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / WarningFadeDuration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _warningRoutine = null;
            HideWarning();
        }

        private void StopWarningAnimation()
        {
            if (_warningRoutine != null)
            {
                StopCoroutine(_warningRoutine);
                _warningRoutine = null;
            }

            if (_warningText != null && _hasWarningBasePosition)
                _warningText.rectTransform.anchoredPosition = _warningBasePosition;
        }

        // --- 参照チェック ---

        private bool HasRequiredReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_closeButton, nameof(_closeButton));
            valid &= ValidateRequiredReference(_loadingRoot, nameof(_loadingRoot));
            valid &= ValidateRequiredReference(_loadingGear, nameof(_loadingGear));
            valid &= ValidateRequiredReference(_resultCanvasGroup, nameof(_resultCanvasGroup));
            valid &= ValidateRequiredReference(_warningText, nameof(_warningText));
            valid &= ValidateRequiredReference(_warningCanvasGroup, nameof(_warningCanvasGroup));
            return valid;
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
