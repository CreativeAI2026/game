using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private bool ValidateCraftFlowReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_loadingPanel, nameof(_loadingPanel));
            valid &= ValidateRequiredReference(_loadingGear, nameof(_loadingGear));
            valid &= ValidateRequiredReference(_resultPanel, nameof(_resultPanel));
            valid &= ValidateRequiredReference(
                _resultPanelBackground,
                nameof(_resultPanelBackground)
            );
            valid &= ValidateRequiredReference(_resultPanelTitle, nameof(_resultPanelTitle));
            valid &= ValidateRequiredReference(_resultItemImage, nameof(_resultItemImage));
            valid &= ValidateRequiredReference(_resultItemName, nameof(_resultItemName));
            valid &= ValidateRequiredReference(_closeButton, nameof(_closeButton));
            valid &= ValidateRequiredReference(_closeButtonButton, nameof(_closeButtonButton));

            if (_closeButtonButton != null)
                UIButtonHoverScaleUtility.ApplyTo(_closeButtonButton);

            return valid;
        }

        private bool ValidateLoadingReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_loadingPanel, nameof(_loadingPanel));
            valid &= ValidateRequiredReference(_loadingGear, nameof(_loadingGear));
            valid &= ValidateRequiredReference(_resultPanel, nameof(_resultPanel));
            return valid;
        }

        private bool ValidateResultReferences()
        {
            bool valid = true;
            valid &= ValidateRequiredReference(_resultPanel, nameof(_resultPanel));
            valid &= ValidateRequiredReference(_resultItemImage, nameof(_resultItemImage));
            valid &= ValidateRequiredReference(_resultItemName, nameof(_resultItemName));
            return valid;
        }

        private void BindCraftFlow()
        {
            if (!ValidateRequiredReference(_resultPanel, nameof(_resultPanel)))
                return;

            _resultCloseOnSelfClick = CraftFlowViewUtility.PrepareCloseOnSelfClick(
                _resultPanel,
                HideSharedResult
            );
        }

        public void ShowLoading()
        {
            if (!ValidateLoadingReferences())
                return;

            CraftFlowViewUtility.ShowLoading(_loadingPanel, _loadingGear, _resultPanel);
        }

        public void HideLoading()
        {
            if (!ValidateRequiredReference(_loadingPanel, nameof(_loadingPanel)))
                return;
            if (!ValidateRequiredReference(_loadingGear, nameof(_loadingGear)))
                return;

            CraftFlowViewUtility.HideLoadingGear(_loadingGear);
            CraftFlowViewUtility.HideLoadingPanel(_loadingPanel);
        }

        public void HideLoadingAndResult()
        {
            if (!ValidateRequiredReference(_loadingPanel, nameof(_loadingPanel)))
                return;
            if (!ValidateRequiredReference(_resultPanel, nameof(_resultPanel)))
                return;

            CraftFlowViewUtility.HidePanels(_loadingPanel, _resultPanel);
        }

        public void RotateLoadingGear(float speed)
        {
            if (!ValidateRequiredReference(_loadingGear, nameof(_loadingGear)))
                return;

            _loadingGear.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
        }

        public void ShowResult(ItemData resultItem, int count, System.Action closeAction)
        {
            if (!ValidateResultReferences())
                return;

            HideWarning();
            CraftFlowViewUtility.ShowResultPanel(
                _resultPanel,
                _resultItemImage,
                _resultItemName,
                resultItem,
                count,
                closeAction
            );
        }

        public void HideResult()
        {
            if (!ValidateRequiredReference(_resultPanel, nameof(_resultPanel)))
                return;

            CraftUIAnimationUtility.PlayResultOut(_resultPanel);
        }

        private void HideSharedResult()
        {
            HideResult();
            HideWarning();
        }

        private void ResetSharedFlow()
        {
            HideLoadingAndResult();
            HideWarning();
            if (_loadingGear != null)
                _loadingGear.localRotation = Quaternion.identity;
        }
    }
}
