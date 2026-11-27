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
    /// <summary>
    /// インベントリ画面。持ち物の一覧とタブ(所持数つき)を出し、選んだ食材をその場で使わせる。
    /// </summary>
    public sealed class InventoryPanel : UIPanel
    {
        [SerializeField]
        private ItemListView _inventory;

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
        private InventoryManager _manager;
        private Tween _cursorTween;

        private void OnEnable()
        {
            _inventory.OnSlotDoubleClicked += OnSlotDoubleClicked;
            _inventory.OnSlotSubmitted += OnSlotSubmitted;
            _inventory.SelectionChanged += OnSelectionChanged;
            _tabs.OnTabDefinitionSelected += OnTabSelected;
            _useButton.onClick.AddListener(UseSelected);

            // 所持品は UI より先に常駐生成される
            _manager = InventoryManager.Instance;
            if (_manager != null)
                _manager.Changed += OnInventoryChanged;
            StartCoroutine(RefreshCountsNextFrame());
            RefreshSelectionInfo();
        }

        // タブのボタンがそろうのを1フレーム待ってから所持数を書く
        private IEnumerator RefreshCountsNextFrame()
        {
            yield return null;
            RefreshCounts();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            _cursorTween?.Kill();
            _inventory.OnSlotDoubleClicked -= OnSlotDoubleClicked;
            _inventory.OnSlotSubmitted -= OnSlotSubmitted;
            _inventory.SelectionChanged -= OnSelectionChanged;
            _tabs.OnTabDefinitionSelected -= OnTabSelected;
            _useButton.onClick.RemoveListener(UseSelected);
            if (_manager != null)
                _manager.Changed -= OnInventoryChanged;
            _manager = null;
        }

        private void LateUpdate()
        {
            if (_tabs.ButtonCount > 1)
            {
                if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.TabPrev))
                    _tabs.SelectTab(
                        (_tabs.CurrentIndex - 1 + _tabs.ButtonCount) % _tabs.ButtonCount
                    );
                else if (UIInputActionMap.WasPressedThisFrame(UIInputActionMap.TabNext))
                    _tabs.SelectTab((_tabs.CurrentIndex + 1) % _tabs.ButtonCount);
            }

            FitGridCellsToWidth();
        }

        private void OnInventoryChanged()
        {
            _inventory.RefreshCurrentTab();
            RefreshCounts();
            RefreshSelectionInfo();
        }

        private void OnSelectionChanged(ItemStack stack) => RefreshSelectionInfo();

        private void OnTabSelected(int index, TabDefinition definition)
        {
            _cursorTween?.Kill();
            RefreshCounts();
        }

        private void OnSlotSubmitted(ItemStack stack) => UseSelected();

        private void OnSlotDoubleClicked(ItemStack stack) => FocusUseButton(stack);

        // 一覧の幅に合わせて、列数を保ったまま正方形のマスの大きさを決め直す
        private void FitGridCellsToWidth()
        {
            float width = ((RectTransform)_grid.transform).rect.width;
            if (Mathf.Approximately(width, _lastWidth) || width <= 0f)
                return;
            _lastWidth = width;
            int columns = Mathf.Max(1, _grid.constraintCount);
            float cellWidth = Mathf.Max(
                1f,
                (width - _grid.padding.horizontal - (columns - 1) * _grid.spacing.x) / columns
            );
            _grid.cellSize = Vector2.one * cellWidth;
        }

        // 選んでいるアイテムの個数・種類と、「使う」ボタンを出すかを更新する
        private void RefreshSelectionInfo()
        {
            var selected = _inventory.SelectedStack;
            int count = selected?.Count ?? 0;
            _quantity.text = count > 0 ? $"{count} 個" : string.Empty;
            ItemData data = selected?.Data;
            _categoryLabel.text = (data != null ? data.category : (ItemCategory?)null) switch
            {
                ItemCategory.Food => "食べ物",
                ItemCategory.Equipment => "装備",
                ItemCategory.Important => "大事なもの",
                _ => string.Empty,
            };
            bool canUse = count > 0 && selected?.Data is FoodData;
            _useButton.gameObject.SetActive(canUse);
            _useButton.interactable = canUse;
            _useLabel.color = canUse ? UIVisualTheme.ActionInk : UIVisualTheme.Muted;
        }

        // 各タブに所持数とラベルを書き、選んでいるタブの色を変える
        private void RefreshCounts()
        {
            var items = _manager != null ? _manager.GetAllItems() : null;
            for (int i = 0; i < _tabs.ButtonCount; i++)
            {
                var button = _tabs.GetButton(i);
                if (button == null)
                    continue;
                var definition = _tabs.GetDefinitionForButtonIndex(i);
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
                bool selected = i == _tabs.CurrentIndex;
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name == "Count")
                    {
                        label.SetText("{0}", count);
                        continue;
                    }
                    label.text =
                        definition != null ? definition.Label ?? string.Empty : string.Empty;
                    label.color = selected ? Color.black : Color.white;
                }
                button.SetIconColor(selected ? UIVisualTheme.Gold : UIVisualTheme.Ink);
            }
        }

        private void UseSelected()
        {
            _cursorTween?.Kill();
            var player = PlayerRegistry.Current;
            if (player != null && player.TryGetComponent(out PlayerStatus status))
                FoodUse.TryEat(status, _inventory.SelectedStack);
        }

        // 食材をダブルクリックしたら、「使う」ボタンを選んでマウスカーソルもそこへ動かす
        private void FocusUseButton(ItemStack stack)
        {
            if (stack?.Data is not FoodData || stack.Count <= 0)
                return;
            _useButton.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_useButton.gameObject);
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

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_inventory == null)
                _inventory = GetComponentInChildren<ItemListView>(true);
            if (_tabs == null)
                _tabs = GetComponentInChildren<TabGroup>(true);
        }
#endif
    }
}
