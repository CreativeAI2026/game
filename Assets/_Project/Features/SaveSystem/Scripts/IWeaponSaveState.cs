namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 選択中の武器をセーブ/復元する境界(seam)。実体はプレイヤーリグの WeaponManager(担当班)が持つため、
    /// システム班の SaveService はこの窓口越しに読み書きする(現在HPの <see cref="ISaveableActor"/> と対称)。
    /// spec §6: プレイヤーリグは「選択武器」も保存対象。
    /// </summary>
    public interface IWeaponSaveState
    {
        /// <summary>保存時に選択中の武器 index を返す。</summary>
        int CaptureSelectedWeaponIndex();

        /// <summary>復元時に武器 index を設定し、その武器を装備して補正を再計算させる。</summary>
        void RestoreSelectedWeaponIndex(int index);
    }
}
