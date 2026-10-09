using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 調合画面。素材2つをインベントリから選び、「ローディング → 結果パネル」の流れで調合する。
    /// </summary>
    public class CraftPanelController : MonoBehaviour
    {
        public const int MaterialSlotCount = 2;

        private const float GearRotationSpeed = 180f;
        private const float WarningShakeDistance = 12f;
        private const float WarningShakeDuration = 0.6f;
        private const float WarningShakeFrequency = 5f;
        private const float WarningFadeDelay = 0.8f;
        private const float WarningFadeDuration = 0.6f;
        private const float ResultFadeDuration = 0.16f;
        private const float ResultAnimationDuration = 0.22f;
        private const float ResultHiddenScale = 0.9f;

        /// <summary>素材を受け付けない理由。警告トーストの文言に対応する。</summary>
        private enum CraftWarning
        {
            CategoryMismatch,
            EquippedMaterial,
            QuickFoodMaterial,
        }

        [SerializeField]
        private CraftRecipeCatalog _recipeCatalog;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        [Min(0f)]
        private float _craftFlowDurationSeconds = 1f;

        [Header("Materials")]
        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private MaterialSlot[] _materialSlots = new MaterialSlot[MaterialSlotCount];

        [SerializeField]
        private SlotIconView _craftedItemSlot;

        [SerializeField]
        private Button _craftButton;

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

        private readonly ItemStack[] _materials = new ItemStack[MaterialSlotCount];
        private readonly HashSet<string> _warnedMessages = new();
        private InventoryManager _subscribedInventoryManager;
        private int _selectedMaterialSlotIndex = -1;
        private bool _isSubscribed;
        private bool _isCrafting;
        private bool _isCraftFlowRunning;
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private ItemData _previewedCraftedItem;
        private Action _resultClosedAction;
        private Coroutine _warningRoutine;
        private Vector2 _warningBasePosition;
        private bool _hasWarningBasePosition;

        public bool IsCraftFlowRunning => _isCraftFlowRunning;

        private bool IsCraftInteractionLocked => _isCrafting || _isCraftFlowRunning;

        private GameObject ResultRoot => _resultCanvasGroup.gameObject;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _inventory ??= GetComponentInChildren<InventoryView>(true);
            if (_materialSlots.Any(slot => slot == null))
                _materialSlots = GetComponentsInChildren<MaterialSlot>(true)
                    .OrderBy(slot => slot.transform.GetSiblingIndex())
                    .Take(MaterialSlotCount)
                    .ToArray();
            _craftButton ??= UIChildFinder.FindButton(transform, "CraftButton");
        }
