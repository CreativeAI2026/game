using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 武器の所持状態と選択中の武器をセーブ/復元する境界(seam)。実体はプレイヤーリグの WeaponManager(担当班)が持つため、
    /// システム班の SaveService はこの窓口越しに読み書きする(現在HPの <see cref="ISaveableActor"/> と対称)。
    /// spec §6: プレイヤーリグは「入手ずみ武器」と「選択武器」の両方を保存対象とする。
    /// </summary>
    public interface IWeaponSaveState
    {
        /// <summary>保存時に選択中の武器 index を返す(1本も持っていなければ -1)。</summary>
        int CaptureSelectedWeaponIndex();

        /// <summary>
        /// 保存時に入手ずみ武器のキー(sword/bow/scythe)を返す。index ではなくキーで持つので
        /// 武器の並び順を変えてもセーブが壊れない。
        /// </summary>
        IReadOnlyList<string> CaptureOwnedWeaponKeys();

        /// <summary>
        /// 復元時に入手ずみ武器と選択武器を設定し、その武器を装備して補正を再計算させる。
        /// 選択が未所持・範囲外なら所持の先頭へ寄せる(0本なら未選択)。
        /// </summary>
        void RestoreWeapons(IReadOnlyList<string> ownedWeaponKeys, int selectedWeaponIndex);
    }
}
