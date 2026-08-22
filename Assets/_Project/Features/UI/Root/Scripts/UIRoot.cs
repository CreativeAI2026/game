using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// セッション常駐の UI レイヤーのルート。HUD(HP) / 右上アイコンバー / 即時食材使用UI / 武器切替UI /
    /// 各パネル(キャラ・インベ・セーブ・調合) / 会話UI を束ねる整理用の親。
    /// Title フローで <see cref="EnsureResident"/> により Prefab から1回だけ生成し、
    /// DontDestroyOnLoad で常駐させる(プレイヤーリグと同じ生成方式)。状態は保存されない。
    /// エリア遷移をまたいで持続し、フィールドシーンには UI を置かない。
    /// 常駐なので配線・購読は生成時の1回だけで済む(HudIconBar が毎シーン読み直す必要がない)。
    /// documents/Specification.md「常駐アーキテクチャ」参照。
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        /// <summary>
        /// セッション常駐の UI レイヤーを Prefab から1回だけ生成する。既に在ればそれを返す。
        /// Core→UI の循環を避けるため <c>SessionBootstrap</c> ではなく UI 層(Title フロー)から呼ぶ
        /// (Inventory と同じ理由)。生成順はマネージャ(GameModeManager 等)の後
        /// ── HudIconBar が生成時にモードを購読するため。spec §6.1。
        /// Prefab 未割当なら警告して null(UI は出ないがゲームは進む)。
        /// </summary>
        public static UIRoot EnsureResident(GameObject uiRootPrefab)
        {
            if (Instance != null)
                return Instance;

            if (uiRootPrefab == null)
            {
                Debug.LogWarning(
                    "[UIRoot] uiRootPrefab が未割当です。UIRoot Prefab を Title の TitleUIController にドラッグしてください。"
                );
                return null;
            }

            var go = Instantiate(uiRootPrefab);
            go.name = uiRootPrefab.name; // "(Clone)" を避ける
            return go.GetComponent<UIRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 連打・タイトル復帰での二重生成をガード(冪等)
                return;
            }
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject); // EditMode では呼ばない
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
