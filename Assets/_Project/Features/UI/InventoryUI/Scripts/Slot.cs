using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class Slot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private float _hoverScale = 1.2f; // ホバー時の拡大率

        [SerializeField]
        private float _animationDuration = 0.2f; // アニメーションの時間
        private RectTransform _icon; // アイコンのRectTransform
        private Image _iconImage;
        private ItemData _itemData;
        Coroutine _currentAnimation;

        private void Awake()
        {
            _iconImage = GetComponentInChildren<Image>(true);
            if (_iconImage != null)
                _icon = _iconImage.rectTransform;
        }

        public void SetItem(ItemData item)
        {
            _itemData = item;

            if (_iconImage == null)
                return;

            if (item != null && item.icon != null)
            {
                _iconImage.sprite = item.icon;
                _iconImage.color = Color.white;
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.color = new Color(0, 0, 0, 0);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StartScale(Vector3.one * _hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StartScale(Vector3.one);
        }

        private void StartScale(Vector3 target)
        {
            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);
            _currentAnimation = StartCoroutine(ScaleTo(target));
        }

        private IEnumerator ScaleTo(Vector3 target)
        {
            if (_icon == null)
                yield break;

            Vector3 initialScale = _icon.localScale;
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                _icon.localScale = Vector3.Lerp(initialScale, target, elapsed / _animationDuration);
                yield return null;
            }
            _icon.localScale = target;
        }
    }
}
