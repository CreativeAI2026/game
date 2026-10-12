using System;
using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 開発用: フィールドを直接 Play したときに始める状態(進行度・フラグ・大事なもの・武器・HP)。
    /// <c>dev_state.json</c> に書き、<see cref="FieldDevBootstrap"/> が読んで反映する。
    /// イベント配置の確認で「この進行度・この分岐・この鍵や武器を持った状態」から始めるためのもの。
    /// </summary>
    [Serializable]
    public sealed class DevState
    {
        /// <summary>Assets からの相対パス。events.json と同じ場所に置き、書く人が迷わないようにする。</summary>
        public const string AssetPath = "Assets/_Project/Features/Event/dev_state.json";

        public int progress;
        public FlagEntry[] flags = Array.Empty<FlagEntry>();
        public string[] importantItems = Array.Empty<string>();

        /// <summary>持たせる武器のキー(sword / bow / scythe)。</summary>
        public string[] weapons = Array.Empty<string>();

        /// <summary>選んでおく武器のキー。空や未所持なら持っている武器の先頭。</summary>
        public string selectedWeapon = string.Empty;

        /// <summary>開始時の HP(必須)。最大HPを超える値は最大HPに丸める。書き忘れは負のまま残り <see cref="HasHp"/> で弾く。</summary>
        public float hp = -1f;

        /// <summary>hp が書かれているか。JsonUtility は省略を区別できないので負の既定値で見分ける。</summary>
        public bool HasHp => hp >= 0f;

        // JsonUtility は Dictionary を読めないので key/value の配列で書く。
        [Serializable]
        public sealed class FlagEntry
        {
            public string key;
            public string value;
        }

        public static DevState Parse(string json) => JsonUtility.FromJson<DevState>(json);

        /// <summary>進行度・フラグを上書きし、大事なものを1個ずつ持たせる。</summary>
        public void Apply(ProgressManager progressManager, IItemGiver giver)
        {
            if (progressManager != null)
            {
                var dict = new Dictionary<string, string>();
                foreach (var flag in flags ?? Array.Empty<FlagEntry>())
                    if (flag != null && !string.IsNullOrEmpty(flag.key))
                        dict[flag.key] = flag.value ?? string.Empty;
                progressManager.LoadState(progress, dict);
            }

            if (giver != null)
            {
                foreach (var itemKey in importantItems ?? Array.Empty<string>())
                    if (!string.IsNullOrEmpty(itemKey))
                        giver.Give(itemKey);
            }
        }

        /// <summary>
        /// 武器と HP を反映する。部品はプレイヤーリグ側にあるので、リグに無ければ何もしない。
        /// 武器を先に入れて最大HPを確定させてから HP を丸める(SaveService の復元と同じ順)。
        /// </summary>
        public void ApplyPlayer(WeaponManager weaponManager, PlayerStatus status)
        {
            if (weaponManager != null)
                weaponManager.RestoreWeapons(
                    weapons ?? Array.Empty<string>(),
                    WeaponManager.IndexOfKey(selectedWeapon)
                );

            if (status != null && HasHp)
                status.RestoreHp(hp);
        }
    }
}
