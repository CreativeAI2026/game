using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 剣・弓・鎌・ハンマーのアセットが守るべき決まりの検証(ID の並び・平均ダメージの釣り合い・在庫外)。
    /// 個々の数値はバランス調整で変わるので、ここでは固定しない。
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

        private static readonly string[] Names = { "Sword", "Bow", "Scythe", "Hammer" };

        [Test]
        public void Ids_FollowWeaponOrder()
        {
            // 武器の ID は 1001 + 並び順(剣・弓・鎌・ハンマー)。所持スロットの番号から ID を引くコードがある
            for (int i = 0; i < Names.Length; i++)
                Assert.AreEqual(1001 + i, LoadRequired(Names[i]).id, Names[i]);
        }

        [Test]
        public void AllWeapons_HaveEqualAverageDamage()
        {
            // 平均ダメージ倍率 =(1 + 攻撃%)×(1 + 会心率 × 会心ダメージ)が4種とも等しくなるよう調整している。
            // 個々の数値は調整で変わるので固定せず、釣り合っていることだけを見る
            float Average(WeaponData w) =>
                (1f + w.attack / 100f) * (1f + (w.criticalRate / 100f) * (w.criticalDamage / 100f));

            float expected = Average(LoadRequired(Names[0]));
            foreach (var name in Names)
                Assert.AreEqual(
                    expected,
                    Average(LoadRequired(name)),
                    1e-4f,
                    $"{name} の平均ダメージが {Names[0]} と釣り合っていない"
                );
        }

        [Test]
        public void Weapons_AreOutsideInventoryCatalog()
        {
            // 武器はインベントリ管理の対象外。giveItem のカタログに混ざらないことを守る。
            foreach (var name in Names)
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
