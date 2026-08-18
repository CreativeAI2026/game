using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// SaveService の保存→復元の往復と、セーブ可否のゲート(documents/Specification.md §0, §6)。
    /// SaveData の JSON 往復だけでなく「マネージャから取り込んで書き、読んで戻す」経路を通す。
    ///
    /// 実ファイル(persistentDataPath/save.json)を使うので、既存のセーブは退避して必ず戻す。
    /// </summary>
    public class SaveServiceTests
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private string _backup;
        private GameObject _pmGo;
        private GameObject _invGo;
        private GameObject _bookGo;
        private GameObject _gmmGo;
        private GameObject _playerGo;
        private ProgressManager _pm;
        private InventoryManager _inv;
        private RecipeBookManager _book;
        private GameModeManager _gmm;
        private WeaponManager _weapons;
        private PlayerParameterData _playerData;
        private readonly List<Object> _assets = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // 開発者の実セーブを壊さないよう退避しておく。
            _backup = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_backup != null)
                File.WriteAllText(SavePath, _backup);
            else if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        private T MakeAsset<T>()
            where T : ScriptableObject
        {
            var a = ScriptableObject.CreateInstance<T>();
            _assets.Add(a);
            return a;
        }

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            _pmGo = new GameObject("PM");
            _pm = _pmGo.AddComponent<ProgressManager>();
            TestReflection.SetStaticProperty("Instance", _pm);

            _invGo = new GameObject("INV");
            _inv = _invGo.AddComponent<InventoryManager>();
            TestReflection.SetStaticProperty("Instance", _inv);

            _bookGo = new GameObject("BOOK");
            _book = _bookGo.AddComponent<RecipeBookManager>();
            TestReflection.SetStaticProperty("Instance", _book);

            _gmmGo = new GameObject("GMM");
            _gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", _gmm);

            _playerData = MakeAsset<PlayerParameterData>();
            _playerData.baseMaxLife = 1000f;
            _playerData.baseAttackPower = 2000f;
            _playerData.baseDefense = 500f;
            _playerData.baseCriticalChance = 0f;
            _playerData.baseCriticalDamageRatio = 0f;

            _playerGo = new GameObject("Player") { tag = "Player" };
            var status = _playerGo.AddComponent<PlayerStatus>();
            TestReflection.SetField(status, "_playerData", _playerData);
            _weapons = _playerGo.AddComponent<WeaponManager>();
        }

        [TearDown]
        public void TearDown()
        {
            EventPlaybackService.SetPlaying(false);
            ItemDB.InjectForTests(null);
            TestReflection.SetStaticProperty<ProgressManager>("Instance", null);
            TestReflection.SetStaticProperty<InventoryManager>("Instance", null);
            TestReflection.SetStaticProperty<RecipeBookManager>("Instance", null);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_playerGo);
            Object.DestroyImmediate(_gmmGo);
            Object.DestroyImmediate(_bookGo);
            Object.DestroyImmediate(_invGo);
            Object.DestroyImmediate(_pmGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        private FoodData MakeFood(int id)
        {
            var f = MakeAsset<FoodData>();
            f.id = id;
            return f;
        }

        private EquipmentData MakeEquipment(int id)
        {
            var e = MakeAsset<EquipmentData>();
            e.id = id;
            return e;
        }

        // --- セーブ可否(spec §0: フィールド移動中のみ) ---

        [Test]
        public void Save_InBattleMode_IsBlocked()
        {
            _gmm.EnterBattle();
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("戦闘モード中")
            );

            SaveService.Save();

            Assert.IsFalse(SaveService.HasSave(), "戦闘中はセーブファイルを作らない");
        }

        [Test]
        public void Save_DuringEventPlayback_IsBlocked()
        {
            EventPlaybackService.SetPlaying(true);
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("会話イベント再生中")
            );

            SaveService.Save();

            Assert.IsFalse(SaveService.HasSave(), "会話UI表示中はセーブファイルを作らない");
        }

        [Test]
        public void Save_InFieldMode_Writes()
        {
            SaveService.Save();

            Assert.IsTrue(SaveService.HasSave());
        }

        [Test]
        public void Load_WithoutSaveFile_ReturnsNull()
        {
            Assert.IsNull(SaveService.Load());
        }

        // --- 保存 → 復元の往復 ---

        [Test]
        public void SaveThenLoad_RestoresProgressAndFlags()
        {
            _pm.LoadState(7, new Dictionary<string, string> { { "girl_choice", "together" } });

            SaveService.Save();
            _pm.LoadState(0, new Dictionary<string, string>()); // 別状態にしてから復元

            var data = SaveService.Load();

            Assert.IsNotNull(data);
            Assert.AreEqual(7, _pm.Progress);
            Assert.AreEqual("together", _pm.GetFlag("girl_choice"));
        }

        [Test]
        public void SaveThenLoad_RestoresItems_StacksInstancesEquippedAndQuickFood()
        {
            var apple = MakeFood(3001);
            var gear = MakeEquipment(2001);
            var rolledGear = MakeEquipment(2002);
            ItemDB.InjectForTests(new ItemData[] { apple, gear, rolledGear });

            _inv.AddItem(apple, 4);
            _inv.AddItem(gear, 1);
            _inv.AddInstance(rolledGear, new List<RolledStat> { new("attackPct", 12.5f) });
            var gearStack = _inv.GetAllItems().Find(s => s.Data == gear);
            var appleStack = _inv.GetAllItems().Find(s => s.Data == apple);
            _inv.SetEquipped(gearStack, true);
            _inv.SetQuickFood(1, appleStack);

            SaveService.Save();
            _inv.Clear();
            Assert.AreEqual(0, _inv.GetAllItems().Count); // 前提: 消えている

            SaveService.Load();

            var all = _inv.GetAllItems();
            Assert.AreEqual(3, all.Count);

            var restoredApple = all.Find(s => s.Data == apple);
            Assert.AreEqual(4, restoredApple.Count, "スタック数が戻る");
            Assert.AreSame(restoredApple, _inv.GetQuickFoodSlots()[1], "即時食材の枠も戻る");

            Assert.IsTrue(all.Find(s => s.Data == gear).IsEquipped, "装備状態が戻る");

            var restoredRolled = all.Find(s => s.Data == rolledGear);
            Assert.IsTrue(restoredRolled.IsInstance, "ロール済み個体は個体のまま戻る");
            Assert.AreEqual("attackPct", restoredRolled.RolledStats[0].stat);
            Assert.AreEqual(12.5f, restoredRolled.RolledStats[0].value, 1e-4f);
        }

        [Test]
        public void SaveThenLoad_RestoresRevealedRecipes()
        {
            var recipe = MakeAsset<CraftRecipeData>();
            // 実カタログの初期解禁(showInRecipeCraft)と id がぶつからないよう、テスト専用の id を使う。
            recipe.resultItem = MakeFood(990001);
            Assert.IsTrue(_book.Reveal(recipe)); // 前提: 新規解禁

            SaveService.Save();
            _book.RestoreRevealed(new List<int>()); // 解禁を落としてから復元
            Assert.IsFalse(_book.IsRevealed(recipe));

            SaveService.Load();

            Assert.IsTrue(_book.IsRevealed(recipe));
        }

        [Test]
        public void SaveThenRestorePlayerState_RestoresPositionHpAndWeapons()
        {
            var status = _playerGo.GetComponent<PlayerStatus>();
            status.RestoreHp(640f);
            _playerGo.transform.SetPositionAndRotation(
                new Vector3(1.5f, 2.5f, -3.5f),
                Quaternion.Euler(0f, 90f, 0f)
            );
            _weapons.GiveWeapon("sword");
            _weapons.GiveWeapon("scythe");
            _weapons.SelectNext(); // 鎌を選択

            SaveService.Save();

            // 位置・HP・武器を崩してから復元する。
            _playerGo.transform.position = Vector3.zero;
            status.RestoreHp(1f);
            _weapons.RestoreWeapons(new List<string>(), WeaponManager.NoWeapon);

            var data = SaveService.Load();
            SaveService.RestorePlayerState(data);

            Assert.AreEqual(new Vector3(1.5f, 2.5f, -3.5f), _playerGo.transform.position);
            Assert.AreEqual(90f, _playerGo.transform.eulerAngles.y, 1e-2f);
            Assert.AreEqual(640f, status.CurrentHp, 1e-2f);
            CollectionAssert.AreEqual(
                new[] { "sword", "scythe" },
                _weapons.CaptureOwnedWeaponKeys().ToList()
            );
            Assert.AreEqual(2, _weapons.CurrentWeaponIndex, "選択中の武器も戻る");
        }

        [Test]
        public void RestorePlayerState_WithoutPlayerState_DoesNothing()
        {
            var status = _playerGo.GetComponent<PlayerStatus>();
            status.RestoreHp(500f);

            SaveService.RestorePlayerState(new SaveData { hasPlayerState = false, currentHp = 1f });

            Assert.AreEqual(
                500f,
                status.CurrentHp,
                1e-2f,
                "旧セーブ(リグ状態なし)は復元をスキップ"
            );
        }

        [Test]
        public void Load_ItemMissingFromCatalog_IsSkippedWithWarning()
        {
            var apple = MakeFood(3001);
            ItemDB.InjectForTests(new ItemData[] { apple });
            _inv.AddItem(apple, 1);
            SaveService.Save();

            // カタログから消えた状態で復元する(アセット削除を模す)。
            ItemDB.InjectForTests(new ItemData[] { });
            _inv.Clear();
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("ItemDB に無し")
            );

            SaveService.Load();

            Assert.AreEqual(0, _inv.GetAllItems().Count);
        }
    }
}
