using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class SlotCountBadgeView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _container;

        [SerializeField]
        private TMP_Text _countText;

        [SerializeField]
        private CanvasGroup _containerCanvasGroup;

        [SerializeField]
        private CanvasGroup _countTextCanvasGroup;

        [SerializeField]
        private Image _backgroundImage;

        private bool _hasWarnedMissingReferences;

        public bool HasRequiredReferences => ResolveReferences();
        public bool IsVisible => _container != null && _container.gameObject.activeSelf;

        public void SetCount(ItemData item, int count)
        {
            if (!ResolveReferences())
                return;

            bool visible = item != null && item.MaxStack > 1 && count > 1;
            if (!visible)
            {
                Hide();
                return;
            }

            _countText.text = count.ToString();
            _container.gameObject.SetActive(true);
            _countText.gameObject.SetActive(true);
            _containerCanvasGroup.alpha = 1f;
            _countTextCanvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            if (!ResolveReferences())
                return;

            KillTween();
            _countText.text = string.Empty;
            _countText.gameObject.SetActive(false);
            _containerCanvasGroup.alpha = 0f;
            _container.gameObject.SetActive(false);
        }

        public void AnimateAppear(float duration)
        {
            if (!ResolveReferences() || !IsVisible)
                return;

            _countText.rectTransform.DOKill();
            _countTextCanvasGroup.DOKill();
            _countText.rectTransform.localScale = Vector3.one * 0.7f;
            _countTextCanvasGroup.alpha = 1f;
            _countText
                .rectTransform.DOScale(Vector3.one, duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void PlayHide(float duration)
        {
            if (!ResolveReferences() || !IsVisible)
                return;

            _countText.rectTransform.DOKill();
            _countTextCanvasGroup.DOKill();
            _countText.rectTransform.DOScale(Vector3.one * 0.35f, duration).SetUpdate(true);
            _countTextCanvasGroup.DOFade(0f, duration).SetUpdate(true);
        }

        public void KillTween()
        {
            if (_countText != null)
                _countText.rectTransform.DOKill();
            _countTextCanvasGroup?.DOKill();
            _containerCanvasGroup?.DOKill();
        }

        public void ResetVisual()
        {
            if (!ResolveReferences())
                return;

            KillTween();
            _countText.rectTransform.localScale = Vector3.one;
            _countTextCanvasGroup.alpha = 1f;
            _containerCanvasGroup.alpha = IsVisible ? 1f : 0f;
        }

        private bool ResolveReferences()
        {
            _container ??= FindContainer();
            if (_container != null)
            {
                _countText ??= _container.GetComponentInChildren<TMP_Text>(true);
                _containerCanvasGroup ??= _container.GetComponent<CanvasGroup>();
                _backgroundImage ??= _container.GetComponent<Image>();
            }

            if (_countText != null)
                _countTextCanvasGroup ??= _countText.GetComponent<CanvasGroup>();

            bool valid =
                _container != null
                && _countText != null
                && _containerCanvasGroup != null
                && _countTextCanvasGroup != null
                && _backgroundImage != null;
            if (!valid)
                WarnMissingReferencesOnce();

            return valid;
        }

        private RectTransform FindContainer()
        {
            if (
                transform is RectTransform selfRect
                && (name == "CountBadge" || name == "numberSlot")
            )
                return selfRect;

            return transform.Find("VisualRoot/CountBadge") as RectTransform
                ?? transform.Find("VisualRoot/numberSlot") as RectTransform
                ?? transform.Find("CountBadge") as RectTransform
                ?? transform.Find("numberSlot") as RectTransform;
        }

        private void WarnMissingReferencesOnce()
        {
            if (_hasWarnedMissingReferences)
                return;

            _hasWarnedMissingReferences = true;
            Debug.LogWarning(
                $"{nameof(SlotCountBadgeView)} '{name}' のCountBadge、CountText、Image、CanvasGroup参照が不足しているため、Count表示をスキップします。Prefab上で設定してください。",
                this
            );
        }
    }
}
