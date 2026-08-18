using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>一行分の立ち絵解決、本文表示準備、履歴記録を調停する。</summary>
    internal sealed class DialogueLineFlow
    {
        private readonly DialoguePortraitPresenter _portraits;
        private readonly ConversationChromePresenter _chrome;
        private readonly TMP_Text _nameText;
        private readonly TMP_Text _bodyText;
        private readonly DialogueCharacterDefinition[] _characters;
        private readonly Sprite _defaultPortrait;
        private readonly DialoguePortraitSide _defaultSide;
        private readonly AudioClip _defaultTypingSound;
        private readonly DialogueSessionState _sessionState;
        private readonly Func<DialogueHistoryPanel> _history;

        public DialogueLineFlow(
            DialoguePortraitPresenter portraits,
            ConversationChromePresenter chrome,
            TMP_Text nameText,
            TMP_Text bodyText,
            DialogueCharacterDefinition[] characters,
            Sprite defaultPortrait,
            DialoguePortraitSide defaultSide,
            AudioClip defaultTypingSound,
            DialogueSessionState sessionState,
            Func<DialogueHistoryPanel> history
        )
        {
            _portraits = portraits;
            _chrome = chrome;
            _nameText = nameText;
            _bodyText = bodyText;
            _characters = characters;
            _defaultPortrait = defaultPortrait;
            _defaultSide = defaultSide;
            _defaultTypingSound = defaultTypingSound;
            _sessionState = sessionState;
            _history = history;
        }

        public AudioClip TypingSound { get; private set; }
        public Image RightPortrait => _portraits.RightPortrait;

        public IEnumerator Prepare(string speaker, string portrait, string text)
        {
            bool narration = string.IsNullOrEmpty(portrait);
            var resolved = narration
                ? new DialoguePortraitPresenter.ResolvedPortrait(
                    null,
                    null,
                    DialoguePortraitSide.Left,
                    string.Empty,
                    new Color(0.78f, 0.82f, 0.9f, 1f),
                    null,
                    Vector2.zero
                )
                : DialoguePortraitResolver.Resolve(
                    portrait,
                    _characters,
                    _defaultPortrait,
                    _defaultSide
                );

            yield return _portraits.Set(resolved);
            TypingSound = resolved.TypingSound != null ? resolved.TypingSound : _defaultTypingSound;

            string displayName = !string.IsNullOrWhiteSpace(speaker)
                ? speaker
                : resolved.DisplayName;
            _sessionState.BeginLine(displayName, portrait, text);
            ConfigureText(displayName, resolved.ThemeColor, narration);

            bool portraitObscured =
                !narration
                && (_portraits.IsObscured(resolved.Side) || displayName is "？？？" or "???");
            _history()
                ?.AddEntry(
                    displayName,
                    DialogueMarkupParser.Parse(text).Text,
                    resolved.Icon,
                    resolved.Side,
                    portraitObscured
                );
        }

        private void ConfigureText(string displayName, Color themeColor, bool narration)
        {
            if (_nameText != null)
            {
                _nameText.text = displayName;
                _nameText.color = themeColor;
                _nameText.gameObject.SetActive(!narration && !string.IsNullOrEmpty(displayName));
            }
            if (_bodyText != null)
                _bodyText.alignment = narration
                    ? TextAlignmentOptions.Center
                    : TextAlignmentOptions.MidlineLeft;

            _chrome.PlayLineTextEntrance(
                _nameText,
                _bodyText,
                !narration && !string.IsNullOrEmpty(displayName)
            );
        }
    }
}
