using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 剣・弓・鎌の固定ステータス(documents/Specification.md §1.1 の表)がアセットに入っているかを固定する。
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

            Assert.AreEqual("sword", w.key);
            Assert.AreEqual(25, w.attack, "剣: 攻撃% +25%");
            Assert.AreEqual(50f, w.criticalDamage, 1e-3f, "剣: 会心ダメージ +50%");
            Assert.AreEqual(0f, w.criticalRate, 1e-3f);
            Assert.AreEqual(0, w.defense);
            Assert.AreEqual(0, w.maxHP);
        }

        [Test]
        public void Bow_MatchesSpec()
        {
            var w = LoadRequired("Bow");

            Assert.AreEqual("bow", w.key);
            Assert.AreEqual(25, w.attack, "弓: 攻撃% +25%");
            Assert.AreEqual(50f, w.criticalRate, 1e-3f, "弓: 会心率 +50%");
            Assert.AreEqual(0f, w.criticalDamage, 1e-3f);
            Assert.AreEqual(0, w.defense);
            Assert.AreEqual(0, w.maxHP);
        }

        [Test]
        public void Scythe_MatchesSpec()
        {
            var w = LoadRequired("Scythe");

            Assert.AreEqual("scythe", w.key);
            Assert.AreEqual(0, w.attack);
            Assert.AreEqual(50f, w.criticalRate, 1e-3f, "鎌: 会心率 +50%");
            Assert.AreEqual(50f, w.criticalDamage, 1e-3f, "鎌: 会心ダメージ +50%");
            Assert.AreEqual(0, w.defense);
            Assert.AreEqual(0, w.maxHP);
        }

        [Test]
        public void AllThreeWeapons_HaveEqualAverageDamage()
        {
            // spec §1.1 の注: 平均ダメージ = 攻撃力 ×(1 + 会心率 × 会心ダメージ)が3種とも等価になるよう設定。
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
            // spec §2: 武器はインベントリ管理の対象外。ItemDB は Inventory/Data フォルダを同期するので、
            // 武器アセットがそこに置かれていないこと(= giveItem のカタログに混ざらないこと)を守る。
            Assert.IsFalse(
                WeaponDir.StartsWith("Assets/_Project/Features/Inventory/Data"),
                "武器アセットを Inventory/Data に置くと ItemDB / giveItem カタログに混入する"
            );

            foreach (var name in new[] { "Sword", "Bow", "Scythe" })
            {
                var w = LoadRequired(name);
                Assert.IsNull(
                    ItemDB.Instance != null ? ItemDB.Instance.GetItemByKey(w.key) : null,
                    $"{name} が ItemDB に載っている(武器は在庫外)"
                );
            }
        }
    }
}
