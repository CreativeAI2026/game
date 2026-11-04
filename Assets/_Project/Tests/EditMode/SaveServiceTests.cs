using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// SaveService の保存→復元の往復(マネージャから取り込み→書き→読み→戻す)と、セーブ可否のゲートの検証。
    /// 保存先は一時フォルダに差し替え、開発者の手元のセーブには触れない。
    /// </summary>
    public class SaveServiceTests
    {
        private string _saveDir;
        private string SavePath => Path.Combine(_saveDir, "save.json");
        private readonly TestResidents _residents = new();
        private GameObject _playerGo;
        private ProgressManager _pm;
        private InventoryManager _inv;
        private GameModeManager _gmm;
        private WeaponManager _weapons;
        private PlayerParameterData _playerData;
        private readonly List<Object> _assets = new();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _saveDir = Path.Combine(
                Path.GetTempPath(),
                "CreativeAI-SaveTests-" + Path.GetRandomFileName()
            );
            Directory.CreateDirectory(_saveDir);
            SaveService.OverrideDirectoryForTests(_saveDir);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            SaveService.OverrideDirectoryForTests(null);
            Directory.Delete(_saveDir, true);
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
            PickedItemRegistry.Clear();

            _pm = _residents.Add<ProgressManager>();
            _inv = _residents.Add<InventoryManager>();
            _gmm = _residents.Add<GameModeManager>();

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
            ItemCatalog.InjectForTests(null);
            _residents.Clear();
            Object.DestroyImmediate(_playerGo);
            foreach (var a in _assets)
                Object.DestroyImmediate(a);
            _assets.Clear();
            PickedItemRegistry.Clear();
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        private FoodData MakeFood(int id)
        {
            var f = MakeAsset<FoodData>();
            f.id = id;
            return f;
        }

        private ItemData MakeEquipment(int id)
        {
            var e = MakeAsset<ItemData>();
            e.category = ItemCategory.Equipment;
            e.id = id;
            return e;
        }

        // --- セーブ可否(フィールド移動中のみ) ---

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
            ItemCatalog.InjectForTests(new ItemData[] { apple, gear, rolledGear });

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
            ItemCatalog.InjectForTests(new ItemData[] { apple });
            _inv.AddItem(apple, 1);
            SaveService.Save();

            // カタログから消えた状態で復元する(アセット削除を模す)。
            ItemCatalog.InjectForTests(new ItemData[] { });
            _inv.Clear();
            LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("ItemCatalog に無し")
            );

            SaveService.Load();

            Assert.AreEqual(0, _inv.GetAllItems().Count);
        }

        // --- フィールドで拾ったアイテムの記録 ---

        private readonly List<GameObject> _pickupGos = new();

        /// <summary>フィールドに置かれた拾えるアイテム(指定の識別子つき)を作る。</summary>
        private FieldItemPickup PlacePickup(string id, ItemData item)
        {
            var go = new GameObject("Pickup_" + id, typeof(BoxCollider));
            _pickupGos.Add(go);
            var pickup = go.AddComponent<FieldItemPickup>();
            TestReflection.SetField(pickup, "_pickupId", id);
            TestReflection.SetField(pickup, "_item", item);
            return pickup;
        }

        [TearDown]
        public void DestroyPickups()
        {
            foreach (var go in _pickupGos)
                if (go != null)
                    Object.DestroyImmediate(go);
            _pickupGos.Clear();
        }

        [Test]
        public void PickThenSaveThenLoad_PickedItemIsNotPlacedAgain()
        {
            var apple = MakeFood(3001);
            ItemCatalog.InjectForTests(new ItemData[] { apple });
            Assert.IsTrue(PlacePickup("Area/apple", apple).TryPickup());
            SaveService.Save();

            // タイトルから「続きから」で入り直す: 記録は空に戻っているが、ロードで復元される。
            PickedItemRegistry.Clear();
            SaveService.Load();

            var again = PlacePickup("Area/apple", apple);
            again.HideIfAlreadyPicked();
            Assert.IsFalse(again.gameObject.activeSelf);
        }

        [Test]
        public void PickAfterSave_ComesBackOnLoad()
        {
            var apple = MakeFood(3001);
            var pear = MakeFood(3002);
            ItemCatalog.InjectForTests(new ItemData[] { apple, pear });
            Assert.IsTrue(PlacePickup("Area/apple", apple).TryPickup());
            SaveService.Save();

            // セーブより後に拾ったものは、ロードで所持品から消えるので、フィールドにまた置かれる。
            Assert.IsTrue(PlacePickup("Area/pear", pear).TryPickup());
            SaveService.Load();

            var appleAgain = PlacePickup("Area/apple", apple);
            var pearAgain = PlacePickup("Area/pear", pear);
            appleAgain.HideIfAlreadyPicked();
            pearAgain.HideIfAlreadyPicked();
            Assert.IsFalse(appleAgain.gameObject.activeSelf, "セーブ前に拾ったものは出ない");
            Assert.IsTrue(pearAgain.gameObject.activeSelf, "セーブ後に拾ったものは戻る");
            Assert.AreEqual(1, _inv.GetItemCount(apple));
            Assert.AreEqual(0, _inv.GetItemCount(pear));
        }

        [Test]
        public void Load_OldSaveWithoutPickedItems_PlacesEverything()
        {
            var apple = MakeFood(3001);
            ItemCatalog.InjectForTests(new ItemData[] { apple });
            Assert.IsTrue(PlacePickup("Area/apple", apple).TryPickup());
            SaveService.Save();

            // 拾い済みの項目が無い旧セーブを模す。
            var json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            data.pickedItemIds = null;
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));

            SaveService.Load();

            var again = PlacePickup("Area/apple", apple);
            again.HideIfAlreadyPicked();
            Assert.IsTrue(again.gameObject.activeSelf);
        }

        [Test]
        public void NewGame_ClearingTheRecord_PlacesEverythingAgain()
        {
            var apple = MakeFood(3001);
            Assert.IsTrue(PlacePickup("Area/apple", apple).TryPickup());

            PickedItemRegistry.Clear(); // 「はじめから」(TitleUIController.OnTapToStart)

            var again = PlacePickup("Area/apple", apple);
            again.HideIfAlreadyPicked();
            Assert.IsTrue(again.gameObject.activeSelf);
        }

        [Test]
        public void PickupWithoutId_IsNotRecorded()
        {
            var apple = MakeFood(3001);
            Assert.IsTrue(PlacePickup("", apple).TryPickup());

            Assert.IsEmpty(PickedItemRegistry.Snapshot());
        }
    }
}
