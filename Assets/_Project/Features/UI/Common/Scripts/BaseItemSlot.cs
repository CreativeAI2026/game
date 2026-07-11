using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(HoverScaleOnPointer))]
    public abstract partial class BaseItemSlot : MonoBehaviour
    {
        [SerializeField]
        protected Image _iconImage;

        [SerializeField]
        protected TMP_Text _countText;

        [SerializeField]
        protected RectTransform _countContainer;

        [SerializeField]
        protected HoverScaleOnPointer _hoverScale;

        protected ItemData _item;
        protected int _count;
        protected bool _isSlotSelected;

        private const float CountContainerVisibleAlpha = 200f / 255f;

        private bool _isInitialized;
        private CanvasGroup _countCanvasGroup;
        private CanvasGroup _countContainerCanvasGroup;
        private Image _countContainerImage;

        public ItemData Item => _item;
        public int Count => _count;

        protected virtual void Awake()
        {
            InitializeBase();
            Refresh();
        }

        public virtual void SetItem(ItemData item, int count = 1)
        {
            InitializeBase();
            KillItemTransition();

            _item = item;
            _count = item == null ? 0 : Mathf.Max(0, count);

            SetCountContainerVisible(ShouldShowCountBadge());

            Refresh();
            ResetItemVisuals();
        }

        public virtual void Clear()
        {
            InitializeBase();
            EnsureCountReferences();
            KillItemTransition();

            _item = null;
            _count = 0;

            if (_iconImage != null)
            {
                _iconImage.sprite = null;
                _iconImage.gameObject.SetActive(false);
            }

            if (_countText != null)
            {
                _countText.text = string.Empty;
                _countText.gameObject.SetActive(false);
            }

            SetCountContainerVisible(false);
            _isSlotSelected = false;
            RefreshSelectionVisuals();

            ResetItemVisuals();
        }

        protected void SetCount(int count)
        {
            _count = _item == null ? 0 : Mathf.Max(0, count);
            Refresh();
        }

        protected virtual void Refresh()
        {
            EnsureCountReferences();
            bool hasItem = _item != null;
            bool hasIcon = _item != null && _item.icon != null;
            bool showCountBadge = ShouldShowCountBadge();

            if (_iconImage != null)
            {
                _iconImage.sprite = hasIcon ? _item.icon : null;
                _iconImage.color = Color.white;
                _iconImage.raycastTarget = false;
                _iconImage.gameObject.SetActive(hasIcon);
            }

            if (_countText != null)
            {
                _countText.text = hasItem ? _count.ToString() : string.Empty;
                _countText.raycastTarget = false;
                _countText.gameObject.SetActive(showCountBadge);
            }

            SetCountContainerVisible(showCountBadge);

            BindHoverTargets();
        }

        private bool ShouldShowCountBadge() => _item != null && _item.MaxStack > 1 && _count > 1;

        private void SetCountContainerVisible(bool visible)
        {
            if (_countContainer != null)
            {
                _countContainer.gameObject.SetActive(visible);
                _countContainerCanvasGroup ??= _countContainer.GetComponent<CanvasGroup>();
                if (_countContainerCanvasGroup == null)
                    _countContainerCanvasGroup =
                        _countContainer.gameObject.AddComponent<CanvasGroup>();
            }

            if (_countContainerImage == null && _countContainer != null)
                _countContainerImage = _countContainer.GetComponent<Image>();
            if (_countContainerImage == null && _countContainer != null)
                _countContainerImage = _countContainer.gameObject.AddComponent<Image>();

            if (_countText != null)
            {
                _countText.raycastTarget = false;
                _countText.gameObject.SetActive(visible);
            }

            if (_countContainerImage != null)
            {
                _countContainerImage.enabled = true;
                _countContainerImage.color = new Color32(0, 0, 0, 200);
                _countContainerImage.raycastTarget = false;
                _countContainerImage.canvasRenderer.SetAlpha(1f);
                _countContainerImage.SetVerticesDirty();
            }

            if (_countContainerCanvasGroup != null)
            {
                _countContainerCanvasGroup.alpha = visible ? 1f : 0f;
                _countContainerCanvasGroup.interactable = false;
                _countContainerCanvasGroup.blocksRaycasts = false;
            }
        }

        public virtual void Select()
        {
            _isSlotSelected = true;
            RefreshSelectionVisuals();
            _hoverScale?.AcquireLock();
        }

        public virtual void Deselect()
        {
            _isSlotSelected = false;
            RefreshSelectionVisuals();
            if (_hoverScale != null && _hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }

        protected virtual void RefreshSelectionVisuals() { }
    }
}
