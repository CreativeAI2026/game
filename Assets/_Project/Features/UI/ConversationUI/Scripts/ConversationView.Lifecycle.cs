using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    public sealed partial class ConversationView
    {
        private void SetChoicesActive(bool active)
        {
            _choicePresenter?.SetActive(active);
            _chromePresenter?.SetChoiceGuide(active);
        }

        private void EnsureHistoryPanel()
        {
            if (_historyPanel == null)
                _historyPanel = GetComponent<DialogueHistoryPanel>();
            if (_historyPanel == null)
                _historyPanel = gameObject.AddComponent<DialogueHistoryPanel>();

            _historyPanel.Initialize(_nameText != null ? _nameText.font : null);
            if (!_historyEventsBound)
            {
                _historyPanel.Closed += HandleHistoryClosed;
                _historyEventsBound = true;
            }
        }

        private void HandleHistoryClosed() => BlockAdvanceInput(0.12f);

        private void BlockAdvanceInput(float duration)
        {
            _advanceBlockedUntil = Mathf.Max(
                _advanceBlockedUntil,
                Time.unscaledTime + Mathf.Max(0f, duration)
            );
        }

        private IEnumerator ShowAnimated()
        {
            InitializePresenters();
            yield return _chromePresenter.Show();
        }

        public IEnumerator HideAnimated(float duration = 0.2f)
        {
            State = ConversationState.Exiting;
            InitializePresenters();
            float textDuration = Mathf.Max(0.05f, duration * 0.35f);
            float portraitDuration = Mathf.Max(0.06f, duration * 0.4f);
            float windowDuration = Mathf.Max(0.07f, duration * 0.45f);
            yield return _chromePresenter.HideLineText(textDuration);
            yield return _portraitPresenter.FadeOutAll(portraitDuration);
            yield return _chromePresenter.Hide(windowDuration);
            _rewardPresenter.HideAll();
            _choicePresenter.Clear();
            _choicePresenter.SetActive(false);
            ResetTransientModes();
            State = ConversationState.Hidden;
        }

        private void HideImmediate()
        {
            State = ConversationState.Exiting;
            _rewardPresenter?.HideAll();
            SetChoicesActive(false);
            ResetTransientModes();
            _chromePresenter?.HideImmediate();
            State = ConversationState.Hidden;
        }

        private void ResetTransientModes()
        {
            _rewardPresentationActive = false;
            _skipMode = false;
            _windowManuallyHidden = false;
            _advanceBlockedUntil = 0f;
            _controlsPresenter.SetSkipActive(false);
            _controlsPresenter.SetHideActive(false);
            if (_historyPanel != null && _historyPanel.IsOpen)
                _historyPanel.SetOpen(false);
        }

        private void CancelRewardPresentation()
        {
            _rewardPresenter?.HideAll();
            _rewardPresentationActive = false;
        }
    }
}
