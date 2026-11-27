using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 武器の持ち物の検証(初期0本 → 入手 → 入手ずみだけ切替 → 選択中の1本の補正 → セーブの往復)。
    /// 入手はイベントの giveWeapon から。
    /// </summary>
    public class WeaponManagerTests
    {
        private GameObject _go;
        private WeaponManager _weapons;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(WeaponManager));
            _weapons = _go.AddComponent<WeaponManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Initially_NoWeaponOwned()
        {
            // 主人公は最初1本も持たない。
            Assert.AreEqual(0, _weapons.OwnedCount);
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void GiveWeapon_FirstOne_IsOwnedAndAutoSelected()
        {
            _weapons.GiveWeapon("scythe");

            Assert.AreEqual(1, _weapons.OwnedCount);
            Assert.IsTrue(_weapons.IsOwned("scythe"));
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex, "最初の1本は自動で選択される");
        }

        [Test]
        public void GiveWeapon_SameKeyTwice_DoesNotDuplicate()
        {
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("sword");

            Assert.AreEqual(1, _weapons.OwnedCount);
        }

        [Test]
        public void GiveWeapon_UnknownKey_IsIgnored()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("axe"));

            _weapons.GiveWeapon("axe");

            Assert.AreEqual(0, _weapons.OwnedCount);
        }

        [Test]
        public void GiveWeapon_RaisesOwnedChanged_ForUIVisibility()
        {
            var counts = new List<int>();
            _weapons.OwnedChanged += () => counts.Add(_weapons.OwnedCount);

            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("sword"); // 重複は通知しない
            _weapons.GiveWeapon("bow");

            CollectionAssert.AreEqual(new[] { 1, 2 }, counts);
        }

        [Test]
        public void Select_OnlyCyclesOwnedWeapons()
        {
            // 剣と鎌だけ所持。弓(index 1)は未入手なのでスキップされる。
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("scythe");
            Assert.AreEqual(0, _weapons.CurrentWeaponIndex);

            Assert.IsTrue(_weapons.SelectNext());
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex, "未入手の弓を飛ばして鎌へ");

            Assert.IsTrue(_weapons.SelectNext());
            Assert.AreEqual(0, _weapons.CurrentWeaponIndex, "一周して剣へ戻る");

            Assert.IsTrue(_weapons.SelectPrevious());
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void Select_FollowsSwitchOrder_NotKeyIndexOrder()
        {
            // 切り替え順は E = 剣→鎌→ハンマー→弓(Q はその逆)。WeaponKeys の番号順(剣/弓/鎌/ハンマー)ではない。
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("bow");
            _weapons.GiveWeapon("scythe");
            _weapons.GiveWeapon("hammer");

            CollectionAssert.AreEqual(
                new[] { 0, 2, 3, 1 },
                _weapons.OwnedIndices,
                "入手順に関係なく切り替え順に並ぶ"
            );

            _weapons.SelectNext();
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex, "E: 剣 → 鎌");
            _weapons.SelectNext();
            Assert.AreEqual(3, _weapons.CurrentWeaponIndex, "E: 鎌 → ハンマー");
            _weapons.SelectNext();
            Assert.AreEqual(1, _weapons.CurrentWeaponIndex, "E: ハンマー → 弓");
            _weapons.SelectNext();
            Assert.AreEqual(0, _weapons.CurrentWeaponIndex, "E: 弓 → 剣");

            _weapons.SelectPrevious();
            Assert.AreEqual(1, _weapons.CurrentWeaponIndex, "Q: 剣 → 弓");
        }

        [Test]
        public void Select_DoesNothing_WhenZeroOrOneWeapon()
        {
            Assert.IsFalse(_weapons.SelectNext(), "0本なら切り替わらない");
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);

            _weapons.GiveWeapon("bow");

            Assert.IsFalse(_weapons.SelectPrevious(), "1本なら押しても変化なし");
            Assert.AreEqual(1, _weapons.CurrentWeaponIndex);
        }

        // --- セーブ復元 ---

        [Test]
        public void SaveState_RoundTrip_PreservesOwnedAndSelected()
        {
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("scythe");
            _weapons.SelectNext(); // 鎌を選択

            var ownedKeys = _weapons.CaptureOwnedWeaponKeys().ToList();
            int selected = _weapons.CurrentWeaponIndex;
            CollectionAssert.AreEqual(new[] { "sword", "scythe" }, ownedKeys);
            Assert.AreEqual(2, selected);

            var restoredGo = new GameObject("restored");
            try
            {
                var restored = restoredGo.AddComponent<WeaponManager>();
                restored.RestoreWeapons(ownedKeys, selected);

                Assert.AreEqual(2, restored.OwnedCount);
                Assert.IsTrue(restored.IsOwned("sword"));
                Assert.IsTrue(restored.IsOwned("scythe"));
                Assert.IsFalse(restored.IsOwned("bow"));
                Assert.AreEqual(2, restored.CurrentWeaponIndex);
            }
            finally
            {
                Object.DestroyImmediate(restoredGo);
            }
        }

        [Test]
        public void RestoreWeapons_EmptySave_RestoresZeroWeapons()
        {
            // 旧セーブ(武器フィールドなし)は 0本・未選択で復元される。
            _weapons.RestoreWeapons(new List<string>(), 0);

            Assert.AreEqual(0, _weapons.OwnedCount);
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void RestoreWeapons_SelectedNotOwned_FallsBackToFirstOwned()
        {
            _weapons.RestoreWeapons(new List<string> { "bow" }, selectedWeaponIndex: 0);

            Assert.AreEqual(1, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void Clear_DropsAllWeapons()
        {
            _weapons.GiveWeapon("sword");

            _weapons.Clear();

            Assert.AreEqual(0, _weapons.OwnedCount);
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void RestoreWeapons_UnknownKeysAreDropped()
        {
            _weapons.RestoreWeapons(new List<string> { "axe", "bow", "bow" }, 1);

            Assert.AreEqual(1, _weapons.OwnedCount);
            Assert.IsTrue(_weapons.IsOwned("bow"));
        }

        [Test]
        public void WeaponKeys_MatchScenarioCatalogOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "sword", "bow", "scythe", "hammer" },
                WeaponManager.WeaponKeys
            );
        }

        // --- 選択中の武器の補正(定義は WeaponCatalog から引く) ---

        private void InjectWeapons()
        {
            // 剣(攻撃%+25/会心ダメ+50) / 弓(攻撃%+25/会心率+50) / 鎌(会心率+50/会心ダメ+50)
            WeaponData Make(int attack, float critRate, float critDamage)
            {
                var d = ScriptableObject.CreateInstance<WeaponData>();
                d.attack = attack;
                d.criticalRate = critRate;
                d.criticalDamage = critDamage;
                _createdWeapons.Add(d);
                return d;
            }
            WeaponCatalog.InjectForTests(
                new[] { Make(25, 0f, 50f), Make(25, 50f, 0f), Make(0, 50f, 50f) }
            );
        }

        private readonly List<WeaponData> _createdWeapons = new();

        [TearDown]
        public void DestroyWeapons()
        {
            WeaponCatalog.InjectForTests(null);
            foreach (var d in _createdWeapons)
                Object.DestroyImmediate(d);
            _createdWeapons.Clear();
        }

        [Test]
        public void NoWeapon_BonusIsZero()
        {
            InjectWeapons();

            var b = _weapons.GetSelectedBonus();

            Assert.AreEqual(0f, b.attackPct);
            Assert.AreEqual(0f, b.criticalChance);
            Assert.AreEqual(0f, b.criticalDamage);
        }

        [Test]
        public void SelectedWeapon_BonusComesFromItsDefinition()
        {
            InjectWeapons();
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("bow");

            var sword = _weapons.GetSelectedBonus();
            Assert.AreEqual(25f, sword.attackPct);
            Assert.AreEqual(50f, sword.criticalDamage);
            Assert.AreEqual(0f, sword.criticalChance);

            _weapons.SelectNext();
            var bow = _weapons.GetSelectedBonus();
            Assert.AreEqual(25f, bow.attackPct);
            Assert.AreEqual(0f, bow.criticalDamage);
            Assert.AreEqual(50f, bow.criticalChance, "選択中の1本の補正だけが乗る(合算しない)");
        }
    }
}
