using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CreativeAI.UI.ConversationUI
{
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

    public static class DialogueMarkupParser
    {
        public static ParsedDialogueText Parse(string source)
        {
            source ??= string.Empty;
            var output = new StringBuilder(source.Length);
            var waits = new Dictionary<int, float>();
            var shakeRanges = new List<(int Start, int End)>();
            var shakeStarts = new Stack<int>();
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
            return new ParsedDialogueText(output.ToString(), waits, shakeRanges);
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
}
