using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftQuantityDialogUtility
    {
        public static void BindButton(Button button, UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public static void HideImmediately(GameObject panel, GameObject dialog)
        {
            if (dialog != null)
                dialog.SetActive(false);
            if (panel != null)
                panel.SetActive(false);
        }
    }
}
