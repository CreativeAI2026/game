using System.Text;
using CreativeAI.UI;
using CreativeAI.UI.InventoryUI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// インベントリ系 Prefab の TabGroup が実際にどんなタブ(definition/カテゴリ/view)を持つかを
    /// 読み取り専用でダンプする診断ツール。武器タブ残骸の在処と、旧構造(_tabEntries)の実効値を特定する。
    /// PrefabUtility.LoadPrefabContents で入れ子 Prefab の override も解決した「実効状態」を見る。
    /// </summary>
    public static class Area01UIDiagnostics
    {
        private static readonly string[] Prefabs =
        {
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab",
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/Inventory.prefab",
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/TabGroup.prefab",
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab",
        };

        [MenuItem("Tools/CreativeAI/UI/Diagnose Inventory Tabs")]
        public static void DiagnoseInventoryTabs()
        {
            var sb = new StringBuilder("\n===== Tab Diagnostics =====\n");
            foreach (string path in Prefabs)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    sb.AppendLine($"### {path}");
                    var groups = root.GetComponentsInChildren<TabGroup>(true);
                    sb.AppendLine($"  TabGroup: {groups.Length}個");
                    foreach (TabGroup g in groups)
                    {
                        var so = new SerializedObject(g);
                        SerializedProperty entries = so.FindProperty("_tabEntries");
                        int n = entries != null ? entries.arraySize : -1;
                        sb.AppendLine($"  ▼ [{GetPath(g.transform)}] entries={n}");
                        for (int i = 0; i < n; i++)
                        {
                            SerializedProperty e = entries.GetArrayElementAtIndex(i);
                            Object def = e.FindPropertyRelative("definition")?.objectReferenceValue;
                            Object view = e.FindPropertyRelative("view")?.objectReferenceValue;
                            string cat = def is InventoryTabDefinition inv
                                ? inv.Category.ToString()
                                : "-";
                            sb.AppendLine(
                                $"      [{i}] definition={(def != null ? def.name : "NULL")} (cat={cat}) view={(view != null ? view.name : "NULL")}"
                            );
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log(sb.ToString());
        }

        private static string GetPath(Transform t)
        {
            string s = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }

        /// <summary>CharacterPanel の WeaponView 配下を、階層・コンポーネント・TMPテキスト付きで全ダンプする。</summary>
        [MenuItem("Tools/CreativeAI/UI/Dump WeaponView Tree")]
        public static void DumpWeaponView()
        {
            const string path =
                "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform wv = FindByName(root.transform, "WeaponView");
                if (wv == null)
                {
                    Debug.LogError("[DumpWV] WeaponView が見つかりません");
                    return;
                }
                var sb = new StringBuilder("\n===== WeaponView Tree =====\n");
                Dump(wv, 0, sb);
                Debug.Log(sb.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            var comps = new System.Collections.Generic.List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null)
                    continue;
                string cn = c.GetType().Name;
                if (c is TMPro.TMP_Text tmp)
                {
                    string txt = tmp.text?.Replace("\n", " / ");
                    if (txt != null && txt.Length > 24)
                        txt = txt.Substring(0, 24) + "…";
                    cn += $"(\"{txt}\")";
                }
                if (cn != "RectTransform" && cn != "CanvasRenderer")
                    comps.Add(cn);
            }
            string rect = "";
            if (t is RectTransform rt)
                rect =
                    $"  anc[{rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##}-{rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}] pos({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0}) size({rt.sizeDelta.x:0}x{rt.sizeDelta.y:0})";
            sb.AppendLine($"{indent}{t.name}  [{string.Join(", ", comps)}]{rect}");
            foreach (Transform c in t)
                Dump(c, depth + 1, sb);
        }

        /// <summary>CharacterPanel 全体の階層をアンカー付きで浅くダンプ(重なり診断用)。</summary>
        [MenuItem("Tools/CreativeAI/UI/Dump CharacterPanel Layout")]
        public static void DumpCharacterPanel()
        {
            const string path =
                "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sb = new StringBuilder("\n===== CharacterPanel Layout =====\n");
                DumpShallow(root.transform, 0, sb, 3);
                Debug.Log(sb.ToString());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DumpShallow(Transform t, int depth, StringBuilder sb, int maxDepth)
        {
            Dump1(t, depth, sb);
            if (depth >= maxDepth)
                return;
            foreach (Transform c in t)
                DumpShallow(c, depth + 1, sb, maxDepth);
        }

        private static void Dump1(Transform t, int depth, StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            string rect = "";
            if (t is RectTransform rt)
                rect =
                    $"  anc[{rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##}-{rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}] pos({rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0}) size({rt.sizeDelta.x:0}x{rt.sizeDelta.y:0}) active={t.gameObject.activeSelf}";
            sb.AppendLine($"{indent}{t.name}{rect}");
        }

        private static Transform FindByName(Transform t, string name)
        {
            if (t.name == name)
                return t;
            foreach (Transform c in t)
            {
                Transform r = FindByName(c, name);
                if (r != null)
                    return r;
            }
            return null;
        }
    }
}
