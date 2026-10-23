using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// セッション常駐の UI レイヤーのルート。HUD / アイコンバー / 即時食材使用UI / 武器切替UI / 各パネル / 会話UI を束ねる親。
    /// Title フローで <see cref="EnsureResident"/> により1回だけ生成し DontDestroyOnLoad で常駐させる。状態は保存せず、フィールドシーンには UI を置かない。
    /// 直下は Canvas の入れ物(描画順はここで決める)で、中身は機能ごとの Prefab(Canvas を持たない)を入れる。
    /// Prefab 編集時の注意: 常駐UIは各自の CanvasGroup で出し分けるため、GameObject をオフで保存しない
    /// (オフで保存すると実行時に二度と表示されない)。オン/オフで開閉するのは <see cref="UiRouter"/> が管理するパネルだけ。
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        /// <summary>
        /// UI レイヤーを Prefab から1回だけ生成する(既に在ればそれを返す。Prefab 未割当なら警告して null)。
        /// <see cref="GameSession"/> から、マネージャ生成後に呼ぶ(HudIconBarView が生成時にモードを購読するため)。
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
