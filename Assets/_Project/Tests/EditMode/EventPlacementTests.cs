using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// <c>Field_Area01_Events</c> への配置ミスを検出する(Issue #87)。
    /// 付け忘れ・Is Trigger の入れ忘れ・敵 Prefab の設定漏れは、どれもエラーが出ないまま発火しないだけになる。
    /// 発火のしかた自体は EventTriggerTests が見る。ここは「シーンに正しく置けているか」だけを見る。
    /// 本物のシーンを開く(作業中のシーンは触らず、このテストが開いたものだけ最後に閉じる)。
    /// </summary>
    public class EventPlacementTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Events.unity";
        private const string DataDir = "Assets/_Project/Features/Event/Data";

        private Scene _scene;
        private bool _openedByTest;
        private List<EventTrigger> _triggers;
        private List<EventDefinition> _events;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _scene = SceneManager.GetSceneByPath(ScenePath);
            if (!_scene.isLoaded)
            {
                _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                _openedByTest = true;
            }

            _triggers = _scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventTrigger>(true))
                .ToList();

            _events = AssetDatabase
                .FindAssets("t:EventDefinition", new[] { DataDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EventDefinition>)
                .Where(e => e != null)
                .OrderBy(e => e.Id)
                .ToList();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_openedByTest && _scene.isLoaded)
                EditorSceneManager.CloseScene(_scene, true);
        }

        private static SerializedObject Serialize(EventTrigger trigger) => new(trigger);

        private static EventDefinition EventOf(EventTrigger trigger) =>
            Serialize(trigger).FindProperty("_event").objectReferenceValue as EventDefinition;

        private static GameObject EnemyOf(EventTrigger trigger) =>
            Serialize(trigger).FindProperty("_enemy").objectReferenceValue as GameObject;

        [Test]
        public void EventAssets_AreFound()
        {
            Assert.That(_events, Is.Not.Empty, $"{DataDir} にイベントの .asset がありません。");
        }

        [Test]
        public void EveryEventAsset_HasATriggerInTheScene()
        {
            var placed = new HashSet<EventDefinition>(_triggers.Select(EventOf));
            var missing = _events.Where(e => !placed.Contains(e)).Select(e => e.Id).ToList();

            Assert.That(
                missing,
                Is.Empty,
                "シーンに置かれていないイベント: " + string.Join(", ", missing)
            );
        }

        [Test]
        public void EveryTrigger_HasAnEventAssigned()
        {
            var empty = _triggers.Where(t => EventOf(t) == null).Select(t => t.name).ToList();

            Assert.That(
                empty,
                Is.Empty,
                "Event 欄が空(Missing 含む)のトリガー: " + string.Join(", ", empty)
            );
        }

        [Test]
        public void EveryTrigger_ColliderIsTrigger()
        {
            var solid = _triggers
                .Where(t =>
                {
                    var col = t.GetComponent<Collider>();
                    return col == null || !col.isTrigger;
                })
                .Select(t => t.name)
                .ToList();

            Assert.That(
                solid,
                Is.Empty,
                "Collider が無い、または Is Trigger がオフのトリガー(壁になって発火しない): "
                    + string.Join(", ", solid)
            );
        }

        [Test]
        public void EveryTrigger_PlayerTagExists()
        {
            var bad = new List<string>();
            foreach (var t in _triggers)
            {
                var tag = Serialize(t).FindProperty("_playerTag").stringValue;
                if (
                    string.IsNullOrEmpty(tag)
                    || !UnityEditorInternal.InternalEditorUtility.tags.Contains(tag)
                )
                    bad.Add($"{t.name}(tag='{tag}')");
            }

            Assert.That(
                bad,
                Is.Empty,
                "Player タグが空、またはプロジェクトに無いタグのトリガー: " + string.Join(", ", bad)
            );
        }

        [Test]
        public void EveryTrigger_IsUnderTheEventsRoot()
        {
            var stray = _triggers
                .Where(t => t.transform.root.name != "Events")
                .Select(t => t.name)
                .ToList();

            Assert.That(
                stray,
                Is.Empty,
                "Events の外に置かれたトリガー(手順書は Events の子に置く): "
                    + string.Join(", ", stray)
            );
        }

        [Test]
        public void BattleEvents_HaveAnEnemyPrefab()
        {
            var noEnemy = new List<string>();
            foreach (var t in _triggers)
            {
                var ev = EventOf(t);
                if (ev == null || !ev.Steps.Any(s => s.Kind == StepKind.Battle))
                    continue;

                var enemy = EnemyOf(t);
                if (enemy == null || !PrefabUtility.IsPartOfPrefabAsset(enemy))
                    noEnemy.Add($"{t.name}(event={ev.Id})");
            }

            Assert.That(
                noEnemy,
                Is.Empty,
                "battle を含むのに Enemy が空、または Prefab でないトリガー: "
                    + string.Join(", ", noEnemy)
            );
        }
    }
}
