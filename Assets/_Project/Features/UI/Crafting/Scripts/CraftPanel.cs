using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 調合画面。素材2つをインベントリから選び、「ローディング → 結果パネル」の流れで調合する。
    /// 完成予想・パラメータ・素材タブの表示もここで描き直す。
    /// </summary>
    public sealed class CraftPanel : MonoBehaviour
    {
        public const int MaterialSlotCount = 2;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        [Min(0f)]
        private float _craftFlowDurationSeconds = 1f;

        [Header("Materials")]
        [SerializeField]
        private ItemListView _inventory;

        [SerializeField]
        private MaterialSlot[] _materialSlots = new MaterialSlot[MaterialSlotCount];

        [SerializeField]
        private SlotIconView _craftedItemSlot;

        [SerializeField]
        private Button _craftButton;

        [Header("素材タブ(食材 / 装備)")]
        [SerializeField]
        private Button _quickButton;

        [SerializeField]
        private Button _equipmentButton;

        [SerializeField]
        private TMP_Text _quickCount;

        [SerializeField]
        private TMP_Text _equipmentCount;

        [Header("完成予想")]
        [SerializeField]
        private Image _predictionIcon;

        [SerializeField]
        private TMP_Text _predictionName;

        [SerializeField]
        private GameObject _equipmentParameters;

        [SerializeField]
        private GameObject _quickParameters;

        [SerializeField]
        private TMP_Text[] _equipmentValues;

        [SerializeField]
        private TMP_Text _healValue;

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
        private InventoryManager _manager;
        private CraftingService _crafting;
        private int _selectedMaterialSlotIndex = -1;
        private bool _isCrafting; // 調合ボタンを押してから結果を閉じるまで
        private Coroutine _craftRoutine;
        private Coroutine _initialSelectionRoutine;
        private ItemCategory _materialCategory = ItemCategory.Food;
        private CraftResultPopup _result;
        private CraftWarningToast _warning;

        /// <summary>調合ボタンを押してから結果を閉じるまでの間か。この間は素材やタブを操作できない。</summary>
        public bool IsCrafting => _isCrafting;

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_inventory == null)
                _inventory = GetComponentInChildren<ItemListView>(true);
            if (_materialSlots.Any(slot => slot == null))
                _materialSlots = GetComponentsInChildren<MaterialSlot>(true)
                    .OrderBy(slot => slot.transform.GetSiblingIndex())
                    .Take(MaterialSlotCount)
                    .ToArray();
            if (_craftButton == null)
                _craftButton = GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "CraftButton");
        }
