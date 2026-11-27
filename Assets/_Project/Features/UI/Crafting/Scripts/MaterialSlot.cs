using System;
using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public class MaterialSlot : BaseItemSlot, IPointerClickHandler
    {
        private const float EmptyIconAlpha = 0f;

        [SerializeField]
        private RectTransform _visualRootRect;

        [SerializeField]
        [Tooltip("空のときに出すもの")]
        private GameObject _emptyObject;

        [SerializeField]
        private GameObject _materialHint;

        private Coroutine _materialAnimationRoutine;
        private bool _isSelected;

        public event Action<MaterialSlot> Clicked;
        public event Action<MaterialSlot> DoubleClicked;

        protected override void Awake()
        {
            if (IconView != null)
                IconView.SetEmptyAlpha(EmptyIconAlpha);
            base.Awake();
            Clear();
            SetSelected(false);
        }

        public void SetMaterial(ItemStack stack)
        {
            SetItem(stack?.Data, stack == null ? 0 : 1);
        }

        public void SetMaterialAnimated(ItemStack stack)
        {
            SetMaterial(stack);
            PlayMaterialChangedAnimation();
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selected)
                Select();
            else
                Deselect();
        }

        public void NormalizeVisualState()
        {
            Refresh();
            if (FrameView != null)
                FrameView.SetSelected(_isSelected);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (eventData.clickCount >= 2)
                DoubleClicked?.Invoke(this);
            else
                Clicked?.Invoke(this);
        }

        protected override void Refresh()
        {
            if (IconView != null)
                IconView.SetEmptyAlpha(EmptyIconAlpha);
            base.Refresh();
            if (_emptyObject != null)
                _emptyObject.SetActive(_item == null);
            if (_materialHint != null)
                _materialHint.SetActive(_item == null);
        }

        public override void Clear()
        {
            base.Clear();
            if (_emptyObject != null)
                _emptyObject.SetActive(true);
            if (_materialHint != null)
                _materialHint.SetActive(true);
        }

        public void ClearMaterialAnimated(Action onCleared = null)
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialClearedRoutine(onCleared));
        }

        private void PlayMaterialChangedAnimation()
        {
            StopMaterialAnimation();
            _materialAnimationRoutine = StartCoroutine(PlayMaterialChangedRoutine());
        }

        private IEnumerator PlayMaterialChangedRoutine()
        {
            const float duration = 0.24f;
            var animatedRect = GetAnimatedVisualRect();
            if (animatedRect != null)
                animatedRect.localScale = Vector3.one * 0.55f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(t));

                if (animatedRect != null)
                    animatedRect.localScale = Vector3.one * scale;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (animatedRect != null)
                animatedRect.localScale = Vector3.one;
            if (FrameView != null)
                FrameView.SetSelected(_isSelected);
            _materialAnimationRoutine = null;
        }

        private IEnumerator PlayMaterialClearedRoutine(Action onCleared)
        {
            const float duration = 0.16f;
            float elapsed = 0f;
            var animatedRect = GetAnimatedVisualRect();
            Vector3 startScale = animatedRect != null ? animatedRect.localScale : Vector3.one;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                if (animatedRect != null)
                    animatedRect.localScale = Vector3.Lerp(startScale, Vector3.one * 0.35f, t);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Clear();
            onCleared?.Invoke();
            _materialAnimationRoutine = null;
            PlayMaterialChangedAnimation();
        }

        private void StopMaterialAnimation()
        {
            if (_materialAnimationRoutine == null)
                return;

            StopCoroutine(_materialAnimationRoutine);
            _materialAnimationRoutine = null;
            var animatedRect = GetAnimatedVisualRect();
            if (animatedRect != null)
                animatedRect.localScale = Vector3.one;
        }

        private RectTransform GetAnimatedVisualRect()
        {
            return _visualRootRect != transform ? _visualRootRect : null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

#if UNITY_EDITOR
        private void Reset() => AutoAssignReferences();

        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            AutoAssignSlotViews();
            if (_visualRootRect == null)
                _visualRootRect = transform.Find("VisualRoot") as RectTransform;
            if (_emptyObject == null)
            {
                var empty = transform.Find("VisualRoot/EmptyText");
                _emptyObject = empty != null ? empty.gameObject : null;
            }
        }
#endif
    }
}
