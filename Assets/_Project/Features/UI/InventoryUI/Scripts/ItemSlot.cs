using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot : BaseItemSlot, IPointerClickHandler
    {
        private ItemStack _itemStack;
        private Inventory _controller;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private RectTransform _iconRect;

        protected override void Awake()
        {
            base.Awake();
            _controller = GetComponentInParent<Inventory>();
            ConfigureVisualRootHover();
            RefreshSelectionVisuals();
        }

        private void OnEnable()
        {
            ConfigureVisualRootHover();
            RefreshSelectionVisuals();
        }

        private void OnRectTransformDimensionsChange()
        {
            ConfigureEquippedMarker();
            ConfigureCraftAssignedMarker();
        }

        public void SetItem(ItemStack stack)
        {
            _itemStack = stack;
            base.SetItem(stack?.Data, stack?.Count ?? 0);
            ConfigureVisualRootHover();
            SetEquipped(stack?.IsEquipped ?? false);
        }

        public ItemStack Stack => _itemStack;

        public void SetReleaseSelectionOnOutsideClick(bool release)
        {
            _hoverScale?.SetReleaseLockOnOutsideClick(release);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_controller != null)
            {
                if (
                    eventData.button == PointerEventData.InputButton.Left
                    && eventData.clickCount >= 2
                )
                {
                    _controller.SelectSlotByDoubleClick(this);
                    return;
                }

                _controller.SelectSlotByClick(this);
                return;
            }

            Select();
        }

        private void ConfigureVisualRootHover()
        {
            ResolveVisualRoot();
            ResolveMarkers();

            if (_hoverScale == null || _visualRootRect == null)
                return;

            _hoverScale.SetTarget(_visualRootRect);
            _hoverScale.SetBounceTarget(null);
            _hoverScale.SetLinkedTargets();
        }

        private void ResolveVisualRoot()
        {
            _visualRootRect ??= transform.Find("VisualRoot") as RectTransform;
            if (_visualRootRect == null)
                _visualRootRect = CreateVisualRoot();

            ConfigureSelectedFrame();
            ConfigureDecorativeRaycasts();
        }

        private RectTransform CreateVisualRoot()
        {
            var visualRootObject = new GameObject("VisualRoot", typeof(RectTransform));
            var rect = visualRootObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            StretchToFill(rect);
            return rect;
        }

        private static void StretchToFill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        private GameObject FindChildGameObject(string path)
        {
            var child = transform.Find(path);
            return child != null ? child.gameObject : null;
        }

        private void ConfigureDecorativeRaycasts()
        {
            DisableGraphicRaycasts(
                transform.Find("VisualRoot/SelectedFrame") ?? transform.Find("SelectedFrame")
            );
            DisableGraphicRaycasts(transform.Find("VisualRoot/Icon") ?? transform.Find("Icon"));
            DisableGraphicRaycasts(
                transform.Find("VisualRoot/EquippedDimOverlay")
                    ?? transform.Find("EquippedDimOverlay")
            );
            DisableGraphicRaycasts(
                transform.Find("VisualRoot/CountBadge") ?? transform.Find("CountBadge")
            );
            DisableGraphicRaycasts(
                transform.Find("VisualRoot/CountBadge/CountText")
                    ?? transform.Find("CountBadge/CountText")
                    ?? transform.Find("VisualRoot/numberSlot/Text")
                    ?? transform.Find("numberSlot/Text")
            );
            DisableGraphicRaycasts(_equippedMarker != null ? _equippedMarker.transform : null);
            DisableGraphicRaycasts(
                _equippedMarker != null
                    ? _equippedMarker.transform.Find("EquippedText")
                        ?? _equippedMarker.transform.Find("EquipText")
                    : transform.Find("VisualRoot/EquippedMarker/EquipText")
                        ?? transform.Find("VisualRoot/EquippedMarker/EquippedText")
                        ?? transform.Find("EquippedMarker/EquipText")
                        ?? transform.Find("EquippedMarker/EquippedText")
            );
            DisableGraphicRaycasts(
                _craftAssignedMarker != null ? _craftAssignedMarker.transform : null
            );
            DisableGraphicRaycasts(
                _craftAssignedMarker != null
                    ? _craftAssignedMarker.transform.Find("CraftAssignedText")
                    : transform.Find("VisualRoot/CraftAssignedMarker/CraftAssignedText")
                        ?? transform.Find("CraftAssignedMarker/CraftAssignedText")
            );
        }

        private static void DisableGraphicRaycasts(Transform root)
        {
            if (root == null)
                return;

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
    }
}
