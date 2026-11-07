using System.Collections.Generic;
using System.Linq;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 本物の Area01(小物・NavMesh)に置いたアイテムの検証。
    /// 人工のコライダーでは分からない「実際の小物と重なっていないか」「プレイヤーが歩いて行けるか」を見る。
    /// シーンはすでに開いているものは触らず、このテストが開いたものだけ最後に閉じる。
    /// </summary>
    public class FieldItemRealScenePlacementTests
    {
        private const string Dir = "Assets/_Project/Scenes/Field/";
        private const string PlanPath =
            "Assets/_Project/Features/Field/Data/ItemPlacementPlan.asset";
        private const string SpawnName = "PlayerSpawn_start";

        // 小物のコライダーが入っているシーン(実行時は AdditiveScenes が重ねる)と、アイテムのシーン。
        // 小物シーンは手で並べると階を足したときに漏れるので、作成ツールの一覧から引く。
        private static readonly string[] SceneNames = PropSceneWorkflow
            .Parts.Select(PropSceneWorkflow.SceneName)
            .Prepend("Field_Area01")
            .Append("Field_Area01_Items")
            .ToArray();

        private readonly List<Scene> _openedByTest = new();
        private List<FieldItemPickup> _pickups;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            foreach (var name in SceneNames)
            {
                var path = $"{Dir}{name}.unity";
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded)
                    continue;
                _openedByTest.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
            }

            var items = SceneManager.GetSceneByPath($"{Dir}Field_Area01_Items.unity");
            _pickups = items
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FieldItemPickup>(true))
                .ToList();
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            foreach (var scene in _openedByTest)
                if (scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
        }

        private static GameObject FindInLoadedScenes(string name)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var found = SceneManager
                    .GetSceneAt(i)
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == name);
                if (found != null)
                    return found.gameObject;
            }
            return null;
        }

        [Test]
        public void PlacedItems_DoNotOverlapProps()
        {
            Assert.That(
                _pickups,
                Is.Not.Empty,
                "Field_Area01_Items にアイテムが置かれていません。"
            );

            var overlapping = _pickups
                .Where(p => ItemSpotFinder.IsBlocked(p.transform.position))
                .Select(p => $"{p.name} {p.transform.position}")
                .ToList();

            Assert.That(overlapping, Is.Empty, "小物や壁と重なっているアイテムがあります。");
        }

        [Test]
        public void PlacedItems_AreReachableFromThePlayerSpawn()
        {
            var spawn = FindInLoadedScenes(SpawnName);
            Assert.IsNotNull(spawn, $"{SpawnName} が見つかりません。");
            Assert.IsTrue(
                NavMesh.SamplePosition(
                    spawn.transform.position,
                    out var from,
                    5f,
                    NavMesh.AllAreas
                ),
                "開始位置の近くに NavMesh がありません(ベイクされていますか)。"
            );

            var unreachable = new List<string>();
            foreach (var pickup in _pickups)
            {
                var position = pickup.transform.position;
                var path = new NavMeshPath();
                var reachable =
                    NavMesh.SamplePosition(position, out var to, 1.5f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete;
                if (!reachable)
                    unreachable.Add($"{pickup.name} {position}");
            }

            Assert.That(unreachable, Is.Empty, "開始位置から歩いて行けないアイテムがあります。");
        }

        [Test]
        public void PlanCandidates_AreAdjustedToFreeSpots()
        {
            var plan = AssetDatabase.LoadAssetAtPath<ItemPlacementPlan>(PlanPath);
            Assert.IsNotNull(plan, $"{PlanPath} がありません。");

            var taken = new List<Vector3>();
            foreach (var candidate in plan.entries.SelectMany(e => e.candidates))
            {
                Assert.IsTrue(
                    ItemSpotFinder.TryFind(candidate, taken, out var spot),
                    $"{candidate} の近くに置ける場所がありません。"
                );
                Assert.IsFalse(
                    ItemSpotFinder.IsBlocked(spot),
                    $"{candidate} → {spot} がまだ重なっています。"
                );
                taken.Add(spot);
            }
        }
    }
}
