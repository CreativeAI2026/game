using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Objects/FoodData")]
    public class FoodData : ItemData
    {
        public int attack; // 攻撃
        public int defense; // 防御
        public float moveSpeed; // 移動速度
        public float attackSpeed; // 攻撃速度
        public float criticalDamage; // 会心ダメージ
        public float criticalRate; // 会心率
        public int healAmount; // 回復量

        private void OnEnable() => category = ItemCategory.Food;
    }
}
