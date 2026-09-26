using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 「はじめる/続きから」で Inspector 指定の PlayerRig Prefab を1体だけ生成し DontDestroyOnLoad する。
    /// Player タグが既に居れば作らない。SessionBootstrap の後に呼ぶ。Prefab の中身は視覚班/プレイヤー担当。
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
