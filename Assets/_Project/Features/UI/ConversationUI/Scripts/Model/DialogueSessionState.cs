using System.Collections.Generic;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>会話中に保持する既読行と選択済み選択肢の状態。</summary>
    internal sealed class DialogueSessionState
    {
        private readonly HashSet<string> _readLineIds = new();
        private readonly HashSet<string> _selectedChoiceValues = new();

        public ISet<string> SelectedChoiceValues => _selectedChoiceValues;
        public string CurrentLineId { get; private set; }
        public bool CurrentLineWasRead { get; private set; }

        public void BeginLine(string speaker, string portrait, string text)
        {
            CurrentLineId = BuildLineId(speaker, portrait, text);
            CurrentLineWasRead = _readLineIds.Contains(CurrentLineId);
        }

        public void BeginReward(string message) => BeginLine("reward", string.Empty, message);

        public void MarkCurrentLineRead()
        {
            if (!string.IsNullOrEmpty(CurrentLineId))
                _readLineIds.Add(CurrentLineId);
        }

        public bool IsLineRead(string speaker, string portrait, string text) =>
            _readLineIds.Contains(BuildLineId(speaker, portrait, text));

        public void MarkLineRead(string speaker, string portrait, string text) =>
            _readLineIds.Add(BuildLineId(speaker, portrait, text));

        public void ClearReadHistory() => _readLineIds.Clear();

        public void MarkChoiceSelected(string value)
        {
            if (!string.IsNullOrEmpty(value))
                _selectedChoiceValues.Add(value);
        }

        private static string BuildLineId(string speaker, string portrait, string text) =>
            $"{speaker}\n{portrait}\n{text}";
    }
}
