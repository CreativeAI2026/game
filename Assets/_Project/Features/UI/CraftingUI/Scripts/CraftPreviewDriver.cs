using System.Collections;
using UnityEngine;

namespace CreativeAI.UI.CraftingUI
{
    /// <summary>
    /// UI_CraftingPreview(調合UIの確認用シーン)専用のプレビュー駆動役。常駐 <see cref="UIRoot"/> 内の <see cref="UiRouter"/> を叩き、
    /// 起動時に調合UIを開く。このシーンにだけ置く(会話UI側の <c>ConversationPreviewDriver</c> と対)。
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
                    : Object.FindAnyObjectByType<UiRouter>(FindObjectsInactive.Exclude);
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
