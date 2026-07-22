namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// giveWeapon ステップの seam。実体は Gameplay(プレイヤーリグの WeaponManager)で実装し、
    /// EventPlayer に注入する。Core は Gameplay を参照しないためこの契約を挟む
    /// (giveItem の <see cref="IItemGiver"/> と対称)。
    /// 実装者(WeaponManager)がまだ無い間は EventPlayer が警告してスキップする(前方互換)。
    /// </summary>
    public interface IWeaponGiver
    {
        /// <summary>weaponKey(sword/bow/scythe)の武器を1本入手する。既に所持なら何もしない。</summary>
        void GiveWeapon(string weaponKey);
    }

    /// <summary>
    /// 実行時に有効な IWeaponGiver を Core 側へ登録する seam。実装者(プレイヤーリグの WeaponManager)が
    /// Awake で自身を登録し、EventPlayer は Inspector 未配線時のフォールバックとしてここを見る
    /// (<see cref="ItemGiverService"/> と同じ思想)。
    /// </summary>
    public static class WeaponGiverService
    {
        public static IWeaponGiver Current { get; set; }
    }
}
