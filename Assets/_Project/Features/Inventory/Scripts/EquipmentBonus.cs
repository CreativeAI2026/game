namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 装備(装備品 + 武器)による最終ステータスへの補正合計を表す共通の器。
    /// 装備品ぶんは `InventoryManager.GetEquippedBonus()`、選択中武器ぶんは `WeaponManager.GetSelectedBonus()`
    /// がそれぞれ積み上げ、`PlayerStatus` が両方を素の値に合算する(武器は在庫外なので別ルート)。
    /// spec: 攻撃/防御/最大HP は 素の値×(1+割合%)、会心率/会心ダメージは加算(Specification.md「プレイヤーステータス」)。
    /// 移動速度/攻撃速度は PlayerStatus の対象外(コントローラ側)なのでここには持たない。
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
