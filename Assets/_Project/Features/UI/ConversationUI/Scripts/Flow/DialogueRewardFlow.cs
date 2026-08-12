using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>報酬メッセージの表示準備、入場、退場を調停する。</summary>
    internal sealed class DialogueRewardFlow
    {
        private readonly MonoBehaviour _owner;
        private readonly ConversationChromePresenter _chrome;
        private readonly TMP_Text _nameText;
        private readonly TMP_Text _bodyText;
        private readonly AudioClip _typingSound;
        private readonly DialogueSessionState _sessionState;
        private readonly Func<DialogueHistoryPanel> _history;
        private readonly Func<string, IEnumerator> _typeBody;
        private readonly Action<AudioClip> _setTypingSound;

        public DialogueRewardFlow(
            MonoBehaviour owner,
            ConversationChromePresenter chrome,
            TMP_Text nameText,
            TMP_Text bodyText,
            AudioClip typingSound,
            DialogueSessionState sessionState,
            Func<DialogueHistoryPanel> history,
            Func<string, IEnumerator> typeBody,
            Action<AudioClip> setTypingSound
        )
        {
            _owner = owner;
            _chrome = chrome;
            _nameText = nameText;
            _bodyText = bodyText;
            _typingSound = typingSound;
            _sessionState = sessionState;
            _history = history;
            _typeBody = typeBody;
            _setTypingSound = setTypingSound;
        }

        public IEnumerator Enter(IEnumerator animation, string message, bool weapon)
        {
            bool hasMessage = !string.IsNullOrWhiteSpace(message);
            PrepareMessage(hasMessage ? message : string.Empty, weapon);
            Coroutine entrance = _owner.StartCoroutine(animation);
            if (hasMessage)
                yield return _typeBody(message);
            yield return entrance;
        }

        public IEnumerator Exit(IEnumerator animation, string message)
        {
            Coroutine textExit = !string.IsNullOrWhiteSpace(message)
                ? _owner.StartCoroutine(_chrome.HideLineText(0.18f))
                : null;
            yield return animation;
            if (textExit != null)
                yield return textExit;
        }

        private void PrepareMessage(string message, bool weapon)
        {
            _chrome.PrepareLineText(_nameText, _bodyText);
            if (_nameText != null)
            {
                _nameText.text = string.Empty;
                _nameText.gameObject.SetActive(false);
            }
            if (_bodyText != null)
            {
                _bodyText.text = string.Empty;
                _bodyText.alignment = TextAlignmentOptions.Center;
            }

            if (string.IsNullOrWhiteSpace(message))
                return;

            _setTypingSound(_typingSound);
            _sessionState.BeginReward(message);
            _chrome.PlayLineTextEntrance(_nameText, _bodyText, false);
            _history()?.AddRewardEntry(DialogueMarkupParser.Parse(message).Text, weapon);
        }
    }
}
