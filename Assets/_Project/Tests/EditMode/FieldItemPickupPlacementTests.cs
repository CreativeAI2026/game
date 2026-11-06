using System.Collections.Generic;
using System.Linq;
using CreativeAI.EditorTools;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// フィールドのアイテムの識別子の自動採番と、<c>Field_Area01_Items</c> への配置ミスの検証。
    /// 識別子の重複・付け忘れ・キラキラの付け忘れは、エラーが出ないまま「拾ったのに復活する」「光らない」になるだけ。
    /// </summary>
    public class FieldItemPickupPlacementTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Items.unity";
        private const string PrefabPath = "Assets/_Project/Art/VFX/Pickup/ItemPickup.prefab";

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null)
                    Object.DestroyImmediate(go);
            _created.Clear();
        }

        private FieldItemPickup NewPickup(string id = null)
        {
            var go = new GameObject("Pickup", typeof(BoxCollider));
            _created.Add(go);
            var pickup = go.AddComponent<FieldItemPickup>();
            if (id != null)
                TestReflection.SetField(pickup, "_pickupId", id);
            return pickup;
        }

        // --- 自動採番 ---

        [Test]
        public void Assign_GivesEveryPickupAnId()
        {
            var pickups = new[] { NewPickup(), NewPickup(), NewPickup() };

            var assigned = FieldItemPickupIdAssigner.Assign(pickups, "Field_Area01_Items");

            Assert.AreEqual(3, assigned);
            Assert.That(pickups.Select(p => p.PickupId), Has.All.Not.Null.And.Not.Empty);
            Assert.That(pickups.Select(p => p.PickupId), Is.Unique);
            Assert.That(pickups.Select(p => p.PickupId), Has.All.StartsWith("Field_Area01_Items/"));
        }

        [Test]
        public void Assign_DuplicatedPickup_GetsANewId()
        {
            // 複製すると識別子もそのままコピーされる。
            var original = NewPickup();
            FieldItemPickupIdAssigner.Assign(new[] { original }, "Area");
            var copy = Object.Instantiate(original.gameObject).GetComponent<FieldItemPickup>();
            _created.Add(copy.gameObject);
            Assert.AreEqual(original.PickupId, copy.PickupId);

            var assigned = FieldItemPickupIdAssigner.Assign(new[] { original, copy }, "Area");

            Assert.AreEqual(1, assigned);
            Assert.AreNotEqual(original.PickupId, copy.PickupId);
        }

        [Test]
        public void Assign_KeepsIdsThatAreAlreadyUnique()
        {
            var a = NewPickup("Area/a");
            var b = NewPickup("Area/b");

            var assigned = FieldItemPickupIdAssigner.Assign(new[] { a, b }, "Area");

            Assert.AreEqual(0, assigned);
            Assert.AreEqual("Area/a", a.PickupId);
            Assert.AreEqual("Area/b", b.PickupId);
        }

        [Test]
        public void Assign_DoesNotCollideWithIdsFromOtherScenes()
        {
            var other = new HashSet<string> { "Area/a" };
            var pickup = NewPickup("Area/a");

            FieldItemPickupIdAssigner.Assign(new[] { pickup }, "Area2", other);

            Assert.AreNotEqual("Area/a", pickup.PickupId);
        }

        // --- Prefab とシーン ---

        [Test]
        public void ItemPickupPrefab_HasSparkleAndNoBakedId()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} がありません。");

            var pickup = prefab.GetComponent<FieldItemPickup>();
            var so = new SerializedObject(pickup);

            var sparkle = so.FindProperty("_sparkle").objectReferenceValue as GameObject;
            Assert.IsNotNull(sparkle, "キラキラ(_sparkle)が割り当てられていません。");
            Assert.That(
                sparkle.GetComponentsInChildren<ParticleSystem>(true),
                Is.Not.Empty,
                "キラキラにパーティクルがありません。"
            );
            Assert.IsEmpty(
                pickup.PickupId ?? "",
                "Prefab 自体に識別子が入っています(置いた分が全部同じ識別子になる)。"
            );
        }

        [Test]
        public void ItemsScene_PickupsAreWellFormed()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedByTest = !scene.isLoaded;
            if (openedByTest)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var pickups = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<FieldItemPickup>(true))
                    .ToList();

                Assert.That(
                    pickups,
                    Is.Not.Empty,
                    "Field_Area01_Items にアイテムが置かれていません。"
                );
                Assert.That(
                    pickups.Where(p => p.Item == null).Select(p => p.name),
                    Is.Empty,
                    "Item が未設定のアイテムがあります。"
                );
                Assert.That(
                    pickups.Where(p => string.IsNullOrEmpty(p.PickupId)).Select(p => p.name),
                    Is.Empty,
                    "識別子が振られていないアイテムがあります。"
                );
                Assert.That(
                    pickups.Select(p => p.PickupId),
                    Is.Unique,
                    "識別子が重なっているアイテムがあります。"
                );
                Assert.That(
                    pickups
                        .Where(p =>
                            new SerializedObject(p).FindProperty("_sparkle").objectReferenceValue
                            == null
                        )
                        .Select(p => p.name),
                    Is.Empty,
                    "キラキラが付いていないアイテムがあります。"
                );
            }
            finally
            {
                if (openedByTest && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
