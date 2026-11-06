using System.Collections.Generic;

namespace CreativeAI.UI
{
    /// <summary>表示文字列と、文字位置に紐づく会話演出情報。</summary>
    public sealed class ParsedDialogueText
    {
        private readonly Dictionary<int, float> _waits;
        private readonly List<(int Start, int End)> _shakeRanges;
        private readonly Dictionary<int, float> _speeds;

        public ParsedDialogueText(
            string text,
            Dictionary<int, float> waits,
            List<(int Start, int End)> shakeRanges,
            Dictionary<int, float> speeds = null,
            float autoAdvanceSeconds = -1f
        )
        {
            Text = text;
            _waits = waits;
            _shakeRanges = shakeRanges;
            _speeds = speeds ?? new Dictionary<int, float>();
            AutoAdvanceSeconds = autoAdvanceSeconds;
        }

        public string Text { get; }

        /// <summary>&lt;auto=秒&gt; の秒数。指定が無ければ負の値(送り入力を待つ)。</summary>
        public float AutoAdvanceSeconds { get; }

        public bool HasAutoAdvance => AutoAdvanceSeconds >= 0f;

        /// <summary>この表示位置から文字送り速度の倍率が変わるなら true(&lt;speed=倍率&gt; / &lt;/speed&gt;)。</summary>
        public bool TryGetSpeedChange(int visibleCharacterIndex, out float scale) =>
            _speeds.TryGetValue(visibleCharacterIndex, out scale);

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
