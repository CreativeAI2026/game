using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core.EventSystem;
using CreativeAI.Scenario.Editor;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// EventImporter.Parse の検証。CharactersAndEvents.md の events.json 書式に対応。
    /// ファイル IO・AssetDatabase を伴わない純粋パーサ部分だけを検証する。
    /// </summary>
    public class EventImporterTests
    {
        [Test]
        public void Parse_ValidEvent_BuildsDefinitionWithSteps()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""cave_encounter"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 5 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""speaker"": ""主人公"", ""portrait"": ""hero_surprised"", ""text"": ""…誰だ?"" },
                        { ""kind"": ""battle"", ""enemyKey"": ""wolf_boss"" },
                        { ""kind"": ""choice"", ""flag"": ""girl_choice"", ""options"": [
                            { ""text"": ""一緒に行く"", ""value"": ""together"" },
                            { ""text"": ""ひとりで行く"", ""value"": ""alone"" }
                        ]},
                        { ""kind"": ""line"", ""speaker"": ""主人公"", ""portrait"": ""hero_normal"", ""text"": ""…そうか。"" }
                    ],
                    ""nextProgress"": 6
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);

            var def = report.Events[0];
            Assert.AreEqual("cave_encounter", def.Id);
            Assert.AreEqual(4, def.Steps.Count);
            Assert.AreEqual(StepKind.Battle, def.Steps[1].Kind);
            Assert.AreEqual("wolf_boss", def.Steps[1].EnemyKey);
            Assert.AreEqual("girl_choice", def.Steps[2].FlagKey);
            Assert.AreEqual(2, def.Steps[2].Options.Count);
            Assert.IsTrue(def.HasNextProgress);
            Assert.AreEqual(6, def.NextProgress);
        }

        [Test]
        public void Parse_OmittedNextProgress_HasNextProgressFalse()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""girl_reunion"",
                    ""conditions"": [ { ""type"": ""flag"", ""key"": ""girl_choice"", ""value"": ""together"" } ],
                    ""steps"": [ { ""kind"": ""line"", ""speaker"": ""少女"", ""portrait"": ""girl_smile"", ""text"": ""ここまで来られたね。"" } ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.IsFalse(report.Events[0].HasNextProgress); // 省略 → 進めない
        }

        [Test]
        public void Parse_UnknownPortrait_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""typo"",
                    ""conditions"": [],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_smirk"", ""text"": ""!"" } ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("hero_smirk")));
        }

        [Test]
        public void Parse_BattleAtStartOrEnd_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_battle"",
                    ""conditions"": [],
                    ""steps"": [
                        { ""kind"": ""battle"", ""enemyKey"": ""wolf_boss"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("先頭")));
        }

        [Test]
        public void Parse_DuplicateId_IsError()
        {
            const string json =
                @"{ ""events"": [
                    { ""id"": ""dup"", ""conditions"": [], ""steps"": [ { ""kind"": ""line"", ""text"": ""a"" } ] },
                    { ""id"": ""dup"", ""conditions"": [], ""steps"": [ { ""kind"": ""line"", ""text"": ""b"" } ] }
                ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("重複")));
        }

        [Test]
        public void Parse_UnknownStepKind_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""x"", ""conditions"": [],
                    ""steps"": [ { ""kind"": ""sing"", ""text"": ""la"" } ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("sing")));
        }

        [Test]
        public void Parse_EnemyAndItemKeys_ProduceWarningsNotErrors()
        {
            // enemyKey / itemKey はカタログ未整備のため「未検証」警告どまり(エラーにしない)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""warns"", ""conditions"": [],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""行くぞ"" },
                        { ""kind"": ""battle"", ""enemyKey"": ""wolf_boss"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""old_key"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);
            Assert.GreaterOrEqual(report.WarningCount, 2);
        }

        [Test]
        public void Parse_UnknownEnemyKey_WithCatalog_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_enemy"", ""conditions"": [],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""来い"" },
                        { ""kind"": ""battle"", ""enemyKey"": ""dragon"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ]
                } ] }";
            var catalog = new EventImporter.ImportCatalog(
                new HashSet<string> { "wolf_boss" },
                null
            );

            var report = EventImporter.Parse(json, catalog);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("dragon")));
        }

        [Test]
        public void Parse_KnownEnemyKey_WithCatalog_Ok()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""ok_enemy"", ""conditions"": [],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""来い"" },
                        { ""kind"": ""battle"", ""enemyKey"": ""wolf_boss"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ]
                } ] }";
            var catalog = new EventImporter.ImportCatalog(
                new HashSet<string> { "wolf_boss" },
                new HashSet<string> { "old_key" }
            );

            var report = EventImporter.Parse(json, catalog);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);
        }

        [Test]
        public void Parse_UnknownItemKey_WithCatalog_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_item"", ""conditions"": [],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""どうぞ"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""nonexistent"" }
                    ]
                } ] }";
            var catalog = new EventImporter.ImportCatalog(null, new HashSet<string> { "old_key" });

            var report = EventImporter.Parse(json, catalog);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("nonexistent")));
        }

        [Test]
        public void Parse_BrokenJson_IsErrorWithNoEvents()
        {
            var report = EventImporter.Parse("{ not json ");

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
        }

        [Test]
        public void Parse_MissingRequiredFields_AreErrors()
        {
            const string json =
                @"{ ""events"": [
                    { ""conditions"": [], ""steps"": [ { ""kind"": ""line"", ""text"": ""a"" } ] },
                    { ""id"": ""no_steps"", ""conditions"": [] }
                ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("id")));
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("steps")));
        }
    }
}
