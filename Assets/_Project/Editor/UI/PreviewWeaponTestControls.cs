using System;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// Play 中に武器の所持を「なし／全部」へ切り替え、キャラクター画面の武器プレビューを確かめるためのメニュー。
    /// </summary>
    public static class PreviewWeaponTestControls
    {
        /// <summary>武器を1つも持たない素手の状態にする。</summary>
        [MenuItem("Tools/CreativeAI/UI/Preview Weapons/None")]
        public static void NoWeapons() => SetOwned(false);

        /// <summary>全武器を持たせ、先頭の武器を装備する。</summary>
        [MenuItem("Tools/CreativeAI/UI/Preview Weapons/All")]
        public static void AllWeapons() => SetOwned(true);

        private static void SetOwned(bool all)
        {
            var manager = Manager;
            if (manager == null)
                return;
            var keys = all ? new string[WeaponManager.WeaponKeys.Count] : Array.Empty<string>();
            if (all)
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = WeaponManager.WeaponKeys[i];
            manager.RestoreWeapons(keys, all ? 0 : WeaponManager.NoWeapon);
        }

        private static WeaponManager Manager =>
            Application.isPlaying && PlayerRegistry.Current != null
                ? PlayerRegistry.Current.GetComponent<WeaponManager>()
                : null;
    }
}
