using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 焦り度ログ（PanicSessionLogger が書く CSV）1ファイル分を読み、列名で値を取り出せるようにする。
    /// 古いログには後から増えた列（boss_state・control_scheme など）が無いので、無い列は空文字として扱う。
    /// </summary>
    public sealed class PanicLogCsv
    {
        private readonly Dictionary<string, int> _columnIndex = new Dictionary<string, int>();

        private PanicLogCsv(string fileName, string[] header, List<string[]> rows)
        {
            FileName = fileName;
            Header = header;
            Rows = rows;
            for (int i = 0; i < header.Length; i++)
            {
                _columnIndex[header[i]] = i;
            }
        }

        /// <summary>元のファイル名（集計結果でどのログの話かを示すのに使う）。</summary>
        public string FileName { get; }

        public string[] Header { get; }

        /// <summary>見出しを除いた行（時刻順）。</summary>
        public IReadOnlyList<string[]> Rows { get; }

        public static PanicLogCsv Load(string path)
        {
            return Parse(Path.GetFileName(path), File.ReadAllLines(path, Encoding.UTF8));
        }

        /// <summary>CSV の行（1行目が見出し）から読む。テストからも使う。</summary>
        public static PanicLogCsv Parse(string fileName, IReadOnlyList<string> lines)
        {
            // BOM が読み込み側で取り除かれない場合に備えて、見出しの先頭から外す
            string[] header = lines.Count > 0 ? SplitLine(lines[0].TrimStart('﻿')) : new string[0];
            var rows = new List<string[]>();
            for (int i = 1; i < lines.Count; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                    rows.Add(SplitLine(lines[i]));
            }
            return new PanicLogCsv(fileName, header, rows);
        }

        public bool HasColumn(string column) => _columnIndex.ContainsKey(column);

        public string Get(string[] row, string column)
        {
            return _columnIndex.TryGetValue(column, out int index) && index < row.Length
                ? row[index]
                : string.Empty;
        }

        public float GetFloat(string[] row, string column)
        {
            return float.TryParse(Get(row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : 0f;
        }

        /// <summary>指標ごとのスコアの列名（「逃避_スコア」など）。</summary>
        public IEnumerable<string> SignalScoreColumns
        {
            get
            {
                foreach (string column in Header)
                {
                    if (column.EndsWith("_スコア"))
                        yield return column;
                }
            }
        }

        // 引用符で囲まれたセル（カンマや引用符を含む名前）にも対応して1行を分ける
        private static string[] SplitLine(string line)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (c == '"')
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                }
                else
                {
                    cell.Append(c);
                }
            }
            cells.Add(cell.ToString());
            return cells.ToArray();
        }
    }
}
