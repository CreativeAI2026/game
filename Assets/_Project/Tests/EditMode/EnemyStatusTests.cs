using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 敵の被ダメージ(防御軽減・怯み・警戒・死亡)と回復の検証。
    /// 怯みのクールダウンは Time.time を基準にするが、EditMode では進まないので「クールダウン中かどうか」だけを見る。
    /// </summary>
    public class EnemyStatusTests
    {
        private GameObject _go;
        private EnemyParameterData _data;
        private EnemyStatus _status;
        private int _flinches;
        private int _alerts;
        private int _deaths;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<EnemyParameterData>();
            _data.characterName = "テスト敵";
            _data.baseMaxLife = 1000f;
            _data.baseAttackPower = 80f;
            _data.baseDefense = 0f;
            _data.flinchDamageThreshold = 150f;
            _data.flinchCooldownTime = 10f;

            _go = new GameObject("Enemy");
            _status = _go.AddComponent<EnemyStatus>();
            TestReflection.SetField(_status, "_enemyData", _data);
            // EditMode では Start が走らないので、開始時のHP初期化を手動で行う。
            TestReflection.Invoke(_status, "Start");

            _flinches = _alerts = _deaths = 0;
            _status.OnFlinchTriggered += () => _flinches++;
            _status.OnAlertTriggered += () => _alerts++;
            _status.OnDeathTriggered += () => _deaths++;
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_data);
        }

        private float CurrentHp => TestReflection.GetField<float>(_status, "_currentHp");

        [Test]
        public void Start_FillsHpToMax_AndExposesBaseParameters()
        {
            _data.baseAttackPower = 123f;
            _data.baseDefense = 45f;

            Assert.AreEqual(1000f, CurrentHp, 1e-4f);
            Assert.AreEqual(1000f, _status.MaxHp, 1e-4f);
            Assert.AreEqual(123f, _status.CurrentAttackPower, 1e-4f);
            Assert.AreEqual(45f, _status.CurrentDefense, 1e-4f);
        }

        [Test]
        public void TakeDamage_ReducesHpByDefenseAdjustedDamage()
        {
            _data.baseDefense = DamageFormula.DefenseConstant;

            _status.TakeDamage(100f, false);

            Assert.AreEqual(950f, CurrentHp, 1e-3f);
        }

        [Test]
        public void TakeDamage_AlwaysDealsAtLeastOne()
        {
            _data.baseDefense = 100000f;

            _status.TakeDamage(1f, false);

            Assert.AreEqual(999f, CurrentHp, 1e-3f);
        }

        [Test]
        public void TakeDamage_RaisesAlertOnEveryHit_EvenWithoutFlinch()
        {
            _status.TakeDamage(10f, false);
            _status.TakeDamage(10f, false);

            Assert.AreEqual(2, _alerts);
            Assert.AreEqual(0, _flinches);
        }

        [Test]
        public void TakeDamage_FlinchesWhenSingleHitReachesThreshold()
        {
            _status.TakeDamage(150f, false);

            Assert.AreEqual(1, _flinches);
        }

        [Test]
        public void TakeDamage_DoesNotFlinchBelowThreshold()
        {
            _status.TakeDamage(149f, false);

            Assert.AreEqual(0, _flinches);
        }

        [Test]
        public void TakeDamage_CriticalFlinchesEvenBelowThreshold()
        {
            _status.TakeDamage(10f, true);

            Assert.AreEqual(1, _flinches);
        }

        [Test]
        public void TakeDamage_AfterFlinch_SuperArmorBlocksFurtherFlinch()
        {
            _status.TakeDamage(200f, false);
            _status.TakeDamage(200f, true);

            Assert.AreEqual(1, _flinches, "クールダウン中は強攻撃・クリティカルでも怯まない");
            Assert.AreEqual(2, _alerts, "怯まなくても警戒は毎回発火する");
        }

        [Test]
        public void TakeDamage_WithZeroCooldown_FlinchesOnEveryHeavyHit()
        {
            _data.flinchCooldownTime = 0f;

            _status.TakeDamage(200f, false);
            _status.TakeDamage(200f, false);

            Assert.AreEqual(2, _flinches);
        }

        [Test]
        public void TakeDamage_LethalHit_KillsOnceAndStopsReactingAfterwards()
        {
            LogAssert.ignoreFailingMessages = true; // 死亡時の Destroy(遅延)が EditMode ではエラーログになる

            _status.TakeDamage(1000f, false);

            Assert.IsTrue(_status.IsDead);
            Assert.AreEqual(1, _deaths);

            float hpAfterDeath = CurrentHp;
            int alertsAfterDeath = _alerts;
            int flinchesAfterDeath = _flinches;

            _status.TakeDamage(500f, true);

            Assert.AreEqual(hpAfterDeath, CurrentHp, 1e-4f);
            Assert.AreEqual(alertsAfterDeath, _alerts);
            Assert.AreEqual(flinchesAfterDeath, _flinches);
            Assert.AreEqual(1, _deaths, "死亡後に当たっても死亡イベントを再発火しない");
        }

        [Test]
        public void TakeDamage_NonLethalHit_KeepsEnemyAlive()
        {
            _status.TakeDamage(999f, false);

            Assert.IsFalse(_status.IsDead);
            Assert.AreEqual(0, _deaths);
        }

        [Test]
        public void Heal_RestoresHp_ButNeverAboveMax()
        {
            _status.TakeDamage(100f, false);

            _status.Heal(30f);
            Assert.AreEqual(930f, CurrentHp, 1e-3f);

            _status.Heal(10000f);
            Assert.AreEqual(1000f, CurrentHp, 1e-3f);
        }
    }
}
