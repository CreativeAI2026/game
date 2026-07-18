using System.Linq;
using CreativeAI.UI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// タブの旧構造(_tabEntries に icon/label しか無く definition=NULL)を、現行の TabDefinition 配線へ移行する。
    /// インベントリは 装備品/食材/大事なもの の3定義(武器タブ除去・spec §2)、キャラクターは
    /// ステータス/武器/装備品/即時使用食材 の4定義(view維持)。定義がアイコンを持つので配線=アイコン表示になる。
    /// </summary>
    public static class Area01UITabMigration
    {
        private const string Inv =
            "Assets/_Project/Features/Inventory/Data/InventoryTabDefinition/";
        private const string Chr = "Assets/_Project/Features/Inventory/Data/TabDefinition/";

        [MenuItem("Tools/CreativeAI/UI/Migrate Tab Definitions")]
        public static void Migrate()
        {
            // インベントリ: 3タブ(武器除去 + アイコン)。base Inventory.prefab に設定 → 全インスタンスへ波及。
            Wire(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/Inventory.prefab",
                _ => true, // このprefabのTabGroupは1つ
                new[]
                {
                    Inv + "EquipmentTabDefinition.asset",
                    Inv + "FoodTabDefinition.asset",
                    Inv + "ImpotantTabDefinition.asset",
                }
            );

            // キャラクター: CharacterPanel 直下の View切替 TabGroup。view を保持しつつ4定義を配線。
            Wire(
                "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab",
                tg => tg.transform.parent != null && tg.transform.parent.name == "CharacterPanel",
                new[]
                {
                    Chr + "StatsTabDefinition.asset",
                    Chr + "WeaponTabDefinition.asset",
                    Chr + "EquipTabDefinition.asset",
                    Chr + "ConsumableDefinition.asset",
                }
            );
        }

        private static void Wire(
            string prefabPath,
            System.Func<TabGroup, bool> pick,
            string[] defPaths
        )
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TabGroup tg = root.GetComponentsInChildren<TabGroup>(true).FirstOrDefault(pick);
                if (tg == null)
                {
                    Debug.LogError($"[MigrateTabs] TabGroup が見つかりません: {prefabPath}");
                    return;
                }

                var so = new SerializedObject(tg);
                SerializedProperty entries = so.FindProperty("_tabEntries");

                // 既存 view を退避(View切替タブは view を維持する)。
                var views = new Object[entries.arraySize];
                for (int i = 0; i < entries.arraySize; i++)
                    views[i] = entries
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("view")
                        ?.objectReferenceValue;

                entries.arraySize = defPaths.Length;
                for (int i = 0; i < defPaths.Length; i++)
                {
                    var def = AssetDatabase.LoadAssetAtPath<TabDefinition>(defPaths[i]);
                    if (def == null)
                    {
                        Debug.LogError($"[MigrateTabs] 定義が見つかりません: {defPaths[i]}");
                        continue;
                    }

                    SerializedProperty e = entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("definition").objectReferenceValue = def;
                    SerializedProperty vp = e.FindPropertyRelative("view");
                    if (vp != null)
                        vp.objectReferenceValue = i < views.Length ? views[i] : null;

                    Debug.Log(
                        $"[MigrateTabs] {System.IO.Path.GetFileName(prefabPath)} [{i}] def={def.name} view={(vp?.objectReferenceValue != null ? vp.objectReferenceValue.name : "-")}"
                    );
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[MigrateTabs] 保存: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
