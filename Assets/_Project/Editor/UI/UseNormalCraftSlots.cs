using System;
using System.Linq;
using CreativeAI.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CreativeAI.EditorTools
{
    public static class UseNormalCraftSlots
    {
        private const string CraftPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftPanel.prefab";
        private const string SlotPath =
            "Assets/_Project/Features/UI/InventoryUI/Prefabs/ItemSlot.prefab";
        private const string ListSlotPath =
            "Assets/_Project/Features/UI/CraftingUI/Prefabs/CraftListItemSlot.prefab";
        private const string ArtPath = "Assets/_Project/Art/UI/Common/Slot/";

        [MenuItem("Tools/CreativeAI/UI/Use Normal Craft Slots %#&s")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode first.");
            StageUtility.GoToMainStage();
            var root = PrefabUtility.LoadPrefabContents(CraftPath);
            try
            {
                var normal = AssetDatabase.LoadAssetAtPath<Sprite>(
                    ArtPath + "Slot_Item_Normal.png"
                );
                var selected = AssetDatabase.LoadAssetAtPath<Sprite>(
                    ArtPath + "Slot_Item_Selected.png"
                );
                var itemSet = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "Slot_Item_Set.png");
                var itemSetSelected = AssetDatabase.LoadAssetAtPath<Sprite>(
                    ArtPath + "Slot_Item_Set_Selected.png"
                );
                var slotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
                var controller = root.GetComponent<CraftPanelController>();
                var controllerSo = new SerializedObject(controller);
                var slots = controllerSo.FindProperty("_materialSlots");
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var previous = (MaterialSlot)
                        slots.GetArrayElementAtIndex(i).objectReferenceValue;
                    // Already migrated: preserve the authored instance and only refresh its frame.
                    if (
                        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(previous.gameObject)
                        == SlotPath
                    )
                    {
                        ConfigureFrame(
                            previous.GetComponent<SlotFrameView>(),
                            itemSet,
                            itemSetSelected
                        );
                        continue;
                    }

                    var replacement = (GameObject)
                        PrefabUtility.InstantiatePrefab(slotAsset, previous.transform.parent);
                    replacement.name = previous.name;
                    replacement.transform.SetSiblingIndex(previous.transform.GetSiblingIndex());
                    CopyRect(
                        (RectTransform)previous.transform,
                        (RectTransform)replacement.transform
                    );
                    var oldLayout = previous.GetComponent<LayoutElement>();
                    if (oldLayout != null)
                    {
                        var layout =
                            replacement.GetComponent<LayoutElement>()
                            ?? replacement.AddComponent<LayoutElement>();
                        EditorUtility.CopySerialized(oldLayout, layout);
                    }
                    Object.DestroyImmediate(replacement.GetComponent<ItemSlot>());
                    var count = replacement.GetComponent<SlotCountBadgeView>();
                    if (count != null)
                        Object.DestroyImmediate(count);
                    foreach (var child in replacement.GetComponentsInChildren<Transform>(true))
                        if (
                            child.name == "CountBadge"
                            || child.name == "EquippedMarker"
                            || child.name == "CraftAssignedMarker"
                            || child.name == "EquippedDimOverlay"
                        )
                            child.gameObject.SetActive(false);

                    var material = replacement.AddComponent<MaterialSlot>();
                    var empty = replacement.AddComponent<SlotEmptyView>();
                    var emptyObject = new GameObject("EmptyText", typeof(RectTransform));
                    emptyObject.transform.SetParent(
                        replacement.transform.Find("VisualRoot"),
                        false
                    );
                    // A blank placeholder keeps the normal frame unobstructed when no material is assigned.
                    Set(empty, "_emptyObject", emptyObject);
                    Set(material, "_visualRootRect", replacement.transform.Find("VisualRoot"));
                    Set(material, "_iconView", replacement.GetComponent<SlotIconView>());
                    Set(material, "_emptyView", empty);
                    Set(material, "_hoverView", replacement.GetComponent<SlotHoverView>());
                    Set(material, "_frameView", replacement.GetComponent<SlotFrameView>());
                    Set(replacement.GetComponent<SlotIconView>(), "_fillRatio", .78f);
                    ConfigureFrame(
                        replacement.GetComponent<SlotFrameView>(),
                        itemSet,
                        itemSetSelected
                    );

                    var oldLabel = previous
                        .GetComponentsInChildren<TMP_Text>(true)
                        .FirstOrDefault(t => t.name == "Label");
                    if (oldLabel != null)
                    {
                        var label = Object.Instantiate(
                            oldLabel.gameObject,
                            replacement.transform,
                            false
                        );
                        label.name = "Label";
                        CopyRect(oldLabel.rectTransform, (RectTransform)label.transform);
                    }
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = material;
                    Object.DestroyImmediate(previous.gameObject);
                }
                controllerSo.ApplyModifiedPropertiesWithoutUndo();

                var crafted = (SlotIconView)
                    controllerSo.FindProperty("_craftedItemSlot").objectReferenceValue;
                ConfigureFrame(crafted.GetComponent<SlotFrameView>(), itemSet, itemSetSelected);
                Set(crafted, "_fillRatio", .78f);
                var predictionFrame = root.GetComponentsInChildren<Transform>(true)
                    .Single(t => t.name == "PreviewFrame")
                    .GetComponent<Image>();
                predictionFrame.sprite = normal;
                predictionFrame.type = Image.Type.Simple;
                predictionFrame.preserveAspect = true;

                var listAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ListSlotPath);
                GameObject listRoot =
                    listAsset != null
                        ? PrefabUtility.LoadPrefabContents(ListSlotPath)
                        : (GameObject)PrefabUtility.InstantiatePrefab(slotAsset, root.scene);
                try
                {
                    listRoot.name = "CraftListItemSlot";
                    ConfigureFrame(listRoot.GetComponent<SlotFrameView>(), normal, selected);
                    ConfigureInventorySlotVisuals.ConfigureSlot(listRoot);
                    PrefabUtility.SaveAsPrefabAsset(listRoot, ListSlotPath);
                }
                finally
                {
                    if (listAsset != null)
                        PrefabUtility.UnloadPrefabContents(listRoot);
                    else
                        Object.DestroyImmediate(listRoot);
                }
                var inventory = (InventoryView)
                    controllerSo.FindProperty("_inventory").objectReferenceValue;
                Set(
                    inventory,
                    "_slotPrefab",
                    AssetDatabase.LoadAssetAtPath<GameObject>(ListSlotPath)
                );
                PrefabUtility.SaveAsPrefabAsset(root, CraftPath);
                Debug.Log(
                    "[NormalCraftSlots] Material I, Material II and crafted item use Slot_Item_Set. Inventory and prediction use the file frame."
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureFrame(SlotFrameView view, Sprite normal, Sprite selected)
        {
            foreach (
                string field in new[] { "_normalSprite", "_itemSetSprite", "_itemWithCountSprite" }
            )
                Set(view, field, normal);
            foreach (
                string field in new[]
                {
                    "_selectedSprite",
                    "_itemSetSelectedSprite",
                    "_itemWithCountSelectedSprite",
                    "_customSelectedSprite",
                }
            )
                Set(view, field, selected);
            var image = (Image)
                new SerializedObject(view).FindProperty("_frame").objectReferenceValue;
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
        }
    }
}
