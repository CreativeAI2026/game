using System.Collections.Generic;
using System.Linq;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 武器の所持と選択の検証(初期0本 → 入手 → 入手ずみだけ切替 → 選択中の1本だけ補正が乗る)。
    /// 入手はイベントの giveWeapon から(documents/Specification.md §1.1, §5, §6)。
    /// </summary>
    public class WeaponManagerTests
    {
        private GameObject _go;
        private WeaponManager _weapons;
        private readonly List<Object> _created = new();

        private WeaponData MakeWeaponData(int attack, float critRate, float critDamage)
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.attack = attack;
            d.criticalRate = critRate;
            d.criticalDamage = critDamage;
            _created.Add(d);
            return d;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(WeaponManager));
            _weapons = _go.AddComponent<WeaponManager>();

            // 剣(攻撃%+25/会心ダメ+50) / 弓(攻撃%+25/会心率+50) / 鎌(会心率+50/会心ダメ+50)
            var stats = new[]
            {
                MakeWeaponData(25, 0f, 50f),
                MakeWeaponData(25, 50f, 0f),
                MakeWeaponData(0, 50f, 50f),
            };
            var so = new SerializedObject(_weapons);
            var prop = so.FindProperty("_weaponStats");
            prop.arraySize = stats.Length;
            for (int i = 0; i < stats.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = stats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Initially_NoWeaponOwned_AndBonusIsZero()
        {
            // spec §1.1: 主人公は最初1本も持たない。§1: 武器を1本も持っていなければ補正 0。
            Assert.AreEqual(0, _weapons.OwnedCount);
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);

            var b = _weapons.GetSelectedBonus();
            Assert.AreEqual(0f, b.attackPct);
            Assert.AreEqual(0f, b.criticalChance);
            Assert.AreEqual(0f, b.criticalDamage);
        }

        [Test]
        public void GiveWeapon_FirstOne_IsOwnedAutoSelected_AndAppliesItsBonus()
        {
            _weapons.GiveWeapon("scythe");

            Assert.AreEqual(1, _weapons.OwnedCount);
            Assert.IsTrue(_weapons.IsOwned("scythe"));
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex, "最初の1本は自動で選択される");

            var b = _weapons.GetSelectedBonus();
            Assert.AreEqual(0f, b.attackPct);
            Assert.AreEqual(50f, b.criticalChance);
            Assert.AreEqual(50f, b.criticalDamage);
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
        public void GiveWeapon_RaisesOwnedCountChanged_ForUIVisibility()
        {
            var counts = new List<int>();
            _weapons.OnOwnedCountChanged += counts.Add;

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
        public void Select_DoesNothing_WhenZeroOrOneWeapon()
        {
            Assert.IsFalse(_weapons.SelectNext(), "0本なら切り替わらない");
            Assert.AreEqual(WeaponManager.NoWeapon, _weapons.CurrentWeaponIndex);

            _weapons.GiveWeapon("bow");

            Assert.IsFalse(_weapons.SelectPrevious(), "1本なら押しても変化なし");
            Assert.AreEqual(1, _weapons.CurrentWeaponIndex);
        }

        [Test]
        public void Select_SwitchesWhichBonusApplies()
        {
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

        // --- セーブ復元(IWeaponSaveState) ---

        [Test]
        public void SaveState_RoundTrip_PreservesOwnedAndSelected()
        {
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("scythe");
            _weapons.SelectNext(); // 鎌を選択

            var ownedKeys = _weapons.CaptureOwnedWeaponKeys().ToList();
            int selected = _weapons.CaptureSelectedWeaponIndex();
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
        public void RestoreWeapons_UnknownKeysAreDropped()
        {
            _weapons.RestoreWeapons(new List<string> { "axe", "bow", "bow" }, 1);

            Assert.AreEqual(1, _weapons.OwnedCount);
            Assert.IsTrue(_weapons.IsOwned("bow"));
        }

        [Test]
        public void WeaponKeys_MatchScenarioReferenceCatalogOrder()
        {
            CollectionAssert.AreEqual(new[] { "sword", "bow", "scythe" }, WeaponManager.WeaponKeys);
        }
    }
}
