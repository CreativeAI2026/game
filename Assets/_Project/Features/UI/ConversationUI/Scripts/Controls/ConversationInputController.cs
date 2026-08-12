using System;
using UnityEngine.InputSystem;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話UIのキーボードショートカットと操作可否更新を担当する。</summary>
    internal sealed class ConversationInputController
    {
        private readonly ConversationControlsPresenter _controls;
        private readonly ConversationChromePresenter _chrome;
        private readonly DialogueAdvanceController _advance;
        private readonly Func<ConversationView.ConversationState> _state;
        private readonly Func<bool> _historyOpen;
        private readonly Func<bool> _windowHidden;
        private readonly Func<bool> _rewardActive;
        private readonly Func<bool> _autoMode;
        private readonly Func<ConversationView.TextSpeed> _textSpeed;
        private readonly Action<bool> _setAutoMode;
        private readonly Action<bool> _setWindowHidden;
        private readonly Action<ConversationView.TextSpeed> _setTextSpeed;
        private readonly Action _handleChoiceInput;

        public ConversationInputController(
            ConversationControlsPresenter controls,
            ConversationChromePresenter chrome,
            DialogueAdvanceController advance,
            Func<ConversationView.ConversationState> state,
            Func<bool> historyOpen,
            Func<bool> windowHidden,
            Func<bool> rewardActive,
            Func<bool> autoMode,
            Func<ConversationView.TextSpeed> textSpeed,
            Action<bool> setAutoMode,
            Action<bool> setWindowHidden,
            Action<ConversationView.TextSpeed> setTextSpeed,
            Action handleChoiceInput
        )
        {
            _controls = controls;
            _chrome = chrome;
            _advance = advance;
            _state = state;
            _historyOpen = historyOpen;
            _windowHidden = windowHidden;
            _rewardActive = rewardActive;
            _autoMode = autoMode;
            _textSpeed = textSpeed;
            _setAutoMode = setAutoMode;
            _setWindowHidden = setWindowHidden;
            _setTextSpeed = setTextSpeed;
            _handleChoiceInput = handleChoiceInput;
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;
            bool historyOpen = _historyOpen();
            bool hidden = _windowHidden() || _state() == ConversationView.ConversationState.Hidden;
            bool choices = _state() == ConversationView.ConversationState.ShowingChoices;

            if (keyboard != null && keyboard.aKey.wasPressedThisFrame && !historyOpen && !hidden)
            {
                _controls.PlayAutoShortcut();
                _setAutoMode(!_autoMode());
            }
            if (
                keyboard != null
                && keyboard.sKey.wasPressedThisFrame
                && !historyOpen
                && !hidden
                && !_rewardActive()
                && !choices
            )
                _controls.PlaySkipShortcut();
            if (
                keyboard != null
                && keyboard.hKey.wasPressedThisFrame
                && !choices
                && !_rewardActive()
                && !historyOpen
            )
            {
                _controls.PlayHideShortcut();
                _setWindowHidden(!_windowHidden());
            }
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame && !historyOpen && !hidden)
            {
                _controls.PlaySpeedShortcut();
                _setTextSpeed((ConversationView.TextSpeed)(((int)_textSpeed() + 1) % 4));
            }

            if (choices && !historyOpen)
                _handleChoiceInput();

            bool autoProgressVisible =
                _state() == ConversationView.ConversationState.WaitingForAdvance
                && !_windowHidden()
                && !historyOpen;
            _chrome.Tick(_autoMode(), autoProgressVisible, _advance.Progress);
            _controls.SetAvailability(historyOpen, hidden, _rewardActive(), choices);
        }
    }
}
