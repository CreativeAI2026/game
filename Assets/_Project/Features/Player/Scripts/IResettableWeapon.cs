namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 怯み・死亡などで行動を強制的に打ち切るときに、武器の状態を中立へ戻す窓口。
    /// WeaponManager に登録された武器のルートに付くコンポーネントが実装する。
    /// 呼び出し側(<see cref="WeaponManager.ResetEquippedWeapon"/>)は武器の種類を知らなくてよい。
    /// </summary>
    public interface IResettableWeapon
    {
        /// <summary>現在の行動を終了し、中立状態に戻す。自分が掛けた行動制限も中立状態の分に戻す。</summary>
        void ForceReset();
    }
}
