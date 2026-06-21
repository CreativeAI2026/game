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
        public string characterName; // キャラクター名

        [Min(1f)]
        public float baseMaxLife = 5000f; // 最大体力の基準値

        [Min(0f)]
        public float baseAttackPower = 100f; // 攻撃力の基準値

        [Min(0f)]
        public float baseDefense = 10f; //防御力の基準値

        [Range(0f, 100f)]
        public float baseCriticalChance = 5f; // 会心率の基準値（0% ~ 100%)
        public float baseCriticalDamageRatio = 2f; // 会心時の、攻撃力への上乗せダメージ率（攻撃力 * この変数）
        public float baseMoveSpeed = 5f; // 移動速度の基準値
        public float baseAttackSpeed = 1f; // 攻撃速度の基準値（2fなら2倍の速度）
    }
}
