using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>
    /// UI_ConversationPreview(会話UIの確認用シーン)専用のプレビュー駆動役。EventPlayer / セッション常駐を
    /// 立ち上げずに、シーン内の <see cref="ConversationView"/> を直接叩いてサンプル会話を再生する。
    /// Prefab には含めず UI_ConversationPreview シーンにだけ置く(本番フローは EventPlayer が seam 経由で叩く)。
    /// 会話の終盤で「りんごを渡す」一幕(<see cref="ConversationView.ShowItemGet"/> でダミー画像を表示)も再生する。
    /// </summary>
    public sealed class ConversationPreviewDriver : MonoBehaviour
    {
        [SerializeField]
        private ConversationView _view; // 未割当なら DialogueViewService.Current にフォールバック

        [SerializeField]
        private bool _loop = true; // プレビューを繰り返す

        private IEnumerator Start()
        {
            IDialogueView view = _view != null ? _view : DialogueViewService.Current;
            if (view == null)
            {
                Debug.LogWarning(
                    "[ConversationPreviewDriver] ConversationView が見つかりません。シーンに配置してください。"
                );
                yield break;
            }

            do
            {
                yield return view.ShowLine(
                    "冒険者",
                    "dummy",
                    "やあ。これは会話UIのプレビューだ。クリックかスペース/Enter/Zキーで先へ送れる。"
                );
                yield return view.ShowLine(
                    "冒険者",
                    "dummy",
                    "テキストは1文字ずつ表示される。送出中にもう一度送ると全文が即表示だ。"
                );

                string picked = null;
                yield return view.ShowChoice(
                    new List<ChoiceOption>
                    {
                        new("もう一度見る", "again"),
                        new("いい感じだね", "good"),
                    },
                    v => picked = v
                );

                yield return view.ShowLine(
                    "冒険者",
                    "dummy",
                    picked == "good"
                        ? "ありがとう。実装に組み込んでいこう。"
                        : "了解、もう一度どうぞ。"
                );

                // --- 受け渡しデモ: アイテム画像 → 武器3Dモデル(いずれも ConversationView が実行時生成) ---
                if (view is ConversationView conversationView)
                {
                    yield return view.ShowLine(
                        "冒険者",
                        "dummy",
                        "そうだ、これを受け取ってくれ。りんごだ。"
                    );
                    yield return conversationView.ShowItemGet();

                    yield return view.ShowLine(
                        "冒険者",
                        "dummy",
                        "それと、この刀も。旅の護りにな。"
                    );
                    yield return conversationView.ShowWeaponGet();
                }
            } while (_loop);
        }
    }
}
