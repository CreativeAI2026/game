using System;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// CSV 1 行のパース(引用符内のカンマ・エスケープ、閉じ忘れの検出)の検証。
    /// </summary>
    public sealed class CsvRecordParserTests
    {
        [Test]
        public void Parse_AllowsCommaAndEscapedQuoteInQuotedField()
        {
            var columns = CsvRecordParser.Parse("item,\"桃, いちご\",\"説明に\"\"引用\"\"を含む\"");

            CollectionAssert.AreEqual(
                new[] { "item", "桃, いちご", "説明に\"引用\"を含む" },
                columns
            );
        }

        [Test]
        public void Parse_RejectsUnclosedQuote()
        {
            Assert.Throws<FormatException>(() => CsvRecordParser.Parse("item,\"未完了"));
        }
    }
}
