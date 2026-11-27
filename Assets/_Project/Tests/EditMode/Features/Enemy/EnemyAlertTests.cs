using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 敵の警戒状態が SetAlerted で切り替わり、変わった時だけ通知されることの検証。
    /// </summary>
    public class EnemyAlertTests
    {
        private GameObject _go;
        private EnemyBaseController _enemy;
        private List<bool> _changes;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Enemy");
            _enemy = _go.AddComponent<EnemyBaseController>();
            _changes = new List<bool>();
            _enemy.AlertedChanged += alerted => _changes.Add(alerted);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void 変わった時だけ通知する()
        {
            _enemy.SetAlerted(true);
            _enemy.SetAlerted(true);
            _enemy.SetAlerted(false);

            Assert.IsFalse(_enemy.IsAlerted);
            CollectionAssert.AreEqual(new[] { true, false }, _changes);
        }
    }
}
