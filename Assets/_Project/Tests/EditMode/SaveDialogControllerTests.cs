using System.IO;
using System.Text.RegularExpressions;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// セーブ確認ダイアログの「はい」でセーブして閉じ、「いいえ」ではセーブせずに閉じるかの検証。
    /// </summary>
    public class SaveDialogControllerTests
    {
        private string _saveDir;
        private string SavePath => Path.Combine(_saveDir, "save.json");
        private GameObject _root;
        private readonly TestResidents _residents = new();
        private Button _yes;
        private Button _no;

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

        [SetUp]
        public void SetUp()
        {
            foreach (var path in Directory.GetFiles(_saveDir, "save.json*"))
                File.Delete(path);

            _root = new GameObject("SaveDialog");
            _yes = new GameObject("Yes").AddComponent<Button>();
            _yes.transform.SetParent(_root.transform);
            _no = new GameObject("No").AddComponent<Button>();
            _no.transform.SetParent(_root.transform);

            var controller = _root.AddComponent<SaveDialogController>();
            TestReflection.SetField(controller, "_yesButton", _yes);
            TestReflection.SetField(controller, "_noButton", _no);
            // EditMode では Awake が走らないので、ボタンの配線を手動で行う。
            TestReflection.Invoke(controller, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            _residents.Clear();
            Object.DestroyImmediate(_root);
            foreach (var path in Directory.GetFiles(_saveDir, "save.json*"))
                File.Delete(path);
        }

        [Test]
        public void Yes_SavesAndClosesDialog()
        {
            _yes.onClick.Invoke();

            Assert.IsTrue(SaveService.HasSave());
            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void No_ClosesDialogWithoutSaving()
        {
            _no.onClick.Invoke();

            Assert.IsFalse(SaveService.HasSave());
            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void Yes_InBattleMode_DoesNotWriteSaveFile_ButStillClosesDialog()
        {
            var gmm = _residents.Add<GameModeManager>();
            gmm.EnterBattle();
            LogAssert.Expect(LogType.Warning, new Regex("戦闘モード中"));

            _yes.onClick.Invoke();

            Assert.IsFalse(SaveService.HasSave(), "戦闘中はセーブファイルを作らない");
            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void Reopened_SavesAgainOnEachYes()
        {
            _yes.onClick.Invoke();
            File.Delete(SavePath);

            _root.GetComponent<SaveDialogController>().Open();
            _yes.onClick.Invoke();

            Assert.IsTrue(SaveService.HasSave());
        }
    }
}
