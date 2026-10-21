using System.Collections;
using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>Presentation bindings for the inventory screen prefab.</summary>
    public sealed class InventoryScreenView : MonoBehaviour
    {
        [SerializeField]
        private InventoryView _inventory;

        [SerializeField]
        private TabGroup _tabs;

        [SerializeField]
        private GridLayoutGroup _grid;

        [SerializeField]
        private TMP_Text _quantity;

        [SerializeField]
        private TMP_Text _categoryLabel;

        [SerializeField]
        private TMP_Text _useLabel;

        [SerializeField]
        private Button _useButton;

        private float _lastWidth = -1f;
        private ItemStack _lastSelection;
        private int _lastCount = -1;
        private InventoryManager _manager;
        private Tween _cursorTween;
        private static readonly Color Ink = UiVisualTheme.Ink;
        private static readonly Color Gold = UiVisualTheme.Gold;

        private void OnEnable()
        {
            _inventory.SetReleaseSelectionOnOutsideClick(false);
            _tabs.OnTabDefinitionSelected += OnTabSelected;
            _useButton.onClick.AddListener(UseSelected);
            StartCoroutine(RefreshAfterStart());
        }

        private IEnumerator RefreshAfterStart()
        {
            yield return null;
            RefreshCounts();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _cursorTween?.Kill();
            _tabs.OnTabDefinitionSelected -= OnTabSelected;
            _useButton.onClick.RemoveListener(UseSelected);
            if (_manager != null)
                _manager.InventoryChanged -= RefreshCounts;
            _manager = null;
        }

        private void LateUpdate()
        {
            if (_manager != InventoryManager.Instance)
            {
                if (_manager != null)
                    _manager.InventoryChanged -= RefreshCounts;
                _manager = InventoryManager.Instance;
                if (_manager != null)
                    _manager.InventoryChanged += RefreshCounts;
                RefreshCounts();
            }

            float width = ((RectTransform)_grid.transform).rect.width;
            if (!Mathf.Approximately(width, _lastWidth) && width > 0f)
            {
                _lastWidth = width;
                int columns = Mathf.Max(1, _grid.constraintCount);
                float cellWidth = Mathf.Max(
                    1f,
                    (width - _grid.padding.horizontal - (columns - 1) * _grid.spacing.x) / columns
                );
                _grid.cellSize = Vector2.one * cellWidth;
            }

            var selected = _inventory.SelectedStack;
            int count = selected?.Count ?? 0;
            if (_lastSelection != selected || _lastCount != count)
            {
                _lastSelection = selected;
                _lastCount = count;
                _quantity.text = count > 0 ? $"{count} 個" : string.Empty;
                if (_categoryLabel != null)
                    _categoryLabel.text = selected?.Data?.category switch
                    {
                        ItemCategory.Food => "食べ物",
                        ItemCategory.Equipment => "装備",
                        ItemCategory.Important => "大事なもの",
                        _ => string.Empty,
                    };
            }
            bool canUse = count > 0 && selected?.Data is FoodData;
            _useButton.gameObject.SetActive(canUse);
            _useButton.interactable = canUse;
            if (_useLabel != null)
                _useLabel.color = canUse ? UiVisualTheme.ActionInk : UiVisualTheme.Muted;
        }

        private void OnTabSelected(int index, TabDefinition definition)
        {
            _cursorTween?.Kill();
            RefreshCounts();
        }

        private void RefreshCounts()
        {
            var items = InventoryManager.Instance?.GetAllItems();
            for (int i = 0; i < _tabs.ButtonCount; i++)
            {
                var button = _tabs.GetButton(i);
                var definition = _tabs.GetDefinitionForButtonIndex(i);
                if (button == null)
                    continue;
                if (button.Icon != null)
                    button.Icon.gameObject.SetActive(false);
                int count = 0;
                if (items != null)
                    foreach (var stack in items)
                        if (
                            stack?.Data != null
                            && (
                                definition is AllItemsTabDefinition
                                || definition is InventoryTabDefinition category
                                    && stack.Data.category == category.Category
                            )
                        )
                            count++;
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.text =
                        label.name == "Count"
                            ? count.ToString()
                            : definition?.Label ?? string.Empty;
                    label.color = i == _tabs.CurrentIndex ? Gold : Ink;
                }
                button.SetIconColor(i == _tabs.CurrentIndex ? Gold : Ink);
            }
        }

        private void UseSelected()
        {
            _cursorTween?.Kill();
            if (_inventory.SelectedStack?.Data is FoodData)
                InventoryManager.Instance?.TryUse(_inventory.SelectedStack);
        }

        public void FocusUseButton(ItemStack stack)
        {
            if (stack?.Data is not FoodData || stack.Count <= 0)
                return;
            _useButton.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            EventSystem.current?.SetSelectedGameObject(_useButton.gameObject);
            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var canvas = _useButton.GetComponentInParent<Canvas>();
            var camera =
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)_useButton.transform;
            Vector2 destination = RectTransformUtility.WorldToScreenPoint(
                camera,
                rect.TransformPoint(rect.rect.center)
            );
            Vector2 position = mouse.position.ReadValue();
            _cursorTween?.Kill();
            _cursorTween = DOTween
                .To(
                    () => position,
                    point =>
                    {
                        position = point;
                        mouse.WarpCursorPosition(point);
                    },
                    destination,
                    .28f
                )
                .SetEase(Ease.InOutCubic)
                .SetUpdate(true)
                .SetTarget(this);
        }
    }
}
