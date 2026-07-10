using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    [RequireComponent(typeof(Image))]
    public class ResultPanelClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        private Action _clickAction;

        private void Awake()
        {
            var image = GetComponent<Image>();
            image.raycastTarget = true;
        }

        public void SetClickAction(Action clickAction)
        {
            _clickAction = clickAction;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _clickAction?.Invoke();
        }
    }
}
