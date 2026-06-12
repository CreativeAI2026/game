using System;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public class CharacterUIController : MonoBehaviour
    {
        public enum TabIndex
        {
            Stats = 0,
            Weapon = 1,
            Equipment = 2,
        }

        [Serializable]
        public struct TabData
        {
            public Button button;
            public Text label;
            public GameObject view;
        }

        [Header("Tabs")]
        [SerializeField]
        private Transform _categoryList;

        [SerializeField]
        private Transform _viewContainer;

        [Header("Equipment Slots")]
        [SerializeField]
        private Transform _equipmentSlotsContainer;

        [Header("Equipment Detail")]
        [SerializeField]
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

        private static readonly Color ActiveLabelColor = new Color(0.55f, 0.75f, 1f, 1f);
        private static readonly Color InactiveLabelColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        private static readonly Color SlotFrameSelected = new Color(0.95f, 0.8f, 0.4f, 0.6f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.15f);

        private TabData[] _tabs;
        private EquipmentSlot[] _slots;

        private void Start()
        {
            // タブ初期化
            _tabs = new TabData[_categoryList.childCount];
            for (int i = 0; i < _categoryList.childCount; i++)
            {
                var child = _categoryList.GetChild(i);
                _tabs[i] = new TabData
                {
                    button = child.GetComponent<Button>(),
                    label = child.GetComponentInChildren<Text>(),
                    view = _viewContainer.GetChild(i).gameObject,
                };
            }

            foreach (TabIndex tab in Enum.GetValues(typeof(TabIndex)))
            {
                TabIndex captured = tab;
                _tabs[(int)tab].button.onClick.AddListener(() => SelectTab(captured));
            }

            // スロット初期化
            _slots = new EquipmentSlot[_equipmentSlotsContainer.childCount];
            for (int i = 0; i < _equipmentSlotsContainer.childCount; i++)
                _slots[i] = _equipmentSlotsContainer.GetChild(i).GetComponent<EquipmentSlot>();

            _slots[0].Item = ItemDB.Instance.GetItemById(2001);

            for (int i = 0; i < _slots.Length; i++)
            {
                int slotIndex = i;
                if (_slots[i].Button != null)
                    _slots[i].Button.onClick.AddListener(() => SelectEquipmentSlot(slotIndex));
            }

            SelectEquipmentSlot(0);
            SelectTab(TabIndex.Stats);

            for (int i = 0; i < _slots.Length; i++)
            {
                Debug.Log(
                    $"Item: {_slots[i].Item?.name ?? "NULL"}, Icon: {_slots[i].Item?.icon ?? null}"
                );
            }
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
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].view.SetActive(i == (int)tab);
                SetLabelColor(_tabs[i].label, i == (int)tab);
            }
        }

        private static void SetLabelColor(Text label, bool isActive)
        {
            if (label == null)
                return;
            label.color = isActive ? ActiveLabelColor : InactiveLabelColor;
        }

        private void SelectEquipmentSlot(int i)
        {
            for (int j = 0; j < _slots.Length; j++)
                _slots[j].SetFrameColor(j == i ? SlotFrameSelected : SlotFrameNormal);

            var item = _slots[i].Item;
            bool hasItem = item != null;

            if (_equipmentDetailIcon != null)
            {
                _equipmentDetailIcon.sprite = hasItem ? item.icon : null;
                _equipmentDetailIcon.color = hasItem ? Color.white : new Color(0, 0, 0, 0);
            }
            if (_equipmentDetailName != null)
                _equipmentDetailName.text = hasItem ? item.itemName : "（未装備）";
            if (_equipmentDetailCategory != null)
                _equipmentDetailCategory.text = hasItem ? item.category : "";
            if (_equipmentDetailStats != null)
                _equipmentDetailStats.text = hasItem ? item.effect : "";
            if (_equipmentDetailPassiveTitle != null)
                _equipmentDetailPassiveTitle.text = hasItem ? item.effect : "";
            if (_equipmentDetailPassiveDesc != null)
                _equipmentDetailPassiveDesc.text = hasItem ? item.description : "";
        }
    }
}
