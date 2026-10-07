using System;
using System.Collections.Generic;
using System.Text;

namespace CreativeAI.EditorTools
{
    /// <summary>CSVの1レコードを解析する。カンマを含む引用フィールドと二重引用符を扱う。</summary>
    public static class CsvRecordParser
    {
        public static IReadOnlyList<string> Parse(string line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));

            var columns = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];
                if (current == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        value.Append('"');
                        i++;
                    }
                    else if (quoted)
                        quoted = false;
                    else if (value.Length == 0)
                        quoted = true;
                    else
                        throw new FormatException("引用符はフィールドの先頭に置いてください。");
                }
                else if (current == ',' && !quoted)
                {
                    columns.Add(value.ToString());
                    value.Clear();
                }
                else
                    value.Append(current);
            }

            if (quoted)
                throw new FormatException("引用符が閉じられていません。");

            columns.Add(value.ToString());
            return columns;
        }
    }
}
