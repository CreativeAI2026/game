using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public class CharacterUIController : MonoBehaviour
    {
        public enum TabIndex
        {
            Stats,
            Weapon,
            Equipment,
        }

        [Serializable]
        public struct TabData
        {
            public Button button;
            public Text label;
            public GameObject view;
            public TabHighlight highlight;
        }

        [Header("Tabs"), SerializeField]
        private Transform _categoryList;

        [SerializeField]
        private Transform _viewContainer;

        [Header("Equipment Slots"), SerializeField]
        private Transform _equipmentSlotsContainer;

        [Header("Equipment Detail"), SerializeField]
        private Image _equipmentDetailIcon;

        [SerializeField]
        private Text _equipmentDetailName;

        [SerializeField]
        private Text _equipmentDetailCategory;

        [SerializeField]
        private Text _equipmentDetailStats;

        [SerializeField]
        private Text _equipmentDetailPassiveTitle;

        [SerializeField]
        private Text _equipmentDetailPassiveDesc;

        [Header("Typing Effect")]
        [SerializeField]
        private float _typingDuration = 0.5f;

        private static readonly Color ActiveLabelColor = new Color(0.55f, 0.75f, 1f, 1f);
        private static readonly Color InactiveLabelColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        private static readonly Color SlotFrameSelected = new Color(0.95f, 0.8f, 0.4f, 0.6f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.15f);

        private List<TabData> _tabs;
        private List<EquipmentSlot> _slots;
        private int _currentSlotIndex = 0;

        private void Start()
        {
            // タブ初期化
            _tabs = new List<TabData>();
            for (int i = 0; i < _categoryList.childCount; i++)
            {
                var child = _categoryList.GetChild(i);
                _tabs.Add(
                    new TabData
                    {
                        button = child.GetComponent<Button>(),
                        label = child.GetComponentInChildren<Text>(),
                        view = _viewContainer.GetChild(i).gameObject,
                        highlight = child.GetComponent<TabHighlight>(),
                    }
                );
            }

            foreach (TabIndex tab in Enum.GetValues(typeof(TabIndex)))
            {
                //TabIndex captured = tab;
                _tabs[(int)tab].button.onClick.AddListener(() => SelectTab(tab));
            }

            // スロット初期化
            _slots = new();
            for (int i = 0; i < _equipmentSlotsContainer.childCount; i++)
            {
                var slot = _equipmentSlotsContainer.GetChild(i).GetComponent<EquipmentSlot>();
                slot.Init(); // 追加
                _slots.Add(slot);
            }

            _slots[0].Item = ItemDB.Instance.GetItemById(2001);
            _slots[1].Item = ItemDB.Instance.GetItemById(3001);

            for (int slotIndex = 0; slotIndex < _slots.Count; slotIndex++)
            {
                var btn = _slots[slotIndex].GetComponent<Button>();
                if (btn != null)
                {
                    int captured = slotIndex;
                    btn.onClick.AddListener(() => SelectEquipmentSlot(captured));
                }
            }

            SelectEquipmentSlot(0);
            SelectTab(TabIndex.Stats);
        }

        private void OnDestroy()
        {
            foreach (var tab in _tabs)
                if (tab.button != null)
                    tab.button.onClick.RemoveAllListeners();

            if (_slots != null)
                foreach (var slot in _slots)
                    if (slot.Button != null)
                        slot.Button.onClick.RemoveAllListeners();
        }

        private void SelectTab(TabIndex tab)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool isActive = i == (int)tab;
                _tabs[i].view.SetActive(isActive);
                _tabs[i].highlight?.SetActive(isActive);
            }

            if (tab == TabIndex.Equipment)
                SelectEquipmentSlot(_currentSlotIndex); // 現在選択中のスロットで再表示
        }

        private void SelectEquipmentSlot(int i)
        {
            _currentSlotIndex = i;
            for (int j = 0; j < _slots.Count; j++)
                _slots[j].SetFrameColor(j == i ? SlotFrameSelected : SlotFrameNormal);

            var item = _slots[i].Item;
            bool hasItem = item != null;

            if (_equipmentDetailIcon != null)
            {
                _equipmentDetailIcon.sprite = hasItem ? item.icon : null;
                _equipmentDetailIcon.color = hasItem ? Color.white : Color.clear;
            }

            TypeText(_equipmentDetailName, hasItem ? item.itemName : "（未装備）");
            TypeText(_equipmentDetailCategory, hasItem ? item.category : "");
            TypeText(_equipmentDetailStats, hasItem ? item.effect : "");
            TypeText(_equipmentDetailPassiveTitle, hasItem ? item.effect : "");
            TypeText(_equipmentDetailPassiveDesc, hasItem ? item.description : "");
        }

        private void TypeText(Text target, string text)
        {
            if (target == null)
                return;
            target.text = "";
            int totalChars = text.Length;
            DOTween
                .To(
                    () => 0f,
                    x => target.text = text.Substring(0, Mathf.RoundToInt(x)),
                    (float)totalChars,
                    _typingDuration
                )
                .SetEase(Ease.Linear);
        }
    }
}
