namespace CreativeAI.StatRoll
{
    /// <summary>
    /// 付与ステータスの型(documents/Specification.md §1.1「アイテムカテゴリと付与ステータス」)。
    /// 装備品/武器は MaxHpPct、食材は HealAmount を持つ(排他)。
    /// アルゴリズム上はどれも「ステータス枠」として等価に扱う。
    /// </summary>
    public enum StatType
    {
        AttackPct, // 攻撃%
        DefensePct, // 防御%
        CritDamage, // 会心ダメージ
        CritRate, // 会心率
        MaxHpPct, // 最大HP%(装備品・武器)
        HealAmount, // HP即時回復(食材)
    }
}
