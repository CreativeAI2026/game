#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CreativeAI.UI;
using CreativeAI.UI.CraftingUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 調合UIの確認用シーン UI_CraftingPreview を作り、調合UI(CraftPanel.prefab)を常駐 UIRoot に組み込む。
    /// 会話UI確認シーン UI_ConversationPreview(ConversationPreviewDriver)の調合版。
    /// Tools &gt; CreativeAI &gt; Setup Crafting Preview から実行(バッチモード -executeMethod も可)。
    ///
    /// 1) CraftPanel.prefab を UIRoot.prefab の <c>CraftUI</c> Canvas 配下にネスト配置し、
    ///    <c>UiRouter._craftUI</c> に配線する(= 本番 Title フローからも調合UIが開けるようになる)。
    /// 2) UI_CraftingPreview を生成(Camera + EventSystem + FieldDevBootstrap + CraftPreviewDriver)。
    ///    FieldDevBootstrap が常駐一式(UIRoot 含む)を生成しテスト品をシードするので、実素材で調合を試せる。
    /// 3) Build Settings に UI_CraftingPreview を追加する。
    ///
    /// 冪等: 再実行しても CraftUI は作り直し・Build Settings も二重登録しない。
    /// </summary>
    public static class SetupCraftingPreviewScene
    {
        private const string UIRootPrefabPath =
            "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string CraftPanelPrefabPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab";
        private const string CraftingPreviewScenePath =
            "Assets/_Project/Scenes/UI/UI_CraftingPreview.unity";
        private const string CraftUiName = "CraftUI";

        [MenuItem("Tools/CreativeAI/Setup Crafting Preview")]
        public static void Run()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            bool integrated = IntegrateCraftUiIntoUIRoot();
            CreateCraftingPreviewScene();
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary =
                "完了:\n"
                + $"- 調合UI(CraftPanel)を UIRoot に組み込み UiRouter._craftUI を配線 (成功={integrated})\n"
                + $"- {CraftingPreviewScenePath} を生成(FieldDevBootstrap + CraftPreviewDriver)\n"
                + "- Build Settings に UI_CraftingPreview を追加\n\n"
                + "UI_CraftingPreview を開いて Play すると調合UIが自動で開きます。";
            if (Application.isBatchMode)
                Debug.Log("[SetupCraftingPreviewScene] " + summary);
            else
                EditorUtility.DisplayDialog("Setup Crafting Preview", summary, "OK");
        }

        /// <summary>CraftPanel.prefab を UIRoot.prefab の CraftUI Canvas 配下に入れ、UiRouter._craftUI に配線する。</summary>
        private static bool IntegrateCraftUiIntoUIRoot()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPrefabPath) == null)
            {
                Debug.LogError(
                    $"[SetupCraftingPreviewScene] UIRoot.prefab が見つかりません: {UIRootPrefabPath}"
                );
                return false;
            }
            var craftPanelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CraftPanelPrefabPath);
            if (craftPanelAsset == null)
            {
                Debug.LogError(
                    $"[SetupCraftingPreviewScene] CraftPanel.prefab が見つかりません: {CraftPanelPrefabPath}"
                );
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(UIRootPrefabPath);
            try
            {
                var router = root.GetComponentInChildren<UiRouter>(true);
                if (router == null)
                {
                    Debug.LogError("[SetupCraftingPreviewScene] UIRoot に UiRouter がありません。");
                    return false;
                }

                // 既存 CraftUI があれば作り直す(冪等)。
                var existing = root.transform.Find(CraftUiName);
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                // CraftUI Canvas(操作パネルと同じ sortingOrder=10)
                var craftUi = new GameObject(CraftUiName);
                craftUi.transform.SetParent(root.transform, false);
                var canvas = craftUi.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = craftUi.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                craftUi.AddComponent<GraphicRaycaster>();

                // CraftPanel をネストプレハブとして配置し、全画面ストレッチにする。
                var craftPanel = (GameObject)
                    PrefabUtility.InstantiatePrefab(craftPanelAsset, craftUi.transform);
                if (craftPanel.GetComponent<RectTransform>() is RectTransform rt)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                // UiRouter._craftUI に配線(他パネルと同じくパネル本体を登録し、UiRouter が SetActive で出し入れ)。
                var so = new SerializedObject(router);
                var prop = so.FindProperty("_craftUI");
                if (prop == null)
                {
                    Debug.LogError(
                        "[SetupCraftingPreviewScene] UiRouter._craftUI が見つかりません。"
                    );
                    return false;
                }
                prop.objectReferenceValue = craftPanel;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, UIRootPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Camera + EventSystem + FieldDevBootstrap + CraftPreviewDriver だけの確認用シーンを作る。</summary>
        private static void CreateCraftingPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );

            // 常駐一式(UIRoot 含む)を Title 非経由で生成 + テスト品シード(調合素材を確保)。
            var boot = new GameObject("DevBootstrap");
            boot.AddComponent<FieldDevBootstrap>();

            // 起動時に調合UIを開くプレビュー駆動役。
            var driver = new GameObject("CraftPreviewDriver");
            driver.AddComponent<CraftPreviewDriver>();

            // UI 入力用 EventSystem(開発シーンは Title を経由しないため自前で置く。UI_ConversationPreview と同様)。
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            Directory.CreateDirectory(Path.GetDirectoryName(CraftingPreviewScenePath));
            EditorSceneManager.SaveScene(scene, CraftingPreviewScenePath);
        }

        /// <summary>UI_CraftingPreview を Build Settings に追加する(既にあれば何もしない)。</summary>
        private static void RegisterBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == CraftingPreviewScenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(CraftingPreviewScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
