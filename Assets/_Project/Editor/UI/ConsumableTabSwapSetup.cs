using CreativeAI.UI.CharacterUI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// CharacterPanel の「即時使用食材」タブ(ConsumableView)を、装備品タブの流用(<see cref="EquipmentViewController"/>)から
    /// 専用の <see cref="QuickFoodViewController"/> に差し替えるツール。
    /// 旧コントローラの参照(スロット親 / 詳細パネル / 所持品グリッド)を読み取り、新コントローラへ移設する。
    /// 冪等: 既に QuickFoodViewController なら何もしない。
    /// </summary>
    public static class ConsumableTabSwapSetup
    {
        private const string CharacterPanelPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
        private const string ViewName = "ConsumableView";

        [MenuItem("Tools/CreativeAI/UI/Swap Consumable Tab To QuickFood")]
        public static void Swap()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPanelPath) == null)
            {
                Debug.LogError(
                    $"[ConsumableSwap] CharacterPanel Prefab が見つかりません: {CharacterPanelPath}"
                );
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(CharacterPanelPath);
            try
            {
                var view = FindByName(root.transform, ViewName);
                if (view == null)
                {
                    Debug.LogError(
                        $"[ConsumableSwap] '{ViewName}' が CharacterPanel に見つかりません。"
                    );
                    return;
                }

                if (view.GetComponent<QuickFoodViewController>() != null)
                {
                    Debug.Log("[ConsumableSwap] 既に QuickFoodViewController 済み(冪等スキップ)。");
                    return;
                }

                var old = view.GetComponent<EquipmentViewController>();
                Object slotsRoot = null,
                    detailPanel = null,
                    inventory = null;
                if (old != null)
                {
                    var oldSo = new SerializedObject(old);
                    slotsRoot = oldSo.FindProperty("_equipmentSlotsRoot")?.objectReferenceValue;
                    detailPanel = oldSo.FindProperty("_detailPanel")?.objectReferenceValue;
                    inventory = oldSo.FindProperty("_inventory")?.objectReferenceValue;
                    Object.DestroyImmediate(old);
                }

                var next = view.gameObject.AddComponent<QuickFoodViewController>();
                var so = new SerializedObject(next);
                so.FindProperty("_slotsRoot").objectReferenceValue = slotsRoot;
                so.FindProperty("_detailPanel").objectReferenceValue = detailPanel;
                so.FindProperty("_inventory").objectReferenceValue = inventory;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanelPath);
                Debug.Log(
                    $"[ConsumableSwap] 差し替え完了(slotsRoot={(slotsRoot != null)}, detail={(detailPanel != null)}, inventory={(inventory != null)})。"
                        + (
                            slotsRoot == null || detailPanel == null || inventory == null
                                ? "\n⚠ 未解決の参照あり。CharacterPanel を開いて QuickFoodViewController の空欄を配線してください。"
                                : ""
                        )
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }
    }
}
