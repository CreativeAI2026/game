using System;
using System.Collections;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanelController
    {
        private bool ValidateCraftFlowReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_loadingOverlayView, nameof(_loadingOverlayView));
            valid &= ValidateRequiredReference(_resultPanelView, nameof(_resultPanelView));
            valid &= ValidateRequiredReference(_warningToastView, nameof(_warningToastView));
            valid &= ValidateRequiredReference(_closeButton, nameof(_closeButton));

            if (_closeButton != null)
                UIButtonHoverScaleUtility.ApplyTo(_closeButton);

            return valid;
        }

        private void BindCraftFlow()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(HideSharedResult);
                _closeButton.onClick.AddListener(HideSharedResult);
            }
        }

        public IEnumerator RunCraftFlow(
            Func<bool> craftAction,
            ItemData resultItem,
            int resultCount,
            Action onResultClosed,
            Action onFailed = null,
            bool showNewBadge = false
        )
        {
            if (craftAction == null || _isCraftFlowRunning || !ValidateCraftFlowReferences())
                yield break;

            bool awaitingResultClose = false;
            try
            {
                SetCraftFlowRunning(true);
                HideWarning();
                ShowLoading();
                yield return new WaitForSecondsRealtime(CraftFlowDurationSeconds);

                bool crafted = craftAction();
                HideLoading();

                if (!crafted)
                {
                    HideLoadingAndResult();
                    onFailed?.Invoke();
                    yield break;
                }

                bool resultClosed = false;
                void CompleteResult()
                {
                    if (resultClosed)
                        return;

                    resultClosed = true;
                    try
                    {
                        onResultClosed?.Invoke();
                    }
                    finally
                    {
                        SetCraftFlowRunning(false);
                    }
                }

                ShowResult(resultItem, resultCount, CompleteResult, showNewBadge);
                awaitingResultClose = true;
            }
            finally
            {
                HideLoading();
                if (!awaitingResultClose)
                    SetCraftFlowRunning(false);
            }
        }

        public void CancelCraftFlow()
        {
            HideLoadingAndResult();
            SetCraftFlowRunning(false);
        }

        public void ShowLoading()
        {
            if (
                !ValidateRequiredReference(_loadingOverlayView, nameof(_loadingOverlayView))
                || !ValidateRequiredReference(_resultPanelView, nameof(_resultPanelView))
            )
                return;

            _resultPanelView.HideImmediate();
            _loadingOverlayView.Show();
        }

        public void HideLoading()
        {
            if (!ValidateRequiredReference(_loadingOverlayView, nameof(_loadingOverlayView)))
                return;

            _loadingOverlayView.Hide();
        }

        public void HideLoadingAndResult()
        {
            _loadingOverlayView?.Hide();
            _resultPanelView?.HideImmediate();
        }

        public void RotateLoadingGear(float speed)
        {
            if (!ValidateRequiredReference(_loadingOverlayView, nameof(_loadingOverlayView)))
                return;

            _loadingOverlayView.RotateGear(speed);
        }

        public void ShowResult(
            ItemData resultItem,
            int count,
            System.Action closeAction,
            bool showNewBadge = false
        )
        {
            if (!ValidateRequiredReference(_resultPanelView, nameof(_resultPanelView)))
                return;

            HideWarning();
            int safeCount = Mathf.Max(1, count);
            string itemName =
                resultItem == null ? string.Empty
                : safeCount > 1 ? $"{resultItem.itemName} \u00d7{safeCount}"
                : resultItem.itemName;
            string itemParameters = ItemStatTextFormatter.BuildStatsText(resultItem);
            _resultPanelView.Show(
                resultItem?.icon,
                itemName,
                itemParameters,
                showNewBadge,
                closeAction
            );
        }

        public void HideResult() => HideSharedResult();

        private void HideSharedResult()
        {
            _resultPanelView?.Hide();
            HideWarning();
        }

        private void ResetSharedFlow()
        {
            CancelCraftFlow();
            HideWarning();
        }

        private void SetCraftFlowRunning(bool running)
        {
            if (_isCraftFlowRunning == running)
                return;

            _isCraftFlowRunning = running;
            var callbacks = CraftInteractionChanged?.GetInvocationList();
            if (callbacks == null)
                return;

            foreach (var callback in callbacks)
            {
                try
                {
                    ((Action<bool>)callback).Invoke(!running);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
