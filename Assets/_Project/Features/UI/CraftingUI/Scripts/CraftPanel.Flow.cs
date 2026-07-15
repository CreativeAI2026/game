using System.Collections;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public partial class CraftPanel
    {
        private void FindCraftFlowReferences()
        {
            _loadingPanel ??= FindDescendant("LoadingPanel")?.gameObject;
            _loadingGear ??= FindDescendant("LoadingGear") as RectTransform;
            _resultPanel ??= FindDescendant("ResultPanel")?.gameObject;
            if (_resultPanel != null)
            {
                _resultPanelBackground ??= FindGameObjectIn(_resultPanel.transform, "Background");
                _resultPanelTitle ??= FindComponentIn<TMP_Text>(_resultPanel.transform, "Title");
                _resultItemImage ??=
                    FindComponentIn<Image>(_resultPanel.transform, "Icon")
                    ?? FindComponentIn<Image>(_resultPanel.transform, "ItemImage");
                _resultItemName ??= FindComponentIn<TMP_Text>(_resultPanel.transform, "ItemName");
            }
            _closeButton ??= FindDescendant("CloseButton")?.gameObject;
            _closeButtonButton ??= _closeButton?.GetComponent<Button>();
            UIButtonHoverScaleUtility.ApplyTo(_closeButtonButton);
        }

        private void BindCraftFlow()
        {
            if (_resultPanel == null)
            {
                WarnMissingReferenceOnce(ref _warnedMissingResultPanel, "ResultPanel");
                return;
            }

            _resultCloseOnSelfClick = CraftFlowViewUtility.PrepareCloseOnSelfClick(
                _resultPanel,
                HideSharedResult
            );
        }

        public void ShowLoading()
        {
            CraftFlowViewUtility.ShowLoading(_loadingPanel, _loadingGear, _resultPanel);
        }

        public void HideLoading()
        {
            CraftFlowViewUtility.HideLoadingGear(_loadingGear);
            CraftFlowViewUtility.HideLoadingPanel(_loadingPanel);
        }

        public void HideLoadingAndResult()
        {
            CraftFlowViewUtility.HidePanels(_loadingPanel, _resultPanel);
        }

        public void RotateLoadingGear(float speed)
        {
            if (_loadingGear != null)
                _loadingGear.Rotate(0f, 0f, -speed * Time.unscaledDeltaTime);
        }

        public void ShowResult(ItemData resultItem, int count, System.Action closeAction)
        {
            FindCraftFlowReferences();
            if (_resultPanel == null)
            {
                WarnMissingReferenceOnce(ref _warnedMissingResultPanel, "ResultPanel");
                return;
            }

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
