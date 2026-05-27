using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class InventoryUIController : MonoBehaviour
    {
        [Serializable]
        public struct ItemData
        {
            public Sprite icon;
            public string itemName;
            public string category;
            public string description;
            public string effect;
        }

        [SerializeField] private Button _weaponTab;
        [SerializeField] private Button _equipmentTab;
        [SerializeField] private Button _foodTab;
        [SerializeField] private Transform _slotsRoot;
        [SerializeField] private Image _detailIcon;
        [SerializeField] private Text _detailName;
        [SerializeField] private Text _detailCategory;
        [SerializeField] private Text _detailDescription;
        [SerializeField] private Text _detailEffect;
        [SerializeField] private Sprite _appleIcon;
        [SerializeField] private Sprite _clockIcon;

        private static readonly Color ActiveColor = new Color(0.55f, 0.7f, 0.95f, 1f);
        private static readonly Color InactiveColor = new Color(0.3f, 0.45f, 0.65f, 1f);

        private Image[] _slotImages;
        private ItemData[] _weaponItems;
        private ItemData[] _equipmentItems;
        private ItemData[] _foodItems;

        private void Awake()
        {
            _slotImages = CollectSlotImages();
            _weaponItems = Array.Empty<ItemData>();
            _equipmentItems = new[]
            {
                new ItemData
                {
                    icon = _clockIcon,
                    itemName = "懐中時計",
                    category = "装備品  ★",
                    description = "金色の縁に古びたローマ数字。\n時の流れを正確に刻み、持つ者に冷静さを与える。",
                    effect = "効果   攻撃速度 +5%",
                },
            };
            _foodItems = new[]
            {
                new ItemData
                {
                    icon = _appleIcon,
                    itemName = "りんご",
                    category = "食材  ★",
                    description = "瑞々しくて甘酸っぱい果実。\nひと口かじれば旅の疲れも和らぐ。",
                    effect = "効果   HP を 50 回復",
                },
            };

            if (_weaponTab != null) _weaponTab.onClick.AddListener(() => ShowCategory(_weaponItems, _weaponTab));
            if (_equipmentTab != null) _equipmentTab.onClick.AddListener(() => ShowCategory(_equipmentItems, _equipmentTab));
            if (_foodTab != null) _foodTab.onClick.AddListener(() => ShowCategory(_foodItems, _foodTab));

            ShowCategory(_foodItems, _foodTab);
        }

        private void OnDestroy()
        {
            if (_weaponTab != null) _weaponTab.onClick.RemoveAllListeners();
            if (_equipmentTab != null) _equipmentTab.onClick.RemoveAllListeners();
            if (_foodTab != null) _foodTab.onClick.RemoveAllListeners();
        }

        private Image[] CollectSlotImages()
        {
            if (_slotsRoot == null) return Array.Empty<Image>();
            var list = new List<Image>();
            foreach (Transform child in _slotsRoot)
            {
                var img = child.GetComponent<Image>();
                if (img != null) list.Add(img);
            }
            return list.ToArray();
        }

        private void ShowCategory(ItemData[] items, Button activeTab)
        {
            UpdateTabColor(_weaponTab, activeTab == _weaponTab);
            UpdateTabColor(_equipmentTab, activeTab == _equipmentTab);
            UpdateTabColor(_foodTab, activeTab == _foodTab);

            for (int i = 0; i < _slotImages.Length; i++)
            {
                if (i < items.Length && items[i].icon != null)
                {
                    _slotImages[i].sprite = items[i].icon;
                    _slotImages[i].color = Color.white;
                }
                else
                {
                    _slotImages[i].sprite = null;
                    _slotImages[i].color = new Color(0, 0, 0, 0);
                }
            }

            if (items.Length > 0) ShowDetail(items[0]);
            else ClearDetail();
        }

        private void UpdateTabColor(Button tab, bool isActive)
        {
            if (tab == null) return;
            var image = tab.GetComponent<Image>();
            if (image == null) return;
            var color = isActive ? ActiveColor : InactiveColor;
            image.color = color;
            var colors = tab.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Clamp01(color.r + 0.1f),
                Mathf.Clamp01(color.g + 0.1f),
                Mathf.Clamp01(color.b + 0.1f), color.a);
            tab.colors = colors;
        }

        private void ShowDetail(ItemData item)
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = item.icon;
                _detailIcon.color = Color.white;
            }
            if (_detailName != null) _detailName.text = item.itemName;
            if (_detailCategory != null) _detailCategory.text = item.category;
            if (_detailDescription != null) _detailDescription.text = item.description;
            if (_detailEffect != null) _detailEffect.text = item.effect;
        }

        private void ClearDetail()
        {
            if (_detailIcon != null)
            {
                _detailIcon.sprite = null;
                _detailIcon.color = new Color(0, 0, 0, 0);
            }
            if (_detailName != null) _detailName.text = "";
            if (_detailCategory != null) _detailCategory.text = "";
            if (_detailDescription != null) _detailDescription.text = "";
            if (_detailEffect != null) _detailEffect.text = "";
        }
    }
}
