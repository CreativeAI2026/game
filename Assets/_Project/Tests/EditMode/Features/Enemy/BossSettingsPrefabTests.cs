using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// ボスの Prefab に保存された行動ごとの設定値が、既定値に戻らずに読めることの確認。
    /// 設定値を行動ごとの Settings にまとめ直した時に、Prefab の値が失われていないかを見る。
    /// </summary>
    public sealed class BossSettingsPrefabTests
    {
        private const string PrefabFolder = "Assets/_Project/Features/Enemy/Prefabs/";

        private static T LoadController<T>(string prefabName)
            where T : Component
        {
            string path = PrefabFolder + prefabName;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var controller = prefab.GetComponentInChildren<T>(true);
            Assert.IsNotNull(controller, $"{path} に {typeof(T).Name} がありません");
            return controller;
        }

        [TestCase("TutorialBoss.prefab")]
        [TestCase("TutorialEnemy.prefab")]
        public void TutorialBoss_SettingsKeepPrefabValues(string prefabName)
        {
            var boss = LoadController<TutorialBossController>(prefabName);

            Assert.AreEqual(12f, boss.Movement.RunSpeed);
            Assert.AreEqual(5f, boss.Engagement.AttackInterval);
            Assert.AreEqual(12f, boss.Engagement.WatchDistanceMax);
            Assert.AreEqual(5f, boss.NormalAttack.Range);
            Assert.AreEqual(10f, boss.NormalAttack2.HomingRotateSpeed);
            Assert.AreEqual(0.25f, boss.NormalAttack2.Chance);
            Assert.AreEqual(15f, boss.SpecialAttack.Range);
            Assert.AreEqual(0.1f, boss.SpecialAttack.Chance);
            Assert.AreEqual(0.5f, boss.Grab.PullLateralOffset);
        }

        [Test]
        public void KurageBoss_SettingsKeepPrefabValues()
        {
            var boss = LoadController<KurageBossController>("KurageBoss.prefab");

            Assert.AreEqual(20f, boss.Orbit.HighSpeed);
            Assert.AreEqual(240f, boss.Stab.TurnSpeed);
        }
    }
}
