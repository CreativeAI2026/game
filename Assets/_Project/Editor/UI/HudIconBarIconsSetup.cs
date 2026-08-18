using CreativeAI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// 右上アイコンバー(<see cref="HudIconBar"/>)の各ボタンへ正式アイコンを事前設定するツール。
    /// UIRoot.prefab を直接編集し、ボタン自身の Image を表示とクリック判定に共用する。
    /// HudIconBar の _characterButton / _inventoryButton / _saveButton 参照からボタンを辿り、
    /// 旧プレースホルダー用の空 Label は除去する。冪等。
    /// </summary>
    public static class HudIconBarIconsSetup
    {
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string IconDir = "Assets/_Project/Art/UI/HUD/IconBar";

        [MenuItem("Tools/CreativeAI/UI/Set HudIconBar Icons")]
        public static void SetIcons()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UIRootPath) == null)
            {
                Debug.LogError($"[HudIconBarIcons] UIRoot Prefab が見つかりません: {UIRootPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(UIRootPath);
            try
            {
                var bar = root.GetComponentInChildren<HudIconBar>(true);
                if (bar == null)
                {
                    Debug.LogError("[HudIconBarIcons] HudIconBar が UIRoot に見つかりません。");
                    return;
                }

                var so = new SerializedObject(bar);
                int applied = 0;
                int removed = 0;
                applied += Apply(so, "_characterButton", "Icon_Character", ref removed);
                applied += Apply(so, "_inventoryButton", "Icon_Inventory", ref removed);
                // Save専用画像が決まるまでMap画像を暫定利用する。
                applied += Apply(so, "_saveButton", "Icon_Map", ref removed);

                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
                Debug.Log(
                    $"[HudIconBarIcons] アイコン事前設定完了({applied}/3、不要要素を{removed}件削除)。"
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static int Apply(
            SerializedObject barSo,
            string buttonField,
            string iconName,
            ref int removed
        )
        {
            var button = barSo.FindProperty(buttonField)?.objectReferenceValue as Button;
            if (button == null)
            {
                Debug.LogWarning($"[HudIconBarIcons] {buttonField} が未割当です。スキップ。");
                return 0;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/{iconName}.png");
            if (sprite == null)
            {
                Debug.LogError(
                    $"[HudIconBarIcons] スプライトが見つかりません: {IconDir}/{iconName}.png(Sprite としてインポートされているか確認)"
                );
                return 0;
            }

            var iconImage = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (iconImage == null)
            {
                Debug.LogWarning(
                    $"[HudIconBarIcons] {button.name} にアイコン用 Image が見つかりません。スキップ。"
                );
                return 0;
            }

            iconImage.sprite = sprite;
            iconImage.enabled = true;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            EditorUtility.SetDirty(iconImage);
            removed += RemoveEmptyLabels(button.transform);
            return 1;
        }

        private static int RemoveEmptyLabels(Transform buttonTransform)
        {
            int removed = 0;
            for (int i = buttonTransform.childCount - 1; i >= 0; i--)
            {
                var child = buttonTransform.GetChild(i);
                if (child.name != "Label")
                    continue;

                Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            return removed;
        }
    }
}
