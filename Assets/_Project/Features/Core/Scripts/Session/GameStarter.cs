using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// タイトルの「はじめる/続きから」でプレイヤーリグを1体だけ生成する開始処理スクリプト。
    /// PlayerImplementation.md の手順どおり、Project の PlayerRig Prefab を Inspector で紐づけ、
    /// 実行時に Instantiate → DontDestroyOnLoad で常駐させる。フィールドシーンには置かない。
    /// 既にプレイヤー(Player タグ)が居れば作らない(連打・タイトル復帰での二重化を防ぐ)。
    /// 生成順は マネージャ → プレイヤー(spec §6.1)なので、SessionBootstrap の後に呼ぶ。
    /// PlayerRig Prefab の中身(モデル・カメラ・PlayerStats)は視覚班/プレイヤー担当。
    /// </summary>
    public sealed class GameStarter : MonoBehaviour
    {
        [SerializeField]
        private GameObject _playerRigPrefab; // Project の PlayerRig Prefab をドラッグ(未割当なら None 表示)

        [SerializeField]
        private string _playerTag = "Player";

        /// <summary>
        /// 未生成ならプレイヤーリグを生成して常駐させ、それを返す。既に居ればそれを返す。
        /// Prefab 未割当なら警告して null(フィールドは読み込めるがプレイヤーは出ない)。
        /// </summary>
        public GameObject EnsurePlayer() => EnsurePlayerRig(_playerRigPrefab, _playerTag);

        /// <summary>
        /// リグ生成・単一化の本体。Title フロー(このクラス)と、Title を経由しない開発用の直接 Play
        /// (<c>FieldDevBootstrap</c>)で同じ経路を通すため static にしてある。
        /// </summary>
        public static GameObject EnsurePlayerRig(GameObject prefab, string playerTag = "Player")
        {
            var existing = GameObject.FindWithTag(playerTag);
            if (existing != null)
                return existing;

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[GameStarter] playerRigPrefab が未割当です。PlayerRig Prefab を Inspector にドラッグしてください。"
                );
                return null;
            }

            var player = Instantiate(prefab);
            player.name = prefab.name; // "(Clone)" を避ける
            if (Application.isPlaying)
                DontDestroyOnLoad(player); // 隠しシーンへ移し常駐(EditMode では呼ばない)
            return player;
        }
    }
}
