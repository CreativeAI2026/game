using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CreativeAI.UI.InventoryUI
{
    public partial class HoverScaleOnPointer
    {
        private void Update()
        {
            if (
                !_releaseLockOnOutsideClick
                || !_isLocked
                || Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame
                || IsPointerOverSelf()
            )
                return;

            ReleaseLockedState();
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

        private bool IsPointerOverSelf()
        {
            if (EventSystem.current == null || Mouse.current == null)
                return false;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue(),
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
    }
}
