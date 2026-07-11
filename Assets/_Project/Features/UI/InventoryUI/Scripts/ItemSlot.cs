using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public class ItemSlot : BaseItemSlot, IPointerClickHandler
    {
        private ItemStack _itemStack;
        private Inventory _controller;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        private RectTransform _selectedFrameRect;

        [SerializeField]
        private GameObject _equippedMarker;

        [SerializeField]
        private Image _equippedDimOverlay;

        [SerializeField]
        private GameObject _craftAssignedMarker;

        private bool _createdEquippedDimOverlay;
        private bool _isEquipped;
        private bool _isCraftAssigned;

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

        public override void Select()
        {
            base.Select();
        }

        public override void Deselect()
        {
            base.Deselect();
        }

        protected override void RefreshSelectionVisuals()
        {
            ConfigureSelectedFrame();
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

        public void SetEquipped(bool isEquipped)
        {
            _isEquipped = isEquipped;
            ResolveMarkers();
            if (_equippedMarker != null)
                _equippedMarker.SetActive(_isEquipped);
            if (_equippedDimOverlay != null)
                _equippedDimOverlay.gameObject.SetActive(_isEquipped);
        }

        public void SetCraftAssigned(bool isAssigned)
        {
            _isCraftAssigned = isAssigned;
            ResolveMarkers();
            if (_craftAssignedMarker != null)
                _craftAssignedMarker.SetActive(_isCraftAssigned);
        }

        private void ResolveMarkers()
        {
            _equippedMarker ??=
                FindChildGameObject("VisualRoot/EquippedMarker")
                ?? FindChildGameObject("EquippedMarker");
            _craftAssignedMarker ??=
                FindChildGameObject("VisualRoot/CraftAssignedMarker")
                ?? FindChildGameObject("CraftAssignedMarker");
            _equippedDimOverlay ??= ResolveEquippedDimOverlay();

            ConfigureSelectedFrame();
            ConfigureEquippedDimOverlay();
            ConfigureDecorativeRaycasts();
            ApplyMarkerStates();
        }

        private void ApplyMarkerStates()
        {
            if (_equippedMarker != null)
                _equippedMarker.SetActive(_isEquipped);
            if (_equippedDimOverlay != null)
                _equippedDimOverlay.gameObject.SetActive(_isEquipped);
            if (_craftAssignedMarker != null)
                _craftAssignedMarker.SetActive(_isCraftAssigned);
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

        private Image ResolveEquippedDimOverlay()
        {
            var overlayTransform =
                transform.Find("VisualRoot/EquippedDimOverlay")
                ?? transform.Find("EquippedDimOverlay");
            if (overlayTransform != null)
                return overlayTransform.GetComponent<Image>();

            if (_visualRootRect == null)
                return null;

            var overlayObject = new GameObject(
                "EquippedDimOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(_visualRootRect, false);
            StretchToFill(overlayRect);

            var overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.35f);
            overlayImage.raycastTarget = false;
            overlayObject.SetActive(false);
            _createdEquippedDimOverlay = true;
            PlaceGeneratedEquippedDimOverlay(overlayImage.rectTransform);
            return overlayImage;
        }

        private void ConfigureSelectedFrame()
        {
            _selectedFrameRect ??=
                transform.Find("VisualRoot/SelectedFrame") as RectTransform
                ?? transform.Find("SelectedFrame") as RectTransform;
            if (_selectedFrameRect == null)
                return;

            DisableGraphicRaycasts(_selectedFrameRect);
            _selectedFrameRect.gameObject.SetActive(_isSlotSelected);
        }

        private void ConfigureEquippedDimOverlay()
        {
            if (_equippedDimOverlay == null)
                return;

            _equippedDimOverlay.color = new Color(0f, 0f, 0f, 0.35f);
            _equippedDimOverlay.raycastTarget = false;
            if (_createdEquippedDimOverlay)
                StretchToFill(_equippedDimOverlay.rectTransform);
        }

        private void PlaceGeneratedEquippedDimOverlay(RectTransform overlayRect)
        {
            if (_visualRootRect == null || overlayRect == null)
                return;

            var overlayTransform = overlayRect.transform;
            if (overlayTransform.parent != _visualRootRect)
                return;

            var iconTransform = transform.Find("VisualRoot/Icon");
            int targetIndex = iconTransform != null ? iconTransform.GetSiblingIndex() + 1 : 0;
            overlayTransform.SetSiblingIndex(
                Mathf.Clamp(targetIndex, 0, _visualRootRect.childCount - 1)
            );
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
                    ? _equippedMarker.transform.Find("EquipText")
                    : transform.Find("VisualRoot/EquippedMarker/EquipText")
                        ?? transform.Find("EquippedMarker/EquipText")
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
