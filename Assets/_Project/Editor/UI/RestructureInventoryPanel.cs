using System.IO;
using System.Linq;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.EditorTools
{
    public static class RestructureInventoryPanel
    {
        private const string Path =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryPanel.prefab";
        private static readonly Color PanelColor = new Color32(27, 43, 56, 255);
        private static readonly Color Ink = new Color32(219, 225, 244, 255);
        private static readonly Color RuleColor = new Color32(126, 142, 165, 150);
        private static Sprite Rounded =>
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/UI/Common/RoundedRect_White_256_R8.png"
            );

        [MenuItem("Tools/CreativeAI/UI/Polish Inventory Readability %#&b")]
        public static void Polish()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                var grid = root.GetComponentInChildren<GridLayoutGroup>(true);
                grid.constraintCount = 7;
                grid.spacing = new Vector2(10, 44);
                var footer = root.transform.Find("MainLayout/ItemsPanel/Footer");
                if (footer != null)
                    Object.DestroyImmediate(footer.gameObject);
                var details = root.transform.Find("MainLayout/DetailsPanel");
                var heading = details.Find("TitleArea");
                if (heading.GetComponent<HorizontalLayoutGroup>() == null)
                    Horizontal(heading.gameObject, 14);
                var favicon = heading.Find("DetailsIcon") as RectTransform;
                if (favicon == null)
                {
                    favicon = Container("DetailsIcon", heading);
                    var graphic = favicon.gameObject.AddComponent<InspectionIconGraphic>();
                    graphic.color = new Color32(178, 187, 225, 255);
                    graphic.raycastTarget = false;
                }
                Size(favicon.gameObject, 44, 44);
                if (favicon.GetComponent<CanvasRenderer>() == null)
                    favicon.gameObject.AddComponent<CanvasRenderer>();
                favicon.GetComponent<InspectionIconGraphic>().SetAllDirty();
                favicon.SetAsFirstSibling();
                var title = heading.Find("DetailTitle").GetComponent<TMP_Text>();
                FlexWidth(title.gameObject, 1);
                title.fontSize = 38;
                title.fontStyle = FontStyles.Bold;
                root
                    .transform.Find("MainLayout/ItemsPanel/TitleArea/ItemsTitle")
                    .GetComponent<TMP_Text>()
                    .fontStyle = FontStyles.Bold;

                var identity = details.Find("ItemIdentity");
                var itemName = identity.Find("Name").GetComponent<TMP_Text>();
                Height(itemName.gameObject, 56);
                itemName.fontSize = 40;
                itemName.fontStyle = FontStyles.Bold;
                var category = identity.GetComponentInChildren<TMP_Text>(true);
                category = identity
                    .GetComponentsInChildren<TMP_Text>(true)
                    .First(t => t.name == "Category");
                var quantity = identity
                    .GetComponentsInChildren<TMP_Text>(true)
                    .First(t => t.name == "SelectedQuantity");
                var metadata = identity.Find("MetadataRow") as RectTransform;
                if (metadata == null)
                {
                    metadata = Container("MetadataRow", identity);
                    var image = metadata.gameObject.AddComponent<Image>();
                    ImageStyle(image, new Color32(20, 34, 45, 255));
                    image.raycastTarget = false;
                    Horizontal(metadata.gameObject, 16);
                    metadata.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(
                        18,
                        18,
                        0,
                        0
                    );
                }
                Height(metadata.gameObject, 46);
                identity.GetComponent<VerticalLayoutGroup>().spacing = 8;
                category.transform.SetParent(metadata, false);
                quantity.transform.SetParent(metadata, false);
                foreach (var label in new[] { category, quantity })
                {
                    Height(label.gameObject, 44);
                    FlexWidth(label.gameObject, 1);
                    label.fontSize = 26;
                    label.fontStyle = FontStyles.Bold;
                    label.overflowMode = TextOverflowModes.Overflow;
                }
                category.alignment = TextAlignmentOptions.MidlineLeft;
                category.color = new Color32(178, 187, 225, 255);
                quantity.alignment = TextAlignmentOptions.MidlineRight;
                quantity.color = new Color32(37, 222, 233, 255);
                var button = details.Find("Actions/UseSelectedButton").GetComponent<Button>();
                ImageStyle(button.GetComponent<Image>(), new Color32(242, 185, 66, 255));
                var useLabel = button.GetComponentInChildren<TMP_Text>();
                useLabel.fontSize = 36;
                useLabel.fontStyle = FontStyles.Bold;
                useLabel.color = new Color32(48, 34, 13, 255);
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1, 1, .9f, 1);
                colors.disabledColor = new Color(.35f, .4f, .48f, .7f);
                button.colors = colors;
                var screen = new SerializedObject(root.GetComponent<InventoryScreenView>());
                Set(screen, "_categoryLabel", category);
                Set(screen, "_useLabel", useLabel);
                screen.ApplyModifiedPropertiesWithoutUndo();
                var confirmLabel = root.GetComponentInChildren<ItemUseDialogPanel>(true)
                    .transform.Find("ItemUseDialog/UseButton")
                    .GetComponentInChildren<TMP_Text>(true);
                confirmLabel.fontStyle = FontStyles.Bold;
                confirmLabel.color = new Color32(48, 34, 13, 255);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[InventoryReadability] Saved seven columns, 44px row spacing, detail favicon, clearer item metadata and button contrast; removed total count footer."
            );
        }

        [MenuItem("Tools/CreativeAI/UI/Refine Inventory Layout %#&e")]
        public static void Refine()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode first.");
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                root
                    .transform.Find("MainLayout/DetailsPanel")
                    .GetComponent<VerticalLayoutGroup>()
                    .enabled = true;
                var details = root.transform.Find("MainLayout/DetailsPanel");
                foreach (var text in details.GetComponentsInChildren<TMP_Text>(true))
                    text.overflowMode = TextOverflowModes.Overflow;
                details
                    .Find("DescriptionAndStats/Description")
                    .GetComponent<LayoutElement>()
                    .flexibleHeight = 0;
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            const string rootPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";
            var uiRoot = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                var panel = uiRoot.GetComponentInChildren<InventoryPanelController>(true);
                // Remove legacy per-instance coordinates that conflict with the new source hierarchy.
                PrefabUtility.RevertPrefabInstance(
                    panel.gameObject,
                    InteractionMode.AutomatedAction
                );
                PrefabUtility.SaveAsPrefabAsset(uiRoot, rootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(uiRoot);
            }
            var definition = AssetDatabase.LoadAssetAtPath<TabDefinition>(
                "Assets/_Project/Features/UI/InventoryUI/Data/ImportantTabDefinition.asset"
            );
            var so = new SerializedObject(definition);
            so.FindProperty("_label").stringValue = "大事なもの";
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[InventoryStructure] Detail layout enabled and category label updated.");
        }

        [MenuItem("Tools/CreativeAI/UI/Restructure Inventory Panel %#&r")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            var root = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                if (root.transform.Find("MainLayout") != null)
                    throw new System.InvalidOperationException("Inventory already restructured.");
                var detail = root.transform.Find("DetailPanel");
                if (PrefabUtility.IsPartOfPrefabInstance(detail.gameObject))
                    PrefabUtility.UnpackPrefabInstance(
                        detail.gameObject,
                        PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction
                    );
                var inventory = root.GetComponentInChildren<InventoryView>(true);
                var tabs = root.GetComponentInChildren<TabGroup>(true);
                var screen = root.GetComponent<InventoryScreenView>();
                var screenSo = new SerializedObject(screen);
                var quantity = (TMP_Text)screenSo.FindProperty("_quantity").objectReferenceValue;
                var useButton = (Button)screenSo.FindProperty("_useButton").objectReferenceValue;
                var owned = root.transform.Find("OwnedCount").GetComponent<TMP_Text>();
                ImageStyle(root.GetComponent<Image>(), new Color32(48, 67, 85, 255));

                var header = Container("Header", root.transform);
                Place(header, new Vector2(0, .89f), Vector2.one, Vector2.zero, Vector2.zero);
                var back = root.transform.Find("BackButton");
                back.SetParent(header, false);
                back.name = "CloseButton";
                Place(
                    (RectTransform)back,
                    new Vector2(0, .5f),
                    new Vector2(0, .5f),
                    new Vector2(70, 0),
                    new Vector2(80, 74)
                );
                ImageStyle(back.GetComponent<Image>(), new Color32(22, 42, 52, 255));
                Object.DestroyImmediate(back.Find("BackChevron").gameObject);
                var arrow = Container("BackIcon", back);
                Place(arrow, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-26, -22));
                var arrowImage = arrow.gameObject.AddComponent<Image>();
                arrowImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/UI/Common/Icon_Back_Chevron.png"
                );
                arrowImage.preserveAspect = true;
                arrowImage.raycastTarget = false;
                back.gameObject.AddComponent<HoverScaleOnPointer>();

                var main = Container("MainLayout", root.transform);
                Place(main, Vector2.zero, new Vector2(1, .89f), Vector2.zero, Vector2.zero);
                main.offsetMin = new Vector2(24, 24);
                main.offsetMax = new Vector2(-24, -8);
                var mainLayout = main.gameObject.AddComponent<HorizontalLayoutGroup>();
                mainLayout.spacing = 24;
                mainLayout.childControlWidth = mainLayout.childControlHeight = true;
                mainLayout.childForceExpandHeight = true;
                mainLayout.childForceExpandWidth = false;

                var items = root.transform.Find("ItemsPanel");
                items.SetParent(main, false);
                ImageStyle(items.GetComponent<Image>(), PanelColor);
                FlexWidth(items.gameObject, 1.65f);
                var itemsLayout = Vertical(items.gameObject, 16, 20);
                var titleArea = Container("TitleArea", items);
                Height(titleArea.gameObject, 66);
                Horizontal(titleArea.gameObject, 16);
                var icon = root.transform.Find("InventoryIcon");
                icon.SetParent(titleArea, false);
                Size(icon.gameObject, 52, 52);
                var title = root.transform.Find("ItemsTitle");
                title.SetParent(titleArea, false);
                FlexWidth(title.gameObject, 1);
                StyleText(title.GetComponent<TMP_Text>(), 42, TextAlignmentOptions.MidlineLeft);
                var titleRule = root.transform.Find("ItemsHeaderRule");
                titleRule.SetParent(titleArea, false);
                Rule(titleRule);

                tabs.transform.SetParent(items, false);
                tabs.name = "CategoryTabs";
                Height(tabs.gameObject, 72);
                tabs.GetComponent<HorizontalLayoutGroup>().spacing = 12;
                var tabSo = new SerializedObject(tabs);
                var entries = tabSo.FindProperty("_tabEntries");
                var categoryNames = new[] { "Food", "Equipment", "Important" };
                entries.arraySize = 3;
                for (int i = 0; i < categoryNames.Length; i++)
                {
                    var definition = AssetDatabase.LoadAssetAtPath<TabDefinition>(
                        "Assets/_Project/Features/UI/InventoryUI/Data/"
                            + categoryNames[i]
                            + "TabDefinition.asset"
                    );
                    entries
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("definition")
                        .objectReferenceValue = definition;
                    entries
                        .GetArrayElementAtIndex(i)
                        .FindPropertyRelative("view")
                        .objectReferenceValue = null;
                    var definitionSo = new SerializedObject(definition);
                    definitionSo.FindProperty("_icon").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>(
                            "Assets/_Project/Art/UI/Inventory/TabIcon/Icon_Tab_"
                                + categoryNames[i]
                                + ".png"
                        );
                    definitionSo.ApplyModifiedPropertiesWithoutUndo();
                }
                tabSo.ApplyModifiedPropertiesWithoutUndo();

                inventory.transform.SetParent(items, false);
                inventory.name = "Inventory";
                var inventoryLayout =
                    inventory.GetComponent<LayoutElement>()
                    ?? inventory.gameObject.AddComponent<LayoutElement>();
                inventoryLayout.minHeight = inventoryLayout.preferredHeight = 0;
                inventoryLayout.flexibleHeight = 1;
                Object.DestroyImmediate(inventory.GetComponent<SlideInPanel>());
                var scroll = inventory.GetComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.horizontalScrollbar = null;
                inventory.transform.Find("Scrollbar Horizontal").gameObject.SetActive(false);
                Place(scroll.viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                scroll.viewport.offsetMax = new Vector2(-14, 0);
                Place(scroll.content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
                scroll.content.pivot = new Vector2(.5f, 1);
                var grid = scroll.content.GetComponent<GridLayoutGroup>();
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 5;
                grid.spacing = new Vector2(8, 20);
                grid.padding = new RectOffset(8, 8, 10, 8);
                Place(
                    (RectTransform)scroll.verticalScrollbar.transform,
                    new Vector2(1, 0),
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(10, 0)
                );
                var footer = Container("Footer", items);
                Height(footer.gameObject, 52);
                var footerRule = root.transform.Find("FooterRule");
                footerRule.SetParent(footer, false);
                Rule(footerRule);
                owned.transform.SetParent(footer, false);
                Place(
                    owned.rectTransform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-16, -8)
                );
                StyleText(owned, 28, TextAlignmentOptions.MidlineLeft);

                detail.SetParent(main, false);
                detail.name = "DetailsPanel";
                FlexWidth(detail.gameObject, 1);
                ImageStyle(detail.GetComponent<Image>(), PanelColor);
                Object.DestroyImmediate(detail.GetComponent<SlideInPanel>());
                Vertical(detail.gameObject, 10, 24);
                var detailTitleArea = Container("TitleArea", detail);
                Height(detailTitleArea.gameObject, 64);
                var detailTitle = root.transform.Find("DetailTitle");
                detailTitle.SetParent(detailTitleArea, false);
                Place(
                    (RectTransform)detailTitle,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero
                );
                StyleText(
                    detailTitle.GetComponent<TMP_Text>(),
                    40,
                    TextAlignmentOptions.MidlineLeft
                );
                var detailRule = root.transform.Find("DetailHeaderRule");
                detailRule.SetParent(detailTitleArea, false);
                Rule(detailRule);

                var detailComponent = detail.GetComponent<ItemDetailPanel>();
                var detailSo = new SerializedObject(detailComponent);
                var itemIcon = detail.Find("Icon");
                var name = detail.Find("Name");
                var category = detail.Find("Category");
                var description = detail.Find("Description");
                var stats = detail.Find("Stats");
                var preview = Container("PreviewArea", detail);
                Height(preview.gameObject, 250);
                var frame = root.transform.Find("PortraitFrame");
                frame.SetParent(preview, false);
                frame.name = "SlotFrame";
                Place(
                    (RectTransform)frame,
                    new Vector2(.5f, .5f),
                    new Vector2(.5f, .5f),
                    Vector2.zero,
                    new Vector2(250, 250)
                );
                var frameImage = frame.GetComponent<Image>();
                frameImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Project/Art/UI/Common/Slot/Slot_Item_Set.png"
                );
                frameImage.color = Color.white;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = true;
                itemIcon.SetParent(frame, false);
                Place(
                    (RectTransform)itemIcon,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    new Vector2(-42, -42)
                );
                itemIcon.GetComponent<LayoutElement>().ignoreLayout = true;
                itemIcon.GetComponent<Image>().preserveAspect = true;
                var identity = Container("ItemIdentity", detail);
                Height(identity.gameObject, 112);
                Vertical(identity.gameObject, 0, 0);
                name.SetParent(identity, false);
                Height(name.gameObject, 50);
                StyleText(name.GetComponent<TMP_Text>(), 36, TextAlignmentOptions.Center);
                category.SetParent(identity, false);
                Height(category.gameObject, 28);
                StyleText(category.GetComponent<TMP_Text>(), 24, TextAlignmentOptions.Center);
                quantity.transform.SetParent(identity, false);
                Height(quantity.gameObject, 32);
                StyleText(quantity, 28, TextAlignmentOptions.Center);

                var body = Container("DescriptionAndStats", detail);
                var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
                bodyLayout.minHeight = 180;
                bodyLayout.preferredHeight = 220;
                bodyLayout.flexibleHeight = 1;
                Vertical(body.gameObject, 16, 0);
                description.SetParent(body, false);
                Height(description.gameObject, 64);
                description.GetComponent<LayoutElement>().flexibleHeight = 1;
                StyleText(description.GetComponent<TMP_Text>(), 28, TextAlignmentOptions.TopLeft);
                stats.SetParent(body, false);
                Height(stats.gameObject, 140);
                StyleText(stats.GetComponent<TMP_Text>(), 28, TextAlignmentOptions.TopLeft);
                stats.GetComponent<TMP_Text>().color = new Color32(30, 224, 236, 255);
                var actions = Container("Actions", detail);
                Height(actions.gameObject, 72);
                useButton.transform.SetParent(actions, false);
                Place(
                    (RectTransform)useButton.transform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero
                );
                ImageStyle(useButton.GetComponent<Image>(), new Color32(220, 165, 63, 255));
                useButton.gameObject.AddComponent<HoverScaleOnPointer>();
                StyleText(
                    useButton.GetComponentInChildren<TMP_Text>(),
                    36,
                    TextAlignmentOptions.Center
                );
                var colors = useButton.colors;
                colors.disabledColor = new Color(.45f, .45f, .45f, .5f);
                useButton.colors = colors;
                Set(detailSo, "_icon", itemIcon.GetComponent<Image>());
                Set(detailSo, "_name", name.GetComponent<TMP_Text>());
                Set(detailSo, "_category", category.GetComponent<TMP_Text>());
                Set(detailSo, "_description", description.GetComponent<TMP_Text>());
                Set(detailSo, "_stats", stats.GetComponent<TMP_Text>());
                detailSo.ApplyModifiedPropertiesWithoutUndo();
                detailTitleArea.SetAsFirstSibling();
                preview.SetSiblingIndex(1);
                identity.SetSiblingIndex(2);
                body.SetSiblingIndex(3);
                actions.SetAsLastSibling();

                var overlays = Container("Overlays", root.transform);
                Place(overlays, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var dialog = root.GetComponentInChildren<ItemUseDialogPanel>(true);
                dialog.transform.SetParent(overlays, false);
                Place(
                    (RectTransform)dialog.transform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero
                );
                ImageStyle(
                    dialog.transform.Find("ItemUseDialog").GetComponent<Image>(),
                    PanelColor
                );
                ImageStyle(
                    dialog.transform.Find("ItemUseDialog/UseButton").GetComponent<Image>(),
                    new Color32(220, 165, 63, 255)
                );
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var tabPath =
                "Assets/_Project/Features/UI/InventoryUI/Prefabs/InventoryTabButton.prefab";
            var tabRoot = PrefabUtility.LoadPrefabContents(tabPath);
            try
            {
                ImageStyle(tabRoot.GetComponent<Image>(), new Color32(20, 36, 47, 255));
                ImageStyle(
                    tabRoot.transform.Find("Selected").GetComponent<Image>(),
                    new Color32(87, 68, 34, 255)
                );
                foreach (var label in tabRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 20;
                    label.fontSizeMax = 32;
                }
                PrefabUtility.SaveAsPrefabAsset(tabRoot, tabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(tabRoot);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[InventoryStructure] Saved grouped inventory prefab, three categories, rounded panels and aligned detail layout."
            );
        }

        private static RectTransform Container(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(
            RectTransform rect,
            Vector2 min,
            Vector2 max,
            Vector2 position,
            Vector2 size
        )
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static LayoutElement Element(GameObject target) =>
            target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();

        private static void Height(GameObject target, float height)
        {
            var layout = Element(target);
            layout.ignoreLayout = false;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void FlexWidth(GameObject target, float weight)
        {
            var layout = Element(target);
            layout.minWidth = layout.preferredWidth = 0;
            layout.flexibleWidth = weight;
        }

        private static void Size(GameObject target, float width, float height)
        {
            Height(target, height);
            var layout = Element(target);
            layout.minWidth = layout.preferredWidth = width;
            layout.flexibleWidth = 0;
        }

        private static VerticalLayoutGroup Vertical(GameObject target, float spacing, int padding)
        {
            var layout =
                target.GetComponent<VerticalLayoutGroup>()
                ?? target.AddComponent<VerticalLayoutGroup>();
            layout.enabled = true;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childScaleWidth = layout.childScaleHeight = false;
            return layout;
        }

        private static void Horizontal(GameObject target, float spacing)
        {
            var layout = target.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        private static void ImageStyle(Image image, Color color)
        {
            image.sprite = Rounded;
            image.overrideSprite = null;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void StyleText(TMP_Text text, float size, TextAlignmentOptions alignment)
        {
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Ink;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static void Rule(Transform target)
        {
            var layout = Element(target.gameObject);
            layout.ignoreLayout = true;
            Place(
                (RectTransform)target,
                new Vector2(0, 0),
                new Vector2(1, 0),
                Vector2.zero,
                new Vector2(0, 2)
            );
            var image = target.GetComponent<Image>();
            image.sprite = null;
            image.color = RuleColor;
            image.raycastTarget = false;
        }

        private static void Set(SerializedObject so, string property, Object value) =>
            so.FindProperty(property).objectReferenceValue = value;

        [MenuItem("Tools/CreativeAI/UI/Inspect Inventory Structure %#&q")]
        public static void Inspect()
        {
            var live = EditorApplication.isPlaying
                ? Object.FindAnyObjectByType<InventoryPanelController>()
                : null;
            var root =
                live != null ? live.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            string Hierarchy(Transform t) =>
                t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
            File.WriteAllLines(
                "Temp/InventoryStructure.txt",
                root.GetComponentsInChildren<Transform>(true)
                    .Select(t =>
                    {
                        var r = t as RectTransform;
                        var im = t.GetComponent<Image>();
                        var tx = t.GetComponent<TMP_Text>();
                        var grid = t.GetComponent<GridLayoutGroup>();
                        return Hierarchy(t)
                            + (
                                r == null
                                    ? ""
                                    : $" anchors={r.anchorMin}:{r.anchorMax} pos={r.anchoredPosition} size={r.sizeDelta} pivot={r.pivot}"
                            )
                            + (
                                im == null
                                    ? ""
                                    : $" IMAGE={(im.sprite != null ? im.sprite.name : "NULL")} color={im.color}"
                            )
                            + (
                                tx == null
                                    ? ""
                                    : $" TEXT={tx.text} font={tx.fontSize} align={tx.alignment} color={tx.color} visible={tx.maxVisibleCharacters}"
                            )
                            + (
                                r == null
                                    ? ""
                                    : $" actual={r.rect.size} active={t.gameObject.activeInHierarchy}"
                            )
                            + (grid == null ? "" : $" GRID={grid.cellSize} spacing={grid.spacing}")
                            + " COMPONENTS="
                            + string.Join(
                                ",",
                                t.GetComponents<Component>()
                                    .Select(c => c != null ? c.GetType().Name : "Missing")
                            );
                    })
            );
            var tabs = root.GetComponentInChildren<TabGroup>(true);
            File.AppendAllText(
                "Temp/InventoryStructure.txt",
                "\nTABS " + EditorJsonUtility.ToJson(tabs, true)
            );
            if (EditorApplication.isPlaying)
                Object.FindAnyObjectByType<UiRouter>().Open(UiRouter.UiId.Inventory);
            Debug.Log("[InventoryStructure] Written Temp/InventoryStructure.txt");
        }
    }
}
