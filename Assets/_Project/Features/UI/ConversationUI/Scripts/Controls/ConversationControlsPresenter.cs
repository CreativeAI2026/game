using System;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話HUDボタンのイベント登録と表示状態を管理する。</summary>
    internal sealed class ConversationControlsPresenter
    {
        private Button _auto;
        private Button _skip;
        private Button _speed;
        private Button _hide;
        private bool _bound;

        public void Configure(Button auto, Button skip, Button speed, Button hide)
        {
            _auto = auto;
            _skip = skip;
            _speed = speed;
            _hide = hide;
        }

        public void Bind(Action toggleAuto, Action toggleSkip, Action cycleSpeed, Action toggleHide)
        {
            if (_bound)
                return;
            _bound = true;
            _auto?.onClick.AddListener(() => toggleAuto());
            _skip?.onClick.AddListener(() => toggleSkip());
            _speed?.onClick.AddListener(() => cycleSpeed());
            _hide?.onClick.AddListener(() => toggleHide());
        }

        public void SetAvailability(bool historyOpen, bool hidden, bool rewardActive, bool choices)
        {
            SetAvailable(_auto, !historyOpen && !hidden);
            SetAvailable(_skip, !historyOpen && !hidden && !rewardActive && !choices);
            SetAvailable(_speed, !historyOpen && !hidden);
            SetAvailable(_hide, !historyOpen && !rewardActive && !choices);
        }

        public void SetAutoActive(bool active) => SetActive(_auto, active);

        public void SetSkipActive(bool active) => SetActive(_skip, active);

        public void SetHideActive(bool active) => SetActive(_hide, active);

        public void PlayAutoShortcut() => PlayShortcut(_auto);

        public void PlaySkipShortcut() => PlayShortcut(_skip);

        public void PlaySpeedShortcut() => PlayShortcut(_speed);

        public void PlayHideShortcut() => PlayShortcut(_hide);

        private static void SetAvailable(Button button, bool available)
        {
            if (button == null)
                return;
            button.interactable = available;
            if (button.TryGetComponent(out ConversationControlButton control))
                control.SetAvailable(available);
        }

        private static void SetActive(Button button, bool active)
        {
            if (button == null)
                return;
            if (button.TryGetComponent(out ConversationControlButton control))
                control.SetActiveState(active);
            else if (button.targetGraphic != null)
                button.targetGraphic.color = active
                    ? new Color(0.16f, 0.42f, 0.62f, 0.96f)
                    : new Color(0.035f, 0.045f, 0.07f, 0.86f);
        }

        private static void PlayShortcut(Button button)
        {
            if (button != null && button.TryGetComponent(out ConversationControlButton control))
                control.PlayShortcutFeedback();
        }
    }
}
