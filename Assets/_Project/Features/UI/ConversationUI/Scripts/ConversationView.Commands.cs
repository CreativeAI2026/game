using System.Collections;

namespace CreativeAI.UI.ConversationUI
{
    public sealed partial class ConversationView
    {
        public void SetAutoMode(bool enabled)
        {
            if (IsAutoMode == enabled)
                return;

            IsAutoMode = enabled;
            InitializePresenters();
            _chromePresenter.SetAutoMode(enabled);
            _controlsPresenter.SetAutoActive(enabled);
        }

        private void BindControlButtons()
        {
            _controlsPresenter.Bind(
                () => SetAutoMode(!IsAutoMode),
                () =>
                {
                    _skipMode = !_skipMode;
                    _controlsPresenter.SetSkipActive(_skipMode);
                },
                () => SetTextSpeed((TextSpeed)(((int)_textSpeed + 1) % 4)),
                () => SetWindowHidden(!_windowManuallyHidden)
            );
        }

        public void SetWindowHidden(bool hidden)
        {
            _windowManuallyHidden = hidden;
            InitializePresenters();
            _chromePresenter.SetWindowHidden(hidden);
            _controlsPresenter.SetHideActive(hidden);
        }

        public void SetTextSpeed(TextSpeed speed)
        {
            _textSpeed = speed;
            InitializePresenters();
            _chromePresenter.SetTextSpeed(speed);
            _speedPresenter.SetSpeed(speed, true);
        }

        public bool IsLineRead(string speaker, string portrait, string text) =>
            _sessionState.IsLineRead(speaker, portrait, text);

        public void MarkLineRead(string speaker, string portrait, string text) =>
            _sessionState.MarkLineRead(speaker, portrait, text);

        public void ClearReadHistory() => _sessionState.ClearReadHistory();

        public void SetPortraitVisible(DialoguePortraitSide side, bool visible)
        {
            InitializePresenters();
            _portraitPresenter.SetVisible(side, visible);
        }

        public IEnumerator PlayPortraitEffect(
            DialoguePortraitSide side,
            PortraitEffect effect,
            float duration = 0.28f
        )
        {
            InitializePresenters();
            yield return _portraitPresenter.PlayEffect(side, effect, duration);
        }

        public IEnumerator SetPortraitObscured(
            DialoguePortraitSide side,
            bool obscured,
            float duration = 0.5f
        )
        {
            InitializePresenters();
            yield return _portraitPresenter.SetObscured(side, obscured, duration);
        }

        public IEnumerator RunPresentationCommand(string command, string argument = null)
        {
            InitializePresenters();
            yield return _presentationCommandRouter.Execute(command, argument);
        }
    }
}
