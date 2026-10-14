using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 剣・弓・鎌の固定ステータスがアセットに入っているかを固定する。
    /// 数値がコード外(アセット)にあると仕様からの乖離に気づけないため、ここで実アセットを読んで突き合わせる。
    /// </summary>
    public class WeaponDataAssetTests
    {
        private const string WeaponDir = "Assets/_Project/Features/Player/Data/Weapons";

        private static WeaponData Load(string fileName) =>
            AssetDatabase.LoadAssetAtPath<WeaponData>($"{WeaponDir}/{fileName}.asset");

        private static WeaponData LoadRequired(string fileName)
        {
            var w = Load(fileName);
            Assert.IsNotNull(w, $"{WeaponDir}/{fileName}.asset が見つからない");
            return w;
        }

        [Test]
        public void Sword_MatchesSpec()
        {
            var w = LoadRequired("Sword");

            Assert.AreEqual(1001, w.id);
            Assert.AreEqual(25, w.attack, "剣: 攻撃% +25%");
            Assert.AreEqual(50f, w.criticalDamage, 1e-3f, "剣: 会心ダメージ +50%");
            Assert.AreEqual(0f, w.criticalRate, 1e-3f);
        }

        [Test]
        public void Bow_MatchesSpec()
        {
            var w = LoadRequired("Bow");

            Assert.AreEqual(1002, w.id);
            Assert.AreEqual(25, w.attack, "弓: 攻撃% +25%");
            Assert.AreEqual(50f, w.criticalRate, 1e-3f, "弓: 会心率 +50%");
            Assert.AreEqual(0f, w.criticalDamage, 1e-3f);
        }

        [Test]
        public void Scythe_MatchesSpec()
        {
            var w = LoadRequired("Scythe");

            Assert.AreEqual(1003, w.id);
            Assert.AreEqual(0, w.attack);
            Assert.AreEqual(50f, w.criticalRate, 1e-3f, "鎌: 会心率 +50%");
            Assert.AreEqual(50f, w.criticalDamage, 1e-3f, "鎌: 会心ダメージ +50%");
        }

        [Test]
        public void Hammer_MatchesSpec()
        {
            var w = LoadRequired("Hammer");

            Assert.AreEqual(1004, w.id);
            Assert.AreEqual(0, w.attack);
            Assert.AreEqual(0f, w.criticalRate, 1e-3f);
            Assert.AreEqual(100f, w.criticalDamage, 1e-3f);
        }

        [Test]
        public void AllThreeWeapons_HaveEqualAverageDamage()
        {
            // 平均ダメージ = 攻撃力 ×(1 + 会心率 × 会心ダメージ)が3種とも等価になるよう設定。
            // 素の値(攻撃2000 / 会心率0 / 会心ダメージ0)でいずれも平均 2500 になる。
            const float baseAttack = 2000f;

            foreach (var name in new[] { "Sword", "Bow", "Scythe" })
            {
                var w = LoadRequired(name);
                float attack = baseAttack * (1f + w.attack / 100f);
                float average = attack * (1f + (w.criticalRate / 100f) * (w.criticalDamage / 100f));

                Assert.AreEqual(2500f, average, 1e-2f, $"{name} の平均ダメージが 2500 でない");
            }
        }

        [Test]
        public void Weapons_AreOutsideInventoryCatalog()
        {
            // 武器はインベントリ管理の対象外。giveItem のカタログに混ざらないことを守る。
            foreach (var name in new[] { "Sword", "Bow", "Scythe", "Hammer" })
            {
                var w = LoadRequired(name);
                Assert.IsNull(
                    ItemCatalog.Instance != null ? ItemCatalog.Instance.GetItemById(w.id) : null,
                    $"{name} が ItemCatalog に載っている(武器は在庫外)"
                );
            }
        }
    }
}