#endif

        private void Awake()
        {
            if (!Initialize())
                enabled = false;
        }

        private void OnEnable()
        {
            if (!Initialize())
            {
                enabled = false;
                return;
            }

            Subscribe();
            ResetSlots(resetInventoryView: true);
            SelectFirstSlotIfNeeded();
            RestartInitialSelectionRoutine();
            StopCraftRoutine();
            CancelCraftFlow();
            HideWarning();
            UpdateCraftButton();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopCraftRoutine();
            StopInitialSelectionRoutine();
            ResetMaterialAssignments(resetSelection: true);
            CancelCraftFlow();
            HideWarning();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.ClearClickAction(HideResult);
            _resultClosedAction = null;
        }

        private void Update()
        {
            if (_loadingGear != null && _loadingGear.gameObject.activeInHierarchy)
                _loadingGear.Rotate(0f, 0f, -GearRotationSpeed * Time.unscaledDeltaTime);
            UpdateMaterialSlotKeyboardNavigation();
        }

        private bool Initialize()
        {
            bool valid = HasCraftFlowReferences();
            valid &= ValidateRequiredReference(_recipeCatalog, nameof(_recipeCatalog));
            valid &= ValidateRequiredReference(_inventory, nameof(_inventory));
            valid &= ValidateRequiredReference(_craftedItemSlot, nameof(_craftedItemSlot));
            valid &= ValidateRequiredReference(_craftButton, nameof(_craftButton));
            if (!valid)
                return false;

            if (
                _materialSlots.Length != MaterialSlotCount
                || _materialSlots.Any(slot => slot == null)
                || _materialSlots.Distinct().Count() != MaterialSlotCount
            )
            {
                WarnOnce(
                    $"{nameof(CraftPanelController)} on {name}: {nameof(_materialSlots)} に MaterialSlot を{MaterialSlotCount}つ、表示順に設定してください。"
                );
                return false;
            }

            UIButtonHoverScaleUtility.ApplyTo(_closeButton);
            _closeButton.onClick.RemoveListener(HideResult);
            _closeButton.onClick.AddListener(HideResult);

            if (!_hasWarningBasePosition)
            {
                _warningBasePosition = _warningText.rectTransform.anchoredPosition;
                _hasWarningBasePosition = true;
            }

            _craftedItemSlot.GetComponent<SlotFrameView>()?.SetRole(SlotFrameRole.ItemSet);
            _inventory.SetSelectFirstSlotOnRefresh(false);
            _inventory.SetReleaseSelectionOnOutsideClick(false);
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);
            _craftButton.onClick.RemoveListener(StartCraft);
            _craftButton.onClick.AddListener(StartCraft);
            return true;
        }

        private void Subscribe()
        {
            if (_isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted += OnInventorySlotSubmitted;
            _inventory.DisplayRefreshRequested += OnInventoryDisplayRefreshRequested;
            _inventory.ItemsRequested += OnInventoryItemsRequested;
            foreach (var slot in _materialSlots)
            {
                slot.Clicked += OnMaterialSlotClicked;
                slot.DoubleClicked += OnMaterialSlotDoubleClicked;
                slot.NormalizeVisualState();
            }
            SetCraftInteractionEnabled(!_isCraftFlowRunning);
            _subscribedInventoryManager = InventoryManager.Instance;
            if (_subscribedInventoryManager != null)
                _subscribedInventoryManager.InventoryChanged += OnInventoryChanged;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            if (_inventory != null)
            {
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
                _inventory.OnSlotSubmitted -= OnInventorySlotSubmitted;
                _inventory.DisplayRefreshRequested -= OnInventoryDisplayRefreshRequested;
                _inventory.ItemsRequested -= OnInventoryItemsRequested;
            }

            foreach (var slot in _materialSlots)
            {
                if (slot == null)
                    continue;
                slot.Clicked -= OnMaterialSlotClicked;
                slot.DoubleClicked -= OnMaterialSlotDoubleClicked;
            }

            if (_subscribedInventoryManager != null)
            {
                _subscribedInventoryManager.InventoryChanged -= OnInventoryChanged;
                _subscribedInventoryManager = null;
            }

            _isSubscribed = false;
        }

        private void OnInventoryChanged()
        {
            _inventory.RefreshCurrentTab();
        }

        private void OnInventoryDisplayRefreshRequested(
            TabDefinition definition,
            int tabIndex,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            if (definition is InventoryTabDefinition inventoryDefinition)
            {
                _inventory.RequestItems(inventoryDefinition.Category, scrollMode);
                return;
            }

            WarnOnce(
                $"{nameof(CraftPanelController)} on {name}: Inventoryのタブ {tabIndex} を解決できません。すべての Inventory TabEntry に {nameof(InventoryTabDefinition)} を設定してください。"
            );
            _inventory.SetItems(null, scrollMode);
        }

        private void OnInventoryItemsRequested(
            ItemCategory category,
            InventoryView.ScrollRefreshMode scrollMode
        )
        {
            var items = InventoryManager.Instance?.GetItemsByCategory(category);
            _inventory.SetItems(items, scrollMode);
        }

        private void SelectFirstSlotIfNeeded()
        {
            if (_selectedMaterialSlotIndex < 0)
                SelectSlot(0);

            ClaimMaterialSlotFocus();
        }

        private void RestartInitialSelectionRoutine()
        {
            StopInitialSelectionRoutine();
            _initialSelectionRoutine = StartCoroutine(SelectFirstSlotNextFrame());
        }

        private IEnumerator SelectFirstSlotNextFrame()
        {
            yield return null;

            SelectSlot(0);
            ClaimMaterialSlotFocus();
            _initialSelectionRoutine = null;
        }

        private void StopInitialSelectionRoutine()
        {
            if (_initialSelectionRoutine == null)
                return;

            StopCoroutine(_initialSelectionRoutine);
            _initialSelectionRoutine = null;
        }

        private void ResetSlots(bool resetInventoryView)
        {
            ResetMaterialAssignments(resetSelection: true);
            if (resetInventoryView)
                _inventory.ResetViewState();
            else
                _inventory.RefreshCurrentTab();
            UpdateCraftButton();
        }

        private void ResetMaterialAssignments(bool resetSelection)
        {
            Array.Clear(_materials, 0, _materials.Length);
            foreach (var slot in _materialSlots)
            {
                if (slot == null)
                    continue;
                slot.Clear();
                slot.SetSelected(false);
            }
            _previewedCraftedItem = null;
            _craftedItemSlot?.Clear();
            if (resetSelection)
                _selectedMaterialSlotIndex = -1;

            SyncInventoryAssignedColors();
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= MaterialSlotCount)
                return;

            _selectedMaterialSlotIndex = index;
            HighlightSlot(index);

            if (_materials[index] != null)
                _inventory.SelectItem(_materials[index]);
            else
                _inventory.ClearSelection();
        }

        private void OnMaterialSlotClicked(MaterialSlot slot)
        {
            int index = Array.IndexOf(_materialSlots, slot);
            if (IsCraftInteractionLocked)
                return;

            SlotKeyboardFocus.Claim(this);
            SelectSlot(index);
        }

        private void OnMaterialSlotDoubleClicked(MaterialSlot slot)
        {
            int index = Array.IndexOf(_materialSlots, slot);
            if (IsCraftInteractionLocked || index < 0)
                return;

            SlotKeyboardFocus.Claim(this);
            ClearSlotAnimated(index);
        }

        private void HighlightSlot(int index)
        {
            for (int i = 0; i < _materialSlots.Length; i++)
                _materialSlots[i].SetSelected(i == index);
        }

        private void ClaimMaterialSlotFocus()
        {
            if (_selectedMaterialSlotIndex >= 0)
                SlotKeyboardFocus.Claim(this);
        }

        private void ClearSlotAnimated(int index)
        {
            SelectSlot(index);
            _materials[index] = null;
            _materialSlots[index]
                .ClearMaterialAnimated(() =>
                {
                    SyncInventoryAssignedColors();
                    _inventory.ClearSelection();
                    UpdateCraftButton();
                });
            _inventory.ClearSelection();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (IsCraftInteractionLocked || _selectedMaterialSlotIndex < 0 || !IsValidStack(stack))
                return;

            if (ShowWarningIfUnusable(stack))
                return;

            // 割り当て済みの素材をもう一度選んだら外す。
            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                ClearSlotAnimated(assignedIndex);
                return;
            }

            if (HasCategoryMismatchWithOtherSlot(stack.Data))
            {
                ShowWarning(CraftWarning.CategoryMismatch);
                return;
            }

            AssignMaterial(_selectedMaterialSlotIndex, stack);
            SelectNextEmptySlot();
        }

        private void OnInventorySlotSubmitted(ItemStack stack)
        {
            if (IsCraftInteractionLocked || !IsValidStack(stack))
                return;

            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                _selectedMaterialSlotIndex = assignedIndex;
                _materials[assignedIndex] = null;
                HighlightSlot(assignedIndex);
                _materialSlots[assignedIndex].ClearMaterialAnimated();
                SyncInventoryAssignedColors();
                UpdateCraftButton();
                _inventory.SelectItem(stack);
                SlotKeyboardFocus.Claim(_inventory);
                return;
            }

            int destinationIndex = Array.IndexOf(_materials, null);
            if (destinationIndex < 0)
                return;

            _selectedMaterialSlotIndex = destinationIndex;
            HighlightSlot(destinationIndex);
            if (ShowWarningIfUnusable(stack))
                return;

            if (HasCategoryMismatchWithOtherSlot(stack.Data))
            {
                ShowWarning(CraftWarning.CategoryMismatch);
                return;
            }

            AssignMaterial(destinationIndex, stack);
            _inventory.SelectItem(stack);
            SlotKeyboardFocus.Claim(_inventory);
        }

        private static bool IsValidStack(ItemStack stack) => stack?.Data != null && stack.Count > 0;

        private void AssignMaterial(int index, ItemStack stack)
        {
            _materials[index] = stack;
            _materialSlots[index].SetMaterialAnimated(stack);
            SyncInventoryAssignedColors();
            UpdateCraftButton();
        }

        private bool ShowWarningIfUnusable(ItemStack stack)
        {
            if (stack.IsEquipped)
            {
                ShowWarning(CraftWarning.EquippedMaterial);
                return true;
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.IsInQuickFood(stack))
            {
                ShowWarning(CraftWarning.QuickFoodMaterial);
                return true;
            }

            return false;
        }

        private bool HasCategoryMismatchWithOtherSlot(ItemData item)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (i == _selectedMaterialSlotIndex || _materials[i]?.Data == null)
                    continue;

                if (_materials[i].Data.category != item.category)
                    return true;
            }

            return false;
        }

        private int FindAssignedIndex(ItemData item)
        {
            return Array.FindIndex(_materials, stack => stack?.Data == item);
        }

        private void SelectNextEmptySlot()
        {
            for (int offset = 1; offset < _materials.Length; offset++)
            {
                int index = (_selectedMaterialSlotIndex + offset) % _materials.Length;
                if (_materials[index] != null)
                    continue;

                SelectSlot(index);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedStacks(_materials.Where(stack => stack != null).ToList());
        }

        private bool HasAllMaterials() => _materials.All(stack => stack != null);

        private bool HasCategoryMismatch() =>
            HasAllMaterials() && _materials[0].Data.category != _materials[1].Data.category;

        private bool HasEquippedMaterial() =>
            _materials.Any(stack => stack != null && stack.IsEquipped);

        private CraftRecipe FindAssignedRecipe()
        {
            return HasAllMaterials()
                ? _recipeCatalog?.FindRecipe(_materials[0].Data, _materials[1].Data)
                : null;
        }

        private bool CanCraft(CraftRecipe recipe)
        {
            return recipe != null
                && (
                    InventoryManager.Instance?.CanCraft(recipe, _materials[0], _materials[1])
                    ?? false
                );
        }

        private void UpdateCraftButton()
        {
            var recipe = FindAssignedRecipe();
            bool canCraft = CanCraft(recipe);

            RefreshCraftedItemSlot(recipe);

            if (_craftButton != null)
                _craftButton.interactable = !IsCraftInteractionLocked && canCraft;

            if (IsCraftInteractionLocked || canCraft)
                HideWarning();
            else if (HasEquippedMaterial())
                ShowWarning(CraftWarning.EquippedMaterial);
            else if (!HasAllMaterials())
                HideWarning();
            else if (HasCategoryMismatch())
                ShowWarning(CraftWarning.CategoryMismatch);
        }

        private void RefreshCraftedItemSlot(CraftRecipe recipe)
        {
            ItemData resultItem = recipe != null ? recipe.resultItem : null;
            if (_craftedItemSlot == null || _previewedCraftedItem == resultItem)
                return;

            _previewedCraftedItem = resultItem;
            if (resultItem != null)
                _craftedItemSlot.SetIcon(resultItem);
            else
                _craftedItemSlot.Clear();
        }

        private void StartCraft()
        {
            if (IsCraftInteractionLocked)
                return;

            var recipe = FindAssignedRecipe();
            if (!CanCraft(recipe))
            {
                if (HasEquippedMaterial())
                    ShowWarning(CraftWarning.EquippedMaterial);
                else if (HasCategoryMismatch())
                    ShowWarning(CraftWarning.CategoryMismatch);

                return;
            }

            StopCraftRoutine();
            _craftRoutine = StartCoroutine(CraftRoutine(recipe, _materials[0], _materials[1]));
        }

        private IEnumerator CraftRoutine(
            CraftRecipe recipe,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        )
        {
            _isCrafting = true;
            ItemStack crafted = null;
            try
            {
                UpdateCraftButton();
                yield return RunCraftFlow(
                    () => crafted = Craft(recipe, firstMaterial, secondMaterial),
                    OnResultClosed
                );

                if (crafted != null)
                {
                    yield return null;
                    ResetMaterialAssignments(resetSelection: false);
                    _inventory.RefreshCurrentTab();
                }
            }
            finally
            {
                _craftRoutine = null;
                _isCrafting = false;
                UpdateCraftButton();
            }
        }

        private static ItemStack Craft(
            CraftRecipe recipe,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        )
        {
            var inventory = InventoryManager.Instance;
            return
                inventory != null
                && inventory.TryCraft(recipe, firstMaterial, secondMaterial, out var crafted)
                ? crafted
                : null;
        }

        private void OnResultClosed()
        {
            ResetSlots(resetInventoryView: false);
            SelectFirstSlotIfNeeded();
        }

        private void StopCraftRoutine()
        {
            if (_craftRoutine != null)
            {
                StopCoroutine(_craftRoutine);
                _craftRoutine = null;
            }

            _isCrafting = false;
        }

        private void UpdateMaterialSlotKeyboardNavigation()
        {
            if (
                IsCraftInteractionLocked
                || _selectedMaterialSlotIndex < 0
                || !SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            int offset =
                keyboard.leftArrowKey.wasPressedThisFrame ? -1
                : keyboard.rightArrowKey.wasPressedThisFrame ? 1
                : 0;
            if (offset != 0)
                SelectSlot(
                    (_selectedMaterialSlotIndex + offset + MaterialSlotCount) % MaterialSlotCount
                );
        }

        private void SetCraftInteractionEnabled(bool enabled)
        {
            _inventory?.SetInteractionEnabled(enabled);
            UpdateCraftButton();
        }

        // --- 実行フロー ---

        /// <summary>
        /// ローディング → 調合 → 結果パネルの流れ。craftAction は作られた1個分を返し、失敗なら null。
        /// </summary>
        public IEnumerator RunCraftFlow(Func<ItemStack> craftAction, Action onResultClosed)
        {
            if (craftAction == null || _isCraftFlowRunning || !HasCraftFlowReferences())
                yield break;

            bool awaitingResultClose = false;
            try
            {
                SetCraftFlowRunning(true);
                HideWarning();
                HideResultImmediately();
                ShowLoading();
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, _craftFlowDurationSeconds));

                ItemStack crafted = craftAction();
                HideLoading();
                if (crafted == null)
                {
                    HideResultImmediately();
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

                ShowResult(crafted, CompleteResult);
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
            HideLoading();
            HideResultImmediately();
            SetCraftFlowRunning(false);
        }

        private void SetCraftFlowRunning(bool running)
        {
            if (_isCraftFlowRunning == running)
                return;

            _isCraftFlowRunning = running;
            SetCraftInteractionEnabled(!running);
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

        public void ShowResult(ItemStack crafted, Action closeAction)
        {
            if (!ValidateRequiredReference(_resultCanvasGroup, nameof(_resultCanvasGroup)))
                return;

            HideWarning();
            _resultClosedAction = closeAction;
            var item = crafted?.Data;
            SetResultContent(
                item != null ? item.icon : null,
                item != null ? item.itemName : string.Empty,
                ItemStatTextFormatter.BuildStatsText(crafted)
            );
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.SetClickAction(HideResult);
            PlayResultIn();
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
            PlayResultOut(() =>
            {
                SetResultContent(null, string.Empty, string.Empty);
                closedAction?.Invoke();
            });
        }

        private void HideResultImmediately()
        {
            if (_resultCanvasGroup == null)
                return;

            _resultClosedAction = null;
            if (_resultCloseOnClick != null)
                _resultCloseOnClick.ClearClickAction(HideResult);
            ResetResultPanel();
            SetResultContent(null, string.Empty, string.Empty);
        }

        private void SetResultContent(Sprite icon, string itemName, string itemParameters)
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
        }

        // --- 警告トースト(横に揺れてからフェードアウト) ---

        private void ShowWarning(CraftWarning reason)
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

        private void HideWarning()
        {
            StopWarningAnimation();
            if (_warningCanvasGroup != null)
                _warningCanvasGroup.alpha = 0f;
            if (_warningText != null)
                _warningText.gameObject.SetActive(false);
        }

        private static string GetWarningMessage(CraftWarning reason)
        {
            return reason switch
            {
                CraftWarning.CategoryMismatch => "同じカテゴリーの素材を選択してください",
                CraftWarning.EquippedMaterial => "装備中のアイテムは素材にできません",
                CraftWarning.QuickFoodMaterial => "即時使用にセット中のアイテムは素材にできません",
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

        // --- 結果パネルの出入りアニメーション ---

        private void PlayResultIn()
        {
            var rect = ResultRoot.transform as RectTransform;
            KillResultTweens(rect);

            if (rect != null)
                rect.localScale = Vector3.one * ResultHiddenScale;
            _resultCanvasGroup.alpha = 0f;
            _resultCanvasGroup.interactable = false;
            _resultCanvasGroup.blocksRaycasts = true;

            ResultRoot.SetActive(true);

            var sequence = DOTween.Sequence().SetTarget(ResultRoot).SetUpdate(true);
            if (rect != null)
                sequence.Join(
                    rect.DOScale(Vector3.one, ResultAnimationDuration).SetEase(Ease.OutBack)
                );
            sequence.Join(_resultCanvasGroup.DOFade(1f, ResultFadeDuration));
            sequence.OnComplete(() =>
            {
                _resultCanvasGroup.interactable = true;
                _resultCanvasGroup.blocksRaycasts = true;
            });
        }

        private void PlayResultOut(Action onComplete)
        {
            if (!ResultRoot.activeSelf)
            {
                onComplete?.Invoke();
                return;
            }

            var rect = ResultRoot.transform as RectTransform;
            KillResultTweens(rect);
            _resultCanvasGroup.interactable = false;
            _resultCanvasGroup.blocksRaycasts = false;

            var sequence = DOTween.Sequence().SetTarget(ResultRoot).SetUpdate(true);
            if (rect != null)
            {
                sequence.Join(
                    rect.DOScale(Vector3.one * ResultHiddenScale, ResultAnimationDuration)
                        .SetEase(Ease.InBack)
                );
            }
            sequence.Join(_resultCanvasGroup.DOFade(0f, ResultFadeDuration));
            sequence.OnComplete(() =>
            {
                ResetResultPanel();
                onComplete?.Invoke();
            });
        }

        // アニメーションを止め、結果パネルを非表示の初期状態に戻す。
        private void ResetResultPanel()
        {
            var rect = ResultRoot.transform as RectTransform;
            KillResultTweens(rect);
            if (rect != null)
                rect.localScale = Vector3.one;
            _resultCanvasGroup.alpha = 1f;
            _resultCanvasGroup.interactable = true;
            _resultCanvasGroup.blocksRaycasts = true;
            ResultRoot.SetActive(false);
        }

        private void KillResultTweens(RectTransform rect)
        {
            DOTween.Kill(ResultRoot);
            rect?.DOKill();
            _resultCanvasGroup.DOKill();
        }

        // --- 参照チェック ---

        private bool HasCraftFlowReferences()
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

            WarnOnce(
                $"{nameof(CraftPanelController)} on {name}: 必須参照 '{fieldName}' が未設定です。Inspectorで設定してください。該当UI処理を中止します。"
            );
            return false;
        }

        private void WarnOnce(string message)
        {
            if (_warnedMessages.Add(message))
                Debug.LogWarning(message, this);
        }
    }
}
