using System.Collections.Generic;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>表示文字列と、文字位置に紐づく会話演出情報。</summary>
    public sealed class ParsedDialogueText
    {
        private readonly Dictionary<int, float> _waits;
        private readonly List<(int Start, int End)> _shakeRanges;

        public ParsedDialogueText(
            string text,
            Dictionary<int, float> waits,
            List<(int Start, int End)> shakeRanges
        )
        {
            Text = text;
            _waits = waits;
            _shakeRanges = shakeRanges;
        }

        public string Text { get; }

        public float GetWaitAfter(int visibleCharacterIndex) =>
            _waits.TryGetValue(visibleCharacterIndex + 1, out var duration) ? duration : 0f;

        public bool IsShaking(int visibleCharacterIndex)
        {
            foreach (var range in _shakeRanges)
            {
                if (visibleCharacterIndex >= range.Start && visibleCharacterIndex < range.End)
                    return true;
            }

            return false;
        }
    }
}
