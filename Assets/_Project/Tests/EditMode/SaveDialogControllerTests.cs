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
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        private string _backup;
        private GameObject _root;
        private GameObject _gmmGo;
        private Button _yes;
        private Button _no;

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

        [SetUp]
        public void SetUp()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

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
            EventPlaybackService.SetPlaying(false);
            TestReflection.SetStaticProperty<GameModeManager>("Instance", null);
            Object.DestroyImmediate(_gmmGo);
            Object.DestroyImmediate(_root);
            if (File.Exists(SavePath))
                File.Delete(SavePath);
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
            _gmmGo = new GameObject("GMM");
            var gmm = _gmmGo.AddComponent<GameModeManager>();
            TestReflection.SetStaticProperty("Instance", gmm);
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
