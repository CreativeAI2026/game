using CreativeAI.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// dev-bootstrap 導入ツール。
    /// ① Create Config: 常駐 Prefab 参照(UIRoot / ConversationView)を集約した ResidentBootstrapConfig を
    ///    Resources に作る。
    /// ② Field Surgery: Field_Area01 から自前の常駐コピー(Canvas=HUD/パネル一式・InventoryManager)を撤去し、
    ///    FieldDevBootstrap を1つ置く。以降そのシーンは直接 Play で常駐UIを生成する。
    /// </summary>
    public static class FieldDevBootstrapSetup
    {
        private const string ConfigPath = "Assets/_Project/Resources/ResidentBootstrapConfig.asset";
        private const string UIRootPrefab =
            "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string ConversationPrefab =
            "Assets/_Project/Features/UI/ConversationUI/Prefabs/ConversationView.prefab";
        private const string Area01 = "Assets/_Project/Scenes/Field/Field_Area01.unity";

        [MenuItem("Tools/CreativeAI/UI/Create Resident Bootstrap Config")]
        public static void CreateConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ResidentBootstrapConfig>(ConfigPath);
            bool isNew = cfg == null;
            if (isNew)
                cfg = ScriptableObject.CreateInstance<ResidentBootstrapConfig>();

            cfg.uiRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPrefab);
            cfg.conversationViewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ConversationPrefab
            );

            if (isNew)
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            else
                EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[BootstrapConfig] 保存: {ConfigPath} (uiRoot={(cfg.uiRootPrefab != null)}, conversation={(cfg.conversationViewPrefab != null)})"
            );
        }

        [MenuItem("Tools/CreativeAI/UI/Field Devscene Surgery (Area01)")]
        public static void FieldSurgery()
        {
            Scene scene = SceneManager.GetSceneByPath(Area01);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(Area01, OpenSceneMode.Additive);

            try
            {
                int removed = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Canvas" || root.name == "InventoryManager")
                    {
                        Debug.Log($"[FieldSurgery] 撤去: {root.name}");
                        Object.DestroyImmediate(root);
                        removed++;
                    }
                }

                // FieldDevBootstrap を1つ設置(既に在れば追加しない)。
                bool hasBootstrap = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<FieldDevBootstrap>() != null)
                        hasBootstrap = true;
                if (!hasBootstrap)
                {
                    var go = new GameObject("FieldDevBootstrap");
                    go.AddComponent<FieldDevBootstrap>();
                    SceneManager.MoveGameObjectToScene(go, scene);
                    Debug.Log("[FieldSurgery] FieldDevBootstrap を設置");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[FieldSurgery] 完了: {removed} 撤去 + bootstrap 設置");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
