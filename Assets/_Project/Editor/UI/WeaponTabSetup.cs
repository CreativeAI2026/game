using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// キャラクター画面の武器UIを spec §1.1 に合わせる。
    /// Step1: 非spec要素(星レーティング/精錬ランク/パッシブ特殊効果/武器変更ボタン)を削除し、
    ///        剣/弓/鎌の TabDefinition(丸アイコンのプレースホルダ)を作成する。
    /// Step2(別メソッド): WeaponView に3タブの TabGroup と表示コントローラーを組む。
    /// </summary>
    public static class WeaponTabSetup
    {
        private const string CharacterPanel =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
        private const string CirclePath =
            "Assets/_Project/Art/UI/Icons/TabIcons/Placeholder_Circle.png";
        private const string DefDir = "Assets/_Project/Features/Inventory/Data/TabDefinition/";

        // WeaponView/DetailPanel から消す非spec要素(名前で特定)。
        private static readonly string[] RemoveNames =
        {
            "WeaponType", // 片手剣 ★★★★★ (星レーティング)
            "Refinement", // 精錬ランク Lv.1
            "PassiveTitle", // パッシブ「…」(特殊効果)
            "PassiveDesc", // 攻撃時 会心率… (特殊効果)
            "ChangeWeaponButton", // ▶ 武器を変更
        };

        [MenuItem("Tools/CreativeAI/UI/Weapon Step1 (clean + tab defs)")]
        public static void Step1()
        {
            Sprite circle = EnsureCircleSprite();
            CreateWeaponTabDef("SwordTabDefinition", "剣", circle);
            CreateWeaponTabDef("BowTabDefinition", "弓", circle);
            CreateWeaponTabDef("KamaTabDefinition", "鎌", circle);
            AssetDatabase.SaveAssets();

            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPanel);
            try
            {
                Transform wv = FindByName(root.transform, "WeaponView");
                if (wv == null)
                {
                    Debug.LogError("[WeaponStep1] WeaponView が見つかりません");
                    return;
                }

                int removed = 0;
                foreach (string name in RemoveNames)
                {
                    Transform t = FindByName(wv, name);
                    if (t != null)
                    {
                        Debug.Log($"[WeaponStep1] 削除: {name}");
                        Object.DestroyImmediate(t.gameObject);
                        removed++;
                    }
                    else
                    {
                        Debug.LogWarning($"[WeaponStep1] 見つからず(スキップ): {name}");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanel);
                Debug.Log(
                    $"[WeaponStep1] 完了: {removed}/{RemoveNames.Length} 削除, 丸アイコン+剣/弓/鎌の定義を作成"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite EnsureCircleSprite()
        {
            if (!System.IO.File.Exists(CirclePath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var center = new Vector2(size / 2f, size / 2f);
                float radius = size / 2f - 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    tex.SetPixel(x, y, d <= radius ? Color.white : new Color(1, 1, 1, 0));
                }
                tex.Apply();
                System.IO.File.WriteAllBytes(CirclePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(CirclePath);
            }

            var imp = AssetImporter.GetAtPath(CirclePath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        }

        private static void CreateWeaponTabDef(string assetName, string label, Sprite icon)
        {
            string path = DefDir + assetName + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<TabDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TabDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            var so = new SerializedObject(def);
            SerializedProperty iconProp = so.FindProperty("_icon");
            if (iconProp != null)
                iconProp.objectReferenceValue = icon;
            SerializedProperty labelProp = so.FindProperty("_label");
            if (labelProp != null && labelProp.propertyType == SerializedPropertyType.String)
                labelProp.stringValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[WeaponStep1] 定義作成: {assetName} (label={label})");
        }

        [MenuItem("Tools/CreativeAI/UI/Weapon Step2 (tabs + controller)")]
        public static void Step2()
        {
            var sword = AssetDatabase.LoadAssetAtPath<TabDefinition>(
                DefDir + "SwordTabDefinition.asset"
            );
            var bow = AssetDatabase.LoadAssetAtPath<TabDefinition>(
                DefDir + "BowTabDefinition.asset"
            );
            var kama = AssetDatabase.LoadAssetAtPath<TabDefinition>(
                DefDir + "KamaTabDefinition.asset"
            );
            var tabGroupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/TabGroup.prefab"
            );
            if (sword == null || bow == null || kama == null || tabGroupPrefab == null)
            {
                Debug.LogError(
                    "[WeaponStep2] 定義/TabGroup.prefab が見つかりません。先に Step1 を実行してください。"
                );
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPanel);
            try
            {
                // 残っているパッシブ(特殊効果)要素を全撤去。
                foreach (string name in new[] { "PassiveTitle", "PassiveDesc" })
                {
                    Transform t;
                    while ((t = FindByName(root.transform, name)) != null)
                    {
                        Debug.Log($"[WeaponStep2] 残パッシブ削除: {name}");
                        Object.DestroyImmediate(t.gameObject);
                    }
                }

                Transform wv = FindByName(root.transform, "WeaponView");
                Transform detail = FindByName(wv, "DetailPanel");
                var weaponName = FindByName(wv, "WeaponName")?.GetComponent<TMP_Text>();
                var weaponStats = FindByName(wv, "WeaponStats")?.GetComponent<TMP_Text>();

                // TabGroup を DetailPanel の先頭に生成し、剣/弓/鎌の3タブを設定(view=null)。
                var tgGo = (GameObject)
                    PrefabUtility.InstantiatePrefab(tabGroupPrefab, detail != null ? detail : wv);
                tgGo.name = "WeaponTabGroup";
                tgGo.transform.SetSiblingIndex(0);
                var tg = tgGo.GetComponent<TabGroup>();
                var so = new SerializedObject(tg);
                SerializedProperty entries = so.FindProperty("_tabEntries");
                entries.arraySize = 3;
                var defs = new[] { sword, bow, kama };
                for (int i = 0; i < 3; i++)
                {
                    SerializedProperty e = entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("definition").objectReferenceValue = defs[i];
                    SerializedProperty vp = e.FindPropertyRelative("view");
                    if (vp != null)
                        vp.objectReferenceValue = null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                // 表示コントローラーを WeaponView に付け、参照を配線。
                var ctrl =
                    wv.GetComponent<WeaponTabViewController>()
                    ?? wv.gameObject.AddComponent<WeaponTabViewController>();
                var cso = new SerializedObject(ctrl);
                cso.FindProperty("_tabGroup").objectReferenceValue = tg;
                cso.FindProperty("_weaponName").objectReferenceValue = weaponName;
                cso.FindProperty("_weaponStats").objectReferenceValue = weaponStats;
                cso.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanel);
                Debug.Log(
                    $"[WeaponStep2] 完了: 剣/弓/鎌タブ生成+コントローラー配線。weaponName={(weaponName != null)} weaponStats={(weaponStats != null)}"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/CreativeAI/UI/Weapon Step3 (left vertical tabs)")]
        public static void Step3LeftVertical()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPanel);
            try
            {
                Transform wtg = FindByName(root.transform, "WeaponTabGroup");
                Transform wv = FindByName(root.transform, "WeaponView");
                if (wtg == null || wv == null)
                {
                    Debug.LogError("[WeaponStep3] WeaponTabGroup / WeaponView が見つかりません");
                    return;
                }

                // DetailPanel の縦レイアウト管理下から出し、WeaponView 直下へ(自由配置にする)。
                wtg.SetParent(wv, false);

                // 横並び(HorizontalLayoutGroup)→ 縦並び(VerticalLayoutGroup)へ差し替え。
                var h = wtg.GetComponent<HorizontalLayoutGroup>();
                if (h != null)
                    Object.DestroyImmediate(h);
                var v =
                    wtg.GetComponent<VerticalLayoutGroup>()
                    ?? wtg.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 12;
                v.childControlWidth = true;
                v.childControlHeight = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                v.childAlignment = TextAnchor.UpperCenter;

                // 画面左側に縦に配置(左端アンカー・縦中央)。値は目安、Editorで微調整可。
                var rt = wtg.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(40f, 0f);
                rt.sizeDelta = new Vector2(150f, 430f);

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanel);
                Debug.Log(
                    "[WeaponStep3] WeaponTabGroup を WeaponView 直下・左・縦レイアウトに変更"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/CreativeAI/UI/Character Fix Overlap + drop Weapon Model")]
        public static void FixOverlapAndModel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPanel);
            try
            {
                // #1: WeaponView の右側武器表示(ModelArea=モデルのプレースホルダ)を撤去。
                Transform wv = FindByName(root.transform, "WeaponView");
                Transform modelArea = wv != null ? wv.Find("ModelArea") : null;
                if (modelArea != null)
                {
                    Debug.Log("[CharFix] WeaponView/ModelArea 撤去");
                    Object.DestroyImmediate(modelArea.gameObject);
                }

                // #2: 各ビューの DetailPanel(右・高さ910)が上部の TabGroup(右上・アイコンバー)と
                //     重なるので、高さを下げて上端をバーの下に逃がす。
                foreach (
                    string viewName in new[]
                    {
                        "StatsView",
                        "WeaponView",
                        "EquipmentView",
                        "ConsumableView",
                    }
                )
                {
                    Transform view = FindByName(root.transform, viewName);
                    Transform dp = view != null ? view.Find("DetailPanel") : null;
                    if (dp is RectTransform rt)
                    {
                        Vector2 s = rt.sizeDelta;
                        s.y = 760f;
                        rt.sizeDelta = s;
                        Debug.Log($"[CharFix] {viewName}/DetailPanel 高さ→760");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanel);
                Debug.Log("[CharFix] 完了");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/CreativeAI/UI/Weapon SectionTitle shows 剣/弓/鎌")]
        public static void WireSectionTitleToWeaponName()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CharacterPanel);
            try
            {
                Transform wv = FindByName(root.transform, "WeaponView");
                var ctrl = wv != null ? wv.GetComponent<WeaponTabViewController>() : null;
                Transform detail = wv != null ? wv.Find("DetailPanel") : null;
                Transform st = detail != null ? detail.Find("SectionTitle") : null;
                var title = st != null ? st.GetComponent<TMP_Text>() : null;
                if (ctrl == null || title == null)
                {
                    Debug.LogError(
                        "[WeaponTitle] WeaponTabViewController / SectionTitle が見つかりません"
                    );
                    return;
                }

                // コントローラーの表示先(_weaponName)を SectionTitle に向ける。
                var so = new SerializedObject(ctrl);
                so.FindProperty("_weaponName").objectReferenceValue = title;
                so.ApplyModifiedPropertiesWithoutUndo();

                // 編集時の見た目も剣(初期タブ)に合わせておく(実行時はコントローラーが上書き)。
                title.text = "剣";

                PrefabUtility.SaveAsPrefabAsset(root, CharacterPanel);
                Debug.Log("[WeaponTitle] SectionTitle をタブ武器名(剣/弓/鎌)表示に配線");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
