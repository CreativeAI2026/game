using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class CraftPanel : MonoBehaviour
    {
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
        private string _notReadyMessage = "素材を2つ選択してください";

        [SerializeField]
        private string _readyMessage = "合成できます";

        [SerializeField]
        private string _categoryMismatchMessage = "同じカテゴリーの素材を選択してください";

        [SerializeField]
        private Color _warningColor = new Color(0.85f, 0.2f, 0.2f, 1f);

        [SerializeField]
        private Color _readyColor = new Color(0.2f, 0.65f, 0.25f, 1f);

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
            if (_initialSelectionRoutine != null)
                StopCoroutine(_initialSelectionRoutine);
            _initialSelectionRoutine = StartCoroutine(EnsureInitialSelectionNextFrame());
            ResetCraftFlow();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopCraftRoutine();
            if (_initialSelectionRoutine != null)
            {
                StopCoroutine(_initialSelectionRoutine);
                _initialSelectionRoutine = null;
            }
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
            _warningText ??= FindDescendant("WarningText")?.GetComponent<TMP_Text>();
            InitializeSlots();
            BindCraftFlow();
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

        private void InitializeSlots()
        {
            if (_slotsRoot == null)
                return;

            foreach (var slot in _slots)
                if (slot != null)
                {
                    slot.Clicked -= SelectSlot;
                    slot.DoubleClicked -= ClearSlot;
                }

            _slots.Clear();
            _selectedSlot = null;

            for (int i = 0; i < _slotsRoot.childCount; i++)
            {
                var slotObject = _slotsRoot.GetChild(i).gameObject;
                var slot = slotObject.GetComponent<MaterialSlot>();
                if (slot == null)
                    slot = slotObject.AddComponent<MaterialSlot>();

                slot.Clicked += SelectSlot;
                slot.DoubleClicked += ClearSlot;
                _slots.Add(slot);
            }
        }

        private void Subscribe()
        {
            if (_inventory == null || _isSubscribed)
                return;

            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (_inventory != null && _isSubscribed)
                _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;

            _isSubscribed = false;
        }

        private void SelectFirstSlotIfNeeded()
        {
            if (_selectedSlot == null && _slots.Count > 0)
                SelectSlot(_slots[0]);
        }

        private IEnumerator EnsureInitialSelectionNextFrame()
        {
            yield return null;

            if (_slots.Count > 0)
                SelectSlot(_slots[0]);

            _initialSelectionRoutine = null;
        }

        private void ResetSlots()
        {
            _selectedSlot = null;

            foreach (var slot in _slots)
            {
                slot.Clear();
                slot.SetSelected(false);
            }

            SyncInventoryAssignedColors();
            _inventory?.ResetViewState();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private void SelectSlot(MaterialSlot selectedSlot)
        {
            if (selectedSlot == null)
                return;

            var previousSlot = _selectedSlot;
            _selectedSlot = selectedSlot;
            foreach (var slot in _slots)
                slot.SetSelected(slot == selectedSlot);

            var selectedStack = InventoryManager
                .Instance?.GetAllItems()
                .Find(stack => stack.Data == selectedSlot.Item);
            if (selectedStack != null)
                _inventory?.SelectItem(selectedStack);
            else
                _inventory?.ClearSelection();

            bool changedBetweenEmptySlots =
                previousSlot != null
                && previousSlot != selectedSlot
                && previousSlot.Item == null
                && selectedSlot.Item == null;
            _detailPanel?.Show(selectedSlot.Item, "（未選択）", changedBetweenEmptySlots);
        }

        private void ClearSlot(MaterialSlot slot)
        {
            if (slot == null)
                return;

            SelectSlot(slot);
            slot.ClearAnimated();
            SyncInventoryAssignedColors();
            _inventory?.ClearSelection();
            _detailPanel?.Clear();
            UpdateCraftButton();
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (_selectedSlot == null || stack?.Data == null || stack.Count <= 0)
                return;

            int currentCount = _selectedSlot.Item == stack.Data ? _selectedSlot.Count : 0;
            int desiredCount = Mathf.Min(currentCount + 1, stack.Count);

            ClearMaterialFromOtherSlots(stack.Data, _selectedSlot);

            _selectedSlot.SetMaterialAnimated(stack.Data, desiredCount);
            SyncInventoryAssignedColors();
            _detailPanel?.Show(stack.Data);
            SelectNextEmptyCraftSlot();
            RefreshDetailFromSelectedCraftSlot();
            UpdateCraftButton();
        }

        private void SelectNextEmptyCraftSlot()
        {
            if (_selectedSlot == null || _slots.Count <= 1)
                return;

            int selectedIndex = _slots.IndexOf(_selectedSlot);
            if (selectedIndex < 0)
                return;

            for (int offset = 1; offset < _slots.Count; offset++)
            {
                int slotIndex = (selectedIndex + offset) % _slots.Count;
                if (_slots[slotIndex].Item != null)
                    continue;

                SelectSlot(_slots[slotIndex]);
                return;
            }
        }

        private void SyncInventoryAssignedColors()
        {
            _inventory?.SetCraftAssignedItems(
                _slots.Where(slot => slot.Item != null).Select(slot => slot.Item)
            );
        }

        private void RefreshDetailFromSelectedCraftSlot()
        {
            if (_detailPanel == null || _selectedSlot == null)
                return;

            _detailPanel.Show(_selectedSlot.Item, "（未選択）");
        }

        private void ClearMaterialFromOtherSlots(ItemData item, MaterialSlot destinationSlot)
        {
            foreach (var slot in _slots)
            {
                if (slot == destinationSlot || slot.Item != item)
                    continue;

                slot.ClearAnimated();
            }
        }

        private void FindCraftFlowReferences()
        {
            _craftButton ??= FindDescendant("CraftButton")?.GetComponent<Button>();
            _loadingPanel ??= FindDescendant("LoadingPanel")?.gameObject;
            _loadingGear ??= FindDescendant("LoadingGear") as RectTransform;
            _resultPanel ??= FindDescendant("ResultPanel")?.gameObject;
            if (_resultPanel != null)
            {
                _resultItemImage ??= FindComponentIn<Image>(_resultPanel.transform, "ItemImage");
                _resultItemName ??= FindComponentIn<TMP_Text>(_resultPanel.transform, "ItemName");
            }
            _closeButton ??= FindDescendant("CloseButton")?.gameObject;
        }

        private Transform FindDescendant(string objectName)
        {
            return GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }

        private void BindCraftFlow()
        {
            if (_craftButton != null)
            {
                _craftButton.onClick.RemoveListener(StartCraft);
                _craftButton.onClick.AddListener(StartCraft);
            }

            if (_resultPanel == null)
                return;

            var resultImage = _resultPanel.GetComponent<Image>();
            if (resultImage == null)
            {
                resultImage = _resultPanel.AddComponent<Image>();
                resultImage.color = Color.clear;
            }
            resultImage.raycastTarget = true;

            _resultClickCatcher = _resultPanel.GetComponent<ResultPanelClickCatcher>();
            if (_resultClickCatcher == null)
                _resultClickCatcher = _resultPanel.AddComponent<ResultPanelClickCatcher>();

            _resultClickCatcher.SetClickAction(CloseResult);
        }

        private void UpdateCraftButton()
        {
            bool hasEnoughMaterials = HasEnoughMaterials();
            bool hasCategoryMismatch = HasCategoryMismatch();
            bool canCraft = hasEnoughMaterials && !hasCategoryMismatch;

            if (_craftButton != null)
                _craftButton.interactable = !_isCrafting && canCraft;

            if (_warningText != null)
            {
                _warningText.text =
                    canCraft ? _readyMessage
                    : hasCategoryMismatch ? _categoryMismatchMessage
                    : _notReadyMessage;
                _warningText.color = canCraft ? _readyColor : _warningColor;
            }
        }

        private void StartCraft()
        {
            if (_isCrafting || !CanCraft())
                return;

            StopCraftRoutine();
            _craftRoutine = StartCoroutine(CraftRoutine());
        }

        private bool CanCraft()
        {
            return HasEnoughMaterials() && !HasCategoryMismatch();
        }

        private bool HasEnoughMaterials()
        {
            return _slots.Count(slot => slot.Item != null) >= 2;
        }

        private bool HasCategoryMismatch()
        {
            var selectedItems = _slots
                .Where(slot => slot.Item != null)
                .Select(slot => slot.Item)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return false;

            return selectedItems[0].category != selectedItems[1].category;
        }

        private IEnumerator CraftRoutine()
        {
            _isCrafting = true;
            _lastCraftedRecipe = FindSelectedRecipe();
            UpdateCraftButton();

            SetCloseButtonVisible(false);

            if (_loadingPanel != null)
                _loadingPanel.SetActive(true);
            if (_loadingGear != null)
            {
                _loadingGear.gameObject.SetActive(true);
                _loadingGear.localRotation = Quaternion.identity;
            }
            if (_resultPanel != null)
                _resultPanel.SetActive(false);

            yield return new WaitForSecondsRealtime(_testCraftDuration);

            _isCrafting = false;
            _craftRoutine = null;

            if (_loadingGear != null)
                _loadingGear.gameObject.SetActive(false);
            if (_resultPanel != null)
            {
                RefreshResultPanel();
                _resultClickCatcher?.SetClickAction(CloseResult);
                _resultPanel.SetActive(true);
            }

            UpdateCraftButton();
        }

        private CraftRecipeData FindSelectedRecipe()
        {
            if (_recipeDB == null)
                return null;

            var selectedItems = _slots
                .Where(slot => slot.Item != null)
                .Select(slot => slot.Item)
                .Take(2)
                .ToList();

            if (selectedItems.Count < 2)
                return null;

            _recipeDB.RevealRecipe(selectedItems[0], selectedItems[1], out var recipe);
            return recipe;
        }

        private void RefreshResultPanel()
        {
            FindCraftFlowReferences();
            var resultItem = _lastCraftedRecipe?.resultItem;

            if (_resultItemImage != null)
            {
                _resultItemImage.sprite = resultItem?.icon;
                _resultItemImage.color = resultItem?.icon != null ? Color.white : Color.clear;
                _resultItemImage.gameObject.SetActive(resultItem?.icon != null);
            }

            if (_resultItemName != null)
                _resultItemName.text = resultItem != null ? resultItem.itemName : string.Empty;
        }

        private void CloseResult()
        {
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);

            SetCloseButtonVisible(true);
            ResetSlots();
            SelectFirstSlotIfNeeded();
        }

        private void ResetCraftFlow()
        {
            StopCraftRoutine();
            _lastCraftedRecipe = null;
            RefreshResultPanel();

            if (_loadingGear != null)
            {
                _loadingGear.localRotation = Quaternion.identity;
                _loadingGear.gameObject.SetActive(true);
            }
            if (_resultPanel != null)
                _resultPanel.SetActive(false);
            if (_loadingPanel != null)
                _loadingPanel.SetActive(false);

            SetCloseButtonVisible(true);
            UpdateCraftButton();
        }

        private void SetCloseButtonVisible(bool visible)
        {
            if (_closeButton != null)
                _closeButton.SetActive(visible);
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

        private static T FindComponentIn<T>(Transform root, string objectName)
            where T : Component
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName && child.TryGetComponent(out T component))
                    return component;

            return null;
        }
    }
}
