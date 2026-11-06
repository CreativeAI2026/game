using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// dev_state.json(開発用の開始状態)の読み取りと反映の検証。
    /// </summary>
    public class DevStateTests
    {
        private sealed class FakeItemGiver : IItemGiver
        {
            public readonly List<string> Given = new();

            public void Give(string itemKey) => Given.Add(itemKey);

            public bool HasImportantItem(string itemKey) => Given.Contains(itemKey);
        }

        private GameObject _pmGo;
        private ProgressManager _pm;

        [SetUp]
        public void SetUp()
        {
            _pmGo = new GameObject("ProgressManager");
            _pm = _pmGo.AddComponent<ProgressManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_pmGo);

        [Test]
        public void Apply_SetsProgressFlagsAndGivesImportantItems()
        {
            var state = DevState.Parse(
                @"{ ""progress"": 8,
                    ""flags"": [ { ""key"": ""girl_choice"", ""value"": ""together"" } ],
                    ""importantItems"": [ ""mysterious_key"" ] }"
            );
            var giver = new FakeItemGiver();

            state.Apply(_pm, giver);

            Assert.AreEqual(8, _pm.Progress);
            Assert.AreEqual("together", _pm.GetFlag("girl_choice"));
            CollectionAssert.AreEqual(new[] { "mysterious_key" }, giver.Given);
        }

        [Test]
        public void Apply_OmittedFieldsStartFresh()
        {
            _pm.SetFlag("old", "x");
            var state = DevState.Parse(@"{ ""progress"": 3 }");
            var giver = new FakeItemGiver();

            state.Apply(_pm, giver);

            Assert.AreEqual(3, _pm.Progress);
            Assert.AreEqual(string.Empty, _pm.GetFlag("old"));
            Assert.IsEmpty(giver.Given);
        }

        [Test]
        public void Parse_OmittedHp_IsDetected()
        {
            var state = DevState.Parse(@"{ ""progress"": 0 }");

            Assert.IsFalse(state.HasHp, "hp は必須なので書き忘れを見分けられる");
            Assert.IsTrue(DevState.Parse(@"{ ""hp"": 0 }").HasHp, "0 は書いた扱い");
            Assert.IsEmpty(state.weapons);
        }

        [Test]
        public void ApplyPlayer_GivesWeaponsSelectsAndSetsHp()
        {
            var (playerGo, status, weapons, data) = MakePlayer();
            try
            {
                var state = DevState.Parse(
                    @"{ ""weapons"": [ ""sword"", ""scythe"" ], ""selectedWeapon"": ""scythe"", ""hp"": 300 }"
                );

                state.ApplyPlayer(weapons, status);

                CollectionAssert.AreEqual(
                    new[] { "sword", "scythe" },
                    weapons.CaptureOwnedWeaponKeys().ToList()
                );
                Assert.AreEqual(WeaponManager.IndexOfKey("scythe"), weapons.CurrentWeaponIndex);
                Assert.AreEqual(300f, status.CurrentHp, 1e-2f);
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void ApplyPlayer_WithoutHp_LeavesHpUnchanged()
        {
            var (playerGo, status, weapons, data) = MakePlayer();
            try
            {
                status.RestoreHp(700f);

                DevState.Parse(@"{ ""progress"": 0 }").ApplyPlayer(weapons, status);

                Assert.AreEqual(700f, status.CurrentHp, 1e-2f);
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void ApplyPlayer_WithoutRigParts_DoesNothing()
        {
            // 仮リグ(WeaponManager / PlayerStatus 無し)でも落ちない。
            Assert.DoesNotThrow(() =>
                DevState.Parse(@"{ ""weapons"": [ ""sword"" ], ""hp"": 1 }").ApplyPlayer(null, null)
            );
        }

        // SaveServiceTests と同じ組み方(最大HP 1000 のリグ)。
        private static (GameObject, PlayerStatus, WeaponManager, PlayerParameterData) MakePlayer()
        {
            var data = ScriptableObject.CreateInstance<PlayerParameterData>();
            data.baseMaxLife = 1000f;
            var go = new GameObject("Player");
            var status = go.AddComponent<PlayerStatus>();
            TestReflection.SetField(status, "_playerData", data);
            var weapons = go.AddComponent<WeaponManager>();
            return (go, status, weapons, data);
        }

        [Test]
        public void CommittedFile_ParsesWithHp()
        {
            var json = System.IO.File.ReadAllText(DevState.AssetPath);
            Assert.IsTrue(DevState.Parse(json).HasHp);
        }

        [Test]
        public void Parse_SpawnFields_AreReadAndDefaultToOff()
        {
            var withSpawn = DevState.Parse(
                @"{ ""useSpawn"": true, ""spawnPosition"": { ""x"": -592, ""y"": -60.8, ""z"": -30.5 }, ""spawnYaw"": 90 }"
            );
            var without = DevState.Parse(@"{ ""progress"": 1 }");

            Assert.IsTrue(withSpawn.useSpawn);
            Assert.AreEqual(new Vector3(-592f, -60.8f, -30.5f), withSpawn.spawnPosition);
            Assert.AreEqual(90f, withSpawn.spawnYaw);
            Assert.IsFalse(without.useSpawn);
        }

        [Test]
        public void ApplySpawn_PlacesThePlayerOnlyWhenEnabled()
        {
            var player = new GameObject("Player");
            try
            {
                var off = new DevState { spawnPosition = new Vector3(1f, 2f, 3f) };
                Assert.IsFalse(off.ApplySpawn(player));
                Assert.AreEqual(Vector3.zero, player.transform.position);

                var on = new DevState
                {
                    useSpawn = true,
                    spawnPosition = new Vector3(1f, 2f, 3f),
                    spawnYaw = 90f,
                };
                Assert.IsTrue(on.ApplySpawn(player));
                Assert.AreEqual(new Vector3(1f, 2f, 3f), player.transform.position);
                Assert.AreEqual(90f, player.transform.eulerAngles.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
