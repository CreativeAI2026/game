using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public sealed class RecipeListView : MonoBehaviour
    {
        [SerializeField]
        private Transform _content;

        [SerializeField]
        private GameObject _slotPrefab;

        private readonly List<RecipeSlot> _slots = new();
        private bool _interactionEnabled = true;
        private int _selectedIndex = -1;

        public event Action<CraftRecipeData> RecipeClicked;
        public event Action<CraftRecipeData> RecipeDoubleClicked;

        public CraftRecipeData FirstRecipe =>
            _slots.FirstOrDefault(slot => slot != null && slot.Recipe != null)?.Recipe;

        public bool HasRequiredReferences =>
            _content != null
            && _slotPrefab != null
            && _slotPrefab.GetComponent<RecipeSlot>() != null;

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            if (!enabled)
                CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        private void Update()
        {
            if (
                !isActiveAndEnabled
                || !_interactionEnabled
                || !IsValidIndex(_selectedIndex)
                || !CreativeAI.UI.SlotKeyboardFocus.IsFocused(this)
            )
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                SelectByOffset(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                SelectByOffset(1);
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SelectVertically(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SelectVertically(1);
            else if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
                SubmitSelectedRecipe();
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            if (_content == null)
            {
                var scrollRect = GetComponent<ScrollRect>();
                _content = scrollRect != null ? scrollRect.content : null;
            }
        }
#endif

        public void SetRecipes(IEnumerable<CraftRecipeData> recipes)
        {
            Clear();
            if (!HasRequiredReferences || recipes == null)
                return;

            int slotIndex = 0;
            foreach (var recipe in recipes.Where(recipe => recipe != null))
            {
                var slotObject = Instantiate(_slotPrefab, _content, false);
                slotObject.name = _slotPrefab.name;
                slotObject.SetActive(true);

                var slot = slotObject.GetComponent<RecipeSlot>();
                slot.SetRecipe(recipe);
                BindSlot(slot);
                CraftUIAnimationUtility.PlayPopIn(slotObject, slotIndex * 0.04f);
                slotIndex++;
            }

            RebuildLayout();
        }

        public void SelectRecipe(CraftRecipeData recipe)
        {
            _selectedIndex = -1;
            for (int i = 0; i < _slots.Count; i++)
            {
                RecipeSlot slot = _slots[i];
                bool selected = slot != null && slot.Recipe == recipe && recipe != null;
                slot?.SetSelected(selected);
                if (selected)
                    _selectedIndex = i;
            }

            if (_selectedIndex >= 0)
            {
                CreativeAI.UI.SlotKeyboardFocus.Claim(this);
                ScrollToSelected();
            }
            else
            {
                CreativeAI.UI.SlotKeyboardFocus.Release(this);
            }
        }

        public void SubmitSelectedRecipe()
        {
            if (!_interactionEnabled || !IsValidIndex(_selectedIndex))
                return;

            RecipeDoubleClicked?.Invoke(_slots[_selectedIndex].Recipe);
        }

        public void RefreshSlots()
        {
            foreach (var slot in _slots)
                slot?.RefreshDisplay();
        }

        public void Clear()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;

                UnbindSlot(slot);
                Destroy(slot.gameObject);
            }

            _slots.Clear();
            _selectedIndex = -1;
            CreativeAI.UI.SlotKeyboardFocus.Release(this);
        }

        public void RebuildLayout()
        {
            if (_content is not RectTransform contentRect)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void OnDestroy()
        {
            foreach (var slot in _slots)
            {
                if (slot != null)
                    UnbindSlot(slot);
            }
        }

        private void BindSlot(RecipeSlot slot)
        {
            slot.Clicked += OnSlotClicked;
            slot.DoubleClicked += OnSlotDoubleClicked;
            _slots.Add(slot);
        }

        private void UnbindSlot(RecipeSlot slot)
        {
            slot.Clicked -= OnSlotClicked;
            slot.DoubleClicked -= OnSlotDoubleClicked;
            slot.SetSelected(false);
        }

        private void OnSlotClicked(RecipeSlot slot)
        {
            if (!_interactionEnabled)
                return;

            FocusSlot(slot);
            RecipeClicked?.Invoke(slot?.Recipe);
        }

        private void OnSlotDoubleClicked(RecipeSlot slot)
        {
            if (!_interactionEnabled)
                return;

            FocusSlot(slot);
            RecipeDoubleClicked?.Invoke(slot?.Recipe);
        }

        private void FocusSlot(RecipeSlot slot)
        {
            int index = _slots.IndexOf(slot);
            if (!IsValidIndex(index))
                return;

            _selectedIndex = index;
            CreativeAI.UI.SlotKeyboardFocus.Claim(this);
        }

        private void SelectByOffset(int offset)
        {
            if (_slots.Count <= 1)
                return;

            int nextIndex = (_selectedIndex + offset + _slots.Count) % _slots.Count;
            SelectAt(nextIndex);
        }

        private void SelectVertically(int rowOffset)
        {
            int columns = GetColumnCount();
            if (_slots.Count <= columns)
                return;

            int nextIndex = _selectedIndex + columns * rowOffset;
            if (nextIndex < 0)
                nextIndex = GetBottomIndexInColumn(_selectedIndex % columns, columns);
            else if (nextIndex >= _slots.Count)
                nextIndex = _selectedIndex % columns;

            if (nextIndex != _selectedIndex)
                SelectAt(nextIndex);
        }

        private void SelectAt(int index)
        {
            if (!IsValidIndex(index))
                return;

            RecipeClicked?.Invoke(_slots[index].Recipe);
        }

        private int GetColumnCount()
        {
            if (
                _content != null
                && _content.TryGetComponent(out GridLayoutGroup grid)
                && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            )
                return Mathf.Max(1, grid.constraintCount);

            return Mathf.Max(1, _slots.Count);
        }

        private int GetBottomIndexInColumn(int column, int columns)
        {
            int bottomIndex = column;
            while (bottomIndex + columns < _slots.Count)
                bottomIndex += columns;
            return bottomIndex;
        }

        private bool IsValidIndex(int index) =>
            index >= 0
            && index < _slots.Count
            && _slots[index] != null
            && _slots[index].Recipe != null;

        private void ScrollToSelected()
        {
            var scrollRect = _content != null ? _content.GetComponentInParent<ScrollRect>() : null;
            int columns = GetColumnCount();
            int rowCount = Mathf.CeilToInt(_slots.Count / (float)columns);
            if (scrollRect == null || rowCount <= 1)
                return;

            int selectedRow = _selectedIndex / columns;
            scrollRect.verticalNormalizedPosition = 1f - selectedRow / (float)(rowCount - 1);
        }
    }
}
