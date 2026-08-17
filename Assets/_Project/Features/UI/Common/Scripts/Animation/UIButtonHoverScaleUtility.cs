using System.Collections.Generic;
using CreativeAI.UI.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.Common
{
    public static class UIButtonHoverScaleUtility
    {
        private const float DefaultButtonHoverScale = 1.1f;
        private static readonly HashSet<Button> WarnedMissingHoverScaleButtons = new();

        public static void ApplyTo(Button button)
        {
            if (button == null)
                return;

            var hoverScale = button.GetComponent<HoverScaleOnPointer>();
            if (hoverScale == null)
            {
                WarnMissingHoverScaleOnce(button);
                return;
            }

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

        private static void WarnMissingHoverScaleOnce(Button button)
        {
            if (button == null || !WarnedMissingHoverScaleButtons.Add(button))
                return;

            Debug.LogWarning(
                $"{nameof(UIButtonHoverScaleUtility)}: Button '{button.name}' に {nameof(HoverScaleOnPointer)} がないため、Hover設定をスキップしました。PrefabまたはScene上で追加してください。",
                button
            );
        }
    }
}
