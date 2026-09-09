using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 武器切替UIの出し分けの検証(モードではなく武器の所持本数で決める)。
    /// 0本=非表示 / 1本以上=表示(documents/Specification.md §5)。
    /// GameObject ごと止めると WeaponManager の購読が切れるため Canvas の enabled で出し入れする。
    /// </summary>
    public class WeaponHUDControllerTests
    {
        private GameObject _go;
        private WeaponManager _weapons;
        private WeaponHUDController _hud;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private readonly List<Object> _created = new();

        private WeaponData MakeWeaponData(int attack)
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.attack = attack;
            _created.Add(d);
            return d;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WeaponSwitchUI");
            _canvas = _go.AddComponent<Canvas>();
            _raycaster = _go.AddComponent<GraphicRaycaster>();
            _weapons = _go.AddComponent<WeaponManager>();
            _hud = _go.AddComponent<WeaponHUDController>();

            var stats = new[] { MakeWeaponData(25), MakeWeaponData(25), MakeWeaponData(0) };
            var so = new SerializedObject(_weapons);
            var prop = so.FindProperty("_weaponStats");
            prop.arraySize = stats.Length;
            for (int i = 0; i < stats.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = stats[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // EditMode では Awake/OnEnable が走らないので、Awake が解決する参照を入れて OnEnable を叩く。
            TestReflection.SetField(_hud, "_canvas", _canvas);
            TestReflection.SetField(_hud, "_raycaster", _raycaster);
            TestReflection.SetField(_hud, "_weaponManager", _weapons);
            TestReflection.Invoke(_hud, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_hud, "OnDisable");
            Object.DestroyImmediate(_go);
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        private bool IsShown => _canvas.enabled && _raycaster.enabled;

        [Test]
        public void ZeroWeapons_IsHidden()
        {
            Assert.AreEqual(0, _weapons.OwnedCount); // 前提: 初期0本
            Assert.IsFalse(IsShown, "武器0本のときは非表示(spec §5)");
        }

        [Test]
        public void FirstWeapon_ShowsIt()
        {
            _weapons.GiveWeapon("sword");

            Assert.IsTrue(IsShown, "1本でも入手すると表示される");
        }

        [Test]
        public void MoreWeapons_StaysShown()
        {
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("bow");

            Assert.IsTrue(IsShown);
        }

        [Test]
        public void RestoringASaveWithoutWeapons_IsHidden()
        {
            _weapons.GiveWeapon("sword");
            Assert.IsTrue(IsShown);

            _weapons.RestoreWeapons(new string[0], WeaponManager.NoWeapon); // 0本のセーブから再開

            Assert.IsFalse(IsShown);
        }

        [Test]
        public void RestoringASaveWithWeapons_IsShown()
        {
            _weapons.RestoreWeapons(new[] { "scythe" }, 2);

            Assert.IsTrue(IsShown);
        }

        [Test]
        public void GameObjectStaysActive_SoSubscriptionsSurvive()
        {
            Assert.IsTrue(
                _go.activeSelf,
                "SetActive(false) で自分を止めると WeaponManager の通知を受け取れなくなる"
            );
        }

        [Test]
        public void SwitchingWeapons_DoesNotHideIt()
        {
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("bow");

            _weapons.SelectNext();

            Assert.IsTrue(IsShown, "切替は表示状態に影響しない(所持本数だけが軸)");
        }
    }
}
