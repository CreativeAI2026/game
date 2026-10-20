using System;
using System.Linq;
using CreativeAI.Gameplay;
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
    public static class RefineCharacterInventoryAndSave
    {
        private const string Ui = "Assets/_Project/Features/UI/";
        private static Sprite Rounded =>
            AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/UI/Common/RoundedRect_White_256_R8.png"
            );

        [MenuItem("Tools/CreativeAI/UI/Align Equipment Icon And Inventory Preview %#&d")]
        private static void AlignEquipmentAndPreview()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            foreach (
                var path in new[]
                {
                    Ui + "CharacterUI/Prefabs/CharacterPanel.prefab",
                    Ui + "InventoryUI/Prefabs/InventoryPanel.prefab",
                    Ui + "Root/Prefabs/UIRoot.prefab",
                }
            )
                Edit(
                    path,
                    root =>
                    {
                        foreach (
                            var loadout in root.GetComponentsInChildren<LoadoutPanelController>(
                                true
                            )
                        )
                        {
                            var title = loadout.transform.Find("EquipArea/TitleArea");
                            var icon = (RectTransform)title.Find("Favicon");
                            // Reserve its width in the row, then position the artwork independently
                            // so a layout rebuild preserves the small optical baseline correction.
                            var layout = title.GetComponent<HorizontalLayoutGroup>();
                            layout.padding.left = 58;
                            var element =
                                icon.GetComponent<LayoutElement>()
                                ?? icon.gameObject.AddComponent<LayoutElement>();
                            element.ignoreLayout = true;
                            icon.anchorMin = icon.anchorMax = new Vector2(0, .5f);
                            icon.pivot = new Vector2(0, .5f);
                            icon.anchoredPosition = new Vector2(0, -6);
                            icon.sizeDelta = new Vector2(48, 48);
                            title.Find("Title").GetComponent<TMP_Text>().alignment =
                                TextAlignmentOptions.MidlineLeft;
                        }
                        foreach (
                            var screen in root.GetComponentsInChildren<InventoryScreenView>(true)
                        )
                        {
                            var image = screen
                                .transform.Find("MainLayout/DetailsPanel/PreviewArea/SlotFrame")
                                .GetComponent<Image>();
                            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                                "Assets/_Project/Art/UI/Common/Slot/Slot_Item_Normal.png"
                            );
                            image.overrideSprite = null;
                            image.type = Image.Type.Simple;
                            image.preserveAspect = true;
                        }
                    }
                );
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[UiAlignment] Equipment heading icon aligned; inventory detail preview uses Slot_Item_Normal."
            );
        }

        [MenuItem("Tools/CreativeAI/UI/Refine Character Inventory And Save %#&u")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            Edit(Ui + "CharacterUI/Prefabs/CharacterPanel.prefab", Character);
            Edit(Ui + "InventoryUI/Prefabs/InventoryTabButton.prefab", Tab);
            Edit(Ui + "InventoryUI/Prefabs/InventoryPanel.prefab", Icons);
            Edit(Ui + "CraftingUI/Prefabs/CraftPanel.prefab", Icons);
            Edit(Ui + "SaveDialog/Prefabs/SaveDialog.prefab", Save);
            Edit(
                Ui + "Root/Prefabs/UIRoot.prefab",
                root =>
                {
                    foreach (
                        var selector in root.GetComponentsInChildren<CharacterWeaponSelector>(true)
                    )
                        ConfigureWeapon(selector);
                    Icons(root);
                    foreach (var save in root.GetComponentsInChildren<SaveDialogController>(true))
                        Save(save.gameObject);
                }
            );
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[UiRefinement] Hammer, weapon selection, inventory tabs, heading icons and save prefab updated."
            );
        }

        [MenuItem("Tools/CreativeAI/UI/Preview Save Theme %#&n")]
        private static void PreviewSave()
        {
            if (EditorApplication.isPlaying)
                Object.FindAnyObjectByType<UiRouter>().Open(UiRouter.UiId.Save);
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

        private static void Character(GameObject root)
        {
            foreach (var selector in root.GetComponentsInChildren<CharacterWeaponSelector>(true))
                ConfigureWeapon(selector);
            Icons(root);
        }

        private static void ConfigureWeapon(CharacterWeaponSelector selector)
        {
            var so = new SerializedObject(selector);
            so.FindProperty("_attackValue").objectReferenceValue = selector
                .transform.Find("WeaponStatusArea/ATK/TextArea/ValueText")
                .GetComponent<TMP_Text>();
            so.FindProperty("_criticalRateValue").objectReferenceValue = selector
                .transform.Find("WeaponStatusArea/CRTR/TextArea/ValueText")
                .GetComponent<TMP_Text>();
            so.FindProperty("_criticalDamageValue").objectReferenceValue = selector
                .transform.Find("WeaponStatusArea/CRTD/TextArea/ValueText")
                .GetComponent<TMP_Text>();
            var weapons = so.FindProperty("_weapons");
            var names = new[] { "Sword", "Bow", "Scythe", "Hammer" };
            for (int i = 0; i < weapons.arraySize && i < names.Length; i++)
                weapons
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("_data")
                    .objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    "Assets/_Project/Features/Player/Data/Weapons/" + names[i] + ".asset"
                );
            var view = selector.transform.Find("ViewArea/WeaponView");
            foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
            var preview = view.Find("WeaponPreview") as RectTransform;
            if (preview == null)
                preview = Rect("WeaponPreview", view);
            Fill(preview, 32);
            var image = preview.GetComponent<Image>() ?? preview.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.sprite = AssetDatabase
                .LoadAssetAtPath<WeaponData>(
                    "Assets/_Project/Features/Player/Data/Weapons/Sword.asset"
                )
                .icon;
            so.FindProperty("_weaponPreview").objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Tab(GameObject root)
        {
            root.transform.Find("Icon").gameObject.SetActive(false);
            var label = (RectTransform)root.transform.Find("Label");
            label.anchorMin = new Vector2(.04f, 0);
            label.anchorMax = new Vector2(.76f, 1);
            label.offsetMin = label.offsetMax = Vector2.zero;
            var count = root.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.name == "Count");
            var badge = root.transform.Find("CountBadge") as RectTransform;
            if (badge == null)
                badge = Rect("CountBadge", root.transform);
            badge.anchorMin = new Vector2(.8f, .19f);
            badge.anchorMax = new Vector2(.96f, .81f);
            badge.offsetMin = badge.offsetMax = Vector2.zero;
            Paint(badge.gameObject, new Color32(4, 12, 17, 255));
            count.transform.SetParent(badge, false);
            Fill((RectTransform)count.transform, 2);
            count.alignment = TextAlignmentOptions.Center;
            count.fontStyle = FontStyles.Bold;
        }

        private static void Icons(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (
                    graphic.transform.parent == null
                    || graphic.transform.parent.name != "TitleArea"
                )
                    continue;
                if (graphic.name is not ("Favicon" or "InventoryIcon" or "DetailsIcon" or "Icon"))
                    continue;
                var rect = graphic.rectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 48);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 48);
                var size =
                    graphic.GetComponent<LayoutElement>()
                    ?? graphic.gameObject.AddComponent<LayoutElement>();
                size.minWidth = size.preferredWidth = 48;
                size.minHeight = size.preferredHeight = 48;
                size.flexibleWidth = size.flexibleHeight = 0;
                graphic.color = UiVisualTheme.Muted;
                if (graphic is Image image)
                    image.preserveAspect = true;
            }
        }

        private static void Save(GameObject root)
        {
            var controller = root.GetComponent<SaveDialogController>();
            var so = new SerializedObject(controller);
            var close = (Button)so.FindProperty("_closeButton").objectReferenceValue;
            var yes = (Button)so.FindProperty("_yesButton").objectReferenceValue;
            var no = (Button)so.FindProperty("_noButton").objectReferenceValue;
            var message = root.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.name == "Message");
            var font = message.font;
            Paint(root, UiVisualTheme.Background);
            var header = root.transform.Find("Header") as RectTransform;
            if (header == null)
                header = Rect("Header", root.transform);
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = Vector2.one;
            header.pivot = Vector2.up;
            header.sizeDelta = new Vector2(0, 110);
            header.anchoredPosition = Vector2.zero;
            close.transform.SetParent(header, false);
            var back = (RectTransform)close.transform;
            back.anchorMin = back.anchorMax = Vector2.up;
            back.pivot = Vector2.up;
            back.anchoredPosition = new Vector2(24, -24);
            back.sizeDelta = new Vector2(72, 72);
            back.localScale = Vector3.one;
            close.colors = ColorBlock.defaultColorBlock;
            Paint(close.gameObject, UiVisualTheme.Tab);
            foreach (var child in close.GetComponentsInChildren<Graphic>(true))
                if (child.gameObject != close.gameObject)
                    child.gameObject.SetActive(false);
            var backIcon = close.transform.Find("BackIcon") as RectTransform;
            if (backIcon == null)
                backIcon = Rect("BackIcon", back);
            backIcon.gameObject.SetActive(true);
            Fill(backIcon, 18);
            var backImage =
                backIcon.GetComponent<Image>() ?? backIcon.gameObject.AddComponent<Image>();
            backImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/UI/Common/Icon_Back_Chevron.png"
            );
            backImage.color = UiVisualTheme.Ink;
            backImage.preserveAspect = true;
            backImage.raycastTarget = false;

            var card = root.transform.Find("SaveCard") as RectTransform;
            if (card == null)
                card = Rect("SaveCard", root.transform);
            card.anchorMin = new Vector2(.19f, .24f);
            card.anchorMax = new Vector2(.81f, .74f);
            card.offsetMin = card.offsetMax = Vector2.zero;
            Paint(card.gameObject, UiVisualTheme.Panel);
            var title = card.Find("Title")?.GetComponent<TMP_Text>();
            if (title == null)
                title = Text("Title", card, font);
            title.text = "セーブ";
            title.fontSize = 40;
            title.fontStyle = FontStyles.Bold;
            title.color = UiVisualTheme.Ink;
            Position(title.rectTransform, new Vector2(.07f, .72f), new Vector2(.93f, .94f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var rule = card.Find("HeaderRule") as RectTransform;
            if (rule == null)
                rule = Rect("HeaderRule", card);
            Position(rule, new Vector2(.07f, .7f), new Vector2(.93f, .7f));
            rule.sizeDelta = new Vector2(0, 2);
            Paint(rule.gameObject, UiVisualTheme.Rule);
            message.transform.SetParent(card, false);
            Position(message.rectTransform, new Vector2(.07f, .36f), new Vector2(.93f, .67f));
            message.text = "ここまでの進行をセーブしますか？";
            message.fontSize = 32;
            message.color = UiVisualTheme.Ink;
            message.alignment = TextAlignmentOptions.Center;
            message.enableAutoSizing = true;
            message.fontSizeMin = 20;
            message.fontSizeMax = 32;
            SaveButton(
                yes,
                card,
                "セーブする",
                new Vector2(.52f, .1f),
                new Vector2(.93f, .28f),
                true
            );
            SaveButton(no, card, "戻る", new Vector2(.07f, .1f), new Vector2(.48f, .28f), false);
            header.SetAsLastSibling();
        }

        private static void SaveButton(
            Button button,
            Transform parent,
            string caption,
            Vector2 min,
            Vector2 max,
            bool primary
        )
        {
            button.transform.SetParent(parent, false);
            Position((RectTransform)button.transform, min, max);
            Paint(button.gameObject, primary ? UiVisualTheme.Gold : UiVisualTheme.Tab);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.text = caption;
            label.fontSize = 32;
            label.fontStyle = FontStyles.Bold;
            label.color = primary ? UiVisualTheme.ActionInk : UiVisualTheme.Ink;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 32;
            Fill(label.rectTransform, 6);
        }

        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Fill(RectTransform rect, float inset)
        {
            Position(rect, Vector2.zero, Vector2.one);
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private static void Position(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Paint(GameObject root, Color color)
        {
            var image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
        }
    }
}
