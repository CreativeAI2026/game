using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI
{
    /// <summary>
    /// UI 入力の司令塔 <see cref="EventSystem"/> をアプリ常駐化する係。表示中の UI にクリック/タップを
    /// 届けるだけで、UI を出す役ではない。Title に自動生成される1つに付け、起動時に DontDestroyOnLoad で
    /// 常駐させ以降の全シーンで使い回す。冪等ガードで二重化を防ぐ(既に常駐が居れば自分を破棄)。
    /// フィールドシーンには UI を置かない設計なので EventSystem も置かず、重複しない。
    /// documents/Specification.md「常駐アーキテクチャ」/ UIImplementation.md 参照。
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public sealed class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
