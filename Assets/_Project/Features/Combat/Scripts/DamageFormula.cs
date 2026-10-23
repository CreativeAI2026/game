using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被ダメージ側の防御軽減式。プレイヤー・敵の両方が使う。
    /// 最終ダメージ = 攻撃側のダメージ × DefenseConstant / (DefenseConstant + 防御力)。
    /// </summary>
    public static class DamageFormula
    {
        /// <summary>
        /// 防御力がこの値と等しいときに被ダメージが半分になる。プレイヤーの素の防御力(仕様: 500)に合わせている。
        /// 減算式(ダメージ − 防御)だと、防御500に対して攻撃力500未満の敵が常に最低保証ダメージしか出せず、
        /// 防御%装備の効きも極端になるため比率式にしている。
        /// </summary>
        public const float DefenseConstant = 500f;

        /// <summary>防御で軽減した最終ダメージ。攻撃が完全に無効化されないよう最低1は保証する。</summary>
        public static float ApplyDefense(float rawDamage, float defense)
        {
            float reduction = DefenseConstant / (DefenseConstant + Mathf.Max(0f, defense));
            return Mathf.Max(1f, rawDamage * reduction);
        }
    }
}
