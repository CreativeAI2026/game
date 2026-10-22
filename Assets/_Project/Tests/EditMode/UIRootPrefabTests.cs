using System.Collections.Generic;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// UIRoot Prefab 直下の常駐UIがオフのまま保存されていないかの検証。
    /// 常駐UI(アイコンバー・即時食材バー・扉プロンプト・会話UI等)は表示/非表示を各自の Canvas.enabled で切り替えるので、
    /// GameObject がオフで保存されると実行時に二度と出ない。
    /// 開閉を UiRouter が SetActive で行うパネル(キャラ/インベ/セーブ/調合)だけはオフで保存されていてよい。
    /// </summary>
    public class UIRootPrefabTests
    {
        private const string PrefabPath = "Assets/_Project/Features/UI/Root/Prefabs/UIRoot.prefab";

        private static readonly string[] RouterPanelFields =
        {
            "_characterUI",
            "_inventoryUI",
            "_saveUI",
            "_craftUI",
        };

        [Test]
        public void DirectChildren_ExceptRouterPanels_AreSavedActive()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            var router = prefab.GetComponent<UiRouter>();
            Assert.IsNotNull(router, "UIRoot に UiRouter がありません。");

            var panels = new HashSet<GameObject>();
            foreach (var field in RouterPanelFields)
            {
                var panel = TestReflection.GetField<GameObject>(router, field);
                if (panel != null)
                    panels.Add(panel);
            }

            foreach (Transform child in prefab.transform)
            {
                if (panels.Contains(child.gameObject))
                    continue;
                Assert.IsTrue(
                    child.gameObject.activeSelf,
                    $"UIRoot/{child.name} がオフで保存されています。常駐UIは Canvas.enabled で出し分けるので、"
                        + "GameObject はオンのまま保存してください(オフだと実行時に表示されません)。"
                );
            }
        }
    }
}
