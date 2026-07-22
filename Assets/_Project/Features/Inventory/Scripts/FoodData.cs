using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Objects/FoodData")]
    public class FoodData : ItemData
    {
        // 食材の効果は「HP即時回復」のみ。回復量は最大HPに対する固定割合で、全食材共通・
        // 素材の組み合わせに依らない(documents/Specification.md §2.1):
        //   合成前 = 最大HPの 20% / 合成後 = 最大HPの 50%
        public const float PreCraftHealFraction = 0.20f;
        public const float PostCraftHealFraction = 0.50f;

        [Tooltip("調合で作られた食材(合成後)なら ON。ON=最大HPの50%回復 / OFF=20%回復。")]
        [SerializeField]
        private bool _craftedResult;

        public bool IsCraftedResult => _craftedResult;

        /// <summary>使用時に回復する最大HPに対する割合(合成前 0.2 / 合成後 0.5)。</summary>
        public float HealFraction => _craftedResult ? PostCraftHealFraction : PreCraftHealFraction;

        /// <summary>食材は必ずスタックできる(同じ食材は1枠にまとめる)。個々のアセット設定に依らずカテゴリのルールで保証。</summary>
        public const int FoodStackCap = 99;

        public override int MaxStack => Mathf.Max(FoodStackCap, base.MaxStack);

        private void OnEnable() => category = ItemCategory.Food;
    }
}
