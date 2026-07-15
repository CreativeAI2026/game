using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Objects/FoodData")]
    public class FoodData : ItemData
    {
        public int healAmount; // 回復量

        private void OnEnable() => category = ItemCategory.Food;
    }
}
