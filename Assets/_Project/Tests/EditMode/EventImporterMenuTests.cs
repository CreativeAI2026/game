using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CreativeAI.Core;
using CreativeAI.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// events.json → .asset の書き出し(ファイル入出力)の検証。
    /// 一番守りたいのは「再インポートは既存 .asset の中身だけ差し替え、GUID を変えない」こと。
    /// GUID が変わるとシーンの EventTrigger の Event 欄が Missing になり、エラーも出ずに発火しなくなる。
    /// 本物の AssetDatabase を使うので、Assets 直下の一時フォルダに書き出して最後に消す。
    /// </summary>
    public class EventImporterMenuTests
    {
        private const string OutputDir = "Assets/__EventImporterMenuTests";
        private string _jsonPath;

        [SetUp]
        public void SetUp()
        {
            _jsonPath = Path.Combine(Path.GetTempPath(), "EventImporterMenuTests_events.json");
            AssetDatabase.DeleteAsset(OutputDir); // 前回の失敗で残っていれば消す
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(OutputDir);
            if (File.Exists(_jsonPath))
                File.Delete(_jsonPath);
        }

        private void WriteJson(string text) =>
            File.WriteAllText(
                _jsonPath,
                @"{ ""events"": [ {
                    ""id"": ""sample"",
                    ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],
                    ""steps"": [ { ""kind"": ""line"", ""text"": """
                    + text
                    + @""" } ],
                    ""nextProgress"": 2
                } ] }"
            );

        private static string AssetPath => $"{OutputDir}/sample.asset";

        private void WriteEvents(params string[] ids) =>
            File.WriteAllText(
                _jsonPath,
                @"{ ""events"": [ "
                    + string.Join(
                        ", ",
                        ids.Select(id =>
                            @"{ ""id"": """
                            + id
                            + @""", ""conditions"": [ { ""type"": ""progress"", ""value"": 1 } ],"
                            + @" ""steps"": [ { ""kind"": ""line"", ""text"": ""……。"" } ], ""nextProgress"": 2 }"
                        )
                    )
                    + " ] }"
            );

        [Test]
        public void FirstImport_CreatesFolderAndAsset()
        {
            WriteJson("はじめまして。");

            Assert.IsTrue(EventImporterMenu.ImportInto(_jsonPath, OutputDir));

            Assert.IsTrue(AssetDatabase.IsValidFolder(OutputDir), "出力フォルダが無ければ作る");
            var ev = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetPath);
            Assert.IsNotNull(ev);
            Assert.AreEqual("はじめまして。", ev.Steps[0].Text);
        }

        [Test]
        public void Reimport_KeepsGuid_AndUpdatesContent()
        {
            WriteJson("古い台詞");
            EventImporterMenu.ImportInto(_jsonPath, OutputDir);
            var guidBefore = AssetDatabase.AssetPathToGUID(AssetPath);
            var assetBefore = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetPath);

            WriteJson("直した台詞"); // 物語班が台詞を直して取り込み直した
            EventImporterMenu.ImportInto(_jsonPath, OutputDir);

            Assert.AreEqual(
                guidBefore,
                AssetDatabase.AssetPathToGUID(AssetPath),
                "GUID が変わるとシーンからの参照が切れる"
            );
            var assetAfter = AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetPath);
            Assert.AreSame(assetBefore, assetAfter, "作り直しではなく同じアセットを編集する");
            Assert.AreEqual("直した台詞", assetAfter.Steps[0].Text);
        }

        [Test]
        public void EventRemovedFromJson_WarnsLeftoverAsset_ButKeepsIt()
        {
            WriteEvents("boss", "robot");
            EventImporterMenu.ImportInto(_jsonPath, OutputDir);

            WriteEvents("boss_battle", "robot"); // boss の id を変えた
            LogAssert.Expect(LogType.Warning, new Regex("残っています.*boss\\.asset"));
            EventImporterMenu.ImportInto(_jsonPath, OutputDir);

            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<EventDefinition>($"{OutputDir}/boss.asset"),
                "シーンの参照を壊さないよう自動では消さない"
            );
        }

        [Test]
        public void InvalidJson_WritesNothing()
        {
            File.WriteAllText(_jsonPath, @"{ ""events"": [ { ""id"": ""sample"" } ] }"); // 必須項目なし
            LogAssert.ignoreFailingMessages = true; // 取り込みエラーを LogError で出すため

            Assert.IsFalse(EventImporterMenu.ImportInto(_jsonPath, OutputDir));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<EventDefinition>(AssetPath));
        }
    }
}
