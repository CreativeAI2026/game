namespace CreativeAI.UI
{
    /// <summary>会話UIのキーボードショートカットと操作可否更新を担当する。</summary>
    internal sealed class ConversationInputController
    {
        private readonly ConversationView _view;
        private readonly ConversationControlsPresenter _controls;
        private readonly ConversationChromePresenter _chrome;
        private readonly DialogueAdvanceController _advance;

        public ConversationInputController(
            ConversationView view,
            ConversationControlsPresenter controls,
            ConversationChromePresenter chrome,
            DialogueAdvanceController advance
        )
        {
            _view = view;
            _controls = controls;
            _chrome = chrome;
            _advance = advance;
        }

        public void Tick()
        {
            bool historyOpen = _view.IsHistoryOpen;
            bool hidden =
                _view.IsWindowHidden || _view.State == ConversationView.ConversationState.Hidden;
            bool choices = _view.State == ConversationView.ConversationState.ShowingChoices;

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.DialogueAuto)
                && !historyOpen
                && !hidden
            )
            {
                _controls.PlayAutoShortcut();
                _view.SetAutoMode(!_view.IsAutoMode);
            }

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.DialogueSkip)
                && !historyOpen
                && !hidden
                && !_view.IsRewardShowing
                && !choices
            )
                _controls.PlaySkipShortcut();

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.DialogueHide)
                && !choices
                && !_view.IsRewardShowing
                && !historyOpen
            )
            {
                _controls.PlayHideShortcut();
                _view.SetWindowHidden(!_view.IsWindowHidden);
            }

            if (
                UIInputActionMap.WasPressedThisFrame(UIInputActionMap.DialogueSpeed)
                && !historyOpen
                && !hidden
            )
            {
                _controls.PlaySpeedShortcut();
                _view.SetTextSpeed(
                    (ConversationView.TextSpeed)(((int)_view.CurrentTextSpeed + 1) % 4)
                );
            }

            if (choices && !historyOpen)
                _view.HandleChoiceKeys();

            // 会話していない間は表示が見えないので、AUTO 表示やボタンの状態を更新しない
            if (_view.State == ConversationView.ConversationState.Hidden)
                return;

            bool autoProgressVisible =
                _view.State == ConversationView.ConversationState.WaitingForAdvance
                && !_view.IsWindowHidden
                && !historyOpen;
            _chrome.Tick(_view.IsAutoMode, autoProgressVisible, _advance.Progress);
            _controls.SetAvailability(historyOpen, hidden, _view.IsRewardShowing, choices);
        }
    }
}
