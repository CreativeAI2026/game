using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class InventoryPanelController : UIPanelStub
    {
        [SerializeField]
        private Inventory _inventory;

        [SerializeField]
        private ItemUseDialogPanel _itemUseDialogPanel;

        protected override void Awake()
        {
            base.Awake();
            ResolveReferences();
            _itemUseDialogPanel?.Hide();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindInventoryEvents();
        }

        private void OnDisable()
        {
            UnbindInventoryEvents();
            _itemUseDialogPanel?.Hide();
        }

        private void OnDestroy()
        {
            UnbindInventoryEvents();
        }

        private void BindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
            _inventory.OnSlotDoubleClicked += OnInventorySlotDoubleClicked;
        }

        private void UnbindInventoryEvents()
        {
            if (_inventory == null)
                return;

            _inventory.OnSlotDoubleClicked -= OnInventorySlotDoubleClicked;
        }

        private void OnInventorySlotDoubleClicked(ItemStack stack)
        {
            if (stack?.Data is not FoodData)
                return;

            _itemUseDialogPanel?.Show(stack);
        }

        private void ResolveReferences()
        {
            _inventory ??= GetComponentInChildren<Inventory>(true);
            _itemUseDialogPanel ??= GetComponentInChildren<ItemUseDialogPanel>(true);

            if (_itemUseDialogPanel != null)
                return;

            var dialogTransform = FindChild("ItemUseDialogPanel");
            if (dialogTransform == null)
            {
                _itemUseDialogPanel = CreateFallbackDialogPanel();
                return;
            }

            _itemUseDialogPanel = dialogTransform.GetComponent<ItemUseDialogPanel>();
            if (_itemUseDialogPanel == null)
                _itemUseDialogPanel = dialogTransform.gameObject.AddComponent<ItemUseDialogPanel>();
        }

        private ItemUseDialogPanel CreateFallbackDialogPanel()
        {
            var panelObject = new GameObject(
                "ItemUseDialogPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.55f);
            panelImage.raycastTarget = true;

            var dialogObject = new GameObject(
                "ItemUseDialog",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var dialogRect = dialogObject.GetComponent<RectTransform>();
            dialogRect.SetParent(panelRect, false);
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(760f, 360f);

            var dialogImage = dialogObject.GetComponent<Image>();
            dialogImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            dialogImage.raycastTarget = true;

            CreateImage(dialogRect, "ItemIcon", new Vector2(0f, 90f), new Vector2(130f, 130f));
            CreateText(dialogRect, "ItemName", new Vector2(0f, 5f), new Vector2(680f, 48f));
            CreateText(dialogRect, "ItemEffect", new Vector2(0f, -55f), new Vector2(680f, 58f));
            CreateButton(
                dialogRect,
                "UseButton",
                "\u4f7f\u7528\u3059\u308b",
                new Vector2(0f, -135f)
            );

            return panelObject.AddComponent<ItemUseDialogPanel>();
        }

        private static void CreateImage(
            RectTransform parent,
            string objectName,
            Vector2 position,
            Vector2 size
        )
        {
            var imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = imageObject.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static void CreateText(
            RectTransform parent,
            string objectName,
            Vector2 position,
            Vector2 size
        )
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontSize = 32;
            text.text = "";
            text.raycastTarget = false;
        }

        private static void CreateButton(
            RectTransform parent,
            string objectName,
            string label,
            Vector2 position
        )
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button)
            );
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(220f, 70f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            CreateText(rect, "Text (TMP)", Vector2.zero, new Vector2(200f, 60f));
            var text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = 28;
            }
        }

        private Transform FindChild(string objectName)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect != null && rect.name == objectName)
                    return rect;
            }

            return null;
        }
    }
}
