using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Handles item use effects. InventoryService remains responsible for consuming stacks.
    /// Crafting material consumption must not call this service.
    /// </summary>
    public class ItemUseService
    {
        private readonly InventoryService _inventoryService;
        private PlayerStatus _playerStatus;

        public ItemUseService(InventoryService inventoryService)
            : this(inventoryService, null) { }

        public ItemUseService(InventoryService inventoryService, PlayerStatus playerStatus)
        {
            _inventoryService = inventoryService;
            _playerStatus = playerStatus;
        }

        public bool TryUse(ItemStack stack)
        {
            if (
                stack == null
                || _inventoryService == null
                || !_inventoryService.ContainsStack(stack)
                || stack.Count <= 0
                || stack.Data is not FoodData food
            )
                return false;

            var playerStatus = ResolvePlayerStatus();
            if (playerStatus != null)
            {
                // 食材はHP即時回復のみ。回復量は最大HPに対する固定割合(合成前20%/合成後50%)。
                float heal = playerStatus.CurrentMaxHp * food.HealFraction;
                playerStatus.Heal(heal);
            }

            return _inventoryService.ConsumeFromStack(stack, 1);
        }

        private PlayerStatus ResolvePlayerStatus()
        {
            if (_playerStatus != null)
                return _playerStatus;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                return null;

            _playerStatus = player.GetComponent<PlayerStatus>();
            return _playerStatus;
        }
    }
}
