using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.Common
{
    public static class UIButtonHoverScaleUtility
    {
        private const float DefaultButtonHoverScale = 1.1f;

        public static void ApplyTo(Button button)
        {
            if (button == null)
                return;

            var hoverScale = button.GetComponent<HoverScaleOnPointer>();
            if (hoverScale == null)
                hoverScale = button.gameObject.AddComponent<HoverScaleOnPointer>();

            hoverScale.SetTarget(button.transform as RectTransform);
            hoverScale.SetHoverScale(DefaultButtonHoverScale);
            hoverScale.SetBounceHeight(0f);
            hoverScale.SetLockEnabled(false);
            hoverScale.SetReleaseLockOnOutsideClick(false);
        }

        public static void ApplyToButtonsIn(Transform root)
        {
            if (root == null)
                return;

            foreach (var button in root.GetComponentsInChildren<Button>(true))
                ApplyTo(button);
        }
    }
}
