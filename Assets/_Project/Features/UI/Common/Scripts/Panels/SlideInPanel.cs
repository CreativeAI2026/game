using DG.Tweening;
using UnityEngine;

namespace CreativeAI.UI
{
    public class SlideInPanel : MonoBehaviour
    {
        public enum SlideDirection
        {
            Left,
            Right,
            Up,
            Down,
        }

        [SerializeField]
        private SlideDirection _direction = SlideDirection.Left;

        [SerializeField]
        private float _duration = 0.35f;

        [SerializeField]
        private Ease _ease = Ease.OutCubic;

        private RectTransform _rect;
        private Vector2 _shownPos;
        private Vector2 _hiddenPos;
        private bool _initialized = false;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        private void Start()
        {
            if (_rect == null)
                return;

            _shownPos = _rect.anchoredPosition;
            _hiddenPos = GetHiddenPos();
            _rect.anchoredPosition = _hiddenPos;
            _initialized = true;

            PlayShowAnimation();
        }

        private void OnEnable()
        {
            if (!_initialized)
                return;

            PlayShowAnimation();
        }

        private void OnDisable()
        {
            if (_rect != null)
                _rect.DOKill();
        }

        private void PlayShowAnimation()
        {
            if (_rect == null)
                return;

            _rect.DOKill();
            _rect.anchoredPosition = _hiddenPos;
            DOTween
                .To(
                    () => _rect.anchoredPosition,
                    v => _rect.anchoredPosition = v,
                    _shownPos,
                    _duration
                )
                .SetEase(_ease)
                .SetUpdate(true);
        }

        private Vector2 GetHiddenPos() =>
            _direction switch
            {
                SlideDirection.Left => new Vector2(_shownPos.x - _rect.rect.width, _shownPos.y),
                SlideDirection.Right => new Vector2(_shownPos.x + _rect.rect.width, _shownPos.y),
                SlideDirection.Up => new Vector2(_shownPos.x, _shownPos.y + _rect.rect.height),
                SlideDirection.Down => new Vector2(_shownPos.x, _shownPos.y - _rect.rect.height),
                _ => _shownPos,
            };
    }
}
