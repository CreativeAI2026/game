using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core.EventSystem;
using CreativeAI.Scenario.Editor;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// events.json の取り込み検証(書式チェック・キー照合・EventDefinition への変換)。
    /// 書式は documents/ScenarioReference.md。
    /// ファイル IO・AssetDatabase を伴わない純粋パーサ部分(EventImporter.Parse)だけを検証する。
    /// </summary>
    public class EventImporterTests
    {
        [Test]
        public void Parse_DocExample_StationAwakening_BuildsDefinitionWithSteps()
        {
            // documents/ScenarioReference.md の例(station_awakening)がそのまま取り込めることを担保する。
            // 地の文(portrait 省略) / ？？？ + obscure・reveal / 演出タグ / battle / giveWeapon + message /
            // choice まで一通り含む。battle は敵を書かず { "kind": "battle" } のみ(敵は EventTrigger に配線)。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""station_awakening"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 5 } ],
                    ""steps"": [
                        { ""kind"": ""line"", ""text"": ""雨音の向こうで、古いレコードが途切れ途切れに鳴っている。"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_surprised"", ""text"": ""……知らない天井だ。"" },
                        { ""kind"": ""command"", ""command"": ""portrait.right.obscure"" },
                        { ""kind"": ""line"", ""speaker"": ""？？？"", ""portrait"": ""girl_fear"",
                          ""text"": ""<whisper>あの……倒れていたあなたを運んだのは、その子です。</whisper>"" },
                        { ""kind"": ""command"", ""command"": ""portrait.right.reveal"" },
                        { ""kind"": ""battle"" },
                        { ""kind"": ""command"", ""command"": ""portrait.left.jump"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_surprised"",
                          ""text"": ""<wait=0.25><shake><shout>蓄音機までしゃべるのか……。</shout></shake>"" },
                        { ""kind"": ""giveWeapon"", ""weaponKey"": ""scythe"", ""message"": ""錆びた鎌を手に入れた。"" },
                        { ""kind"": ""choice"", ""flag"": ""girl_choice"", ""options"": [
                            { ""text"": ""一緒に行く"", ""value"": ""together"" },
                            { ""text"": ""ひとりで行く"", ""value"": ""alone"" }
                        ]},
                        { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""…そうか。"" }
                    ],
                    ""nextProgress"": 6
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);

            var def = report.Events[0];
            Assert.AreEqual("station_awakening", def.Id);
            Assert.AreEqual(11, def.Steps.Count);
            // 地の文は portrait 空のまま通る(実行時にナレーション表示になる)。
            Assert.IsTrue(string.IsNullOrEmpty(def.Steps[0].Portrait));
            Assert.AreEqual("portrait.right.obscure", def.Steps[2].CommandName);
            Assert.AreEqual(StepKind.Battle, def.Steps[5].Kind);
            Assert.AreEqual("錆びた鎌を手に入れた。", def.Steps[8].Message);
            Assert.AreEqual("girl_choice", def.Steps[9].FlagKey);
            Assert.AreEqual(2, def.Steps[9].Options.Count);
            Assert.IsTrue(def.HasNextProgress);
            Assert.AreEqual(6, def.NextProgress);
        }

        [Test]
        public void Parse_DocExample_RobotSupply_SceneTransitionCommands()
        {
            // 同じく doc の例(robot_supply)。battle が無いイベントは command で始まってよい。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""robot_supply"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 7 } ],
                    ""steps"": [
                        { ""kind"": ""command"", ""command"": ""window.hide"" },
                        { ""kind"": ""command"", ""command"": ""wait"", ""arg"": ""0.45"" },
                        { ""kind"": ""command"", ""command"": ""window.show"" },
                        { ""kind"": ""line"", ""text"": ""少女と蓄音機が、旅支度を運んできた。"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""apple"", ""message"": ""傷のあるりんごを手に入れた。"" },
                        { ""kind"": ""giveItem"", ""itemKey"": ""umbrella"" },
                        { ""kind"": ""line"", ""portrait"": ""hero_smile"", ""text"": ""助かるよ。"" }
                    ],
                    ""nextProgress"": 8
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            var steps = report.Events[0].Steps;
            Assert.AreEqual("0.45", steps[1].Arg);
            // message 省略時は null のまま渡り、既定文の組み立ては UI 側が行う。
            Assert.IsNull(steps[5].Message);
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
        public void Parse_Command_KnownName_BuildsStep()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""shaken"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [
                        { ""kind"": ""command"", ""command"": ""portrait.left.shake"" },
                        { ""kind"": ""command"", ""command"": ""wait"", ""arg"": ""0.5"" },
                        { ""kind"": ""line"", ""text"": ""揺れた。"" }
                    ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            var steps = report.Events[0].Steps;
            Assert.AreEqual(StepKind.Command, steps[0].Kind);
            Assert.AreEqual("portrait.left.shake", steps[0].CommandName);
            Assert.AreEqual("0.5", steps[1].Arg);
        }

        [Test]
        public void Parse_Command_ExternalPrefix_IsAllowed()
        {
            // camera.* / background.* は会話UIの外へ委譲するので接頭辞だけ見て通す。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""pan"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [
                        { ""kind"": ""command"", ""command"": ""camera.pan"", ""arg"": ""left"" },
                        { ""kind"": ""line"", ""text"": ""景色が流れる。"" }
                    ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
        }

        [Test]
        public void Parse_Command_UnknownName_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bogus"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [ { ""kind"": ""command"", ""command"": ""window.explode"" } ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
        }

        [Test]
        public void Parse_Command_WaitWithoutArg_IsError()
        {
            // 秒数が無い wait は何も待たずに素通りするので取り込み時に弾く。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_wait"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [ { ""kind"": ""command"", ""command"": ""wait"" } ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void Parse_GiveItem_Message_IsCarried()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""supply"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [ { ""kind"": ""giveItem"", ""itemKey"": ""apple"", ""message"": ""赤いりんごを受け取った。"" } ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.AreEqual("赤いりんごを受け取った。", report.Events[0].Steps[0].Message);
        }

        [Test]
        public void Parse_Portrait_NotRegisteredInAssets_IsWarningNotError()
        {
            // 絵の準備待ちでも書き進められるよう、カタログにあるが未登録の立ち絵は警告どまり。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""pending_art"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_anger"", ""text"": ""…っ"" } ],
                    ""nextProgress"": 2
                } ] }";

            var catalog = new EventImporter.ImportCatalog(
                null,
                null,
                new HashSet<string> { "hero_normal" }
            );

            var report = EventImporter.Parse(json, catalog);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.WarningCount);
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

        // --- conditions: hasItem(ScenarioReference.md「hasItem の制約」= 大事なもの限定) ---

        private const string HasItemJsonTemplate =
            @"{ ""events"": [ {
                ""id"": ""locked_door"",
                ""conditions"": [
                    { ""type"": ""progress"", ""value"": 10 },
                    { ""type"": ""hasItem"", ""itemKey"": ""{KEY}"" }
                ],
                ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""開きそうだ。"" } ],
                ""nextProgress"": 11
            } ] }";

        private static string HasItemJson(string key) => HasItemJsonTemplate.Replace("{KEY}", key);

        private static EventImporter.ImportCatalog KeyItemCatalog(params string[] keyItemKeys) =>
            new EventImporter.ImportCatalog(
                new HashSet<string> { "apple", "umbrella", "mysterious_key" },
                new HashSet<string>(keyItemKeys)
            );

        [Test]
        public void Parse_HasItem_KnownKeyItem_BuildsCondition()
        {
            var report = EventImporter.Parse(
                HasItemJson("mysterious_key"),
                KeyItemCatalog("mysterious_key", "card_key")
            );

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            var def = report.Events[0];
            Assert.AreEqual(2, def.Conditions.Count);
            Assert.AreEqual(ConditionType.HasItem, def.Conditions[1].Type);
            Assert.AreEqual("mysterious_key", def.Conditions[1].ItemKey);
        }

        [Test]
        public void Parse_HasItem_TypoKey_IsError()
        {
            // 打ち間違いを通すと実行時に常に false になりイベントが永久に発火しない。
            var report = EventImporter.Parse(
                HasItemJson("mysterious_ky"),
                KeyItemCatalog("mysterious_key")
            );

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("mysterious_ky")));
        }

        [Test]
        public void Parse_HasItem_NonKeyItemKey_IsError()
        {
            // 装備品/食材の key は hasItem に書けない(大事なもの限定)。
            // itemKey カタログ(giveItem 用)には存在するが、大事なものカタログには無いケース。
            var report = EventImporter.Parse(HasItemJson("umbrella"), KeyItemCatalog("card_key"));

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("umbrella")));
        }

        [Test]
        public void Parse_HasItem_WithoutKeyItemCatalog_ProducesWarningNotError()
        {
            // 大事なものアセット未作成の段階で全 hasItem を弾かない(giveItem と同じ方針)。
            var report = EventImporter.Parse(HasItemJson("mysterious_key"));

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Diagnostics));
            Assert.AreEqual(1, report.Events.Count);
            Assert.GreaterOrEqual(report.WarningCount, 1);
        }

        [Test]
        public void Parse_HasItem_MissingItemKey_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""no_item_key"",
                    ""conditions"": [
                        { ""type"": ""progress"", ""value"": 1 },
                        { ""type"": ""hasItem"" }
                    ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""a"" } ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json, KeyItemCatalog("mysterious_key"));

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("itemKey")));
        }

        [Test]
        public void Parse_UnknownConditionType_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""bad_cond"",
                    ""conditions"": [
                        { ""type"": ""progress"", ""value"": 1 },
                        { ""type"": ""hasWeapon"", ""weaponKey"": ""sword"" }
                    ],
                    ""steps"": [ { ""kind"": ""line"", ""portrait"": ""hero_normal"", ""text"": ""a"" } ],
                    ""nextProgress"": 2
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("hasWeapon")));
        }

        // --- steps: choice(ScenarioReference.md「フォーマット」) ---

        [Test]
        public void Parse_Choice_MissingFlag_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""choice_no_flag"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [
                        { ""kind"": ""choice"", ""options"": [ { ""text"": ""はい"", ""value"": ""yes"" } ] }
                    ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("flag")));
        }

        [Test]
        public void Parse_Choice_FourOptions_IsError()
        {
            // 選択肢UIは3択ぶんの高さで中央寄せするので4つ以上は会話ウィンドウに被る。
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""choice_too_many"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""choice"", ""flag"": ""dest"", ""options"": [
                        { ""text"": ""南の村"", ""value"": ""village"" },
                        { ""text"": ""旧遺跡"", ""value"": ""ruins"" },
                        { ""text"": ""北の森"", ""value"": ""forest"" },
                        { ""text"": ""ここに残る"", ""value"": ""stay"" }
                    ] } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("options")));
        }

        [Test]
        public void Parse_Choice_SingleOption_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""choice_single"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""choice"", ""flag"": ""ok"", ""options"": [
                        { ""text"": ""はい"", ""value"": ""yes"" }
                    ] } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
        }

        [Test]
        public void Parse_Choice_EmptyOptions_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""choice_no_options"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""choice"", ""flag"": ""girl_choice"", ""options"": [] } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("options")));
        }

        [Test]
        public void Parse_Choice_OptionMissingTextOrValue_IsError()
        {
            const string json =
                @"{ ""events"": [ {
                    ""id"": ""choice_bad_option"", ""conditions"": [ { ""type"": ""progress"", ""value"": 0 } ],
                    ""steps"": [ { ""kind"": ""choice"", ""flag"": ""girl_choice"", ""options"": [
                        { ""text"": ""一緒に行く"", ""value"": ""together"" },
                        { ""text"": ""ひとりで行く"" }
                    ] } ],
                    ""nextProgress"": 1
                } ] }";

            var report = EventImporter.Parse(json);

            Assert.IsTrue(report.HasErrors);
            Assert.AreEqual(0, report.Events.Count);
            Assert.IsTrue(report.Diagnostics.Any(d => d.Message.Contains("options[1]")));
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
