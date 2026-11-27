using CreativeAI.Gameplay;
using CreativeAI.Session;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// プレイヤーが倒れたらタイトルへ戻し始めることの検証。
    /// </summary>
    public sealed class ReturnToTitleOnDeathTests
    {
        private GameObject _player;
        private PlayerStatus _status;
        private PlayerParameterData _parameters;
        private ReturnToTitleOnDeath _returner;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _status = _player.AddComponent<PlayerStatus>();
            // 被ダメージの計算に素のステータスが要る(Prefab なしで作るのでここで渡す)。
            _parameters = ScriptableObject.CreateInstance<PlayerParameterData>();
            TestReflection.SetField(_status, "_playerData", _parameters);

            _returner = _player.AddComponent<ReturnToTitleOnDeath>();
            // EditMode では Awake / OnEnable が自動で走らないので、自分で呼んで購読を始める。
            TestReflection.Invoke(_returner, "Awake");
            TestReflection.Invoke(_returner, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_returner, "OnDisable");
            Object.DestroyImmediate(_player);
            Object.DestroyImmediate(_parameters);
        }

        [Test]
        public void Alive_DoesNotStartReturning()
        {
            Assert.IsFalse(IsReturning());
        }

        [Test]
        public void Died_StartsReturningToTitle()
        {
            Kill();

            Assert.IsTrue(IsReturning(), "倒れる動きを見せてからタイトルへ戻し始める");
        }

        // 倒れるまでダメージを与える(持続ダメージ扱いにして無敵時間に止められないようにする)。
        private void Kill() => _status.TakeDamage(float.MaxValue, false, suppressFlinch: true);

        private bool IsReturning() => TestReflection.GetField<bool>(_returner, "_returning");
    }
}
