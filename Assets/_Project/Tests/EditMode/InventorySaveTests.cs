using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 個体ステータス付きアイテム(ItemStack)とセーブDTOの往復・進行度復元の検証。
    /// EditMode では Awake が走らないため Instance/DontDestroyOnLoad には依存しない。
    /// </summary>
    public class InventorySaveTests
    {
        private GameObject _go;
        private InventoryManager _inv;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject(nameof(InventoryManager));
            _inv = _go.AddComponent<InventoryManager>();
            // Awake は EditMode で走らないためテスト品は入らない(_items は空から始まる)。
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private static ItemData MakeItem(int id, ItemCategory category = ItemCategory.Food)
        {
            var d = ScriptableObject.CreateInstance<ItemData>();
            d.id = id;
            d.category = category;
            return d;
        }

        private static FoodData MakeFood(int id)
        {
            var d = ScriptableObject.CreateInstance<FoodData>(); // OnEnable が category=Food を設定
            d.id = id;
            return d;
        }

        [Test]
        public void AddFood_SameData_StacksIntoOne()
        {
            // 食材はカテゴリのルールで必ずスタックする(FoodData.MaxStack)。同じ食材を足すと1枠に集約される。
            var apple = MakeFood(1);
            Assert.GreaterOrEqual(
                apple.MaxStack,
                5,
                "食材はスタック可能であること(FoodData ルール)"
            );

            _inv.AddItem(apple, 3);
            _inv.AddItem(apple, 2);

            var all = _inv.GetAllItems();
            Assert.AreEqual(1, all.Count); // りんご5個 = 1スタック
            Assert.AreEqual(5, all[0].Count); // 中身は 3+2=5 個
            Assert.IsFalse(all[0].IsInstance); // 個体差ロールなしの通常スタック
        }

        [Test]
        public void AddInstance_SameData_DoesNotMerge()
        {
            var sword = MakeItem(10, ItemCategory.Equipment);

            _inv.AddInstance(sword, new List<RolledStat> { new("attackPct", 12f) });
            _inv.AddInstance(sword, new List<RolledStat> { new("attackPct", 30f) });

            var all = _inv.GetAllItems();
            Assert.AreEqual(2, all.Count); // 個体差があるので別スタック
            Assert.IsTrue(all[0].IsInstance);
            Assert.AreEqual(12f, all[0].RolledStats[0].value);
            Assert.AreEqual(30f, all[1].RolledStats[0].value);
        }

        [Test]
        public void AddItem_DoesNotMergeIntoInstance()
        {
            var gear = MakeItem(20, ItemCategory.Equipment);
            _inv.AddInstance(gear, new List<RolledStat> { new("defensePct", 5f) });

            _inv.AddItem(gear, 1); // スタック品は個体に合流しない

            var all = _inv.GetAllItems();
            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void Clear_EmptiesInventory()
        {
            _inv.AddItem(MakeItem(1), 1);
            _inv.Clear();
            Assert.AreEqual(0, _inv.GetAllItems().Count);
        }

        [Test]
        public void GetEquippedBonus_SumsEquippedOnly()
        {
            var gear = ScriptableObject.CreateInstance<EquipmentData>();
            gear.attack = 10;
            gear.defense = 5;
            gear.maxHP = 100;
            gear.criticalRate = 3f;
            gear.criticalDamage = 0.5f;

            // 武器は在庫外(仕様 L30)。仮に在庫へ入れて装備フラグを立てても、
            // GetEquippedBonus は武器を加算しない(補正は WeaponManager 経由に一本化)。
            var weapon = ScriptableObject.CreateInstance<WeaponData>();
            weapon.attack = 20;
            weapon.maxHP = 50;

            var unequipped = ScriptableObject.CreateInstance<EquipmentData>();
            unequipped.attack = 999;

            _inv.AddItem(gear, 1);
            _inv.AddItem(weapon, 1);
            _inv.AddItem(unequipped, 1);
            var stacks = _inv.GetAllItems();
            _inv.SetEquipped(stacks.Find(s => s.Data == gear), true);
            _inv.SetEquipped(stacks.Find(s => s.Data == weapon), true);
            // unequipped は装備しない

            var b = _inv.GetEquippedBonus();

            Assert.AreEqual(10f, b.attackPct); // gear のみ(武器20・unequipped999 は除外)
            Assert.AreEqual(5f, b.defensePct);
            Assert.AreEqual(100f, b.maxHpPct); // gear のみ(武器50 は除外)
            Assert.AreEqual(3f, b.criticalChance);
            Assert.AreEqual(0.5f, b.criticalDamage);
        }

        [Test]
        public void SetQuickFood_SetsSlot_AndRejectsNonFood()
        {
            var apple = MakeFood(1);
            var gear = MakeItem(20, ItemCategory.Equipment);
            _inv.AddItem(apple, 1);
            _inv.AddItem(gear, 1);
            var appleStack = _inv.GetAllItems().Find(s => s.Data == apple);
            var gearStack = _inv.GetAllItems().Find(s => s.Data == gear);

            Assert.IsTrue(_inv.SetQuickFood(0, appleStack));
            Assert.IsFalse(_inv.SetQuickFood(1, gearStack)); // 食材以外は不可
            Assert.IsFalse(_inv.SetQuickFood(9, appleStack)); // 範囲外は不可

            var slots = _inv.GetQuickFoodSlots();
            Assert.AreEqual(3, slots.Count);
            Assert.AreSame(appleStack, slots[0]);
            Assert.IsNull(slots[1]);
        }

        [Test]
        public void SetQuickFood_SameStackTwice_MovesInsteadOfDuplicating()
        {
            var apple = MakeFood(1);
            _inv.AddItem(apple, 5);
            var appleStack = _inv.GetAllItems().Find(s => s.Data == apple);

            _inv.SetQuickFood(0, appleStack);
            _inv.SetQuickFood(2, appleStack); // 別スロットへ移動(重複しない)

            var slots = _inv.GetQuickFoodSlots();
            Assert.IsNull(slots[0]);
            Assert.AreSame(appleStack, slots[2]);
        }

        [Test]
        public void ConsumingSetFood_ToZero_AutoClearsSlot()
        {
            var apple = MakeFood(1);
            _inv.AddItem(apple, 1);
            var appleStack = _inv.GetAllItems().Find(s => s.Data == apple);
            _inv.SetQuickFood(0, appleStack);

            _inv.RemoveItem(apple, 1); // 使い切り → スタックが在庫から消える

            Assert.IsNull(_inv.GetQuickFoodSlots()[0]); // スロットも自動でクリア
        }

        [Test]
        public void Clear_AlsoClearsQuickFoodSlots()
        {
            var apple = MakeFood(1);
            _inv.AddItem(apple, 1);
            _inv.SetQuickFood(0, _inv.GetAllItems().Find(s => s.Data == apple));

            _inv.Clear();

            Assert.IsNull(_inv.GetQuickFoodSlots()[0]);
        }

        [Test]
        public void SaveData_JsonRoundTrip_PreservesQuickFoodSlot()
        {
            var data = new SaveData();
            data.items.Add(
                new ItemEntry
                {
                    itemId = 1,
                    count = 2,
                    inQuickFood = true,
                    quickFoodSlot = 2,
                }
            );

            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.IsTrue(back.items[0].inQuickFood);
            Assert.AreEqual(2, back.items[0].quickFoodSlot);
        }

        [Test]
        public void SaveData_LegacyEntry_DefaultsToNotInQuickFood()
        {
            // inQuickFood を持たない旧 JSON。既定 false(未セット)で復元され、slot=0 と誤判定しない。
            var back = JsonUtility.FromJson<SaveData>(
                "{\"items\":[{\"itemId\":1,\"count\":2,\"equipped\":false}]}"
            );

            Assert.IsFalse(back.items[0].inQuickFood);
        }

        [Test]
        public void SaveData_JsonRoundTrip_PreservesFields()
        {
            var data = new SaveData { progress = 5 };
            data.flags.Add(new FlagEntry { key = "girl_choice", value = "together" });
            data.items.Add(
                new ItemEntry
                {
                    itemId = 3,
                    count = 2,
                    equipped = true,
                    rolledStats = new List<RolledStat> { new("attackPct", 1.5f) },
                }
            );

            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.AreEqual(5, back.progress);
            Assert.AreEqual("girl_choice", back.flags[0].key);
            Assert.AreEqual("together", back.flags[0].value);
            Assert.AreEqual(3, back.items[0].itemId);
            Assert.AreEqual(2, back.items[0].count);
            Assert.IsTrue(back.items[0].equipped);
            Assert.AreEqual("attackPct", back.items[0].rolledStats[0].stat);
            Assert.AreEqual(1.5f, back.items[0].rolledStats[0].value);
        }

        [Test]
        public void SaveData_JsonRoundTrip_PreservesPlayerState()
        {
            var data = new SaveData
            {
                hasPlayerState = true,
                sceneName = "Field_Area03",
                posX = 1.5f,
                posY = 2.5f,
                posZ = -3.5f,
                rotationY = 90f,
                currentHp = 42f,
            };

            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.IsTrue(back.hasPlayerState);
            Assert.AreEqual("Field_Area03", back.sceneName);
            Assert.AreEqual(1.5f, back.posX);
            Assert.AreEqual(2.5f, back.posY);
            Assert.AreEqual(-3.5f, back.posZ);
            Assert.AreEqual(90f, back.rotationY);
            Assert.AreEqual(42f, back.currentHp);
        }

        [Test]
        public void SaveData_PlayerState_DefaultsToDisabled()
        {
            // 旧セーブ(プレイヤー状態なし)を模した JSON。hasPlayerState が既定 false で復元スキップされる。
            var back = JsonUtility.FromJson<SaveData>("{\"progress\":3}");

            Assert.AreEqual(3, back.progress);
            Assert.IsFalse(back.hasPlayerState);
            Assert.AreEqual(string.Empty, back.sceneName ?? string.Empty);
        }

        [Test]
        public void ProgressManager_LoadState_RestoresProgressAndFlags()
        {
            var pmGo = new GameObject(nameof(ProgressManager));
            var pm = pmGo.AddComponent<ProgressManager>();
            try
            {
                pm.LoadState(7, new Dictionary<string, string> { { "girl_choice", "together" } });

                Assert.AreEqual(7, pm.Progress);
                Assert.AreEqual("together", pm.GetFlag("girl_choice"));
                Assert.AreEqual(1, pm.Flags.Count);
            }
            finally
            {
                Object.DestroyImmediate(pmGo);
            }
        }
    }
}
