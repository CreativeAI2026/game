using CreativeAI.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 小物へ当たりを付けるツールの検証。一番守りたいのは「手で付けた当たりを上書きしない」ことと
    /// 「何度実行しても二重に付かない」こと(全部の小物シーンにまとめて掛けるので、壊すと気づきにくい)。
    /// </summary>
    public class PropColliderToolTests
    {
        private GameObject _root;
        private int _obstacle;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Props_Test");
            _obstacle = LayerMask.NameToLayer("Obstacle");
            Assert.That(_obstacle, Is.GreaterThanOrEqualTo(0), "Obstacle レイヤーが必要");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        /// <summary>当たりの無いメッシュを root の下に作る(CreatePrimitive が付ける当たりは外す)。</summary>
        private GameObject AddMesh(string name, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            return go;
        }

        [Test]
        public void メッシュに形どおりのBoxColliderを付けObstacleレイヤーにする()
        {
            var desk = AddMesh("RectangularTable_01");

            var r = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.Added, Is.EqualTo(1));
            var box = desk.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            var bounds = desk.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(box.center, Is.EqualTo(bounds.center));
            Assert.That(box.size, Is.EqualTo(bounds.size));
            Assert.That(desk.layer, Is.EqualTo(_obstacle));
        }

        [Test]
        public void グループの下に入れ子で置いた小物にも付ける()
        {
            var group = new GameObject("Room_A").transform;
            group.SetParent(_root.transform, false);
            var chair = AddMesh("Chair_03", group);

            PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(chair.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [Test]
        public void 除外リストのモデルには付けない()
        {
            var pen = AddMesh("Pen_red_02");

            var r = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.Excluded, Is.EqualTo(1));
            Assert.That(pen.GetComponent<Collider>(), Is.Null);
            Assert.That(pen.layer, Is.Not.EqualTo(_obstacle));
        }

        [Test]
        public void 手で付けた当たりがある小物は触らない()
        {
            var gate = AddMesh("Security gate");
            var manual = gate.AddComponent<SphereCollider>();

            var r = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.AlreadyHad, Is.EqualTo(1));
            Assert.That(gate.GetComponents<Collider>(), Is.EqualTo(new Collider[] { manual }));
        }

        [Test]
        public void 二回実行しても二重に付かない()
        {
            var desk = AddMesh("RectangularTable_01");

            PropColliderTool.AddColliders(_root, _obstacle);
            var second = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(second.Added, Is.EqualTo(0));
            Assert.That(desk.GetComponents<Collider>().Length, Is.EqualTo(1));
        }

        /// <summary>パーツを子に並べた小物(配置ツールが生成した椅子の形)を作る。</summary>
        private GameObject AddAssembly(string name, float spacing)
        {
            var prop = new GameObject(name);
            prop.transform.SetParent(_root.transform, false);
            AddMesh("Seat", prop.transform).transform.localPosition = new Vector3(
                -spacing / 2f,
                0f,
                0f
            );
            AddMesh("Leg", prop.transform).transform.localPosition = new Vector3(
                spacing / 2f,
                0f,
                0f
            );
            return prop;
        }

        [Test]
        public void 小さい小物は全体を囲む箱を1つだけ付ける()
        {
            var chair = AddAssembly("Chair", spacing: 1f);

            var r = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.Added, Is.EqualTo(1));
            Assert.That(chair.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            var box = chair.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null, "箱は小物の根に付く");
            Assert.That(box.size, Is.EqualTo(new Vector3(2f, 1f, 1f)));
            Assert.That(chair.layer, Is.EqualTo(_obstacle));
        }

        [Test]
        public void 大きい小物はメッシュごとに付けて間を塞がない()
        {
            var counter = AddAssembly("Counter", spacing: PropColliderTool.MergeMaxFootprint + 1f);

            var r = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.Added, Is.EqualTo(2));
            Assert.That(counter.GetComponent<Collider>(), Is.Null);
            foreach (Transform part in counter.transform)
                Assert.That(part.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [Test]
        public void 以前メッシュごとに付けた小さい小物は1つの箱に付け直す()
        {
            var chair = AddAssembly("Chair", spacing: 1f);
            foreach (Transform part in chair.transform)
            {
                var box = part.gameObject.AddComponent<BoxCollider>();
                var bounds = part.GetComponent<MeshFilter>().sharedMesh.bounds;
                box.center = bounds.center;
                box.size = bounds.size;
            }

            var r = PropColliderTool.AddColliders(_root, _obstacle);
            var second = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(r.Removed, Is.EqualTo(2));
            Assert.That(r.Added, Is.EqualTo(1));
            Assert.That(chair.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.That(second.Added + second.Removed, Is.EqualTo(0), "付け直した後はもう触らない");
        }

        [Test]
        public void 入れ子の小物の当たりを親の小物が外さない()
        {
            var desk = AddMesh("Desk");
            var lamp = AddMesh("Lamp", desk.transform);

            PropColliderTool.AddColliders(_root, _obstacle);
            var second = PropColliderTool.AddColliders(_root, _obstacle);

            Assert.That(second.Added + second.Removed, Is.EqualTo(0), "2回目は何も付け直さない");
            Assert.That(desk.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(lamp.GetComponent<BoxCollider>(), Is.Not.Null);
        }

        [TestCase("Chair_03", "Chair")]
        [TestCase("Chair (2)", "Chair")]
        [TestCase("Spoon holder", "Spoon holder")]
        [TestCase("Bookshelf_2 tiers", "Bookshelf_2 tiers")]
        public void Prefabでない小物のモデル名は連番を除いた名前(string name, string expected)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);

            Assert.That(PropColliderTool.ModelName(go), Is.EqualTo(expected));
        }
    }
}
