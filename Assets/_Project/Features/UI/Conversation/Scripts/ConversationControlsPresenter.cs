using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>会話HUDボタンのイベント登録と表示状態を管理する。</summary>
    internal sealed class ConversationControlsPresenter
    {
        private Button _auto;
        private Button _skip;
        private Button _speed;
        private Button _hide;
        private ConversationControlButton _autoControl;
        private ConversationControlButton _skipControl;
        private ConversationControlButton _speedControl;
        private ConversationControlButton _hideControl;
        private bool _bound;

        public void Configure(Button auto, Button skip, Button speed, Button hide)
        {
            _auto = auto;
            _skip = skip;
            _speed = speed;
            _hide = hide;
            _autoControl = FindControl(auto);
            _skipControl = FindControl(skip);
            _speedControl = FindControl(speed);
            _hideControl = FindControl(hide);
        }

        public void Bind(Action toggleAuto, Action toggleSkip, Action cycleSpeed, Action toggleHide)
        {
            if (_bound)
                return;
            _bound = true;
            if (_auto != null)
                _auto.onClick.AddListener(() => toggleAuto());
            if (_skip != null)
                _skip.onClick.AddListener(() => toggleSkip());
            if (_speed != null)
                _speed.onClick.AddListener(() => cycleSpeed());
            if (_hide != null)
                _hide.onClick.AddListener(() => toggleHide());
        }

        public void SetAvailability(bool historyOpen, bool hidden, bool rewardActive, bool choices)
        {
            SetAvailable(_auto, _autoControl, !historyOpen && !hidden);
            SetAvailable(_skip, _skipControl, !historyOpen && !hidden && !rewardActive && !choices);
            SetAvailable(_speed, _speedControl, !historyOpen && !hidden);
            SetAvailable(_hide, _hideControl, !historyOpen && !rewardActive && !choices);
        }

        public void SetAutoActive(bool active) => SetActive(_auto, _autoControl, active);

        public void SetSkipActive(bool active) => SetActive(_skip, _skipControl, active);

        public void SetHideActive(bool active) => SetActive(_hide, _hideControl, active);

        public void PlayAutoShortcut() => PlayShortcut(_autoControl);

        public void PlaySkipShortcut() => PlayShortcut(_skipControl);

        public void PlaySpeedShortcut() => PlayShortcut(_speedControl);

        public void PlayHideShortcut() => PlayShortcut(_hideControl);

        private static ConversationControlButton FindControl(Button button) =>
            button != null && button.TryGetComponent(out ConversationControlButton control)
                ? control
                : null;

        private static void SetAvailable(
            Button button,
            ConversationControlButton control,
            bool available
        )
        {
            if (button == null)
                return;
            button.interactable = available;
            if (control != null)
                control.SetAvailable(available);
        }

        private static void SetActive(Button button, ConversationControlButton control, bool active)
        {
            if (button == null)
                return;
            if (control != null)
                control.SetActiveState(active);
            else if (button.targetGraphic != null)
                button.targetGraphic.color = active
                    ? new Color(0.16f, 0.42f, 0.62f, 0.96f)
                    : new Color(0.035f, 0.045f, 0.07f, 0.86f);
        }

        private static void PlayShortcut(ConversationControlButton control)
        {
            if (control != null)
                control.PlayShortcutFeedback();
        }
    }
}
