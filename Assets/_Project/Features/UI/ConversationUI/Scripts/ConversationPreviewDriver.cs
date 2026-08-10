using System;
using System.Collections;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>
    /// UI_ConversationPreview 専用のプレビュー駆動役。
    /// 本番のシナリオデータとは分離しつつ、発話・分岐・受け渡しの見た目を一連の流れで確認する。
    /// </summary>
    public sealed class ConversationPreviewDriver : MonoBehaviour
    {
        private readonly struct Speaker
        {
            public Speaker(string portrait)
            {
                Portrait = portrait;
            }

            public string Portrait { get; }
        }

        private static readonly Speaker Protagonist = new("protagonist_normal");
        private static readonly Speaker Robot = new("robot_normal");
        private static readonly Speaker FragileGirl = new("fragile_girl_normal");
        private static readonly Speaker FragileGirlWorried = new("fragile_girl_worried_smile");
        private static readonly Speaker FragileGirlFrightened = new("fragile_girl_frightened");
        private static readonly Speaker FragileGirlSmile = new("fragile_girl_smile");
        private static readonly Speaker FragileGirlDetermined = new("fragile_girl_determined");
        private static readonly Speaker FragileGirlSurprised = new("fragile_girl_surprised");
        private static readonly Speaker Gramophone = new("gramophone_normal");

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
                yield return PlayPreview(view);
            } while (_loop);
        }

        private static IEnumerator PlayPreview(IDialogueView view)
        {
            yield return PlayIntroduction(view);

            yield return Line(
                view,
                Protagonist,
                "これは<wait=0.35><shake><color=#ff9aa8>本当</color></shake>なのか……？"
            );
            if (view is ConversationView introductionView)
                yield return introductionView.PlayPortraitEffect(
                    DialoguePortraitSide.Left,
                    ConversationView.PortraitEffect.Jump
                );

            string firstResponse = null;
            yield return Choice(
                view,
                value => firstResponse = value,
                new ChoiceOption("案内をお願いする", "accept_guidance"),
                new ChoiceOption("まず事情を聞く", "ask_situation")
            );
            yield return PlayFirstResponse(view, firstResponse);

            string destination = null;
            yield return PlayDestinationChoice(view, value => destination = value);
            yield return PlayDestinationResponse(view, destination);
            yield return Line(view, Protagonist, "よし、行き先は決まった。準備を始めよう。");

            if (view is ConversationView conversationView)
                yield return PlayGiftSequence(view, conversationView);

            yield return Line(
                view,
                Protagonist,
                "ありがとう。準備は整った――それじゃあ、出発しよう。"
            );

            if (view is ConversationView closingView)
            {
                yield return closingView.HideAnimated();
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        private static IEnumerator PlayIntroduction(IDialogueView view)
        {
            yield return Line(
                view,
                Protagonist,
                "見慣れない場所だ……。あの三人に、この辺りのことを聞いてみよう。"
            );
            yield return Line(
                view,
                Robot,
                "接近する生命反応を確認。敵意、検出されません。会話を推奨します。"
            );
            yield return Line(view, Protagonist, "しゃべれるロボなのか。少し驚いたな……。");
            yield return Line(
                view,
                FragileGirlFrightened,
                "あの……気をつけてください。蓄音機の方は、少し変わった話し方をするので……。"
            );
            yield return Line(view, Protagonist, "蓄音機の方……？");
            yield return Line(
                view,
                Gramophone,
                "おや、新しい旅人とは珍しい。まずは一曲……いや、先に道案内が必要かな？"
            );
            yield return Line(view, Protagonist, "怪しい感じはしない。どう答えよう？");
        }

        private static IEnumerator PlayFirstResponse(IDialogueView view, string response)
        {
            if (response == "accept_guidance")
            {
                yield return Line(
                    view,
                    FragileGirlSmile,
                    "よかった……。この方、見た目は不思議ですけど、道案内は確かなんです。"
                );
                yield break;
            }

            yield return Line(
                view,
                FragileGirlDetermined,
                "慎重なんですね。それなら私たちが知っていることから説明します。"
            );
        }

        private static IEnumerator PlayDestinationChoice(
            IDialogueView view,
            Action<string> onSelected
        )
        {
            yield return Line(
                view,
                Robot,
                "目的地候補を三件抽出。村、旧遺跡、北の森。危険度は順に上昇します。"
            );
            yield return Line(view, Protagonist, "最初の目的地を決めよう。");
            yield return Choice(
                view,
                onSelected,
                new ChoiceOption("近くの村へ向かう", "village"),
                new ChoiceOption("旧遺跡を調べる", "ruins"),
                new ChoiceOption("北の森を抜ける", "forest")
            );
        }

        private static IEnumerator PlayDestinationResponse(IDialogueView view, string destination)
        {
            switch (destination)
            {
                case "village":
                    yield return Line(
                        view,
                        FragileGirlWorried,
                        "村なら私も途中まで一緒に行けます。少し安心しました。"
                    );
                    break;
                case "ruins":
                    yield return Line(
                        view,
                        Robot,
                        "旧遺跡ルートを設定。瓦礫と旧式警備装置への警戒を推奨します。"
                    );
                    break;
                default:
                    yield return Line(
                        view,
                        Gramophone,
                        "北の森か。風の音がよく響く、実に趣のある道だ。迷わないよう私が案内しよう。"
                    );
                    break;
            }
        }

        private static IEnumerator PlayGiftSequence(
            IDialogueView view,
            ConversationView conversationView
        )
        {
            yield return Line(
                view,
                FragileGirlSurprised,
                "出発するなら、これを持っていってください。さっき拾った、きれいなりんごです。"
            );
            yield return conversationView.ShowItemGet();
            yield return Line(view, Protagonist, "ありがとう。道中で大事に食べるよ。");

            yield return Line(
                view,
                Gramophone,
                "そして護身用に、この刀を。旅の旋律が途切れぬよう、大切に扱ってくれたまえ。"
            );
            yield return conversationView.ShowWeaponGet();
        }

        private static IEnumerator Line(IDialogueView view, Speaker speaker, string text) =>
            view.ShowLine(null, speaker.Portrait, text);

        private static IEnumerator Choice(
            IDialogueView view,
            Action<string> onSelected,
            params ChoiceOption[] options
        ) => view.ShowChoice(options, onSelected);
    }
}
