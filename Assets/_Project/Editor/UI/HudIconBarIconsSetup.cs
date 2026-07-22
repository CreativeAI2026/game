using CreativeAI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools.UI
{
    /// <summary>
    /// 右上アイコンバー(<see cref="HudIconBar"/>)の各ボタンのアイコンを、プレースホルダ(丸)から
    /// 本物のアイコン(Art/UI/Icons/IconBar)へ差し替えるツール。UIRoot.prefab を直接編集する。
    /// HudIconBar の _characterButton / _inventoryButton / _saveButton 参照からボタンを辿り、
    /// その子 "Icon"(Image)のスプライトを設定する。冪等。
    /// </summary>
    public static class HudIconBarIconsSetup
    {
        private const string UIRootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private const string IconDir = "Assets/_Project/Art/UI/Icons/IconBar";

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
                applied += Apply(so, "_characterButton", "Icon_Character");
                applied += Apply(so, "_inventoryButton", "Icon_Inventory");
                applied += Apply(so, "_saveButton", "Icon_Save");

                PrefabUtility.SaveAsPrefabAsset(root, UIRootPath);
                Debug.Log($"[HudIconBarIcons] アイコン差し替え完了({applied}/3)。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static int Apply(SerializedObject barSo, string buttonField, string iconName)
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

            // ボタン直下の "Icon"(Image)を優先。無ければボタンの targetGraphic → 自身の Image。
            var iconImage = FindIconImage(button);
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
            return 1;
        }

        private static Image FindIconImage(Button button)
        {
            foreach (var t in button.GetComponentsInChildren<Transform>(true))
            {
                if (t == button.transform)
                    continue;
                if (t.name == "Icon" && t.TryGetComponent<Image>(out var img))
                    return img;
            }
            if (button.targetGraphic is Image target)
                return target;
            return button.GetComponent<Image>();
        }
    }
}
