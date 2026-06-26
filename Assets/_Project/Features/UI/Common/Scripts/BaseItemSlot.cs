using System;
using CreativeAI.Gameplay;
using CreativeAI.UI.InventoryUI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(HoverScaleOnPointer))]
    public abstract class BaseItemSlot : MonoBehaviour
    {
        [SerializeField]
        protected Image _iconImage;

        [SerializeField]
        protected TMP_Text _countText;

        [SerializeField]
        protected HoverScaleOnPointer _hoverScale;

        protected ItemData _item;
        protected int _count;

        private bool _isInitialized;
        private CanvasGroup _countCanvasGroup;
        private const float ItemTransitionDuration = 0.2f;

        public ItemData Item => _item;
        public int Count => _count;

        protected virtual void Awake()
        {
            InitializeBase();
            Refresh();
        }

        protected void InitializeBase()
        {
            if (_isInitialized)
                return;

            _iconImage ??= GetOrCreateIconImage();
            _countText ??= FindCountText();
            if (_countText != null)
            {
                _countCanvasGroup = _countText.GetComponent<CanvasGroup>();
                if (_countCanvasGroup == null)
                    _countCanvasGroup = _countText.gameObject.AddComponent<CanvasGroup>();
            }
            _hoverScale ??= GetComponent<HoverScaleOnPointer>();
            _hoverScale ??= GetComponentInChildren<HoverScaleOnPointer>(true);

            BindHoverTargets();
            _isInitialized = true;
        }

        public virtual void SetItem(ItemData item, int count = 1)
        {
            InitializeBase();
            KillItemTransition();
            _item = item;
            _count = item == null ? 0 : Mathf.Max(0, count);
            Refresh();
            ResetItemVisuals();
        }

        public void SetItemAnimated(ItemData item, int count = 1)
        {
            SetItem(item, count);
            if (_iconImage == null || item == null || item.icon == null)
                return;

            var iconRect = _iconImage.rectTransform;
            iconRect.localScale = Vector3.one * 0.55f;
            _iconImage.color = new Color(1f, 1f, 1f, 0f);

            iconRect
                .DOScale(Vector3.one, ItemTransitionDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            _iconImage.DOFade(1f, ItemTransitionDuration * 0.65f).SetUpdate(true);

            if (_countText != null && _countText.gameObject.activeSelf)
            {
                _countCanvasGroup.alpha = 1f;
                _countText.rectTransform.localScale = Vector3.one * 0.7f;
                _countText
                    .rectTransform.DOScale(Vector3.one, ItemTransitionDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
        }

        public virtual void Clear()
        {
            InitializeBase();
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

            ResetItemVisuals();
        }

        public void ClearAnimated(Action onComplete = null)
        {
            InitializeBase();
            KillItemTransition();

            _item = null;
            _count = 0;

            if (_iconImage == null || !_iconImage.gameObject.activeSelf)
            {
                Clear();
                onComplete?.Invoke();
                return;
            }

            var iconRect = _iconImage.rectTransform;
            var sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(
                iconRect.DOScale(Vector3.one * 0.35f, ItemTransitionDuration).SetEase(Ease.InBack)
            );
            sequence.Join(_iconImage.DOFade(0f, ItemTransitionDuration));

            if (_countText != null && _countText.gameObject.activeSelf)
            {
                sequence.Join(
                    _countText.rectTransform.DOScale(Vector3.one * 0.35f, ItemTransitionDuration)
                );
                if (_countCanvasGroup != null)
                    sequence.Join(_countCanvasGroup.DOFade(0f, ItemTransitionDuration));
            }

            sequence.OnComplete(() =>
            {
                Clear();
                onComplete?.Invoke();
            });
        }

        protected void SetCount(int count)
        {
            _count = _item == null ? 0 : Mathf.Max(0, count);
            Refresh();
        }

        protected virtual void Refresh()
        {
            bool hasItem = _item != null && _item.icon != null;

            if (_iconImage != null)
            {
                _iconImage.sprite = hasItem ? _item.icon : null;
                _iconImage.color = Color.white;
                _iconImage.gameObject.SetActive(hasItem);
            }

            if (_countText != null)
            {
                _countText.text = hasItem ? _count.ToString() : string.Empty;
                _countText.gameObject.SetActive(hasItem && _count > 1);
            }

            BindHoverTargets();
        }

        public virtual void Select()
        {
            _hoverScale?.AcquireLock();
        }

        public virtual void Deselect()
        {
            if (_hoverScale != null && _hoverScale.IsLocked())
                _hoverScale.ReleaseLock();
        }

        protected void BindHoverTargets()
        {
            if (_hoverScale == null)
                return;

            if (_iconImage != null)
                _hoverScale.SetTarget(_iconImage.rectTransform);
            if (_countText != null)
                _hoverScale.SetBounceTarget(_countText.rectTransform);
        }

        private void KillItemTransition()
        {
            if (_iconImage != null)
            {
                _iconImage.DOKill();
                _iconImage.rectTransform.DOKill();
            }

            if (_countText != null)
            {
                _countText.DOKill();
                _countText.rectTransform.DOKill();
                _countCanvasGroup?.DOKill();
            }
        }

        private void ResetItemVisuals()
        {
            if (_iconImage != null)
            {
                _iconImage.rectTransform.localScale = Vector3.one;
                var color = _iconImage.color;
                color.a = 1f;
                _iconImage.color = color;
            }

            if (_countText != null)
            {
                _countText.rectTransform.localScale = Vector3.one;
                var color = _countText.color;
                color.a = 1f;
                _countText.color = color;
                if (_countCanvasGroup != null)
                    _countCanvasGroup.alpha = 1f;
            }
        }

        private Image GetOrCreateIconImage()
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null && iconTransform.TryGetComponent(out Image icon))
                return icon;

            var rootImage = GetComponent<Image>();
            var iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            iconRect.SetAsFirstSibling();

            var iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            if (rootImage != null)
            {
                iconImage.sprite = rootImage.sprite;
                iconImage.color = rootImage.color;
                rootImage.sprite = null;
                rootImage.color = Color.clear;
            }

            return iconImage;
        }

        private TMP_Text FindCountText()
        {
            var countTransform = transform.Find("CountText");
            if (countTransform != null && countTransform.TryGetComponent(out TMP_Text countText))
                return countText;

            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.transform == transform || text.name == "EmptyText")
                    continue;

                return text;
            }

            return null;
        }
    }
}
