using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>選択肢の生成、選択待機、確定演出、履歴記録を調停する。</summary>
    internal sealed class DialogueChoiceFlow
    {
        private readonly DialogueChoicePresenter _presenter;
        private readonly ConversationChromePresenter _chrome;
        private readonly DialogueSessionState _sessionState;
        private readonly Func<DialogueHistoryPanel> _history;
        private readonly Action<bool> _setActive;
        private readonly Action<float> _blockAdvance;

        public DialogueChoiceFlow(
            DialogueChoicePresenter presenter,
            ConversationChromePresenter chrome,
            DialogueSessionState sessionState,
            Func<DialogueHistoryPanel> history,
            Action<bool> setActive,
            Action<float> blockAdvance
        )
        {
            _presenter = presenter;
            _chrome = chrome;
            _sessionState = sessionState;
            _history = history;
            _setActive = setActive;
            _blockAdvance = blockAdvance;
        }

        public IEnumerator Execute(IReadOnlyList<ChoiceOption> options, Action<string> onSelected)
        {
            _presenter.Clear();
            _presenter.SetActive(true);
            _chrome.SetChoiceGuide(true);

            string picked = null;
            string pickedText = null;
            Button pickedButton = null;
            bool hasPicked = false;
            int choiceCount = _presenter.Spawn(
                options,
                _sessionState.SelectedChoiceValues,
                (value, optionText, button) =>
                {
                    picked = value;
                    pickedText = optionText;
                    pickedButton = button;
                    hasPicked = true;
                }
            );

            if (choiceCount == 0)
            {
                Debug.LogWarning("[DialogueChoiceFlow] 表示できる選択肢がありません。");
                _setActive(false);
                onSelected?.Invoke(null);
                yield break;
            }

            yield return _presenter.AnimateIn();
            _presenter.SelectFirst();
            while (!hasPicked)
                yield return null;

            yield return _presenter.AnimateSelection(pickedButton);
            _presenter.Clear();
            _setActive(false);
            _history()?.AddChoiceEntry(options, pickedText);
            _sessionState.MarkChoiceSelected(picked);
            _blockAdvance(0.15f);
            onSelected?.Invoke(picked);
        }
    }
}
