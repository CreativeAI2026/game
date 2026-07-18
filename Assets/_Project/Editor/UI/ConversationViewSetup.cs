using CreativeAI.UI.ConversationUI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// 会話UI(<see cref="ConversationView"/>)を <see cref="CreativeAI.UI.UIRoot"/> Prefab の子として
    /// 入れ子(ネストPrefab)注入するツール。仕様§6のとおり UIRoot が会話UIも束ねる。
    /// 既存の ConversationView.prefab をそのままネストするので内部配線(立ち絵/ウィンドウ/選択肢)は保持される。
    /// 常駐・単一化・DontDestroyOnLoad は UIRoot が担うため、Title/config への追加配線は不要。
    /// 冪等: 既に "ConversationView" 子が在れば作り直す。
    /// </summary>
    public static class ConversationViewSetup
    {
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string ConversationPrefabPath =
            "Assets/_Project/Features/UI/ConversationUI/Prefabs/ConversationView.prefab";
        private const string ChildName = "ConversationView";

        [MenuItem("Tools/CreativeAI/UI/Inject Conversation View Into UIRoot")]
        public static void InjectIntoUIRoot()
        {
            var convPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConversationPrefabPath);
            if (convPrefab == null || convPrefab.GetComponent<ConversationView>() == null)
            {
                Debug.LogError(
                    $"[ConversationView] ConversationView Prefab が見つかりません: {ConversationPrefabPath}"
                );
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPath) == null)
            {
                Debug.LogError($"[ConversationView] UIRoot Prefab が見つかりません: {UIRootPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UIRootPath);
            try
            {
                var existing = root.transform.Find(ChildName);
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                // 既存 Prefab をネスト(内部配線を保持)。
                var nested = (GameObject)
                    PrefabUtility.InstantiatePrefab(convPrefab, root.transform);
                nested.name = ChildName;
                nested.transform.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[ConversationView] UIRoot.prefab に '{ChildName}' をネスト注入しました({UIRootPath})。\n"
                    + "UIRoot は Title で既に常駐生成されるため、Title/config への追加配線は不要。\n"
                    + "会話UIは Awake で DialogueViewService seam に自己登録する(EventPlayer が参照)。"
            );
        }
    }
}
