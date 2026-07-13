using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace CreativeAI.UI.Common
{
    public class CloseOnSelfClick : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private GameObject _targetToHide;

        [SerializeField]
        private UnityEvent _onSelfClick;

        private Action _runtimeSelfClickAction;

        public void SetClickAction(Action clickAction)
        {
            _runtimeSelfClickAction = clickAction;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (
                eventData.button != PointerEventData.InputButton.Left
                || eventData.pointerPressRaycast.gameObject != gameObject
                || eventData.pointerCurrentRaycast.gameObject != gameObject
            )
            {
                return;
            }

            _onSelfClick?.Invoke();
            if (_runtimeSelfClickAction != null)
            {
                _runtimeSelfClickAction.Invoke();
                return;
            }

            if (_targetToHide != null)
                _targetToHide.SetActive(false);
        }
    }
}
