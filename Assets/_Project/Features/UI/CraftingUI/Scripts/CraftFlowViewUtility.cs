using System;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftFlowViewUtility
    {
        public static void SetCloseButtonVisible(GameObject closeButton, bool visible)
        {
            if (closeButton != null)
                closeButton.SetActive(visible);
        }

        public static void StopCraftRoutine(
            MonoBehaviour owner,
            ref Coroutine routine,
            ref bool isCrafting
        )
        {
            if (routine != null && owner != null)
            {
                owner.StopCoroutine(routine);
                routine = null;
            }

            isCrafting = false;
        }

        public static void CompleteCraftRoutine(ref Coroutine routine, ref bool isCrafting)
        {
            routine = null;
            isCrafting = false;
        }

        public static ResultPanelClickCatcher PrepareClickCatcher(
            GameObject panel,
            Action clickAction
        )
        {
            if (panel == null)
                return null;

            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
                image.color = Color.clear;
            }
            image.raycastTarget = true;

            var clickCatcher = panel.GetComponent<ResultPanelClickCatcher>();
            if (clickCatcher == null)
                clickCatcher = panel.AddComponent<ResultPanelClickCatcher>();

            clickCatcher.SetClickAction(clickAction);
            return clickCatcher;
        }

        public static void ShowLoading(
            GameObject loadingPanel,
            RectTransform loadingGear,
            GameObject resultPanel
        )
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(true);
            if (loadingGear != null)
            {
                loadingGear.localRotation = Quaternion.identity;
                loadingGear.gameObject.SetActive(true);
            }
            if (resultPanel != null)
                resultPanel.SetActive(false);
        }

        public static void HideLoadingGear(RectTransform loadingGear)
        {
            if (loadingGear != null)
                loadingGear.gameObject.SetActive(false);
        }

        public static void HidePanels(GameObject loadingPanel, GameObject resultPanel)
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        public static void ShowResultPanel(
            GameObject resultPanel,
            Image itemImage,
            TMP_Text itemName,
            ItemData resultItem,
            int count,
            Action closeAction
        )
        {
            if (resultPanel == null)
                return;

            RefreshResult(itemImage, itemName, resultItem, count);
            PrepareClickCatcher(resultPanel, closeAction);
            CraftUIAnimationUtility.PlayResultIn(resultPanel);
        }

        public static void RefreshResult(
            Image itemImage,
            TMP_Text itemName,
            ItemData resultItem,
            int count
        )
        {
            if (itemImage != null)
            {
                itemImage.sprite = resultItem?.icon;
                itemImage.color = resultItem?.icon != null ? Color.white : Color.clear;
                itemImage.gameObject.SetActive(resultItem?.icon != null);
            }

            if (itemName == null)
                return;

            int safeCount = Mathf.Max(1, count);
            itemName.text =
                resultItem == null ? string.Empty
                : safeCount > 1 ? $"{resultItem.itemName} \u00d7{safeCount}"
                : resultItem.itemName;
        }
    }
}
