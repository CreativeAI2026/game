using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI
{
    public sealed partial class RevolverTabGroup : MonoBehaviour, IMoveHandler, ISubmitHandler
    {
        [Header("Data")]
        [SerializeField]
        private List<RevolverTabEntry> _entries = new();

        [Header("Build")]
        [SerializeField]
        private RevolverTabItemView _itemPrefab;

        [SerializeField]
        private RectTransform _itemRoot;

        [SerializeField, Min(0)]
        private int _initialIndex;

        [Header("Layout")]
        [SerializeField]
        private RevolverTabLayoutSettings _layout = new();

        [Header("Animation")]
        [SerializeField, Min(0f)]
        private float _moveDuration = 0.25f;

        [SerializeField]
        private Ease _ease = Ease.OutCubic;

        [Header("Interaction")]
        [SerializeField]
        private bool _loop = true;

        [SerializeField]
        private bool _clickSelect = true;

        [SerializeField]
        private bool _submitOnSelectedClick = true;

        private readonly List<RevolverTabItemView> _items = new();
        private Tween _selectionTween;
        private float _selectionPosition;
        private int _selectedIndex = -1;
        private int _animationVersion;
        private bool _built;
        private bool _interactionEnabled = true;

        public int SelectedIndex => _selectedIndex;
        public int CurrentIndex => _selectedIndex;
        public int EntryCount => _entries?.Count ?? 0;
        public int ItemCount => _items.Count;
        public bool IsAnimating => _selectionTween != null && _selectionTween.IsActive();
        public TabDefinition CurrentDefinition => GetDefinition(_selectedIndex);
        public GameObject CurrentView => GetView(_selectedIndex);

        public event Action<int, TabDefinition, GameObject> SelectionChanged;
        public event Action<int, TabDefinition, GameObject> Submitted;

        private void Start()
        {
            if (!_built)
                Build();
        }

        private void OnEnable()
        {
            if (_built)
            {
                RefreshLayout();
                FocusSelectedItem();
            }
        }

        private void OnDisable()
        {
            KillSelectionTween();
            _selectionPosition = _selectedIndex;
        }

        private void OnDestroy()
        {
            KillSelectionTween();
            ClearGeneratedItems();
        }

        public TabDefinition GetDefinition(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
                return null;
            return _entries[index]?.Definition;
        }

        public GameObject GetView(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Count)
                return null;
            return _entries[index]?.View;
        }

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            RefreshLayout();
        }

        public void Select(int index, bool immediate = false)
        {
            if (!_built || IsAnimating || EntryCount == 0)
                return;

            int targetIndex = ResolveTargetIndex(index);
            if (targetIndex < 0 || targetIndex == _selectedIndex)
            {
                RefreshLayout();
                return;
            }

            if (immediate || _moveDuration <= 0f || !isActiveAndEnabled)
            {
                CompleteSelection(targetIndex);
                return;
            }

            AnimateSelection(targetIndex);
        }

        public void SelectNext()
        {
            if (!_interactionEnabled || IsAnimating || _selectedIndex < 0)
                return;
            Select(_selectedIndex + 1);
        }

        public void SelectPrevious()
        {
            if (!_interactionEnabled || IsAnimating || _selectedIndex < 0)
                return;
            Select(_selectedIndex - 1);
        }

        public void SubmitSelected()
        {
            if (!_interactionEnabled || IsAnimating || _selectedIndex < 0)
                return;

            Submitted?.Invoke(_selectedIndex, CurrentDefinition, CurrentView);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (eventData == null || !_interactionEnabled || IsAnimating)
                return;

            if (
                !RevolverTabNavigationUtility.TryResolveNavigationStep(
                    _layout.Placement,
                    _layout.ReverseOrder,
                    eventData.moveDir,
                    out int step
                )
            )
                return;

            if (step > 0)
                SelectNext();
            else
                SelectPrevious();
            eventData.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            SubmitSelected();
        }

        private int ResolveTargetIndex(int index)
        {
            if (_loop)
                return RevolverTabIndexUtility.WrapIndex(index, EntryCount);
            return index >= 0 && index < EntryCount ? index : -1;
        }

        private void CompleteSelection(int targetIndex)
        {
            KillSelectionTween();
            int normalizedIndex = RevolverTabIndexUtility.WrapIndex(targetIndex, EntryCount);
            if (normalizedIndex < 0)
                return;

            bool changed = normalizedIndex != _selectedIndex;
            _selectedIndex = normalizedIndex;
            _selectionPosition = normalizedIndex;
            ApplySelectedView();
            RefreshLayout();

            if (changed)
                SelectionChanged?.Invoke(_selectedIndex, CurrentDefinition, CurrentView);
            FocusSelectedItem();
        }

        private void HandleItemClicked(int dataIndex)
        {
            if (!_interactionEnabled || IsAnimating)
                return;

            FocusItem(dataIndex);

            if (dataIndex == _selectedIndex)
            {
                if (_submitOnSelectedClick)
                    SubmitSelected();
                return;
            }

            if (_clickSelect)
                Select(dataIndex);
        }

        private void FocusSelectedItem()
        {
            FocusItem(_selectedIndex);
        }

        private void FocusItem(int index)
        {
            if (
                !isActiveAndEnabled
                || !gameObject.activeInHierarchy
                || EventSystem.current == null
                || index < 0
                || index >= _items.Count
                || _items[index] == null
            )
                return;

            EventSystem.current.SetSelectedGameObject(_items[index].gameObject);
        }
    }
}
