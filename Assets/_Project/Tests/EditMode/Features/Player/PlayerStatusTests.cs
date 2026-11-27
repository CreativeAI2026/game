using System.Collections.Generic;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 最終ステータス = 素の値 + 装備の補正 + 武器の補正の検証。
    /// 攻撃/防御/最大HP は %(パーセントポイント)なので base×(1+Σ%/100)、
    /// 会心率・会心ダメージは %ポイントの単純加算(会心率のみ 0〜100 でクランプ)。
    /// </summary>
    public class PlayerStatusTests
    {
        private readonly TestResidents _residents = new();
        private readonly List<Object> _created = new();
        private InventoryManager _inv;
        private EquipmentSlots _equipment;
        private WeaponManager _weapons;
        private GameObject _go;
        private PlayerStatus _status;
        private PlayerParameterData _data;

        [SetUp]
        public void SetUp()
        {
            _inv = _residents.Add<InventoryManager>();
            _equipment = _inv.Equipment;
            _weapons = _residents.Add<WeaponManager>();

            // 主人公の素の値。
            _data = ScriptableObject.CreateInstance<PlayerParameterData>();
            _data.baseAttackPower = 2000f;
            _data.baseDefense = 500f;
            _data.baseMaxLife = 1000f;
            _data.baseCriticalChance = 0f;
            _data.baseCriticalDamageRatio = 0f;
            _created.Add(_data);

            _go = new GameObject(nameof(PlayerStatus));
            _status = _go.AddComponent<PlayerStatus>();
            TestReflection.SetField(_status, "_playerData", _data);
            // EditMode では Awake / OnEnable が走らないので、自分で呼んで装備・武器の変化を受け取らせる。
            TestReflection.Invoke(_status, "Awake");
            TestReflection.Invoke(_status, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            TestReflection.Invoke(_status, "OnDisable");
            WeaponCatalog.InjectForTests(null);
            Object.DestroyImmediate(_go);
            _residents.Clear();
            foreach (var created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
        }

        // 装備の補正を、この補正だけを持つ装備品1つに付け替える(枠 0)。
        private void Equip(EquipmentBonus bonus)
        {
            var stats = new List<RolledStat>();
            void Add(StatType type, float value)
            {
                if (value != 0f)
                    stats.Add(new RolledStat(type.ToString(), value));
            }
            Add(StatType.AttackPct, bonus.attackPct);
            Add(StatType.DefensePct, bonus.defensePct);
            Add(StatType.MaxHpPct, bonus.maxHpPct);
            Add(StatType.CritRate, bonus.criticalChance);
            Add(StatType.CritDamage, bonus.criticalDamage);

            var gear = ScriptableObject.CreateInstance<ItemData>();
            gear.category = ItemCategory.Equipment;
            _created.Add(gear);
            _equipment.Set(0, _inv.AddInstance(gear, stats));
        }

        // 剣の定義をこの補正の武器にして、それを選ぶ。
        private void HoldWeapon(int attack = 0, float criticalRate = 0f, float criticalDamage = 0f)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.attack = attack;
            weapon.criticalRate = criticalRate;
            weapon.criticalDamage = criticalDamage;
            _created.Add(weapon);
            WeaponCatalog.InjectForTests(new[] { weapon });
            _weapons.GiveWeapon("sword");
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
            Equip(
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
            Equip(new EquipmentBonus { attackPct = 10f });
            HoldWeapon(attack: 25);

            Assert.AreEqual(2000f * 1.35f, _status.CurrentAttackPower, 1e-3f);
        }

        [Test]
        public void CritStats_AreAddedAsPercentagePoints()
        {
            // 弓(攻撃%+25 / 会心率+50)を選択中、装備で会心ダメージ+50 のイメージ。
            Equip(new EquipmentBonus { criticalDamage = 50f });
            HoldWeapon(attack: 25, criticalRate: 50f);

            Assert.AreEqual(50f, _status.CurrentCriticalChance, 1e-3f);
            Assert.AreEqual(50f, _status.CurrentCriticalDamageRatio, 1e-3f);
        }

        [Test]
        public void CriticalChance_IsClampedTo0To100()
        {
            Equip(new EquipmentBonus { criticalChance = 80f });
            HoldWeapon(criticalRate: 50f);
            Assert.AreEqual(100f, _status.CurrentCriticalChance, 1e-3f);

            _weapons.Clear();
            Equip(new EquipmentBonus { criticalChance = -30f });
            Assert.AreEqual(0f, _status.CurrentCriticalChance, 1e-3f);
        }

        [Test]
        public void RollDamage_CriticalAddsCritDamagePercentOfAttack()
        {
            // 会心率100%で必ず会心。会心ダメージ+50% → 攻撃力の1.5倍。
            HoldWeapon(criticalRate: 100f, criticalDamage: 50f);

            float dmg = _status.RollDamage(1f, out bool isCritical);

            Assert.IsTrue(isCritical);
            Assert.AreEqual(2000f * 1.5f, dmg, 1e-2f);
        }

        [Test]
        public void RollDamage_ZeroCritChance_NeverCrits()
        {
            // 素の会心率は0%。抽選の端(0)が出ても会心にしない。
            for (int i = 0; i < 1000; i++)
            {
                _status.RollDamage(1f, out bool isCritical);
                Assert.IsFalse(isCritical);
            }
        }

        [Test]
        public void TakeDamage_IsReducedByDefenseRatio()
        {
            _status.RestoreHp(1000f); // 防御 500 = 定数と同じ → 半減

            _status.TakeDamage(400f, isCritical: false);

            Assert.AreEqual(800f, _status.CurrentHp, 1e-2f, "400 × 500/(500+500) = 200 ダメージ");
        }

        [Test]
        public void TakeDamage_GuaranteesAtLeastOneDamage()
        {
            // 軽減後に1未満になってもノーダメージにはしない。
            _status.RestoreHp(1000f);

            _status.TakeDamage(1f, isCritical: false);

            Assert.AreEqual(999f, _status.CurrentHp, 1e-2f, "最低1ダメージは保証する");
        }

        [Test]
        public void TakeDamage_DefenseBonusRaisesMitigation()
        {
            // 防御% +100 → 防御 1000。同じ攻撃でも軽減が増える。
            Equip(new EquipmentBonus { defensePct = 100f });
            _status.RestoreHp(1000f);

            _status.TakeDamage(600f, isCritical: false);

            Assert.AreEqual(800f, _status.CurrentHp, 1e-2f, "600 × 500/(500+1000) = 200 ダメージ");
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
            Equip(new EquipmentBonus { maxHpPct = 100f }); // 最大HP 2000
            _status.Heal(9999f);
            Assert.AreEqual(2000f, _status.CurrentHp, 1e-3f);

            _equipment.Clear(0); // 最大HP 1000 に戻る
            Assert.AreEqual(1000f, _status.CurrentHp, 1e-3f);
        }

        [Test]
        public void MaxHpIncrease_AddsGainedAmountToCurrentHp()
        {
            _status.RestoreHp(600f); // 最大HP 1000 のうち 600

            Equip(new EquipmentBonus { maxHpPct = 50f }); // 最大HP 1500

            Assert.AreEqual(1100f, _status.CurrentHp, 1e-3f, "増えた 500 だけ現在HPも増える");
        }

        [Test]
        public void MaxHpIncrease_WhileDead_DoesNotRevive()
        {
            _status.RestoreHp(0f); // 死亡扱い

            Equip(new EquipmentBonus { maxHpPct = 50f });

            Assert.AreEqual(0f, _status.CurrentHp, 1e-3f);
        }
    }
}
