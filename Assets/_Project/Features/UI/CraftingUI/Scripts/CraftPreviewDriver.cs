using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    /// <summary>
    /// UI_CraftingPreview(調合UIの確認用シーン)専用のプレビュー駆動役。
    /// <see cref="FieldDevBootstrap"/> が生成した常駐 <see cref="UIRoot"/> 内の <see cref="UiRouter"/> を叩き、
    /// 起動時に調合UIを開いて見た目・調合フローを確認できるようにする。
    /// Prefab には含めず UI_CraftingPreview シーンにだけ置く(本番は調合場所のトリガーが開く想定)。
    /// 会話UI確認シーン UI_ConversationPreview の <c>ConversationPreviewDriver</c> と対になる。
    /// </summary>
    public sealed class CraftPreviewDriver : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // FieldDevBootstrap.Awake が UIRoot を生成し、UiRouter.Awake が CloseAll するまで1フレーム待つ。
            yield return null;

            var router =
                UIRoot.Instance != null
                    ? UIRoot.Instance.GetComponentInChildren<UiRouter>(true)
                    : Object.FindAnyObjectByType<UiRouter>();
            if (router == null)
            {
                Debug.LogWarning(
                    "[CraftPreviewDriver] UiRouter が見つかりません。"
                        + "FieldDevBootstrap と ResidentBootstrapConfig(uiRootPrefab) を確認してください。"
                );
                yield break;
            }

            router.Open(UiRouter.UiId.Craft);
        }
    }
}
