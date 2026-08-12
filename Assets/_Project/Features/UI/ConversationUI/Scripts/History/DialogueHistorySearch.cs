using System;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>履歴検索の一致判定と表示用ハイライトを担当する。</summary>
    internal static class DialogueHistorySearch
    {
        private const string MarkupPrefix = "<mark=#3A84A880>";
        private const string MarkupSuffix = "</mark>";

        public static bool Matches(DialogueHistoryEntry entry, string query) =>
            string.IsNullOrEmpty(query)
            || Contains(entry.Speaker, query)
            || Contains(entry.Body, query);

        public static string Highlight(string source, string query)
        {
            source ??= string.Empty;
            if (string.IsNullOrEmpty(query))
                return source;

            int index = source.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return source;

            return source.Substring(0, index)
                + MarkupPrefix
                + source.Substring(index, query.Length)
                + MarkupSuffix
                + source.Substring(index + query.Length);
        }

        private static bool Contains(string source, string query) =>
            source?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
