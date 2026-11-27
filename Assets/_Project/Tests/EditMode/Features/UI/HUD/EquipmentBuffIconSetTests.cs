using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 装備で付く能力値には、どれもバフのアイコンが登録されていることの検証。
    /// 登録が抜けると、その効果が付いていても HUD に出ない。
    /// </summary>
    public class EquipmentBuffIconSetTests
    {
        [Test]
        public void EveryEquipmentStat_HasAnIcon()
        {
            var set = AssetDatabase.LoadAssetAtPath<EquipmentBuffIconSet>(
                "Assets/_Project/Features/UI/HUD/Data/EquipmentBuffIconSet.asset"
            );
            Assert.IsNotNull(set);

            foreach (StatType stat in ItemStatTextFormatter.EquipmentStats)
            {
                var entry = set.Entries.FirstOrDefault(e => e.effect == stat);
                Assert.IsNotNull(entry, $"{stat} のアイコンが登録されていない");
                Assert.IsNotNull(entry.icon, $"{stat} のアイコン画像が未設定");
            }
        }
    }
}
