using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class InventoryManager : MonoBehaviour
    {
        private const int InitialEquippedTestItemCountPerCategory = 2;
        private const int InitialTestItemMinCount = 5;
        private const int InitialTestItemMaxCountExclusive = 16;

        public static InventoryManager Instance { get; private set; }

        public event System.Action InventoryChanged;

        [SerializeField]
        private bool _addTestItemsOnAwake = true;

        private readonly List<ItemStack> _items = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_addTestItemsOnAwake)
            {
                AddTestItems();
                EquipInitialTestItems();
            }
        }

        public void AddItem(ItemData data, int count = 1)
        {
            if (data == null)
                return;

            var existing = _items.Find(stack => stack.Data == data);
            if (existing != null)
                existing.Count += count;
            else
                _items.Add(new ItemStack(data, count));

            InventoryChanged?.Invoke();
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            var stack = _items.Find(stack => stack.Data == data);
            if (stack == null)
                return;

            stack.Count -= count;
            if (stack.Count <= 0)
                _items.Remove(stack);

            InventoryChanged?.Invoke();
        }

        public int GetItemCount(ItemData data)
        {
            if (data == null)
                return 0;

            return _items.Find(stack => stack.Data == data)?.Count ?? 0;
        }

        public bool CanCraft(CraftRecipeData recipe, int quantity = 1)
        {
            if (recipe == null || recipe.resultItem == null || quantity <= 0)
                return false;

            var materials = recipe.Materials.ToList();
            if (materials.Count != 2)
                return false;

            if (HasEquippedMaterial(materials))
                return false;

            return materials
                .GroupBy(material => material)
                .All(group => GetItemCount(group.Key) >= group.Count() * quantity);
        }

        public bool TryCraft(CraftRecipeData recipe, int quantity)
        {
            if (!CanCraft(recipe, quantity))
                return false;

            foreach (var group in recipe.Materials.GroupBy(material => material))
                RemoveItem(group.Key, group.Count() * quantity);

            AddItem(recipe.resultItem, quantity);
            return true;
        }

        public void SetEquipped(ItemStack stack, bool equipped)
        {
            if (stack != null)
                stack.IsEquipped = equipped;
        }

        public bool IsEquipped(ItemStack stack) => stack?.IsEquipped ?? false;

        public bool IsItemEquipped(ItemData data)
        {
            return data != null && _items.Any(stack => stack.Data == data && stack.IsEquipped);
        }

        public bool HasEquippedMaterial(IEnumerable<ItemData> materials)
        {
            return materials != null && materials.Any(IsItemEquipped);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return _items.FindAll(stack => stack.Data.category == category);
        }

        public List<ItemStack> GetAllItems() => new(_items);

        private void AddTestItems()
        {
            if (ItemDB.Instance == null)
                return;

            var testItems = ItemDB.Instance.Items.Where(HasZeroSecondDigit).ToList();
            foreach (var item in testItems)
                AddItem(
                    item,
                    Random.Range(InitialTestItemMinCount, InitialTestItemMaxCountExclusive)
                );
        }

        private void EquipInitialTestItems()
        {
            EquipInitialTestItems(ItemCategory.Equipment);
            EquipInitialTestItems(ItemCategory.Food);
        }

        private void EquipInitialTestItems(ItemCategory category)
        {
            if (_items.Any(stack => stack.Data.category == category && stack.IsEquipped))
                return;

            foreach (
                var stack in _items
                    .Where(stack => stack.Data.category == category)
                    .Take(InitialEquippedTestItemCountPerCategory)
            )
            {
                stack.IsEquipped = true;
            }
        }

        private static bool HasZeroSecondDigit(ItemData item)
        {
            if (item == null)
                return false;

            string id = Mathf.Abs(item.id).ToString();
            return id.Length >= 2 && id[1] == '0';
        }
    }
}
