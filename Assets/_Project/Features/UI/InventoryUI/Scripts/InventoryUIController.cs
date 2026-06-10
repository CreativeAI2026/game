using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class InventoryUIController : MonoBehaviour
    {
        [SerializeField]
        private Transform _tabsRoot;

        [SerializeField]
        private Transform _slotsRoot;

        [SerializeField]
        private Image _detailIcon;

        [SerializeField]
        private Text _detailName;

        [SerializeField]
        private Text _detailCategory;

        [SerializeField]
        private Text _detailDescription;

        [SerializeField]
        private Text _detailEffect;

        [SerializeField]
        private Slot _slotPrefab;

        [SerializeField]
        private ItemDB _itemDB;

        private enum ItemCategory
        {
            Weapon = 0,
            Equipment = 1,
            Food = 2,
            Important = 3,
        }

        private static readonly Color ActiveColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color InactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private List<ItemData> _weapons;
        private List<ItemData> _equipments;
        private List<ItemData> _foods;
        private List<ItemData> _importants;
        private Dictionary<ItemCategory, Button> _tabs;
        private Dictionary<ItemCategory, List<ItemData>> _categories;
        private bool _navigationDisabled = false;

        private void Awake()
        {
            _tabs = new Dictionary<ItemCategory, Button>
            {
                { ItemCategory.Weapon, _tabsRoot.Find("WeaponTab").GetComponent<Button>() },
                { ItemCategory.Equipment, _tabsRoot.Find("EquipmentTab").GetComponent<Button>() },
                { ItemCategory.Food, _tabsRoot.Find("FoodTab").GetComponent<Button>() },
                { ItemCategory.Important, _tabsRoot.Find("ImportantTab").GetComponent<Button>() },
            };

            BuildCategoryLists();

            _categories = new Dictionary<ItemCategory, List<ItemData>>
            {
                { ItemCategory.Weapon, _weapons },
                { ItemCategory.Equipment, _equipments },
                { ItemCategory.Food, _foods },
                { ItemCategory.Important, _importants },
            };

            foreach (var (index, category) in _categories)
            {
                if (_tabs[index] == null)
                    continue;
                _tabs[index].onClick.AddListener(() => ShowCategory(category, _tabs[index]));
            }

            ShowCategory(_categories[ItemCategory.Weapon], _tabs[ItemCategory.Weapon]);
            _tabs[ItemCategory.Weapon].GetComponent<HoverScaleOnPointer>()?.AcquireLock();
        }

        private void OnDestroy()
        {
            foreach (var (category, tab) in _tabs)
            {
                if (tab != null)
                    tab.onClick.RemoveAllListeners();
            }
        }

        private void BuildCategoryLists()
        {
            _weapons = new List<ItemData>();
            _equipments = new List<ItemData>();
            _foods = new List<ItemData>();
            _importants = new List<ItemData>();

            AddItemById(_equipments, 2001);
            for (int i = 0; i < 40; i++)
            {
                int id = UnityEngine.Random.Range(0, 2) == 0 ? 3001 : 2001;
                AddItemById(_foods, id);
            }
        }

        private void AddItemById(List<ItemData> target, int id)
        {
            if (_itemDB == null || target == null)
                return;

            var item = _itemDB.GetItemById(id);
            if (item != null)
                target.Add(item);
        }

        private void ShowCategory(List<ItemData> items, Button activeTab)
        {
            foreach (var (index, tab) in _tabs)
            {
                UpdateTabColor(tab, tab == activeTab);
            }

            RefreshSlots(items);

            ShowDetail(items != null && items.Count > 0 ? items[0] : null);
        }

        private void RefreshSlots(List<ItemData> items)
        {
            ClearSlots();

            if (_slotsRoot == null || _slotPrefab == null || items == null)
                return;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                var slot = Instantiate(_slotPrefab, _slotsRoot, false);
                slot.SetItem(item);
            }

            // Force rebuild layout so Content size updates when using built-in layout components
            if (_slotsRoot is RectTransform contentRect)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

                var scroll = contentRect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void ClearSlots()
        {
            if (_slotsRoot == null)
                return;

            for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_slotsRoot.GetChild(i).gameObject);
            }
        }

        private void UpdateTabColor(Button tab, bool isActive)
        {
            if (tab == null)
                return;
            var color = isActive ? ActiveColor : InactiveColor;

            var images = tab.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                if (image != null)
                    image.color = color;
            }

            var texts = tab.GetComponentsInChildren<TMP_Text>(true);
            foreach (var text in texts)
            {
                if (text != null)
                    text.color = color;
            }

            var colors = tab.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Clamp01(color.r + 0.1f),
                Mathf.Clamp01(color.g + 0.1f),
                Mathf.Clamp01(color.b + 0.1f),
                color.a
            );
            tab.colors = colors;
        }

        private void ShowDetail(ItemData item)
        {
            bool hasItem = item != null;

            if (_detailIcon != null)
            {
                _detailIcon.sprite = hasItem ? item.icon : null;
                _detailIcon.color = hasItem ? Color.white : new Color(0, 0, 0, 0);
            }
            if (_detailName != null)
                _detailName.text = hasItem ? item.itemName : "";
            if (_detailCategory != null)
                _detailCategory.text = hasItem ? item.category : "";
            if (_detailDescription != null)
                _detailDescription.text = hasItem ? item.description : "";
            if (_detailEffect != null)
                _detailEffect.text = hasItem ? item.effect : "";
        }

        // Called by Slot when clicked to select it explicitly
        public void SelectSlot(Slot slot)
        {
            if (slot == null)
                return;

            // Acquire lock on the slot (will release other locked instance)
            slot.Select();

            ShowDetail(slot.Item);

            // Disable event system navigation while a slot is locked
            if (!_navigationDisabled && EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                _navigationDisabled = true;
            }
        }
    }
}
