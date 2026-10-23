using System.Collections.Generic;
using CreativeAI.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// UIRoot Prefab の構成の検証。
    /// 直下は Canvas の入れ物で、中身は機能ごとの Prefab(Canvas を持たない)を入れる。
    /// 常駐UI(アイコンバー・即時食材バー・扉プロンプト・会話UI等)は表示/非表示を各自の CanvasGroup で切り替えるので、
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
                    $"UIRoot/{child.name} がオフで保存されています。常駐UIは CanvasGroup で出し分けるので、"
                        + "GameObject はオンのまま保存してください(オフだと実行時に表示されません)。"
                );
            }
        }

        [Test]
        public void DirectChildren_AreCanvasWrappers_WithContentPrefabsWithoutCanvas()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (Transform wrapper in root.transform)
                {
                    Assert.IsNotNull(
                        wrapper.GetComponent<Canvas>(),
                        $"UIRoot/{wrapper.name} に Canvas がありません。UIRoot 直下は Canvas の入れ物にしてください。"
                    );
                    foreach (Transform content in wrapper)
                    {
                        string path = $"UIRoot/{wrapper.name}/{content.name}";
                        Assert.IsTrue(
                            PrefabUtility.IsAnyPrefabInstanceRoot(content.gameObject),
                            $"{path} が Prefab ではありません。中身は機能ごとの Prefab にして入れてください。"
                        );
                        Assert.IsNull(
                            content.GetComponent<Canvas>(),
                            $"{path} が Canvas を持っています。Canvas は UIRoot 側の入れ物だけに置いてください。"
                        );
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
