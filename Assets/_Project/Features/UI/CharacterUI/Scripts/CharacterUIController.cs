using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public class CharacterUIController : MonoBehaviour
    {
        [Serializable]
        public struct EquipmentData
        {
            public Sprite icon;
            public string itemName;
            public string category;
            public string stats;
            public string passiveTitle;
            public string passiveDesc;
        }

        [Header("Tabs")]
        [SerializeField]
        private Button _statsTab;

        [SerializeField]
        private Button _weaponTab;

        [SerializeField]
        private Button _equipmentTab;

        [SerializeField]
        private Text _statsTabLabel;

        [SerializeField]
        private Text _weaponTabLabel;

        [SerializeField]
        private Text _equipmentTabLabel;

        [Header("Views")]
        [SerializeField]
        private GameObject _statsView;

        [SerializeField]
        private GameObject _weaponView;

        [SerializeField]
        private GameObject _equipmentView;

        [Header("Equipment Slots (3)")]
        [SerializeField]
        private Button _equipmentSlot1;

        [SerializeField]
        private Button _equipmentSlot2;

        [SerializeField]
        private Button _equipmentSlot3;

        [SerializeField]
        private Image _equipmentSlot1Icon;

        [SerializeField]
        private Image _equipmentSlot2Icon;

        [SerializeField]
        private Image _equipmentSlot3Icon;

        [SerializeField]
        private Text _equipmentSlot1Empty;

        [SerializeField]
        private Text _equipmentSlot2Empty;

        [SerializeField]
        private Text _equipmentSlot3Empty;

        [SerializeField]
        private Image _equipmentSlot1Frame;

        [SerializeField]
        private Image _equipmentSlot2Frame;

        [SerializeField]
        private Image _equipmentSlot3Frame;

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

        [Header("Equipment Sources")]
        [SerializeField]
        private Sprite _clockIcon;

        private static readonly Color ActiveLabelColor = new Color(0.55f, 0.75f, 1f, 1f);
        private static readonly Color InactiveLabelColor = new Color(0.75f, 0.75f, 0.8f, 1f);
        private static readonly Color SlotFrameSelected = new Color(0.95f, 0.8f, 0.4f, 0.6f);
        private static readonly Color SlotFrameNormal = new Color(1f, 1f, 1f, 0.15f);

        private EquipmentData[] _equipmentItems;
        private Button[] _slotButtons;
        private Image[] _slotIcons;
        private Text[] _slotEmpties;
        private Image[] _slotFrames;

        private void Awake()
        {
            BuildEquipmentData();

            _slotButtons = new[] { _equipmentSlot1, _equipmentSlot2, _equipmentSlot3 };
            _slotIcons = new[] { _equipmentSlot1Icon, _equipmentSlot2Icon, _equipmentSlot3Icon };
            _slotEmpties = new[]
            {
                _equipmentSlot1Empty,
                _equipmentSlot2Empty,
                _equipmentSlot3Empty,
            };
            _slotFrames = new[]
            {
                _equipmentSlot1Frame,
                _equipmentSlot2Frame,
                _equipmentSlot3Frame,
            };

            if (_statsTab != null)
                _statsTab.onClick.AddListener(ShowStats);
            if (_weaponTab != null)
                _weaponTab.onClick.AddListener(ShowWeapon);
            if (_equipmentTab != null)
                _equipmentTab.onClick.AddListener(ShowEquipment);

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int slotIndex = i;
                if (_slotButtons[i] != null)
                    _slotButtons[i].onClick.AddListener(() => SelectEquipmentSlot(slotIndex));
                RenderSlot(i);
            }

            SelectEquipmentSlot(0);
            ShowStats();
        }

        private void OnDestroy()
        {
            if (_statsTab != null)
                _statsTab.onClick.RemoveAllListeners();
            if (_weaponTab != null)
                _weaponTab.onClick.RemoveAllListeners();
            if (_equipmentTab != null)
                _equipmentTab.onClick.RemoveAllListeners();
            if (_slotButtons != null)
            {
                foreach (var b in _slotButtons)
                {
                    if (b != null)
                        b.onClick.RemoveAllListeners();
                }
            }
        }

        private void BuildEquipmentData()
        {
            _equipmentItems = new EquipmentData[3];
            _equipmentItems[0] = new EquipmentData
            {
                icon = _clockIcon,
                itemName = "懐中時計",
                category = "装備品  ★",
                stats = "攻撃速度 +5%",
                passiveTitle = "パッシブ「時の支配」",
                passiveDesc = "攻撃時、確率で敵の動きを\nわずかに遅らせる。",
            };
            // _equipmentItems[1], [2] は空（default の icon == null で判定）
        }

        private void ShowStats() => Switch(_statsView, _statsTabLabel);

        private void ShowWeapon() => Switch(_weaponView, _weaponTabLabel);

        private void ShowEquipment() => Switch(_equipmentView, _equipmentTabLabel);

        private void Switch(GameObject activeView, Text activeLabel)
        {
            if (_statsView != null)
                _statsView.SetActive(activeView == _statsView);
            if (_weaponView != null)
                _weaponView.SetActive(activeView == _weaponView);
            if (_equipmentView != null)
                _equipmentView.SetActive(activeView == _equipmentView);

            SetLabelColor(_statsTabLabel, activeLabel == _statsTabLabel);
            SetLabelColor(_weaponTabLabel, activeLabel == _weaponTabLabel);
            SetLabelColor(_equipmentTabLabel, activeLabel == _equipmentTabLabel);
        }

        private static void SetLabelColor(Text label, bool isActive)
        {
            if (label == null)
                return;
            label.color = isActive ? ActiveLabelColor : InactiveLabelColor;
        }

        private void RenderSlot(int i)
        {
            var item = _equipmentItems[i];
            bool hasItem = item.icon != null;
            if (_slotIcons[i] != null)
            {
                _slotIcons[i].sprite = hasItem ? item.icon : null;
                _slotIcons[i].color = hasItem ? Color.white : new Color(0, 0, 0, 0);
            }
            if (_slotEmpties[i] != null)
                _slotEmpties[i].gameObject.SetActive(!hasItem);
        }

        private void SelectEquipmentSlot(int i)
        {
            for (int j = 0; j < _slotFrames.Length; j++)
            {
                if (_slotFrames[j] != null)
                    _slotFrames[j].color = (j == i) ? SlotFrameSelected : SlotFrameNormal;
            }

            var item = _equipmentItems[i];
            bool hasItem = item.icon != null;

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
                _equipmentDetailStats.text = hasItem ? item.stats : "";
            if (_equipmentDetailPassiveTitle != null)
                _equipmentDetailPassiveTitle.text = hasItem ? item.passiveTitle : "";
            if (_equipmentDetailPassiveDesc != null)
                _equipmentDetailPassiveDesc.text = hasItem ? item.passiveDesc : "";
        }
    }
}
