using System;
using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEngine;
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

        public event Action<CraftRecipeData> RecipeClicked;
        public event Action<CraftRecipeData> RecipeDoubleClicked;

        public CraftRecipeData FirstRecipe =>
            _slots.FirstOrDefault(slot => slot != null && slot.Recipe != null)?.Recipe;

        public bool HasRequiredReferences =>
            _content != null
            && _slotPrefab != null
            && _slotPrefab.GetComponent<RecipeSlot>() != null;

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
            foreach (var slot in _slots)
                slot?.SetSelected(slot.Recipe == recipe);
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
            RecipeClicked?.Invoke(slot?.Recipe);
        }

        private void OnSlotDoubleClicked(RecipeSlot slot)
        {
            RecipeDoubleClicked?.Invoke(slot?.Recipe);
        }
    }
}
