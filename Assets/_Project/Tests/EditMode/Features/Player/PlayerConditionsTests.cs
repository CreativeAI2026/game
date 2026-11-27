using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 行動の制限・怯み中・掴まれ中を owner ごとに出し入れし、他の owner の分を消さないことの検証(制限は新たに掛かった分だけ通知する)。
    /// </summary>
    public class PlayerConditionsTests
    {
        private PlayerConditions _conditions;
        private List<PlayerLock> _locked;

        [SetUp]
        public void SetUp()
        {
            _conditions = new PlayerConditions();
            _locked = new List<PlayerLock>();
            _conditions.Locked += added => _locked.Add(added);
        }

        [Test]
        public void 自分の制限を外しても他の制限は残る()
        {
            object flinch = new();
            object weapon = new();
            _conditions.Set(flinch, PlayerLock.All);
            _conditions.Set(weapon, PlayerLock.Move | PlayerLock.ChangeWeapon);

            _conditions.Release(weapon);

            Assert.IsTrue(_conditions.IsLocked(PlayerLock.Attack));
            Assert.IsTrue(_conditions.IsLocked(PlayerLock.Move));

            _conditions.Release(flinch);

            Assert.IsFalse(_conditions.IsLocked(PlayerLock.All));
        }

        [Test]
        public void 誰も掛けていなかった制限だけを通知する()
        {
            object weapon = new();
            object flinch = new();
            _conditions.Set(weapon, PlayerLock.Move | PlayerLock.ChangeWeapon);
            _conditions.Set(flinch, PlayerLock.All);

            CollectionAssert.AreEqual(
                new[] { PlayerLock.Move | PlayerLock.ChangeWeapon, PlayerLock.Attack },
                _locked
            );
        }

        [Test]
        public void 既に掛かっている制限を重ねても通知しない()
        {
            object grab = new();
            object death = new();
            _conditions.Set(grab, PlayerLock.All);
            _locked.Clear();

            _conditions.Set(death, PlayerLock.All);

            CollectionAssert.IsEmpty(_locked);
        }

        [Test]
        public void 怯みを打ち切っても_はじき返されている間は怯み中のまま()
        {
            object flinch = new();
            object repel = new();
            _conditions.SetFlinching(repel, true);
            _conditions.SetFlinching(flinch, true);

            _conditions.SetFlinching(flinch, false);

            Assert.IsTrue(_conditions.IsFlinching);

            _conditions.SetFlinching(repel, false);

            Assert.IsFalse(_conditions.IsFlinching);
        }

        [Test]
        public void 掴んだ全員が離すまで掴まれ中のまま()
        {
            object boss = new();
            object ring = new();
            _conditions.SetGrabbed(boss, true);
            _conditions.SetGrabbed(ring, true);

            _conditions.SetGrabbed(boss, false);

            Assert.IsTrue(_conditions.IsGrabbed);

            _conditions.SetGrabbed(ring, false);

            Assert.IsFalse(_conditions.IsGrabbed);
        }
    }
}
