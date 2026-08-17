using System;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Per-item state for equipment only. Shared item definition stays in EquipmentData.
    /// </summary>
    public class EquipmentInstance
    {
        public EquipmentInstance(
            string instanceId,
            int durability,
            int attackBonus = 0,
            int defenseBonus = 0
        )
        {
            InstanceId = string.IsNullOrWhiteSpace(instanceId)
                ? Guid.NewGuid().ToString("N")
                : instanceId;
            Durability = durability;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
        }

        public string InstanceId { get; }
        public int Durability { get; set; }
        public int AttackBonus { get; set; }
        public int DefenseBonus { get; set; }
    }
}
