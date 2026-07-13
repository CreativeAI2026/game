using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.InventoryUI
{
    public partial class ItemSlot
    {
        [SerializeField]
        private GameObject _equippedMarker;

        [SerializeField]
        private Image _equippedMarkerImage;

        [SerializeField]
        private TMP_Text _equippedText;

        [SerializeField]
        private Image _equippedDimOverlay;

        [SerializeField]
        private GameObject _craftAssignedMarker;

        [SerializeField]
        private Image _craftAssignedMarkerImage;

        private bool _createdEquippedDimOverlay;
        private bool _isEquipped;
        private bool _isCraftAssigned;

        private static readonly Color EquippedMarkerColor = new Color32(0xF2, 0x5C, 0x54, 0xFF);
        private static readonly Color EquippedTextColor = new Color32(0x11, 0x11, 0x11, 0xFF);
        private static readonly Color CraftAssignedMarkerColor = new Color32(
            0x66,
            0xD9,
            0xEF,
            0xFF
        );
        private static readonly Color EquippedDimOverlayColor = new Color(0f, 0f, 0f, 0.8f);
        private const float EquippedMarkerScale = 0.28f;
        private const float EquippedMarkerMaxIconRatio = 0.33f;
        private const float EquippedMarkerMinSize = 14f;
        private const float EquippedMarkerMaxSize = 44f;
        private const float EquippedTextVisibleMinSize = 26f;
        private const float EquippedTextMaxFontSizeScale = 0.55f;

        public void SetEquipped(bool isEquipped)
        {
            _isEquipped = isEquipped;
            ResolveMarkers();
            if (_equippedMarker != null)
                _equippedMarker.SetActive(_isEquipped);
            if (_equippedDimOverlay != null)
                _equippedDimOverlay.gameObject.SetActive(_isEquipped);
            ConfigureEquippedMarker();
        }

        public void SetCraftAssigned(bool isAssigned)
        {
            _isCraftAssigned = isAssigned;
            ResolveMarkers();
            if (_craftAssignedMarker != null)
                _craftAssignedMarker.SetActive(CanControlCraftAssignedMarker());
            ConfigureCraftAssignedMarker();
        }

        private void ResolveMarkers()
        {
            _equippedMarker ??=
                FindChildGameObject("VisualRoot/EquippedMarker")
                ?? FindChildGameObject("EquippedMarker");
            _iconRect ??=
                _iconImage != null
                    ? _iconImage.rectTransform
                    : transform.Find("VisualRoot/Icon") as RectTransform
                        ?? transform.Find("Icon") as RectTransform;
            ResolveEquippedMarkerParts();
            _craftAssignedMarker ??=
                FindChildGameObject("VisualRoot/CraftAssignedMarker")
                ?? FindChildGameObject("CraftAssignedMarker");
            if (MarkersShareReference())
                _craftAssignedMarker = null;

            ResolveCraftAssignedMarkerParts();
            _equippedDimOverlay ??= ResolveEquippedDimOverlay();

            ConfigureSelectedFrame();
            ConfigureEquippedMarker();
            ConfigureCraftAssignedMarker();
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
                _craftAssignedMarker.SetActive(CanControlCraftAssignedMarker());
        }

        private void ResolveEquippedMarkerParts()
        {
            if (_equippedMarker == null)
                return;

            _equippedMarkerImage ??= _equippedMarker.GetComponent<Image>();
            _equippedText ??=
                _equippedMarker.transform.Find("EquippedText")?.GetComponent<TMP_Text>()
                ?? _equippedMarker.transform.Find("EquipText")?.GetComponent<TMP_Text>();
        }

        private void ResolveCraftAssignedMarkerParts()
        {
            if (_craftAssignedMarker == null)
                return;

            _craftAssignedMarkerImage ??= _craftAssignedMarker.GetComponent<Image>();
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
            overlayImage.color = EquippedDimOverlayColor;
            overlayImage.raycastTarget = false;
            overlayObject.SetActive(false);
            _createdEquippedDimOverlay = true;
            PlaceGeneratedEquippedDimOverlay(overlayImage.rectTransform);
            return overlayImage;
        }

        private void ConfigureEquippedDimOverlay()
        {
            if (_equippedDimOverlay == null)
                return;

            _equippedDimOverlay.color = EquippedDimOverlayColor;
            _equippedDimOverlay.raycastTarget = false;
            if (_createdEquippedDimOverlay)
                StretchToFill(_equippedDimOverlay.rectTransform);
        }

        private void ConfigureEquippedMarker()
        {
            if (_equippedMarker == null || _visualRootRect == null)
                return;

            ResolveEquippedMarkerParts();

            if (_equippedMarker.transform is RectTransform markerRect)
            {
                float iconShortSide = GetIconShortSide();
                if (iconShortSide > 0f)
                {
                    float markerSize = CalculateMarkerSize(iconShortSide);

                    markerRect.anchorMin = new Vector2(0f, 1f);
                    markerRect.anchorMax = new Vector2(0f, 1f);
                    markerRect.pivot = new Vector2(0f, 1f);
                    markerRect.anchoredPosition = new Vector2(5f, -5f);
                    markerRect.sizeDelta = new Vector2(markerSize, markerSize);

                    if (_equippedText != null)
                        _equippedText.gameObject.SetActive(
                            markerSize >= EquippedTextVisibleMinSize
                        );

                    ConfigureEquippedTextSize(markerSize);
                }
            }

            if (_equippedMarkerImage != null)
            {
                _equippedMarkerImage.color = EquippedMarkerColor;
                _equippedMarkerImage.raycastTarget = false;
            }

            if (_equippedText != null)
            {
                _equippedText.text = "E";
                _equippedText.color = EquippedTextColor;
                _equippedText.raycastTarget = false;
            }
        }

        private void ConfigureCraftAssignedMarker()
        {
            if (_craftAssignedMarker == null)
                return;

            ResolveCraftAssignedMarkerParts();

            if (_craftAssignedMarker.transform is RectTransform markerRect)
            {
                float iconShortSide = GetIconShortSide();
                if (iconShortSide > 0f)
                {
                    float markerSize = CalculateMarkerSize(iconShortSide);

                    markerRect.anchorMin = new Vector2(0f, 1f);
                    markerRect.anchorMax = new Vector2(0f, 1f);
                    markerRect.pivot = new Vector2(0f, 1f);
                    markerRect.anchoredPosition = new Vector2(5f, -5f);
                    markerRect.sizeDelta = new Vector2(markerSize, markerSize);
                }
            }

            if (_craftAssignedMarkerImage != null)
            {
                _craftAssignedMarkerImage.color = CraftAssignedMarkerColor;
                _craftAssignedMarkerImage.raycastTarget = false;
            }

            _craftAssignedMarker.SetActive(CanControlCraftAssignedMarker());
        }

        private bool ShouldShowCraftAssignedMarker() => _isCraftAssigned && !_isEquipped;

        private bool CanControlCraftAssignedMarker() =>
            !MarkersShareReference() && ShouldShowCraftAssignedMarker();

        private bool MarkersShareReference() =>
            _equippedMarker != null
            && _craftAssignedMarker != null
            && ReferenceEquals(_equippedMarker, _craftAssignedMarker);

        private static float CalculateMarkerSize(float iconShortSide)
        {
            float markerSize = Mathf.Clamp(
                iconShortSide * EquippedMarkerScale,
                EquippedMarkerMinSize,
                EquippedMarkerMaxSize
            );
            return Mathf.Min(markerSize, iconShortSide * EquippedMarkerMaxIconRatio);
        }

        private float GetIconShortSide()
        {
            _iconRect ??=
                _iconImage != null
                    ? _iconImage.rectTransform
                    : transform.Find("VisualRoot/Icon") as RectTransform
                        ?? transform.Find("Icon") as RectTransform;

            if (_iconRect == null)
                return 0f;

            return Mathf.Min(_iconRect.rect.width, _iconRect.rect.height);
        }

        private void ConfigureEquippedTextSize(float markerSize)
        {
            if (_equippedText == null)
                return;

            float fontSize = markerSize * EquippedTextMaxFontSizeScale;
            _equippedText.fontSizeMax = fontSize;
            _equippedText.fontSize = fontSize;
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
    }
}
