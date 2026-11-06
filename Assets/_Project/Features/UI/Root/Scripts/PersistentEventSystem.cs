using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// UI 入力の <see cref="EventSystem"/> をアプリ常駐化する係(UI を出す役ではない)。Title に自動生成される1つに付け、
    /// DontDestroyOnLoad で全シーンで使い回す。既に常駐が居れば自分を破棄する。フィールドシーンには EventSystem を置かない。
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

            if (
                TryGetComponent<InputSystemUIInputModule>(out var module)
                && module.actionsAsset != null
            )
            {
                module.actionsAsset.FindActionMap("UI", false)?.Enable();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
