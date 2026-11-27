using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(
        fileName = "NewPlayerParameterData",
        menuName = "creativeAI/PlayerParameterData"
    )]
    public class PlayerParameterData : ScriptableObject
    {
        [Header("プレイヤーのパラメータ")]
        [Min(1f)]
        public float baseMaxLife = 5000f; // 最大体力の基準値

        [Min(0f)]
        public float baseAttackPower = 100f; // 攻撃力の基準値

        [Min(0f)]
        public float baseDefense = 10f; //防御力の基準値

        [Range(0f, 100f)]
        public float baseCriticalChance = 5f; // 会心率の基準値（0% ~ 100%)
        public float baseCriticalDamageRatio = 2f; // 会心時に攻撃力へ上乗せする%(パーセントポイント。RollDamage が ÷100 して適用)。仕様の素の値は0
    }
}
