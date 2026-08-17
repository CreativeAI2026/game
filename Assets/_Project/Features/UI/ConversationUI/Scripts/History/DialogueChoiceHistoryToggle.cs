using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>履歴内の選択肢候補を、選択結果だけの表示と全候補表示で切り替える。</summary>
    public sealed class DialogueChoiceHistoryToggle : MonoBehaviour
    {
        private TMP_Text _body;
        private TMP_Text _badge;
        private string _fullText;
        private string _selectedText;
        private bool _expanded;
        private bool _searching;

        public void Configure(Button button, TMP_Text body, TMP_Text badge, string fullText)
        {
            _body = body;
            _badge = badge;
            _fullText = fullText ?? string.Empty;
            _selectedText = ExtractSelected(_fullText);
            button.onClick.AddListener(Toggle);
            Refresh();
        }

        public void SetSearchText(string highlightedText)
        {
            _searching = highlightedText != null;
            if (_searching)
            {
                _body.text = highlightedText;
                _badge.text = "CHOICE";
            }
            else
                Refresh();
        }

        private void Toggle()
        {
            if (_searching)
                return;
            _expanded = !_expanded;
            Refresh();
        }

        private void Refresh()
        {
            if (_body == null || _badge == null)
                return;
            _body.text = _expanded ? _fullText : _selectedText;
            _badge.text = _expanded ? "CHOICE  -" : "CHOICE  +";
        }

        private static string ExtractSelected(string source)
        {
            foreach (string line in source.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("✓ "))
                    return trimmed;
            }
            int newline = source.IndexOf('\n');
            return newline >= 0 ? source.Substring(0, newline) : source;
        }
    }
}
