using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// プレハブに配置済みのクラフト画面へ、選択中の素材と完成予想を反映する。
    /// レイアウトやUIオブジェクトの実行時生成は行わない。
    /// </summary>
    public sealed class CraftUiLayout : MonoBehaviour
    {
        [Header("完成予想")]
        [SerializeField]
        private Image _predictionIcon;

        [SerializeField]
        private TMP_Text _predictionHint;

        [SerializeField]
        private TMP_Text _predictionName;

        [Header("素材詳細")]
        [SerializeField]
        private Image _detailsIcon;

        [SerializeField]
        private TMP_Text _detailsName;

        [SerializeField]
        private TMP_Text _detailsCount;

        [SerializeField]
        private TMP_Text _detailsDescription;

        [SerializeField]
        private TMP_Text _detailsEffect;

        private CraftPanelController _controller;
        private InventoryView _inventory;
        private ItemStack _lastDetailsStack;
        private bool _detailsInitialized;
        private bool _isSubscribed;

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RefreshDetails(_inventory != null ? _inventory.SelectedStack : null);
            RefreshPrediction(_controller != null ? _controller.PreviewedCraftedItem : null);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void ResolveReferences()
        {
            _controller = GetComponent<CraftPanelController>();
            _inventory = GetComponentInChildren<InventoryView>(true);
        }

        private void Subscribe()
        {
            if (_isSubscribed)
                return;

            if (_inventory != null)
                _inventory.OnSlotClicked += RefreshDetails;
            if (_controller != null)
                _controller.PreviewChanged += RefreshPrediction;

            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            if (_inventory != null)
                _inventory.OnSlotClicked -= RefreshDetails;
            if (_controller != null)
                _controller.PreviewChanged -= RefreshPrediction;

            _isSubscribed = false;
        }

        private void RefreshDetails(ItemStack stack)
        {
            if (_detailsInitialized && ReferenceEquals(stack, _lastDetailsStack))
                return;

            _detailsInitialized = true;
            _lastDetailsStack = stack;
            bool hasItem = stack?.Data != null;

            if (_detailsIcon != null)
            {
                _detailsIcon.sprite = hasItem ? stack.Data.icon : null;
                _detailsIcon.color = hasItem && stack.Data.icon != null ? Color.white : Color.clear;
            }

            SetText(_detailsName, hasItem ? stack.Data.itemName : "素材を選択してください");
            SetText(_detailsCount, hasItem ? $"x{stack.Count}" : string.Empty);
            SetText(_detailsDescription, hasItem ? stack.Data.description : string.Empty);
            SetText(
                _detailsEffect,
                hasItem ? ItemStatTextFormatter.BuildStatsText(stack) : string.Empty
            );
        }

        private void RefreshPrediction(ItemData item)
        {
            bool hasPreview = item != null;

            if (_predictionIcon != null)
            {
                _predictionIcon.sprite = hasPreview ? item.icon : null;
                _predictionIcon.color = hasPreview && item.icon != null ? Color.white : Color.clear;
            }

            SetText(_predictionName, hasPreview ? item.itemName : "？？？");
            SetText(_predictionHint, hasPreview ? "調合可能な完成品" : "素材を2つ選択してください");
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
