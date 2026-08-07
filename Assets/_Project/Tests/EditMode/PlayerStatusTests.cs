using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 最終ステータス = 素の値 + 装備の補正 + 武器の補正(documents/Specification.md §1)の検証。
    /// 攻撃/防御/最大HP は %(パーセントポイント)なので base×(1+Σ%/100)、
    /// 会心率・会心ダメージは %ポイントの単純加算(会心率のみ 0〜100 でクランプ)。
    /// </summary>
    public class PlayerStatusTests
    {
        private GameObject _go;
        private PlayerStatus _status;
        private PlayerParameterData _data;

        [SetUp]
        public void SetUp()
        {
            // Specification.md §1 の主人公の素の値。
            _data = ScriptableObject.CreateInstance<PlayerParameterData>();
            _data.baseAttackPower = 2000f;
            _data.baseDefense = 500f;
            _data.baseMaxLife = 1000f;
            _data.baseCriticalChance = 0f;
            _data.baseCriticalDamageRatio = 0f;

            _go = new GameObject(nameof(PlayerStatus));
            _status = _go.AddComponent<PlayerStatus>();
            var so = new SerializedObject(_status);
            so.FindProperty("_playerData").objectReferenceValue = _data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void NoBonuses_FinalEqualsBase()
        {
            Assert.AreEqual(2000f, _status.CurrentAttackPower, 1e-3f);
            Assert.AreEqual(500f, _status.CurrentDefense, 1e-3f);
            Assert.AreEqual(1000f, _status.CurrentMaxHp, 1e-3f);
            Assert.AreEqual(0f, _status.CurrentCriticalChance, 1e-3f);
            Assert.AreEqual(0f, _status.CurrentCriticalDamageRatio, 1e-3f);
        }

        [Test]
        public void PercentBonuses_ApplyAsPercentOfBase_NotFlat()
        {
            // 攻撃% +10 は「+10」ではなく「base の 10%」= +200。
            _status.SetEquipment(
                new EquipmentBonus
                {
                    attackPct = 10f,
                    defensePct = 20f,
                    maxHpPct = 50f,
                }
            );

            Assert.AreEqual(2200f, _status.CurrentAttackPower, 1e-3f);
            Assert.AreEqual(600f, _status.CurrentDefense, 1e-3f);
            Assert.AreEqual(1500f, _status.CurrentMaxHp, 1e-3f);
        }

        [Test]
        public void EquipmentAndWeaponPercents_AreSummedBeforeApplying()
        {
            // 装備 +10% と武器 +25% は %ポイントで合算してから1回だけ乗せる(1.1×1.25 ではない)。
            _status.SetEquipment(new EquipmentBonus { attackPct = 10f, maxHpPct = 10f });
            _status.SetWeaponBonus(new EquipmentBonus { attackPct = 25f, maxHpPct = 40f });

            Assert.AreEqual(2000f * 1.35f, _status.CurrentAttackPower, 1e-3f);
            Assert.AreEqual(1000f * 1.50f, _status.CurrentMaxHp, 1e-3f);
        }

        [Test]
        public void CritStats_AreAddedAsPercentagePoints()
        {
            // 弓(攻撃%+25 / 会心率+50)を選択中、装備で会心ダメージ+50 のイメージ。
            _status.SetEquipment(new EquipmentBonus { criticalDamage = 50f });
            _status.SetWeaponBonus(new EquipmentBonus { attackPct = 25f, criticalChance = 50f });

            Assert.AreEqual(50f, _status.CurrentCriticalChance, 1e-3f);
            Assert.AreEqual(50f, _status.CurrentCriticalDamageRatio, 1e-3f);
        }

        [Test]
        public void CriticalChance_IsClampedTo0To100()
        {
            _status.SetEquipment(new EquipmentBonus { criticalChance = 80f });
            _status.SetWeaponBonus(new EquipmentBonus { criticalChance = 50f });
            Assert.AreEqual(100f, _status.CurrentCriticalChance, 1e-3f);

            _status.SetEquipment(new EquipmentBonus { criticalChance = -30f });
            _status.SetWeaponBonus(default);
            Assert.AreEqual(0f, _status.CurrentCriticalChance, 1e-3f);
        }

        [Test]
        public void RollDamage_CriticalAddsCritDamagePercentOfAttack()
        {
            // 会心率100%で必ず会心。会心ダメージ+50% → 攻撃力の1.5倍。
            _status.SetWeaponBonus(
                new EquipmentBonus { criticalChance = 100f, criticalDamage = 50f }
            );

            float dmg = _status.RollDamage(1f, out bool isCritical);

            Assert.IsTrue(isCritical);
            Assert.AreEqual(2000f * 1.5f, dmg, 1e-2f);
        }

        [Test]
        public void TakeDamage_IsReducedByDefense()
        {
            _status.RestoreHp(1000f); // 防御 500

            _status.TakeDamage(800f, isCritical: false);

            Assert.AreEqual(700f, _status.CurrentHp, 1e-2f, "800 - 防御500 = 300 ダメージ");
        }

        [Test]
        public void TakeDamage_GuaranteesAtLeastOneDamage()
        {
            // 防御力がダメージを上回ってもノーダメージにはしない。
            _status.RestoreHp(1000f);

            _status.TakeDamage(10f, isCritical: false);

            Assert.AreEqual(999f, _status.CurrentHp, 1e-2f, "最低1ダメージは保証する");
        }

        [Test]
        public void TakeDamage_ExactlyEqualToDefense_StillDealsOne()
        {
            _status.RestoreHp(1000f);

            _status.TakeDamage(500f, isCritical: false); // ダメージ == 防御

            Assert.AreEqual(999f, _status.CurrentHp, 1e-2f);
        }

        [Test]
        public void TakeDamage_DefenseBonusRaisesMitigation()
        {
            // 防御% +100 → 防御 1000。同じ攻撃でも軽減が増える。
            _status.SetEquipment(new EquipmentBonus { defensePct = 100f });
            _status.RestoreHp(1000f);

            _status.TakeDamage(1200f, isCritical: false);

            Assert.AreEqual(800f, _status.CurrentHp, 1e-2f, "1200 - 防御1000 = 200 ダメージ");
        }

        [Test]
        public void TakeDamage_CanReduceHpToZeroOrBelow()
        {
            _status.RestoreHp(100f);

            _status.TakeDamage(5000f, isCritical: false);

            Assert.LessOrEqual(_status.CurrentHp, 0f, "HP0 以下まで落ちる(死亡判定は Die 側)");
        }

        [Test]
        public void MaxHpDecrease_ClampsCurrentHp()
        {
            _status.SetEquipment(new EquipmentBonus { maxHpPct = 100f }); // 最大HP 2000
            _status.Heal(9999f);
            Assert.AreEqual(2000f, _status.CurrentHp, 1e-3f);

            _status.SetEquipment(default); // 最大HP 1000 に戻る
            Assert.AreEqual(1000f, _status.CurrentHp, 1e-3f);
        }
    }
}
