using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CreativeAI.UI
{
    public static class DialogueMarkupParser
    {
        // 同じ本文を表示・履歴・送り待ちで続けて解析するので、直前の結果だけ使い回す(結果は読み取り専用)。
        private static string _lastSource;
        private static ParsedDialogueText _lastParsed;

        public static ParsedDialogueText Parse(string source)
        {
            source ??= string.Empty;
            if (source == _lastSource)
                return _lastParsed;
            var output = new StringBuilder(source.Length);
            var waits = new Dictionary<int, float>();
            var shakeRanges = new List<(int Start, int End)>();
            var shakeStarts = new Stack<int>();
            var speeds = new Dictionary<int, float>();
            float autoAdvanceSeconds = -1f;
            int visibleIndex = 0;

            for (int i = 0; i < source.Length; )
            {
                if (TryReadTag(source, i, out var tag, out int next))
                {
                    if (tag.StartsWith("wait=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (
                            float.TryParse(
                                tag.AsSpan(5),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float duration
                            )
                        )
                            waits[visibleIndex] = Math.Clamp(duration, 0f, 10f);
                    }
                    else if (tag.StartsWith("speed=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (
                            float.TryParse(
                                tag.AsSpan(6),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float scale
                            )
                        )
                            speeds[visibleIndex] = Math.Clamp(scale, 0.1f, 5f);
                    }
                    else if (tag.Equals("/speed", StringComparison.OrdinalIgnoreCase))
                    {
                        speeds[visibleIndex] = 1f;
                    }
                    else if (tag.StartsWith("auto=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (
                            float.TryParse(
                                tag.AsSpan(5),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float auto
                            )
                        )
                            autoAdvanceSeconds = Math.Clamp(auto, 0f, 10f);
                    }
                    else if (tag.Equals("shake", StringComparison.OrdinalIgnoreCase))
                    {
                        shakeStarts.Push(visibleIndex);
                    }
                    else if (
                        tag.Equals("/shake", StringComparison.OrdinalIgnoreCase)
                        && shakeStarts.Count > 0
                    )
                    {
                        shakeRanges.Add((shakeStarts.Pop(), visibleIndex));
                    }
                    else if (tag.Equals("whisper", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("<size=85%><color=#B7C1D0>");
                    }
                    else if (tag.Equals("/whisper", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("</color></size>");
                    }
                    else if (tag.Equals("shout", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("<size=115%><b>");
                    }
                    else if (tag.Equals("/shout", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("</b></size>");
                    }
                    else if (tag.Equals("emphasis", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("<color=#86D7FF><b>");
                    }
                    else if (tag.Equals("/emphasis", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Append("</b></color>");
                    }
                    else
                    {
                        output.Append('<').Append(tag).Append('>');
                    }
                    i = next;
                    continue;
                }

                output.Append(source[i]);
                visibleIndex++;
                i++;
            }

            while (shakeStarts.Count > 0)
                shakeRanges.Add((shakeStarts.Pop(), visibleIndex));
            var parsedText = new ParsedDialogueText(
                output.ToString(),
                waits,
                shakeRanges,
                speeds,
                autoAdvanceSeconds
            );
            _lastSource = source;
            _lastParsed = parsedText;
            return parsedText;
        }

        private static bool TryReadTag(string source, int index, out string tag, out int next)
        {
            tag = null;
            next = index;
            if (source[index] != '<')
                return false;
            int end = source.IndexOf('>', index + 1);
            if (end < 0)
                return false;
            tag = source.Substring(index + 1, end - index - 1);
            next = end + 1;
            return true;
        }
    }

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
