using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// イベントの battle ステップが呼ぶ戦闘エリア(arena)を、ボスシーンに置き忘れていないかの検証。
    /// 置き忘れや名前の食い違いは、実行時に警告が出て戦闘が飛ばされるだけで気付きにくい。
    /// 本物のシーンを開く(作業中のシーンは触らず、このテストが開いたものだけ最後に閉じる)。
    /// </summary>
    public sealed class BossEncounterPlacementTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Bosses.unity";
        private const string DataDir = "Assets/_Project/Features/Event/Data";

        /// <summary>
        /// まだボスシーンに置いていない戦闘の arena。置いたらこの一覧から消す
        /// (置いたのに残っていると <see cref="NotPlacedYetList_HasNoPlacedEncounter"/> が落ちる)。
        /// </summary>
        private static readonly HashSet<string> NotPlacedYet = new()
        {
            "girl_13",
            "goldfish_20",
            "goldfish_12nt",
        };

        private Scene _scene;
        private bool _openedByTest;
        private List<string> _placedEncounterIds;
        private List<(string EventId, string EncounterId)> _battles;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            if (!_scene.isLoaded)
            {
                _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                _openedByTest = true;
            }

            _placedEncounterIds = _scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BattleEncounter>(true))
                .Select(arena =>
                    new SerializedObject(arena).FindProperty("_encounterId").stringValue
                )
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            _battles = AssetDatabase
                .FindAssets("t:EventDefinition", new[] { DataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EventDefinition>)
                .Where(e => e != null)
                .SelectMany(e =>
                    e.Steps.Where(s => s.Kind == StepKind.Battle).Select(s => (e.Id, s.EncounterId))
                )
                .ToList();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_openedByTest && _scene.isLoaded)
                EditorSceneManager.CloseScene(_scene, true);
        }

        [Test]
        public void EveryBattleStep_HasAnEncounterName()
        {
            var noName = _battles
                .Where(b => string.IsNullOrWhiteSpace(b.EncounterId))
                .Select(b => b.EventId)
                .ToList();

            Assert.That(
                noName,
                Is.Empty,
                "battle ステップに arena の名前が無いイベント(events.json を取り込み直す): "
                    + string.Join(", ", noName)
            );
        }

        [Test]
        public void EveryBattleEncounter_IsPlacedInTheBossScene()
        {
            var missing = _battles
                .Where(b =>
                    !_placedEncounterIds.Contains(b.EncounterId)
                    && !NotPlacedYet.Contains(b.EncounterId)
                )
                .Select(b => $"{b.EventId}(arena={b.EncounterId})")
                .ToList();

            Assert.That(
                missing,
                Is.Empty,
                "ボスシーンに BattleEncounter が無い戦闘(未配置なら NotPlacedYet に足す): "
                    + string.Join(", ", missing)
            );
        }

        [Test]
        public void NotPlacedYetList_HasNoPlacedEncounter()
        {
            var stale = NotPlacedYet.Where(_placedEncounterIds.Contains).ToList();

            Assert.That(
                stale,
                Is.Empty,
                "もうボスシーンに置いてある arena が NotPlacedYet に残っている: "
                    + string.Join(", ", stale)
            );
        }

        [Test]
        public void ArenaNames_AreUnique()
        {
            var duplicated = _placedEncounterIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.That(
                duplicated,
                Is.Empty,
                "同じ名前の BattleEncounter が複数ある(イベントが先に見つかった方を始めてしまう): "
                    + string.Join(", ", duplicated)
            );
        }

        [Test]
        public void EachBattleEncounter_IsUsedByOneBattleStep()
        {
            // 同じ戦闘エリアを2つのイベントが呼ぶと、倒した後に再戦できない。
            var shared = _battles
                .GroupBy(b => b.EncounterId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.That(
                shared,
                Is.Empty,
                "複数の battle ステップが呼んでいる arena(戦闘ごとに別のエリアを置く): "
                    + string.Join(", ", shared)
            );
        }
    }
}
