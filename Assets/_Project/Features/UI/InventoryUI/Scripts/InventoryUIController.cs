using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class InventoryUIController : MonoBehaviour
    {
        [SerializeField]
        private Button _weaponTab;

        [SerializeField]
        private Button _equipmentTab;

        [SerializeField]
        private Button _foodTab;

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
        private Sprite _appleIcon;

        [SerializeField]
        private Sprite _clockIcon;

        [SerializeField]
        private Slot _slotPrefab;

        [SerializeField]
        private ItemDB _itemDB;

        private static readonly Color ActiveColor = new Color(0.55f, 0.7f, 0.95f, 1f);
        private static readonly Color InactiveColor = new Color(0.3f, 0.45f, 0.65f, 1f);

        private List<ItemData> _weapons;
        private List<ItemData> _equipments;
        private List<ItemData> _foods;

        private void Awake()
        {
            BuildCategoryLists();

            if (_weaponTab != null)
                _weaponTab.onClick.AddListener(() => ShowCategory(_weapons, _weaponTab));
            if (_equipmentTab != null)
                _equipmentTab.onClick.AddListener(() => ShowCategory(_equipments, _equipmentTab));
            if (_foodTab != null)
                _foodTab.onClick.AddListener(() => ShowCategory(_foods, _foodTab));

            ShowCategory(_foods, _foodTab);
        }

        private void OnDestroy()
        {
            if (_weaponTab != null)
                _weaponTab.onClick.RemoveAllListeners();
            if (_equipmentTab != null)
                _equipmentTab.onClick.RemoveAllListeners();
            if (_foodTab != null)
                _foodTab.onClick.RemoveAllListeners();
        }

        private void BuildCategoryLists()
        {
            _weapons = new List<ItemData>();
            _equipments = new List<ItemData>();
            _foods = new List<ItemData>();

            AddItemById(_equipments, 2001);
            AddItemById(_foods, 3001);
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
            UpdateTabColor(_weaponTab, activeTab == _weaponTab);
            UpdateTabColor(_equipmentTab, activeTab == _equipmentTab);
            UpdateTabColor(_foodTab, activeTab == _foodTab);

            RefreshSlots(items);

            if (items != null && items.Count > 0)
                ShowDetail(items[0]);
            else
                ClearDetail();
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
            var image = tab.GetComponent<Image>();
            if (image == null)
                return;
            var color = isActive ? ActiveColor : InactiveColor;
            image.color = color;
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
            if (_detailIcon != null)
            {
                _detailIcon.sprite = item.icon;
                _detailIcon.color = Color.white;
            }
            if (_detailName != null)
                _detailName.text = item.itemName;
            if (_detailCategory != null)
                _detailCategory.text = item.category;
            if (_detailDescription != null)
                _detailDescription.text = item.description;
            if (_detailEffect != null)
                _detailEffect.text = item.effect;
        }

        private void ClearDetail()
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = null;
                _detailIcon.color = new Color(0, 0, 0, 0);
            }
            if (_detailName != null)
                _detailName.text = "";
            if (_detailCategory != null)
                _detailCategory.text = "";
            if (_detailDescription != null)
                _detailDescription.text = "";
            if (_detailEffect != null)
                _detailEffect.text = "";
        }
    }
}
