using System;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public static class CraftFlowViewUtility
    {
        private static readonly System.Collections.Generic.HashSet<GameObject> WarnedMissingCloseOnSelfClick =
            new();

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

        public static CloseOnSelfClick PrepareCloseOnSelfClick(GameObject panel, Action clickAction)
        {
            if (panel == null)
                return null;

            var closeOnSelfClick = panel.GetComponent<CloseOnSelfClick>();
            if (closeOnSelfClick == null)
            {
                WarnMissingCloseOnSelfClickOnce(panel);
                return null;
            }

            closeOnSelfClick.SetClickAction(clickAction);
            return closeOnSelfClick;
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
                CraftUIAnimationUtility.HideResultImmediately(resultPanel);
        }

        public static void HideLoadingGear(RectTransform loadingGear)
        {
            if (loadingGear != null)
                loadingGear.gameObject.SetActive(false);
        }

        public static void HideLoadingPanel(GameObject loadingPanel)
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        public static void HidePanels(GameObject loadingPanel, GameObject resultPanel)
        {
            if (resultPanel != null)
                CraftUIAnimationUtility.HideResultImmediately(resultPanel);
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
            if (PrepareCloseOnSelfClick(resultPanel, closeAction) == null)
                return;

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

        private static void WarnMissingCloseOnSelfClickOnce(GameObject panel)
        {
            if (panel == null)
                return;

            if (!WarnedMissingCloseOnSelfClick.Add(panel))
                return;

            Debug.LogWarning(
                $"{nameof(CraftFlowViewUtility)}: {panel.name} に {nameof(CloseOnSelfClick)} が見つかりません。Inspectorで追加してから外側クリックの閉じる処理を設定してください。",
                panel
            );
        }
    }
}
