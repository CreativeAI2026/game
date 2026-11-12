using CreativeAI.Gameplay;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine.UI;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// キャラクター画面で武器を選んだときの表示の検証。
    /// 選んだ武器の名前・ステータス・3Dプレビューに切り替わり、武器切替バーの他の武器が暗くなる。
    /// 単体の CharacterPanel と、UIRoot に組み込まれた状態の両方で見る。
    /// </summary>
    public class CharacterWeaponSelectorTests
    {
        [TestCase("Character/Prefabs/CharacterPanel.prefab")]
        [TestCase("Root/Prefabs/UIRoot.prefab")]
        public void HammerSelection_UpdatesDetailAndPreview_AndDimsOtherWeapons(string path)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/Features/UI/" + path);
            try
            {
                var selector = root.GetComponentInChildren<CharacterWeaponSelector>(true);
                selector.Select(3);
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(
                    "Assets/_Project/Features/Player/Data/Weapons/Hammer.asset"
                );
                Assert.AreEqual(
                    data.itemName,
                    selector.transform.Find("ViewArea/Name").GetComponent<TMP_Text>().text
                );
                Assert.AreSame(data, selector.SelectedWeapon);
                var preview = selector.transform.Find("ViewArea/WeaponView/WeaponPreview");
                Assert.False(
                    preview.GetComponent<Image>().enabled,
                    "武器の表示は2Dスプライトではなく3Dプレビューで行う"
                );
                Assert.NotNull(preview.GetComponentInChildren<ModelPreview>(true));
                Assert.NotNull(preview.GetComponentInChildren<RawImage>(true));
                Assert.AreEqual(
                    "+100%",
                    selector
                        .transform.Find("WeaponStatusArea/CRTD/TextArea/ValueText")
                        .GetComponent<TMP_Text>()
                        .text
                );
                var bar = selector.transform.parent.Find("WeaponChangeBar");
                Assert.Less(
                    bar.Find("BOW/WeaponIcon").GetComponent<Image>().color.r,
                    bar.Find("HAMMER/WeaponIcon").GetComponent<Image>().color.r
                );
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
