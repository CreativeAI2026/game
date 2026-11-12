using System;
using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.AI;

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
        public const string AssetPath = "Assets/_Project/Features/Session/Data/dev_state.json";

        /// <summary>
        /// 個人用の置き場(.gitignore 済み)。あればこちらが <see cref="AssetPath"/> より優先される。
        /// 開始位置のように人ごとに違う設定は、共有の dev_state.json ではなくこちらへ書く。
        /// </summary>
        public const string LocalAssetPath =
            "Assets/_Project/Features/Session/Data/dev_state.local.json";

        /// <summary>読む dev_state のパス。個人用があればそれ、無ければ共有のもの(どちらも無くても共有のパスを返す)。</summary>
        public static string ResolvePath() =>
            System.IO.File.Exists(LocalAssetPath) ? LocalAssetPath : AssetPath;

        public int progress;
        public FlagEntry[] flags = Array.Empty<FlagEntry>();
        public string[] importantItems = Array.Empty<string>();

        /// <summary>持たせる武器のキー(sword / bow / scythe / hammer)。</summary>
        public string[] weapons = Array.Empty<string>();

        /// <summary>選んでおく武器のキー。空や未所持なら持っている武器の先頭。</summary>
        public string selectedWeapon = string.Empty;

        /// <summary>開始時の HP(必須)。最大HPを超える値は最大HPに丸める。書き忘れは負のまま残り <see cref="HasHp"/> で弾く。</summary>
        public float hp = -1f;

        /// <summary>hp が書かれているか。JsonUtility は省略を区別できないので負の既定値で見分ける。</summary>
        public bool HasHp => hp >= 0f;

        /// <summary>true なら、開始位置を <see cref="spawnPosition"/> / <see cref="spawnYaw"/> で上書きする。</summary>
        public bool useSpawn;

        /// <summary>開始位置(足元のワールド座標)。歩ける床(NavMesh)が近くにあれば、その高さに合わせる。</summary>
        public Vector3 spawnPosition;

        /// <summary>開始時の向き(Y 軸まわりの角度)。</summary>
        public float spawnYaw;

        /// <summary>NavMesh の床へ合わせる範囲。イベント位置から求めた座標が多少ずれていても床に乗せる。</summary>
        private const float SpawnSnapDistance = 1.5f;

        /// <summary>
        /// <see cref="useSpawn"/> なら、プレイヤーを指定の開始位置へ置く。false なら何もしない。
        /// 近くに歩ける床があればその高さへ合わせ、無ければ指定の座標のまま置く。
        /// </summary>
        public bool ApplySpawn(GameObject player)
        {
            if (!useSpawn || player == null)
                return false;

            var position = spawnPosition;
            if (
                NavMesh.SamplePosition(position, out var floor, SpawnSnapDistance, NavMesh.AllAreas)
            )
                position = floor.position;
            // CharacterController は自前で位置を保持するので、当てが効かないよう一旦切ってから動かす
            // (SpawnPoint.Place と同じ扱い。Core は触らず、開発用の置き方はここで完結させる)。
            bool hasController = player.TryGetComponent(out CharacterController controller);
            if (hasController)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, spawnYaw, 0f));
            if (hasController)
                controller.enabled = true;
            return true;
        }

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
