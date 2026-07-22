using CreativeAI.Gameplay;
using TMPro;
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

        public static void RefreshItem(Image itemImage, TMP_Text itemName, ItemData item)
        {
            if (itemImage != null)
            {
                itemImage.sprite = item?.icon;
                itemImage.gameObject.SetActive(itemImage.sprite != null);
            }

            if (itemName != null)
                itemName.text = item?.itemName ?? string.Empty;
        }

        public static void RefreshQuantity(
            TMP_Text countLabel,
            TMP_InputField input,
            Button craftButton,
            int quantity,
            int max,
            bool isCrafting
        )
        {
            if (countLabel != null)
            {
                countLabel.text = $"\u4f5c\u6210\u6570\uff08\u6700\u5927 {max}\uff09";
                CraftUIAnimationUtility.PlayBump(countLabel.rectTransform);
            }
            if (input != null)
            {
                input.SetTextWithoutNotify(quantity.ToString());
                CraftUIAnimationUtility.PlayBump(input.transform as RectTransform);
            }
            if (craftButton != null)
                craftButton.interactable = max > 0 && !isCrafting;
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
