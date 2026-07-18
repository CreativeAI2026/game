using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 常駐UI(UIRoot / 会話UI)の Prefab 参照を1箇所に集約する設定アセット。
    /// Resources から読み込み、フィールドの開発シーン(<see cref="FieldDevBootstrap"/>)が
    /// Title を経由せず常駐UIを生成するのに使う。Prefab の正はここに集約する。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResidentBootstrapConfig",
        menuName = "CreativeAI/Resident Bootstrap Config"
    )]
    public class ResidentBootstrapConfig : ScriptableObject
    {
        [Tooltip("セッション常駐の UI レイヤー(UIRoot Prefab)")]
        public GameObject uiRootPrefab;

        [Tooltip("セッション常駐の会話UI(ConversationView Prefab)")]
        public GameObject conversationViewPrefab;
    }
}
