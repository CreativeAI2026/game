using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.DevTools
{
    /// <summary>
    /// UI_CraftingPreview(調合UIの確認用シーン)専用のプレビュー駆動役。常駐の <see cref="UIManager"/> で起動時に調合UIを開く。
    /// このシーンにだけ置く(会話UI側の <c>ConversationPreviewDriver</c> と対)。
    /// </summary>
    public sealed class CraftPreviewDriver : MonoBehaviour
    {
        private IEnumerator Start()
        {
            // FieldDevBootstrap.Awake が UI を生成し、UIManager.Awake が CloseAll するまで1フレーム待つ。
            yield return null;

            var ui = UIManager.Instance;
            if (ui == null)
            {
                Debug.LogWarning(
                    "[CraftPreviewDriver] UIManager が見つかりません。"
                        + "FieldDevBootstrap と ResidentBootstrapConfig(uiRootPrefab) を確認してください。"
                );
                yield break;
            }

            ui.Open(UIManager.UIId.Craft);
        }
    }
}
