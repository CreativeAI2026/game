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

            string firstResponse = null;
            yield return Choice(
                view,
                value => firstResponse = value,
                new ChoiceOption("ここがどこか尋ねる", "ask_situation"),
                new ChoiceOption("先に出口を教えてもらう", "accept_guidance")
            );
            yield return PlayFirstResponse(view, firstResponse);

            string destination = null;
            yield return PlayDestinationChoice(view, value => destination = value);
            yield return PlayDestinationResponse(view, destination);
            yield return Line(view, Protagonist, "分かった。その道を行こう。");

            if (view is ConversationView conversationView)
            {
                yield return PlayShortSceneTransition(conversationView);
                yield return PlayGiftSequence(view, conversationView);
            }

            yield return Line(
                view,
                Protagonist,
                "ありがとう。これなら外へ出られそうだ。――行ってくる。"
            );

            if (view is ConversationView closingView)
            {
                yield return closingView.HideAnimated();
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        private static IEnumerator PlayIntroduction(IDialogueView view)
        {
            yield return Narration(
                view,
                "雨音の向こうで、古いレコードが途切れ途切れに鳴っている。"
            );
            yield return Line(view, Protagonist, "……知らない天井だ。ここは駅舎、なのか？");
            yield return Line(
                view,
                Robot,
                "覚醒を確認。外傷なし。現在地の記憶に欠落があると推定します。"
            );
            yield return Line(view, Protagonist, "しゃべった……。君が助けてくれたのか？");
            yield return Narration(view, "待合室の奥、壊れた照明の下で人影が動いた。");
            if (view is ConversationView mysteryView)
            {
                mysteryView.SetPortraitVisible(DialoguePortraitSide.Right, false);
                yield return mysteryView.SetPortraitObscured(DialoguePortraitSide.Right, true, 0f);
            }
            yield return LineAs(
                view,
                "？？？",
                FragileGirlFrightened,
                "<whisper>あの……倒れていたあなたを運んだのは、その子です。私は毛布を掛けただけで……。</whisper>"
            );
            yield return Line(view, Protagonist, "そこに誰かいるのか？　暗くて顔が見えない。");
            if (view is ConversationView revealView)
                yield return revealView.SetPortraitObscured(
                    DialoguePortraitSide.Right,
                    false,
                    0.75f
                );
            yield return Line(
                view,
                FragileGirlWorried,
                "ご、ごめんなさい。明るいところが少し苦手で……。驚かせるつもりはなかったんです。"
            );
            yield return Line(
                view,
                Gramophone,
                "そして私は、冷えた客人に<emphasis>一曲</emphasis>添えた。目覚めの旋律としては上出来だったろう？"
            );
            yield return Line(
                view,
                Protagonist,
                "<wait=0.25><shake><shout>蓄音機までしゃべるのか……。</shout></shake>でも、敵意はなさそうだ。"
            );
            if (view is ConversationView conversationView)
                yield return conversationView.PlayPortraitEffect(
                    DialoguePortraitSide.Left,
                    ConversationView.PortraitEffect.Jump
                );
        }

        private static IEnumerator PlayFirstResponse(IDialogueView view, string response)
        {
            if (response == "accept_guidance")
            {
                yield return Line(
                    view,
                    FragileGirlSmile,
                    "出口なら、線路沿いの扉から出られます。でも、その先の道が少し危なくて……。"
                );
                yield break;
            }

            yield return Line(
                view,
                FragileGirlDetermined,
                "ここは使われなくなった北駅です。昨夜の嵐のあと、あなたがホームに倒れていました。"
            );
            yield return Line(
                view,
                Protagonist,
                "北駅……。名前を聞いても、やっぱり思い出せないな。"
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
                "駅から移動可能な経路は<emphasis>三つ</emphasis>。南の村、旧遺跡、北の森です。"
            );
            yield return Line(
                view,
                Gramophone,
                "記憶を探すなら人のいる村、手掛かりを探すなら遺跡。森は近道だが、夜までには抜けたいね。"
            );
            yield return Choice(
                view,
                onSelected,
                new ChoiceOption("南の村で話を聞く", "village"),
                new ChoiceOption("旧遺跡で手掛かりを探す", "ruins"),
                new ChoiceOption("北の森を抜けて先を急ぐ", "forest")
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
                        "村なら、私も分かれ道まで一緒に行けます。知っている人がいるか聞いてみましょう。"
                    );
                    break;
                case "ruins":
                    yield return Line(
                        view,
                        Robot,
                        "旧遺跡への経路を設定。瓦礫と、停止していない警備装置に注意してください。"
                    );
                    break;
                default:
                    yield return Line(
                        view,
                        Gramophone,
                        "北の森か。近道ではあるが霧が深い。分かれ道までは私の音を頼りにするといい。"
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
                "待ってください。朝に拾ったりんごがあるんです。少し傷がありますけど、食べられますから。"
            );
            yield return conversationView.ShowItemGet(null, "傷のあるりんごを手に入れた。");
            yield return Line(view, Protagonist, "助かるよ。道中で食べさせてもらう。");

            yield return Line(
                view,
                Gramophone,
                "ホームの倉庫には、古い刀も眠っていた。君の物かは分からないが、丸腰よりはいい。"
            );
            yield return conversationView.ShowWeaponGet(null, "古い刀を手に入れた。");
            yield return Line(
                view,
                Robot,
                "携行を確認。危険を検知した場合は、戦闘より退避を優先してください。"
            );
        }

        private static IEnumerator PlayShortSceneTransition(ConversationView view)
        {
            yield return view.RunPresentationCommand("window.hide");
            yield return view.RunPresentationCommand("wait", "0.45");
            yield return view.RunPresentationCommand("window.show");
            yield return Narration(view, "少女と蓄音機が、ホーム脇の倉庫から旅支度を運んできた。");
        }

        private static IEnumerator Line(IDialogueView view, Speaker speaker, string text) =>
            view.ShowLine(null, speaker.Portrait, text);

        private static IEnumerator LineAs(
            IDialogueView view,
            string displayName,
            Speaker speaker,
            string text
        ) => view.ShowLine(displayName, speaker.Portrait, text);

        private static IEnumerator Narration(IDialogueView view, string text) =>
            view.ShowLine(null, null, text);

        private static IEnumerator Choice(
            IDialogueView view,
            Action<string> onSelected,
            params ChoiceOption[] options
        ) => view.ShowChoice(options, onSelected);
    }
}
