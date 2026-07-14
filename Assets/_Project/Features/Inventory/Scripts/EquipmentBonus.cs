namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 装備(装備品 + 武器)による最終ステータスへの補正合計。
    /// `InventoryManager.GetEquippedBonus()` が装備中アイテムから積み上げ、`PlayerStatus` が素の値に合算する。
    /// spec: 最終ステータス = 素の値 + 装備の補正(Specification.md「プレイヤーステータス」)。
    /// 移動速度/攻撃速度は PlayerStatus の対象外(コントローラ側)なのでここには持たない。
    /// </summary>
    public struct EquipmentBonus
    {
        public float attack;
        public float defense;
        public float maxHp;
        public float criticalChance;
        public float criticalDamage;
    }
}
