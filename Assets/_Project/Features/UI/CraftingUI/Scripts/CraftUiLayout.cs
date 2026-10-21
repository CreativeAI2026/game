using System.Collections;
using System.Globalization;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>Binds the authored CraftPanel prefab without generating UI at runtime.</summary>
    public sealed class CraftUiLayout : MonoBehaviour
    {
        [SerializeField]
        private Image _predictionIcon;

        [SerializeField]
        private TMP_Text _predictionHint;

        [SerializeField]
        private TMP_Text _predictionName;

        [SerializeField]
        private Button _quickButton;

        [SerializeField]
        private Button _equipmentButton;

        [SerializeField]
        private TMP_Text _quickCount;

        [SerializeField]
        private TMP_Text _equipmentCount;

        [SerializeField]
        private GameObject _equipmentParameters;

        [SerializeField]
        private GameObject _quickParameters;

        [SerializeField]
        private TMP_Text[] _equipmentValues;

        [SerializeField]
        private TMP_Text _healValue;

        private CraftPanelController _controller;
        private InventoryManager _manager;
        private static readonly StatType[] EquipmentTypes =
        {
            StatType.AttackPct,
            StatType.DefensePct,
            StatType.CritRate,
            StatType.CritDamage,
            StatType.MaxHpPct,
        };

        private void OnEnable()
        {
            _controller = GetComponent<CraftPanelController>();
            _controller.PreviewChanged += RefreshPrediction;
            _controller.ResultShown += RefreshResult;
            _quickButton.onClick.AddListener(SelectQuick);
            _equipmentButton.onClick.AddListener(SelectEquipment);
            RefreshPrediction(_controller.PreviewedCraftedItem);
            StartCoroutine(SubscribeWhenReady());
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (InventoryManager.Instance == null)
                yield return null;
            _manager = InventoryManager.Instance;
            _manager.InventoryChanged += RefreshCounts;
            RefreshCounts();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (_manager != null)
                _manager.InventoryChanged -= RefreshCounts;
            _manager = null;
            _controller.PreviewChanged -= RefreshPrediction;
            _controller.ResultShown -= RefreshResult;
            _quickButton.onClick.RemoveListener(SelectQuick);
            _equipmentButton.onClick.RemoveListener(SelectEquipment);
        }

        private void SelectQuick() => _controller.SelectMaterialCategory(ItemCategory.Food);

        private void SelectEquipment() =>
            _controller.SelectMaterialCategory(ItemCategory.Equipment);

        private void Update()
        {
            bool interactable = !_controller.IsCraftFlowRunning;
            _quickButton.interactable = interactable;
            _equipmentButton.interactable = interactable;
        }

        private void RefreshCounts()
        {
            int quick = 0,
                equipment = 0;
            if (_manager != null)
                foreach (var stack in _manager.GetAllItems())
                {
                    if (stack?.Data == null || stack.Count <= 0)
                        continue;
                    // Same base-material filter as this inventory prefab.
                    string id = Mathf.Abs(stack.Data.id).ToString(CultureInfo.InvariantCulture);
                    if (id.Length < 2 || id[1] != '0')
                        continue;
                    if (stack.Data.category == ItemCategory.Food)
                        quick++;
                    if (stack.Data.category == ItemCategory.Equipment)
                        equipment++;
                }
            _quickCount.text = quick.ToString(CultureInfo.InvariantCulture);
            _equipmentCount.text = equipment.ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshPrediction(ItemData item)
        {
            RefreshHeader(item);
            bool quick = _controller.MaterialCategory == ItemCategory.Food;
            _quickParameters.SetActive(quick);
            _equipmentParameters.SetActive(!quick);
            SetTabColors(quick);
            for (int i = 0; i < _equipmentValues.Length && i < EquipmentTypes.Length; i++)
                _equipmentValues[i].text =
                    item == null
                        ? "—"
                        : CraftParameterTextFormatter.Preview(
                            EquipmentTypes[i],
                            _controller.FirstMaterial,
                            _controller.SecondMaterial
                        );
            _healValue.text = item is FoodData food
                ? CraftParameterTextFormatter.Recovery(food)
                : "—";
        }

        private void RefreshResult(ItemStack stack)
        {
            RefreshHeader(stack?.Data);
            for (int i = 0; i < _equipmentValues.Length && i < EquipmentTypes.Length; i++)
                _equipmentValues[i].text = CraftParameterTextFormatter.Actual(
                    EquipmentTypes[i],
                    stack
                );
            _healValue.text = stack?.Data is FoodData food
                ? CraftParameterTextFormatter.Recovery(food)
                : "—";
        }

        private void RefreshHeader(ItemData item)
        {
            _predictionIcon.sprite = item != null ? item.icon : null;
            _predictionIcon.color = item != null && item.icon != null ? Color.white : Color.clear;
            _predictionName.text = item != null ? item.itemName : "素材を2つ選択";
            if (_predictionHint != null)
                _predictionHint.text =
                    item == null
                        ? "素材をダブルクリックして選択"
                        : "完成予想（装備の数値は抽選範囲）";
        }

        private void SetTabColors(bool quick)
        {
            var active = UiVisualTheme.SelectedTab;
            var inactive = UiVisualTheme.Tab;
            _quickButton.targetGraphic.color = quick ? active : inactive;
            _equipmentButton.targetGraphic.color = quick ? inactive : active;
            foreach (var text in _quickButton.GetComponentsInChildren<TMP_Text>(true))
                text.color = quick ? UiVisualTheme.Gold : UiVisualTheme.Ink;
            foreach (var text in _equipmentButton.GetComponentsInChildren<TMP_Text>(true))
                text.color = quick ? UiVisualTheme.Ink : UiVisualTheme.Gold;
        }
    }
}
