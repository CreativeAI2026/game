using System.Collections.Generic;
using System.Linq;
using CreativeAI.Crafting;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ロール済み個体(調合品・拾得品)の付与ステータスが最終ステータスまで届くかの検証
    /// (documents/Specification.md §1「装備の補正」/ §2.1.1「ロールするのは拾った瞬間」/ §2.3)。
    ///
    /// ここが守っているのは <see cref="RolledStat.stat"/> の<b>語彙の一致</b>:
    /// 書き出し(CraftStatBridge.RollEquipment / RollDrop)と読み取り(Accumulate)がズレると、
    /// 装備しても補正が 0 のまま黙って無視される。
    /// </summary>
    public class RolledStatBonusTests
    {
        private GameObject _invGo;
        private InventoryManager _inv;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _invGo = new GameObject(nameof(InventoryManager));
            _inv = _invGo.AddComponent<InventoryManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_invGo);
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
        }

        private EquipmentData MakeEquipment(int id, int attack = 0, int seedPower = 0)
        {
            var d = ScriptableObject.CreateInstance<EquipmentData>(); // OnEnable が category=Equipment
            d.id = id;
            d.attack = attack + seedPower;
            _created.Add(d);
            return d;
        }

        private ItemStack AddEquippedInstance(EquipmentData data, params RolledStat[] rolled)
        {
            var stack = _inv.AddInstance(data, rolled);
            _inv.SetEquipped(stack, true);
            return stack;
        }

        // --- 語彙: 書き出しは StatType 名 ---

        [Test]
        public void RollDrop_WritesStatTypeNames()
        {
            var seed = MakeEquipment(2101, seedPower: 20);

            var rolled = CraftStatBridge.RollDrop(seed, new SystemRandomSource(1));

            CollectionAssert.IsNotEmpty(rolled.ToList());
            foreach (var r in rolled)
                Assert.IsTrue(
                    System.Enum.TryParse<StatType>(r.stat, ignoreCase: false, out _),
                    $"'{r.stat}' は StatType の名前ではない(Accumulate が読めない語彙)"
                );
        }

        [Test]
        public void RollEquipment_WritesStatTypeNames()
        {
            var a = MakeEquipment(2102, seedPower: 10);
            var b = MakeEquipment(2103, seedPower: 10);

            var rolled = CraftStatBridge.RollEquipment(a, b, new SystemRandomSource(2));

            foreach (var r in rolled)
                Assert.IsTrue(System.Enum.TryParse<StatType>(r.stat, ignoreCase: false, out _));
        }

        // --- 合算: ロール値が装備補正に乗る ---

        [Test]
        public void EquippedInstance_AddsItsRolledValues_NotTheSeedValues()
        {
            // 個体の補正はロール値で決まる。アセットの固定値(§2.1.1: 総パワーの宣言)は使わない。
            var gear = MakeEquipment(2104, seedPower: 999);
            AddEquippedInstance(
                gear,
                new RolledStat(nameof(StatType.AttackPct), 12f),
                new RolledStat(nameof(StatType.CritRate), 8f)
            );

            var bonus = _inv.GetEquippedBonus();

            Assert.AreEqual(12f, bonus.attackPct, 1e-3f);
            Assert.AreEqual(8f, bonus.criticalChance, 1e-3f);
            Assert.AreEqual(0f, bonus.defensePct, 1e-3f, "アセットの固定値が混ざってはいけない");
        }

        [Test]
        public void EveryStatType_MapsToItsOwnBonusField()
        {
            var gear = MakeEquipment(2105);
            AddEquippedInstance(
                gear,
                new RolledStat(nameof(StatType.AttackPct), 1f),
                new RolledStat(nameof(StatType.DefensePct), 2f),
                new RolledStat(nameof(StatType.MaxHpPct), 3f),
                new RolledStat(nameof(StatType.CritRate), 4f),
                new RolledStat(nameof(StatType.CritDamage), 5f)
            );

            var bonus = _inv.GetEquippedBonus();

            Assert.AreEqual(1f, bonus.attackPct, 1e-3f);
            Assert.AreEqual(2f, bonus.defensePct, 1e-3f);
            Assert.AreEqual(3f, bonus.maxHpPct, 1e-3f);
            Assert.AreEqual(4f, bonus.criticalChance, 1e-3f);
            Assert.AreEqual(5f, bonus.criticalDamage, 1e-3f);
        }

        [Test]
        public void LegacyCamelCaseStatNames_StillApply()
        {
            // 旧セーブ・手書きデータの "attackPct" 表記。読めないと「装備しても効かない」で黙って壊れる。
            var gear = MakeEquipment(2106);
            AddEquippedInstance(gear, new RolledStat("attackPct", 7f));

            Assert.AreEqual(7f, _inv.GetEquippedBonus().attackPct, 1e-3f);
        }

        [Test]
        public void UnknownStatName_IsIgnored()
        {
            var gear = MakeEquipment(2107);
            AddEquippedInstance(
                gear,
                new RolledStat("moveSpeed", 50f), // PlayerStatus の対象外
                new RolledStat(null, 50f),
                new RolledStat(nameof(StatType.AttackPct), 3f)
            );

            var bonus = _inv.GetEquippedBonus();

            Assert.AreEqual(
                3f,
                bonus.attackPct,
                1e-3f,
                "未知の型に引きずられて落ちない/混ざらない"
            );
        }

        [Test]
        public void HealAmount_DoesNotLeakIntoEquipmentBonus()
        {
            // HealAmount は食材専用(§2.1)。装備補正のどのフィールドにも足さない。
            var gear = MakeEquipment(2108);
            AddEquippedInstance(gear, new RolledStat(nameof(StatType.HealAmount), 40f));

            var bonus = _inv.GetEquippedBonus();

            Assert.AreEqual(0f, bonus.attackPct, 1e-3f);
            Assert.AreEqual(0f, bonus.defensePct, 1e-3f);
            Assert.AreEqual(0f, bonus.maxHpPct, 1e-3f);
            Assert.AreEqual(0f, bonus.criticalChance, 1e-3f);
            Assert.AreEqual(0f, bonus.criticalDamage, 1e-3f);
        }

        [Test]
        public void UnequippedInstance_IsNotCounted()
        {
            var gear = MakeEquipment(2109);
            _inv.AddInstance(gear, new[] { new RolledStat(nameof(StatType.AttackPct), 99f) });

            Assert.AreEqual(0f, _inv.GetEquippedBonus().attackPct, 1e-3f);
        }

        [Test]
        public void ThreeInstances_AreSummed()
        {
            for (int i = 0; i < 3; i++)
                AddEquippedInstance(
                    MakeEquipment(2110 + i),
                    new RolledStat(nameof(StatType.AttackPct), 5f)
                );

            Assert.AreEqual(15f, _inv.GetEquippedBonus().attackPct, 1e-3f);
        }

        // --- 縦串: 拾う → 装備する → 最終ステータスが上がる ---

        [Test]
        public void PickingUpAndEquipping_RaisesFinalStats()
        {
            var playerData = ScriptableObject.CreateInstance<PlayerParameterData>();
            playerData.baseAttackPower = 2000f; // spec §1 の素の値
            playerData.baseMaxLife = 1000f;
            playerData.baseDefense = 500f;
            playerData.baseCriticalChance = 0f;
            playerData.baseCriticalDamageRatio = 0f;
            _created.Add(playerData);

            var statusGo = new GameObject(nameof(PlayerStatus));
            try
            {
                var status = statusGo.AddComponent<PlayerStatus>();
                TestReflection.SetField(status, "_playerData", playerData);
                Assert.AreEqual(2000f, status.CurrentAttackPower, 1e-3f); // 前提: 補正なし

                // 拾得と同じ経路でロールした個体を在庫へ入れて装備する。
                var seed = MakeEquipment(2120, seedPower: 20);
                var rolled = CraftStatBridge.RollDrop(seed, new SystemRandomSource(5));
                AddEquippedInstance(seed, rolled.ToArray());

                status.SetEquipment(_inv.GetEquippedBonus());

                float total = rolled.Sum(r => r.value);
                Assert.Greater(total, 0f, "ロール結果が空だと検証にならない");
                Assert.Greater(
                    status.CurrentAttackPower
                        + status.CurrentDefense
                        + status.CurrentMaxHp
                        + status.CurrentCriticalChance
                        + status.CurrentCriticalDamageRatio,
                    2000f + 500f + 1000f,
                    "拾った装備品を装備しても最終ステータスが素の値から動いていない"
                );
            }
            finally
            {
                Object.DestroyImmediate(statusGo);
            }
        }
    }
}
