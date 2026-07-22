using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI.InventoryUI
{
    [CreateAssetMenu(
        fileName = "InventoryTabDefinition",
        menuName = "CreativeAI/UI/Inventory Tab Definition"
    )]
    public sealed class InventoryTabDefinition : TabDefinition
    {
        [SerializeField]
        private ItemCategory _category;

        public ItemCategory Category => _category;
    }
}
