namespace CreativeAI.Gameplay
{
    /// <summary>
    /// giveWeapon ステップの届け先。実体はセッション常駐の WeaponManager で、プレビュー・テストは差し替える。
    /// 常駐が無ければ EventPlayer が警告してスキップする。
    /// </summary>
    public interface IWeaponGiver
    {
        /// <summary>weaponKey(sword/bow/scythe/hammer)の武器を1本入手する。既に所持なら何もしない。</summary>
        void GiveWeapon(string weaponKey);
    }
}
