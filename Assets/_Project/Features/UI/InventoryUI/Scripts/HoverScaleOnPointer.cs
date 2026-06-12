using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CreativeAI.UI.InventoryUI
{
    public class HoverScaleOnPointer
        : MonoBehaviour,
            IPointerEnterHandler,
            IPointerExitHandler,
            IPointerClickHandler
    {
        private static readonly Dictionary<string, HoverScaleOnPointer> _lockedInstances = new();

        [SerializeField]
        private string _group = "default";

        [SerializeField]
        private bool _lockEnabled = true;

        [SerializeField]
        private float _hoverScale = 1.2f;

        [SerializeField]
        private float _animationDuration = 0.2f;

        private RectTransform _targetRect;
        private Coroutine _currentAnimation;
        private bool _isLocked;

        public void SetTarget(RectTransform target)
        {
            _targetRect = target;
        }

        private void Awake()
        {
            if (_targetRect == null)
                _targetRect = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (!_isLocked || !Input.GetMouseButtonDown(0) || IsPointerOverSelf())
                return;

            ReleaseLockedState();
        }

        private void OnDisable()
        {
            //Debug.Log($"OnDisable: {gameObject.name}, group: {_group}, isLocked: {_isLocked}");

            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
            {
                _lockedInstances.Remove(_group);
                _isLocked = false;
                if (_targetRect != null)
                    _targetRect.localScale = Vector3.one;
                if (_currentAnimation != null)
                {
                    StopCoroutine(_currentAnimation);
                    _currentAnimation = null;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isLocked)
                return;

            StartScale(Vector3.one * _hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isLocked)
                return;

            StartScale(Vector3.one);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_lockEnabled)
                return;

            LockSelection();
        }

        private void LockSelection()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current != this)
                current.ReleaseLockedState();

            _lockedInstances[_group] = this;
            _isLocked = true;
            StartScale(Vector3.one * _hoverScale);
        }

        private void ReleaseLockedState()
        {
            if (_lockedInstances.TryGetValue(_group, out var current) && current == this)
                _lockedInstances.Remove(_group);

            _isLocked = false;
            StartScale(Vector3.one);
        }

        public static HoverScaleOnPointer GetLockedInstance(string group) =>
            _lockedInstances.TryGetValue(group, out var instance) ? instance : null;

        public bool IsLocked() => _isLocked;

        public void AcquireLock()
        {
            if (!_lockEnabled)
                return;
            LockSelection();
        }

        public void ReleaseLock() => ReleaseLockedState();

        private void StartScale(Vector3 target)
        {
            if (_targetRect == null)
                return;

            if (_currentAnimation != null)
                StopCoroutine(_currentAnimation);

            _currentAnimation = StartCoroutine(ScaleTo(target));
        }

        private bool IsPointerOverSelf()
        {
            if (EventSystem.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition,
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                if (
                    result.gameObject == gameObject
                    || result.gameObject.transform.IsChildOf(transform)
                )
                    return true;
            }

            return false;
        }

        private IEnumerator ScaleTo(Vector3 target)
        {
            Vector3 initialScale = _targetRect.localScale;
            float elapsed = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                _targetRect.localScale = Vector3.Lerp(
                    initialScale,
                    target,
                    elapsed / _animationDuration
                );
                yield return null;
            }

            _targetRect.localScale = target;
            _currentAnimation = null;
        }
    }
}