#endif

        private void Awake()
        {
            _result = new CraftResultPopup(
                _loadingRoot,
                _loadingGear,
                _resultCanvasGroup,
                _resultCloseOnClick,
                _resultItemImage,
                _resultItemName,
                _resultItemParameters,
                HideResult
            );
            _warning = new CraftWarningToast(this, _warningText, _warningCanvasGroup);

            _closeButton.onClick.AddListener(HideResult);
            _craftButton.onClick.AddListener(StartCraft);
            _quickButton.onClick.AddListener(() => SelectMaterialCategory(ItemCategory.Food));
            _equipmentButton.onClick.AddListener(() =>
                SelectMaterialCategory(ItemCategory.Equipment)
            );
            if (_craftedItemSlot.TryGetComponent(out SlotFrameView craftedFrame))
                craftedFrame.SetRole(SlotFrameRole.ItemSet);
            _inventory.SetSelectFirstSlotOnRefresh(false);
        }

        private void OnEnable()
        {
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted += OnInventorySlotSubmitted;
            _inventory.ItemsProvider = MaterialItems;
            foreach (var slot in _materialSlots)
            {
                slot.Clicked += OnMaterialSlotClicked;
                slot.DoubleClicked += OnMaterialSlotDoubleClicked;
                slot.NormalizeVisualState();
            }
            // 所持品は UI より先に常駐生成される
            _manager = InventoryManager.Instance;
            if (_manager != null)
            {
                _manager.Changed += OnInventoryChanged;
                _crafting = new CraftingService(_manager);
            }

            ResetSlots(resetInventoryView: true);
            SelectFirstSlotIfNeeded();
            RestartInitialSelectionRoutine();
            CancelCraft();
            RefreshCounts();
        }

        private void OnDisable()
        {
            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotSubmitted -= OnInventorySlotSubmitted;
            _inventory.ItemsProvider = null;
            foreach (var slot in _materialSlots)
            {
                slot.Clicked -= OnMaterialSlotClicked;
                slot.DoubleClicked -= OnMaterialSlotDoubleClicked;
            }
            if (_manager != null)
                _manager.Changed -= OnInventoryChanged;
            _manager = null;
            _crafting = null;

            CancelCraft();
            StopInitialSelectionRoutine();
            ResetMaterialAssignments(resetSelection: true);
        }

        private void OnDestroy() => _result?.Dispose();

        private void Update()
        {
            _result.Tick();
            HandleKeyboard();
        }

        private void OnInventoryChanged()
        {
            _inventory.RefreshCurrentTab();
            RefreshCounts();
        }

        // 調合の一覧はタブを持たず、選んでいる素材カテゴリのアイテムだけを並べる。
        private List<ItemStack> MaterialItems() =>
            _manager != null ? _manager.GetItemsByCategory(_materialCategory) : null;

        private void SelectMaterialCategory(ItemCategory category)
        {
            if (_isCrafting)
                return;
            _materialCategory = category;
            ResetSlots(resetInventoryView: true);
            SelectFirstSlotIfNeeded();
        }

        // --- 素材スロット ---

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
            Refresh();
        }

        private void ResetMaterialAssignments(bool resetSelection)
        {
            Array.Clear(_materials, 0, _materials.Length);
            foreach (var slot in _materialSlots)
            {
                slot.Clear();
                slot.SetSelected(false);
            }
            _craftedItemSlot.Clear();
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
            if (_isCrafting)
                return;
            SlotKeyboardFocus.Claim(this);
            SelectSlot(Array.IndexOf(_materialSlots, slot));
        }

        private void OnMaterialSlotDoubleClicked(MaterialSlot slot)
        {
            int index = Array.IndexOf(_materialSlots, slot);
            if (_isCrafting || index < 0)
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

        // 素材を外す。外し終わるアニメーションのあとで表示をそろえる
        private void ClearSlotAnimated(int index)
        {
            SelectSlot(index);
            _materials[index] = null;
            _materialSlots[index]
                .ClearMaterialAnimated(() =>
                {
                    SyncInventoryAssignedColors();
                    _inventory.ClearSelection();
                    Refresh();
                });
            _inventory.ClearSelection();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (_isCrafting || _selectedMaterialSlotIndex < 0 || !IsValidStack(stack))
                return;
            if (WarnIfCannotPlace(stack, null))
                return;

            // 割り当て済みの素材をもう一度選んだら外す。
            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                ClearSlotAnimated(assignedIndex);
                return;
            }

            if (WarnIfCannotPlace(stack, OtherMaterial(_selectedMaterialSlotIndex)))
                return;

            AssignMaterial(_selectedMaterialSlotIndex, stack);
            SelectNextEmptySlot();
        }

        private void OnInventorySlotSubmitted(ItemStack stack)
        {
            if (_isCrafting || !IsValidStack(stack))
                return;

            // 割り当て済みの素材を決定したら外す。一覧の選択はそのまま残す
            int assignedIndex = FindAssignedIndex(stack.Data);
            if (assignedIndex >= 0)
            {
                _selectedMaterialSlotIndex = assignedIndex;
                _materials[assignedIndex] = null;
                HighlightSlot(assignedIndex);
                _materialSlots[assignedIndex].ClearMaterialAnimated();
                SyncInventoryAssignedColors();
                Refresh();
                _inventory.SelectItem(stack);
                SlotKeyboardFocus.Claim(_inventory);
                return;
            }

            int destinationIndex = Array.IndexOf(_materials, null);
            if (destinationIndex < 0)
                return;

            _selectedMaterialSlotIndex = destinationIndex;
            HighlightSlot(destinationIndex);
            if (WarnIfCannotPlace(stack, OtherMaterial(destinationIndex)))
                return;

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
            Refresh();
        }

        // 素材に置けなければ、理由を警告に出して true。決まりは CraftingService が持つ。
        private bool WarnIfCannotPlace(ItemStack stack, ItemStack other)
        {
            if (_crafting == null)
                return false;
            var problem = _crafting.CheckMaterial(stack, other);
            if (problem == CraftMaterialProblem.None)
                return false;
            ShowWarning(problem);
            return true;
        }

        // slotIndex 以外の枠に置いてある素材(無ければ null)。
        private ItemStack OtherMaterial(int slotIndex)
        {
            for (int i = 0; i < _materials.Length; i++)
                if (i != slotIndex && _materials[i] != null)
                    return _materials[i];
            return null;
        }

        // 置いてある素材の問題(装備中・食材の枠・カテゴリ違い)。無ければ None。
        private CraftMaterialProblem MaterialsProblem()
        {
            if (_crafting == null)
                return CraftMaterialProblem.None;
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null)
                    continue;
                var problem = _crafting.CheckMaterial(_materials[i], OtherMaterial(i));
                if (problem != CraftMaterialProblem.None)
                    return problem;
            }
            return CraftMaterialProblem.None;
        }

        private int FindAssignedIndex(ItemData item) =>
            Array.FindIndex(_materials, stack => stack?.Data == item);

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

        private void SyncInventoryAssignedColors() =>
            _inventory.SetCraftAssignedStacks(_materials.Where(stack => stack != null).ToList());

        private bool HasAllMaterials() => _materials.All(stack => stack != null);

        // 置いた素材2つでできる完成品。そろっていない・組み合わせが無ければ null
        private ItemData FindAssignedResult()
        {
            var catalog = ItemCatalog.Instance;
            return HasAllMaterials() && catalog != null
                ? catalog.FindCraftResult(_materials[0].Data, _materials[1].Data)
                : null;
        }

        private bool CanCraft(ItemData result) =>
            result != null
            && _crafting != null
            && _crafting.CanCraft(result, _materials[0], _materials[1]);

        // --- 描き直し(素材・調合中かが変わるたびにここだけを呼ぶ) ---

        private void Refresh()
        {
            var result = FindAssignedResult();
            bool canCraft = CanCraft(result);

            if (result != null)
                _craftedItemSlot.SetIcon(result);
            else
                _craftedItemSlot.Clear();
            _craftButton.interactable = !_isCrafting && canCraft;
            _inventory.SetInteractionEnabled(!_isCrafting);
            _quickButton.interactable = !_isCrafting;
            _equipmentButton.interactable = !_isCrafting;

            bool quick = _materialCategory == ItemCategory.Food;
            SetTabColors(quick);
            _quickParameters.SetActive(quick);
            _equipmentParameters.SetActive(!quick);
            ShowPrediction(result);

            var problem = _isCrafting || canCraft ? CraftMaterialProblem.None : MaterialsProblem();
            if (problem == CraftMaterialProblem.None)
                HideWarning();
            else
                ShowWarning(problem);
        }

        private void ShowPrediction(ItemData item)
        {
            ShowHeader(item);
            var stats = ItemStatTextFormatter.EquipmentStats;
            for (int i = 0; i < _equipmentValues.Length && i < stats.Count; i++)
                _equipmentValues[i].text =
                    item == null
                        ? "—"
                        : CraftParameterTextFormatter.Preview(
                            stats[i],
                            _materials[0],
                            _materials[1]
                        );
            _healValue.text = item is FoodData food ? ItemStatTextFormatter.Recovery(food) : "—";
        }

        private void ShowResultParameters(ItemStack stack)
        {
            ShowHeader(stack?.Data);
            var stats = ItemStatTextFormatter.EquipmentStats;
            for (int i = 0; i < _equipmentValues.Length && i < stats.Count; i++)
                _equipmentValues[i].text = ItemStatTextFormatter.Actual(stats[i], stack);
            _healValue.text = stack?.Data is FoodData food
                ? ItemStatTextFormatter.Recovery(food)
                : "—";
        }

        private void ShowHeader(ItemData item)
        {
            _predictionIcon.sprite = item != null ? item.icon : null;
            _predictionIcon.color = item != null && item.icon != null ? Color.white : Color.clear;
            _predictionName.text = item != null ? item.itemName : "素材を2つ選択";
        }

        // 各タブに、調合に使える(合成前の)素材の数を出す
        private void RefreshCounts()
        {
            int quick = 0,
                equipment = 0;
            if (_manager != null)
                foreach (var stack in _manager.GetAllItems())
                {
                    if (stack?.Data == null || stack.Count <= 0 || !stack.Data.IsBaseMaterial)
                        continue;
                    if (stack.Data.category == ItemCategory.Food)
                        quick++;
                    else if (stack.Data.category == ItemCategory.Equipment)
                        equipment++;
                }
            _quickCount.text = quick.ToString(CultureInfo.InvariantCulture);
            _equipmentCount.text = equipment.ToString(CultureInfo.InvariantCulture);
        }

        private void SetTabColors(bool quick)
        {
            _quickButton.targetGraphic.color = quick
                ? UIVisualTheme.SelectedTab
                : UIVisualTheme.Tab;
            _equipmentButton.targetGraphic.color = quick
                ? UIVisualTheme.Tab
                : UIVisualTheme.SelectedTab;
            foreach (var text in _quickButton.GetComponentsInChildren<TMP_Text>(true))
                text.color = quick ? UIVisualTheme.Gold : UIVisualTheme.Ink;
            foreach (var text in _equipmentButton.GetComponentsInChildren<TMP_Text>(true))
                text.color = quick ? UIVisualTheme.Ink : UIVisualTheme.Gold;
        }

        // --- 調合の流れ ---

        private void StartCraft()
        {
            if (_isCrafting)
                return;

            var result = FindAssignedResult();
            if (!CanCraft(result))
            {
                var problem = MaterialsProblem();
                if (problem != CraftMaterialProblem.None)
                    ShowWarning(problem);
                return;
            }

            var first = _materials[0];
            var second = _materials[1];
            _craftRoutine = StartCoroutine(CraftRoutine(() => Craft(result, first, second)));
        }

        // ローディング → 調合 → 結果パネル → 閉じるまでを1本で進める。craft は作られた1個分を返し、失敗なら null。
        private IEnumerator CraftRoutine(Func<ItemStack> craft)
        {
            if (_isCrafting)
                yield break;

            SetCrafting(true);
            _result.HideImmediately();
            _result.ShowLoading();
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _craftFlowDurationSeconds));

            ItemStack crafted = craft();
            _result.HideLoading();
            if (crafted == null)
            {
                _result.HideImmediately();
                FinishCraft();
                yield break;
            }

            bool closed = false;
            ShowResult(crafted, () => closed = true);
            yield return null;
            ResetMaterialAssignments(resetSelection: false);
            _inventory.RefreshCurrentTab();

            while (!closed)
                yield return null;
            OnResultClosed();
            FinishCraft();
        }

        private ItemStack Craft(
            ItemData result,
            ItemStack firstMaterial,
            ItemStack secondMaterial
        ) =>
            _crafting != null
            && _crafting.TryCraft(result, firstMaterial, secondMaterial, out var crafted)
                ? crafted
                : null;

        private void OnResultClosed()
        {
            ResetSlots(resetInventoryView: false);
            SelectFirstSlotIfNeeded();
        }

        // 調合を途中でやめる(画面を閉じたとき)。結果パネルも隠す。
        private void CancelCraft()
        {
            if (_craftRoutine != null)
                StopCoroutine(_craftRoutine);
            _result.HideLoading();
            _result.HideImmediately();
            FinishCraft();
        }

        private void FinishCraft()
        {
            _craftRoutine = null;
            SetCrafting(false);
        }

        private void SetCrafting(bool crafting)
        {
            _isCrafting = crafting;
            Refresh();
        }

        /// <summary>結果パネルにできたアイテムを出す。閉じ終わったら closeAction を呼ぶ。</summary>
        public void ShowResult(ItemStack crafted, Action closeAction)
        {
            HideWarning();
            _result.Show(crafted, closeAction);
            ShowResultParameters(crafted);
        }

        // 閉じるボタン・結果パネルのクリックから呼ばれる。
        private void HideResult()
        {
            HideWarning();
            _result.Close();
        }

        // --- キー操作 ---

        private void HandleKeyboard()
        {
            if (_result.IsWaitingForClose)
            {
                if (
                    UIInputActionMap.WasSubmitPressedThisFrame()
                    || UIInputActionMap.WasCancelPressedThisFrame()
                )
                    HideResult();
                return;
            }
            if (_isCrafting)
                return;

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.TabPrev)
                || UIInputActionMap.WasPressedThisFrame(UIInputActionMap.TabNext)
            )
            {
                SelectMaterialCategory(
                    _materialCategory == ItemCategory.Food
                        ? ItemCategory.Equipment
                        : ItemCategory.Food
                );
                return;
            }

            if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.CraftExecute))
            {
                StartCraft();
                return;
            }

            if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.AuxAction))
            {
                if (SlotKeyboardFocus.IsFocused(this))
                    _inventory.SelectFirstVisibleSlotAndClaimFocus();
                else
                    SelectFirstSlotIfNeeded();
                return;
            }

            if (_selectedMaterialSlotIndex < 0 || !SlotKeyboardFocus.IsFocused(this))
                return;

            if (UIInputActionMap.TryReadDiscreteMove(out Vector2Int step))
            {
                if (step.x != 0)
                {
                    int offset = step.x > 0 ? 1 : -1;
                    SelectSlot(
                        (_selectedMaterialSlotIndex + offset + MaterialSlotCount)
                            % MaterialSlotCount
                    );
                }
                else if (step.y < 0)
                    _inventory.SelectFirstVisibleSlotAndClaimFocus();
            }
            else if (UIInputActionMap.WasSubmitPressedThisFrame())
            {
                if (_materials[_selectedMaterialSlotIndex] != null)
                    ClearSlotAnimated(_selectedMaterialSlotIndex);
                else
                    _inventory.SelectFirstVisibleSlotAndClaimFocus();
            }
        }

        // --- 警告トースト ---

        private void ShowWarning(CraftMaterialProblem problem) =>
            _warning.Show(
                problem switch
                {
                    CraftMaterialProblem.CategoryMismatch =>
                        "同じカテゴリーの素材を選択してください",
                    CraftMaterialProblem.Equipped => "装備中のアイテムは素材にできません",
                    CraftMaterialProblem.InQuickFood =>
                        "即時使用にセット中のアイテムは素材にできません",
                    _ => string.Empty,
                }
            );

        private void HideWarning() => _warning.Hide();
    }
}
