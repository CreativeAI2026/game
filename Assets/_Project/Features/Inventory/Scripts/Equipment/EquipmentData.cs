using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "Scriptable Objects/EquipmentData")]
    public class EquipmentData : ItemData
    {
        public int attack; // 攻撃
        public int defense; // 防御
        public float moveSpeed; // 移動速度
        public float attackSpeed; // 攻撃速度
        public float criticalDamage; // 会心ダメージ
        public float criticalRate; // 会心率
        public int maxHP; // 最大HP
    }
}
