using System;
using System.Linq;
using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreativeAI.EditorTools
{
    public static class UnifyUiParametersAndTheme
    {
        private const string ParameterPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/ItemParamaterArea.prefab";
        private const string InventoryPath =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab";
        private const string CharacterPath =
            "Assets/_Project/Features/UI/CharacterUI/Prefabs/CharacterPanel.prefab";
        private const string CraftPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab";
        private const string RootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
        private static Sprite Rounded =>
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/UI/Common/RoundedRect_White_256_R8.png"
            );

        [MenuItem("Tools/CreativeAI/UI/Preview Craft Theme %#&o")]
        private static void PreviewCraft()
        {
            if (EditorApplication.isPlaying)
                Object.FindAnyObjectByType<UiRouter>().Open(UiRouter.UiId.Craft);
        }

        [MenuItem("Tools/CreativeAI/UI/Unify Parameters And Theme %#&y")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            Edit(ParameterPath, root => StyleParameter(root.transform));
            Edit(
                InventoryPath,
                root =>
                {
                    BuildInventoryParameters(root);
                    ApplyTheme(root);
                }
            );
            Edit(CharacterPath, ApplyTheme);
            Edit(CraftPath, ApplyTheme);
            Edit(RootPath, ApplyTheme);
            Edit(
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryTabButton.prefab",
                root =>
                {
                    Paint(root.GetComponent<Image>(), UiVisualTheme.Tab);
                    Paint(
                        root.transform.Find("Selected").GetComponent<Image>(),
                        UiVisualTheme.SelectedTab
                    );
                }
            );
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[UiTheme] Inventory uses ItemParamaterArea prefab rows. Character, crafting and inventory now share parameter typography, panel colors and #FFC346 accents."
            );
        }

        private static void Edit(string path, Action<GameObject> change)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                change(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildInventoryParameters(GameObject root)
        {
            var details = root.transform.Find("MainLayout/DetailsPanel");
            var detail = details.GetComponent<ItemDetailPanel>();
            var body = details.Find("DescriptionAndStats");
            if (body.Find("Viewport") != null)
                return;
            var description = body.Find("Description");
            var legacy = body.Find("Stats");
            legacy.gameObject.SetActive(false);
            Object.DestroyImmediate(body.GetComponent<VerticalLayoutGroup>());
            var viewport = NewRect("Viewport", body);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-14, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            Vertical(content.gameObject, 12);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            description.SetParent(content, false);
            var descriptionLayout = description.GetComponent<LayoutElement>();
            descriptionLayout.minHeight = 0;
            descriptionLayout.preferredHeight = -1;
            descriptionLayout.flexibleHeight = 0;
            legacy.SetParent(content, false);
            var parameterRoot = NewRect("ParameterArea", content);
            Vertical(parameterRoot.gameObject, 8);
            var view = parameterRoot.gameObject.AddComponent<ItemParameterListView>();
            var so = new SerializedObject(view);
            var rows = so.FindProperty("_equipmentRows");
            rows.arraySize = 5;
            var names = new[] { "Attack", "Defense", "CriticalRate", "CriticalDamage", "MaxHP" };
            var labels = new[] { "攻撃", "防御", "会心率", "会心ダメージ", "最大HP" };
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParameterPath);
            for (int i = 0; i < names.Length; i++)
            {
                var row = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parameterRoot);
                row.name = names[i];
                row.transform.Find("Label").GetComponent<TMP_Text>().text = labels[i];
                AssignRow(rows.GetArrayElementAtIndex(i), row);
            }
            var recovery = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parameterRoot);
            recovery.name = "Recovery";
            recovery.transform.Find("Label").GetComponent<TMP_Text>().text = "回復量";
            AssignRow(so.FindProperty("_recoveryRow"), recovery);
            so.ApplyModifiedPropertiesWithoutUndo();
            var detailSo = new SerializedObject(detail);
            detailSo.FindProperty("_parameters").objectReferenceValue = view;
            detailSo.ApplyModifiedPropertiesWithoutUndo();
            var scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var inventoryScrollbar = root.GetComponentInChildren<InventoryView>(true)
                .GetComponent<ScrollRect>()
                .verticalScrollbar;
            var scrollbarObject = Object.Instantiate(inventoryScrollbar.gameObject, body, false);
            scrollbarObject.name = "ScrollbarVertical";
            var scrollbarRect = (RectTransform)scrollbarObject.transform;
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1, .5f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            scrollbarRect.sizeDelta = new Vector2(10, 0);
            scroll.verticalScrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void AssignRow(SerializedProperty property, GameObject row)
        {
            property.FindPropertyRelative("root").objectReferenceValue = row;
            property.FindPropertyRelative("label").objectReferenceValue = row
                .transform.Find("Label")
                .GetComponent<TMP_Text>();
            property.FindPropertyRelative("value").objectReferenceValue = row
                .transform.Find("Value")
                .GetComponent<TMP_Text>();
        }

        private static void ApplyTheme(GameObject root)
        {
            foreach (var row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                if (
                    row.transform.Find("Label")?.GetComponent<TMP_Text>() != null
                    && row.transform.Find("Value")?.GetComponent<TMP_Text>() != null
                )
                    StyleParameter(row.transform);

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name is "InventoryPanel" or "CharacterPanel" or "CraftPanel")
                    Paint(image, UiVisualTheme.Background);
                else if (
                    image.name
                    is "ItemsPanel"
                        or "DetailsPanel"
                        or "StatusPanel"
                        or "WeaponPanel"
                        or "InventoryArea"
                        or "QuickItemArea"
                        or "EquipArea"
                        or "MaterialsPanel"
                        or "CraftingPanel"
                        or "PredictionPanel"
                )
                    Paint(image, UiVisualTheme.Panel);
                else if (image.name.Contains("Rule") || image.name.StartsWith("Devider"))
                    image.color = UiVisualTheme.Rule;
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (
                    (
                        text.transform.parent.name == "TitleArea"
                        && text.name is "Title" or "ItemsTitle" or "DetailTitle"
                    ) || text.name is "ItemsTitle" or "DetailTitle"
                )
                {
                    Text(text, UiVisualTheme.HeadingSize, UiVisualTheme.Ink);
                    text.fontStyle = FontStyles.Bold;
                }
                if (text.name is "UnderBar" or "Underline")
                    text.color = UiVisualTheme.Gold;
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name is "UnderBar" or "Underline")
                    image.color = UiVisualTheme.Gold;

            foreach (var loadout in root.GetComponentsInChildren<LoadoutPanelController>(true))
            {
                var so = new SerializedObject(loadout);
                so.FindProperty("_tabNormalColor").colorValue = UiVisualTheme.Tab;
                so.FindProperty("_tabSelectedColor").colorValue = UiVisualTheme.SelectedTab;
                so.FindProperty("_tabNormalTextColor").colorValue = UiVisualTheme.Ink;
                so.FindProperty("_tabSelectedTextColor").colorValue = UiVisualTheme.Gold;
                so.ApplyModifiedPropertiesWithoutUndo();
                var button = loadout
                    .transform.Find("InventoryArea/DetailArea/ItemRow/ButtonArea/Button")
                    ?.GetComponent<Button>();
                if (button != null)
                    ActionButton(button);
            }
            foreach (var screen in root.GetComponentsInChildren<InventoryScreenView>(true))
            {
                var so = new SerializedObject(screen);
                ActionButton((Button)so.FindProperty("_useButton").objectReferenceValue);
            }
            foreach (var dialog in root.GetComponentsInChildren<ItemUseDialogPanel>(true))
            {
                var so = new SerializedObject(dialog);
                ActionButton((Button)so.FindProperty("_useButton").objectReferenceValue);
            }
            foreach (var craft in root.GetComponentsInChildren<CraftPanelController>(true))
            {
                var so = new SerializedObject(craft);
                ActionButton((Button)so.FindProperty("_craftButton").objectReferenceValue);
            }
        }

        private static void StyleParameter(Transform root)
        {
            Paint(root.GetComponent<Image>(), UiVisualTheme.Parameter);
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 4, 4);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            layout.childScaleHeight = layout.childScaleWidth = false;
            var size =
                root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 0;
            size.flexibleWidth = 1;
            size.minHeight = 44;
            size.preferredHeight = UiVisualTheme.ParameterHeight;
            size.flexibleHeight = 0;
            var label = root.Find("Label").GetComponent<TMP_Text>();
            var value = root.Find("Value").GetComponent<TMP_Text>();
            if (label.text == "最大体力")
                label.text = "最大HP";
            Text(label, UiVisualTheme.ParameterLabelSize, UiVisualTheme.Ink);
            Text(value, UiVisualTheme.ParameterValueSize, UiVisualTheme.Value);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = value.enableAutoSizing = true;
            label.fontSizeMin = 18;
            label.fontSizeMax = UiVisualTheme.ParameterLabelSize;
            value.fontSizeMin = 20;
            value.fontSizeMax = UiVisualTheme.ParameterValueSize;
            foreach (var text in new[] { label, value })
            {
                var element =
                    text.GetComponent<LayoutElement>()
                    ?? text.gameObject.AddComponent<LayoutElement>();
                element.minWidth = element.preferredWidth = 0;
                element.flexibleWidth = 1;
                element.minHeight = element.preferredHeight = -1;
                element.flexibleHeight = 1;
            }
        }

        private static void ActionButton(Button button)
        {
            if (button == null)
                return;
            Paint(button.GetComponent<Image>(), UiVisualTheme.Gold);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, .9f, 1);
            colors.disabledColor = new Color(.35f, .4f, .48f, .7f);
            button.colors = colors;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                var source = button.transform.root.GetComponentInChildren<TMP_Text>(true);
                var labelRect = NewRect("Label", button.transform);
                Stretch(labelRect);
                label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                label.font = source.font;
                label.text = "合成";
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }
            var outline = button.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = false;
            Text(label, 36, UiVisualTheme.ActionInk);
            label.fontStyle = FontStyles.Bold;
            var style =
                button.GetComponent<UiActionButtonStyle>()
                ?? button.gameObject.AddComponent<UiActionButtonStyle>();
            var so = new SerializedObject(style);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Paint(Image image, Color color)
        {
            if (image == null)
                return;
            image.sprite = Rounded;
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void Text(TMP_Text text, float size, Color color)
        {
            if (text == null)
                return;
            text.fontSize = size;
            text.color = color;
            text.margin = Vector4.zero;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Vertical(GameObject root, float spacing)
        {
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
    }
}
