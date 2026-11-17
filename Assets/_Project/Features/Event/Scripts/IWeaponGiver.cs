namespace CreativeAI.Gameplay
{
    /// <summary>
    /// giveWeapon ステップの届け先。実体はプレイヤーの WeaponManager で、プレビュー・テストは差し替える。
    /// プレイヤーが居なければ EventPlayer が警告してスキップする。
    /// </summary>
    public interface IWeaponGiver
    {
        /// <summary>weaponKey(sword/bow/scythe/hammer)の武器を1本入手する。既に所持なら何もしない。</summary>
        void GiveWeapon(string weaponKey);
    }
}
