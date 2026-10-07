using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI
{
    public sealed class RevolverTabGroup : MonoBehaviour, IMoveHandler, ISubmitHandler
    {
        [Header("Data")]
        [SerializeField]
        private List<RevolverTabEntry> _entries = new();

        [Header("Build")]
        [SerializeField]
        private GameObject _itemPrefab;

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
        private readonly List<int> _renderOrder = new();
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

        private void AnimateSelection(int targetIndex)
        {
            KillSelectionTween();
            int step = _loop
                ? RevolverTabIndexUtility.ShortestStep(_selectedIndex, targetIndex, EntryCount)
                : targetIndex - _selectedIndex;
            float endPosition = _selectionPosition + step;
            int version = ++_animationVersion;

            _selectionTween = DOTween
                .To(
                    () => _selectionPosition,
                    value =>
                    {
                        _selectionPosition = value;
                        RefreshLayout();
                    },
                    endPosition,
                    _moveDuration
                )
                .SetEase(_ease)
                .SetUpdate(true)
                .SetTarget(this)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() =>
                {
                    if (this == null || version != _animationVersion)
                        return;

                    _selectionTween = null;
                    CompleteSelection(targetIndex);
                })
                .OnKill(() =>
                {
                    if (this != null && version == _animationVersion)
                        _selectionTween = null;
                });

            RefreshLayout();
        }

        private void KillSelectionTween()
        {
            _animationVersion++;
            if (_selectionTween != null)
            {
                _selectionTween.Kill();
                _selectionTween = null;
            }
        }

        private void RefreshLayout()
        {
            if (!_built || _items.Count == 0)
                return;

            bool canInteract = _interactionEnabled && !IsAnimating;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null)
                    continue;

                float relativePosition = _loop
                    ? RevolverTabIndexUtility.SignedWrappedDistance(
                        item.DataIndex,
                        _selectionPosition,
                        EntryCount
                    )
                    : item.DataIndex - _selectionPosition;
                item.ApplyLayout(
                    RevolverTabLayoutCalculator.Calculate(
                        relativePosition,
                        _layout,
                        _loop ? EntryCount * 0.5f : float.PositiveInfinity
                    ),
                    canInteract
                );
            }

            bool renderOrderChanged = _renderOrder.Count != _items.Count;
            for (int rank = 0; rank < _items.Count; rank++)
            {
                int itemIndex = FindItemAtRenderRank(rank);
                if (!renderOrderChanged && _renderOrder[rank] != itemIndex)
                    renderOrderChanged = true;
                if (rank < _renderOrder.Count)
                    _renderOrder[rank] = itemIndex;
                else
                    _renderOrder.Add(itemIndex);
            }

            if (!renderOrderChanged)
                return;

            // Far items are placed first; the closest item is therefore rendered in front.
            for (int rank = 0; rank < _renderOrder.Count; rank++)
            {
                int itemIndex = _renderOrder[rank];
                if (itemIndex >= 0 && _items[itemIndex] != null)
                    _items[itemIndex].transform.SetAsLastSibling();
            }
        }

        private int FindItemAtRenderRank(int targetRank)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null)
                    continue;

                float distance = GetAbsoluteDistance(_items[i]);
                int rank = 0;
                for (int j = 0; j < _items.Count; j++)
                {
                    if (i == j || _items[j] == null)
                        continue;

                    float otherDistance = GetAbsoluteDistance(_items[j]);
                    if (
                        otherDistance > distance
                        || (
                            Mathf.Approximately(otherDistance, distance)
                            && _items[j].DataIndex < _items[i].DataIndex
                        )
                    )
                        rank++;
                }

                if (rank == targetRank)
                    return i;
            }
            return -1;
        }

        private float GetAbsoluteDistance(RevolverTabItemView item) =>
            Mathf.Abs(
                _loop
                    ? RevolverTabIndexUtility.SignedWrappedDistance(
                        item.DataIndex,
                        _selectionPosition,
                        EntryCount
                    )
                    : item.DataIndex - _selectionPosition
            );

        private void ApplySelectedView()
        {
            if (_entries == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var view = _entries[i]?.View;
                if (view != null)
                    view.SetActive(i == _selectedIndex);
                if (i < _items.Count && _items[i] != null)
                    _items[i].SetSelected(i == _selectedIndex);
            }
        }

        public bool Build()
        {
            KillSelectionTween();
            ClearGeneratedItems();
            _built = false;
            _selectedIndex = -1;

            if (_entries == null || _entries.Count == 0)
                return FailBuild("Entry list is null or empty.");
            if (_itemPrefab == null)
                return FailBuild("Item Prefab is not assigned.");
            if (_itemRoot == null)
                return FailBuild("Item Root is not assigned.");

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i]?.Definition == null)
                {
                    ClearGeneratedItems();
                    return FailBuild($"Entry {i} has no TabDefinition.");
                }

                var itemObject = Instantiate(_itemPrefab, _itemRoot, false);
                if (itemObject == null)
                {
                    ClearGeneratedItems();
                    return FailBuild($"Failed to instantiate Entry {i}.");
                }

                if (!itemObject.TryGetComponent(out RevolverTabItemView item))
                {
                    DestroyGeneratedObject(itemObject);
                    ClearGeneratedItems();
                    return FailBuild($"Entry {i} Item Prefab has no RevolverTabItemView.");
                }

                if (!item.IsConfigured)
                {
                    DestroyGeneratedItem(item);
                    ClearGeneratedItems();
                    return FailBuild($"Entry {i} Item View has no configured TabButton.");
                }

                item.Bind(_entries[i].Definition, i, HandleItemClicked, OnMove, OnSubmit);
                _items.Add(item);
            }

            _built = true;
            int initialIndex = Mathf.Clamp(_initialIndex, 0, _entries.Count - 1);
            CompleteSelection(initialIndex);
            return true;
        }

        private bool FailBuild(string reason)
        {
            Debug.LogError($"{nameof(RevolverTabGroup)} '{name}' could not build: {reason}", this);
            return false;
        }

        private void ClearGeneratedItems()
        {
            UnbindItems();
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item == null)
                    continue;

                DestroyGeneratedItem(item);
            }
            _items.Clear();
            _renderOrder.Clear();
        }

        private void DestroyGeneratedItem(RevolverTabItemView item)
        {
            if (item == null)
                return;

            DestroyGeneratedObject(item.gameObject);
        }

        private void DestroyGeneratedObject(GameObject itemObject)
        {
            if (itemObject == null)
                return;

            if (Application.isPlaying)
                Destroy(itemObject);
            else
                DestroyImmediate(itemObject);
        }

        private void UnbindItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                    _items[i].Unbind();
            }
        }
    }
}
