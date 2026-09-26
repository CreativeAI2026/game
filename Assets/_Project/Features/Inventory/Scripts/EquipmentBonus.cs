namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 装備(装備品 + 武器)による補正合計。InventoryManager / WeaponManager が積み上げ、PlayerStatus が素の値に合算する。
    /// 攻撃/防御/最大HP は 素の値×(1+割合%)、会心系は加算。移動・攻撃速度はコントローラ側なので持たない。
    /// </summary>
    public struct EquipmentBonus
    {
        // すべて %(パーセントポイント)。attackPct/defensePct/maxHpPct は素の値への割合で、
        // PlayerStatus が base×(1+Σ%/100) で適用する。criticalChance は会心率(%・加算)、
        // criticalDamage は会心ダメージ(%・会心時に攻撃力へ ÷100 で上乗せ)。
        public float attackPct;
        public float defensePct;
        public float maxHpPct;
        public float criticalChance;
        public float criticalDamage;
    }
}
