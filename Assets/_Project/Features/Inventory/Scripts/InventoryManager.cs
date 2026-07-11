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

        private readonly InventoryStorage _storage = new();
        private InventoryService _inventoryService;
        private RecipeCraftingService _recipeCraftingService;
        private ItemUseService _itemUseService;

        public InventoryService InventoryService => _inventoryService ??= CreateInventoryService();

        public RecipeCraftingService RecipeCraftingService =>
            _recipeCraftingService ??= new RecipeCraftingService(InventoryService);

        public ItemUseService ItemUseService =>
            _itemUseService ??= new ItemUseService(InventoryService);

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
            InventoryService.AddItem(data, count);
        }

        public void AddEquipmentItem(EquipmentData data, EquipmentInstance instance)
        {
            InventoryService.AddEquipmentItem(data, instance);
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            InventoryService.RemoveItem(data, count);
        }

        public bool ConsumeItem(ItemData data, int count = 1)
        {
            return InventoryService.ConsumeItem(data, count);
        }

        public bool TryUse(ItemStack stack)
        {
            return ItemUseService.TryUse(stack);
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return InventoryService.HasItem(data, count);
        }

        public int GetItemCount(ItemData data)
        {
            return InventoryService.GetItemCount(data);
        }

        public bool CanCraft(CraftRecipeData recipe, int quantity = 1)
        {
            return RecipeCraftingService.CanCraft(recipe, quantity);
        }

        public bool CanCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return RecipeCraftingService.CanCraft(recipe, materialA, materialB);
        }

        public int GetMaximumCraftable(CraftRecipeData recipe)
        {
            return RecipeCraftingService.GetMaximumCraftable(recipe);
        }

        public bool TryCraft(CraftRecipeData recipe, int quantity)
        {
            return RecipeCraftingService.TryCraft(recipe, quantity);
        }

        public bool TryCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return RecipeCraftingService.TryCraft(recipe, materialA, materialB);
        }

        public void SetEquipped(ItemStack stack, bool equipped)
        {
            if (stack != null)
                stack.IsEquipped = equipped;
        }

        public bool IsEquipped(ItemStack stack) => stack?.IsEquipped ?? false;

        public bool IsItemEquipped(ItemData data)
        {
            return data != null
                && InventoryService
                    .GetAllItems()
                    .Any(stack => stack.Data == data && stack.IsEquipped);
        }

        public bool HasEquippedMaterial(IEnumerable<ItemData> materials)
        {
            return materials != null && materials.Any(IsItemEquipped);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return InventoryService.GetItemsByCategory(category);
        }

        public List<ItemStack> GetAllItems() => InventoryService.GetAllItems();

        private void AddTestItems()
        {
            if (ItemDB.Instance == null)
                return;

            var testItems = ItemDB.Instance.Items.Where(HasZeroSecondDigit).ToList();
            foreach (var item in testItems)
            {
                int count =
                    item.MaxStack > 1
                        ? Random.Range(InitialTestItemMinCount, InitialTestItemMaxCountExclusive)
                        : 1;
                AddItem(item, count);
            }
        }

        private void EquipInitialTestItems()
        {
            EquipInitialTestItems(ItemCategory.Equipment);
            EquipInitialTestItems(ItemCategory.Food);
        }

        private void EquipInitialTestItems(ItemCategory category)
        {
            var items = InventoryService.GetAllItems();
            if (items.Any(stack => stack.Data.category == category && stack.IsEquipped))
                return;

            foreach (
                var stack in items
                    .Where(stack => stack.Data.category == category)
                    .Take(InitialEquippedTestItemCountPerCategory)
            )
            {
                stack.IsEquipped = true;
            }
        }

        private InventoryService CreateInventoryService()
        {
            var service = new InventoryService(_storage);
            service.InventoryChanged += OnInventoryServiceChanged;
            return service;
        }

        private void OnInventoryServiceChanged()
        {
            InventoryChanged?.Invoke();
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
