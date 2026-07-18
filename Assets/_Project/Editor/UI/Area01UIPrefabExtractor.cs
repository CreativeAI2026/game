using System.IO;
using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// Field_Area01 に素組みされている各UIパネルを Prefab 化(単一ソース化)し、
    /// 常駐 UIRoot.prefab に載せて Title フローから使える状態にする Editor ツール。
    /// 手書き YAML を避け、Unity の Prefab システムに正しくシリアライズさせるための道具。
    /// 会話UI(ConversationView)と同じ「Prefab化 → 常駐に載せる」方式を他パネルへ広げる。
    ///
    /// 使い方(必ずこの順に):
    ///   ① [1. Extract] … Field_Area01/Canvas/Panels 配下の4パネルを Prefab 化する。
    ///        実行後、Field_Area01 側のパネルは Prefab インスタンスに変わる(＝単一ソース化)。
    ///   ② 目視 + [Tools/CreativeAI/UI/Validate Area01 UI] で配線が保たれているか確認。
    ///   ③ [2. Wire Into UIRoot] … 常駐3パネル(Character/Inventory/Save)を UIRoot.prefab の
    ///        受け皿 Canvas に載せ替え、UiRouter を再配線する。CraftPanel は調合場所でのみ開く
    ///        ため常駐には載せない(spec §5)。実行後は UIRoot のレイアウトを必ず目視確認する
    ///        (親 Canvas が変わるので RectTransform の位置調整が要る場合がある)。
    /// </summary>
    public static class Area01UIPrefabExtractor
    {
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01.unity";
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";

        /// <summary>Field_Area01/Canvas/Panels 配下の素組みパネル → 出力先/常駐の受け皿。</summary>
        private struct PanelTarget
        {
            public string SceneName; // Panels 配下の GameObject 名(一意)
            public string PrefabPath; // 保存先 Prefab
            public string UIRootCanvas; // UIRoot 内の受け皿 Canvas 名(null=常駐に載せない=Field専用)
            public string UiRouterField; // UiRouter の対応 SerializeField 名(null=配線しない)
        }

        private static readonly PanelTarget[] Targets =
        {
            new PanelTarget
            {
                SceneName = "CharacterPanel",
                PrefabPath =
                    "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab",
                UIRootCanvas = "CharacterUI",
                UiRouterField = "_characterUI",
            },
            new PanelTarget
            {
                SceneName = "InventoryPanel",
                PrefabPath =
                    "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab",
                UIRootCanvas = "InventoryUI",
                UiRouterField = "_inventoryUI",
            },
            new PanelTarget
            {
                SceneName = "SaveDialog",
                PrefabPath = "Assets/_Project/Features/UI/SaveDialog/Prefabs/SaveDialog.prefab",
                UIRootCanvas = "SaveUI",
                UiRouterField = "_saveUI",
            },
            new PanelTarget
            {
                // 調合UIは「調合場所でのみ」開く(spec §5)。常駐には載せず Prefab 化だけ行う。
                SceneName = "CraftPanel",
                PrefabPath = "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab",
                UIRootCanvas = null,
                UiRouterField = null,
            },
        };

        // ---- ① 抽出: シーンの素組みパネルを Prefab 化する -------------------------------------

        [MenuItem("Tools/CreativeAI/UI/1. Extract Area01 Panels To Prefabs")]
        public static void ExtractPanels()
        {
            Scene scene = OpenSceneAdditiveIfNeeded(out bool opened);
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    Debug.LogError($"[Extract] {ScenePath} を開けませんでした。");
                    return;
                }

                Transform panels = FindPanelsRoot(scene);
                if (panels == null)
                {
                    Debug.LogError(
                        "[Extract] Field_Area01 に Canvas/Panels が見つかりません。シーン構造が変わっていないか確認してください。"
                    );
                    return;
                }

                int ok = 0;
                foreach (PanelTarget t in Targets)
                {
                    Transform child = panels.Find(t.SceneName);
                    if (child == null)
                    {
                        Debug.LogWarning(
                            $"[Extract] '{t.SceneName}' が Panels 配下に見つかりません。スキップします。"
                        );
                        continue;
                    }

                    EnsureFolder(Path.GetDirectoryName(t.PrefabPath).Replace('\\', '/'));
                    GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                        child.gameObject,
                        t.PrefabPath,
                        InteractionMode.AutomatedAction,
                        out bool success
                    );
                    if (success && prefab != null)
                    {
                        ok++;
                        Debug.Log($"[Extract] {t.SceneName} → {t.PrefabPath}");
                    }
                    else
                    {
                        Debug.LogError($"[Extract] {t.SceneName} の Prefab 化に失敗しました。");
                    }
                }

                // Prefab 接続(シーン側インスタンス化)を永続化する。
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"[Extract] 完了: {ok}/{Targets.Length} パネルを Prefab 化しました。"
                        + " 次に目視 + 'Validate Area01 UI' で確認し、問題なければ '2. Wire Into UIRoot' を実行してください。"
                );
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---- ② 配線: 常駐3パネルを UIRoot.prefab に載せ替え、UiRouter を再配線 --------------

        [MenuItem("Tools/CreativeAI/UI/2. Wire Panels Into UIRoot (verify layout after)")]
        public static void WireIntoUIRoot()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UIRootPath);
            if (root == null)
            {
                Debug.LogError($"[Wire] {UIRootPath} を読み込めませんでした。");
                return;
            }

            try
            {
                var router = root.GetComponentInChildren<UiRouter>(true);
                SerializedObject routerSo = router != null ? new SerializedObject(router) : null;
                if (router == null)
                    Debug.LogWarning(
                        "[Wire] UIRoot に UiRouter が見つかりません。UiRouter 配線はスキップします。"
                    );

                int wired = 0;
                foreach (PanelTarget t in Targets)
                {
                    if (t.UIRootCanvas == null)
                        continue; // Craft は Field 専用

                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(t.PrefabPath);
                    if (asset == null)
                    {
                        Debug.LogError(
                            $"[Wire] {t.PrefabPath} がありません。先に '1. Extract' を実行してください。"
                        );
                        continue;
                    }

                    Transform canvas = FindDeep(root.transform, t.UIRootCanvas);
                    if (canvas == null)
                    {
                        Debug.LogWarning(
                            $"[Wire] UIRoot に受け皿 Canvas '{t.UIRootCanvas}' が見つかりません。スキップします。"
                        );
                        continue;
                    }

                    // 既存スタブ(受け皿 Canvas 配下の子)を除去してから新パネルを載せる。
                    for (int i = canvas.childCount - 1; i >= 0; i--)
                        Object.DestroyImmediate(canvas.GetChild(i).gameObject);

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas);
                    inst.name = asset.name; // "(Clone)" を避ける

                    if (routerSo != null && t.UiRouterField != null)
                    {
                        SerializedProperty prop = routerSo.FindProperty(t.UiRouterField);
                        if (prop != null)
                            prop.objectReferenceValue = inst;
                        else
                            Debug.LogWarning(
                                $"[Wire] UiRouter に '{t.UiRouterField}' が見つかりません。"
                            );
                    }

                    wired++;
                    Debug.Log($"[Wire] {t.SceneName} → UIRoot/{t.UIRootCanvas}");
                }

                routerSo?.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
                AssetDatabase.SaveAssets();
                Debug.LogWarning(
                    $"[Wire] 完了: {wired} パネルを UIRoot に載せました。"
                        + " 親 Canvas が変わったため、UIRoot.prefab を開いて各パネルの RectTransform(位置/アンカー)を必ず目視確認してください。"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- helpers ----------------------------------------------------------------------

        private static Scene OpenSceneAdditiveIfNeeded(out bool opened)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            return scene;
        }

        /// <summary>Field_Area01 の Canvas/Panels を返す(見つからなければ深さ優先で "Panels" を探す)。</summary>
        private static Transform FindPanelsRoot(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Canvas")
                {
                    Transform panels = go.transform.Find("Panels");
                    if (panels != null)
                        return panels;
                }
            }
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                Transform p = FindDeep(go.transform, "Panels");
                if (p != null)
                    return p;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
                return t;
            foreach (Transform c in t)
            {
                Transform r = FindDeep(c, name);
                if (r != null)
                    return r;
            }
            return null;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
                return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
