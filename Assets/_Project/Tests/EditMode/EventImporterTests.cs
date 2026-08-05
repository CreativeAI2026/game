using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core.EventSystem;
using CreativeAI.Scenario.Editor;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// EventImporter.Parse の検証。ScenarioReference.md の events.json 書式に対応。
    /// ファイル IO・AssetDatabase を伴わない純粋パーサ部分だけを検証する。
    /// </summary>
    public class EventImporterTests
    {
        [Test]
        public void Parse_ValidEvent_BuildsDefinitionWithSteps()
        {
            // battle は敵を書かず { "kind": "battle" } のみ(敵は EventTrigger に配線)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""cave_encounter"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 5 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""speaker"": ""主人公"", ""portrait"": ""hero_surprised"", ""text"": ""…誰だ?"" },
                        { ""kind"": ""battle"" },
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
            Assert.AreEqual("girl_choice", def.Steps[2].FlagKey);
            Assert.AreEqual(2, def.Steps[2].Options.Count);
            Assert.IsTrue(def.HasNextProgress);
            Assert.AreEqual(6, def.NextProgress);
        }

        [Test]
        public void Parse_GiveWeapon_ValidKey_BuildsStep()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""girl_gift"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 5 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""speaker"": ""少女"", ""portrait"": ""girl_resolve"", ""text"": ""身を守って。"" },
                        { ""kind"": ""giveWeapon"", ""weaponKey"": ""scythe"" },
                        { ""kind"": ""line"", ""speaker"": ""主人公"", ""portrait"": ""hero_normal"", ""text"": ""ありがとう。"" }
                    ],
                    ""nextProgress"": 6
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            var def = report.Events[0];
            Assert.AreEqual(StepKind.GiveWeapon, def.Steps[1].Kind);
            Assert.AreEqual("scythe", def.Steps[1].WeaponKey);
        }

        [Test]
        public void Parse_GiveWeapon_InvalidKey_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_weapon"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""speaker"": ""x"", ""portrait"": ""hero_normal"", ""text"": ""a"" },
                        { ""kind"": ""giveWeapon"", ""weaponKey"": ""axe"" },
                        { ""kind"": ""line"", ""speaker"": ""x"", ""portrait"": ""hero_normal"", ""text"": ""b"" }
                    ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("axe")));
        }

        [Test]
        public void Parse_GiveWeapon_MissingKey_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""no_weapon_key"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""speaker"": ""x"", ""portrait"": ""hero_normal"", ""text"": ""a"" },
                        { ""kind"": ""giveWeapon"" },
                        { ""kind"": ""line"", ""speaker"": ""x"", ""portrait"": ""hero_normal"", ""text"": ""b"" }
                    ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("weaponKey")));
        }

        [Test]
        public void Parse_OmittedNextProgress_IsError()
        {
            // nextProgress は必須(省略はエラー)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""girl_reunion"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 8 } ],
                    ""steps"": [ { ""kind"": ""line"", ""speaker"": ""少女"", ""portrait"": ""girl_smile"", ""text"": ""来られたね。"" } ]
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("nextProgress")));
        }

        [Test]
        public void Parse_NextProgressNotGreaterThanProgress_IsError()
        {
            // nextProgress は progress の value より大きくなければならない(== 発火 → 進めて二度と一致しない)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""no_advance"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 5 } ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""a"" } ],
                    ""nextProgress"": 5
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("nextProgress")));
        }

        [Test]
        public void Parse_MissingProgressCondition_IsError()
        {
            // progress 条件を必ず1つ含む(flag だけは不可)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""flag_only"",
                    ""conditions"": [ { ""type"": ""flag"", ""key"": ""k"", ""value"": ""v"" } ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""a"" } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("progress")));
        }

        [Test]
        public void Parse_BattleWithEnemyKey_IsError()
        {
            // 敵は JSON に書けない(EventTrigger に配線)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""enemy_in_json"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""来い"" },
                        { ""kind"": ""battle"", ""enemyKey"": ""wolf_boss"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("enemyKey")));
        }

        [Test]
        public void Parse_MultipleBattles_IsError()
        {
            // battle は1イベントにつき最大1つ。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""two_battles"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""a"" },
                        { ""kind"": ""battle"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""b"" },
                        { ""kind"": ""battle"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""c"" }
                    ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("battle")));
        }

        [Test]
        public void Parse_UnknownPortrait_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""typo"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_smirk"", ""text"": ""!"" } ],
                    ""nextProgress"": 1
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
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""battle"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""終わり"" }
                    ],
                    ""nextProgress"": 1
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
                    { ""id"": ""dup"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ], ""steps"": [ { ""kind"": ""line"", ""text"": ""a"" } ], ""nextProgress"": 1 },
                    { ""id"": ""dup"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ], ""steps"": [ { ""kind"": ""line"", ""text"": ""b"" } ], ""nextProgress"": 1 }
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
                    ""id"": ""x"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""sing"", ""text"": ""la"" } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("sing")));
        }

        [Test]
        public void Parse_ItemKey_WithoutCatalog_ProducesWarningNotError()
        {
            // itemKey はカタログ未提供なら「未検証」警告どまり(エラーにしない)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""warns"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""どうぞ"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""old_key"" }
                    ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);
            Assert.GreaterOrEqual(report.WarningCount, 1);
        }

        [Test]
        public void Parse_KnownItemKey_WithCatalog_Ok()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""ok_item"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""どうぞ"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""old_key"" }
                    ],
                    ""nextProgress"": 1
                } ] }";
            var catalog = new EventImporter.ImportCatalog(new HashSet<string> { "old_key" });

            var report = EventImporter.Parse(json, catalog);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);
        }

        [Test]
        public void Parse_UnknownItemKey_WithCatalog_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_item"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""どうぞ"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""nonexistent"" }
                    ],
                    ""nextProgress"": 1
                } ] }";
            var catalog = new EventImporter.ImportCatalog(new HashSet<string> { "old_key" });

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
                    { ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ], ""steps"": [ { ""kind"": ""line"", ""text"": ""a"" } ], ""nextProgress"": 1 },
                    { ""id"": ""no_steps"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ], ""nextProgress"": 1 }
                ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("id")));
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("steps")));
        }
    }
}
