using System.Collections;
using System.Globalization;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>スクリプトエンジン由来の演出コマンドを会話UI操作へ変換する。</summary>
    internal sealed class DialoguePresentationCommandRouter
    {
        private readonly ConversationView _view;
        private readonly DialoguePortraitView _portraits;
        private readonly DialogueStageCommands _stage;

        public DialoguePresentationCommandRouter(
            ConversationView view,
            DialoguePortraitView portraits,
            DialogueStageCommands stage
        )
        {
            _view = view;
            _portraits = portraits;
            _stage = stage;
        }

        public IEnumerator Execute(string command, string argument)
        {
            switch (command?.Trim().ToLowerInvariant())
            {
                case "window.hide":
                    _view.SetWindowHidden(true);
                    break;
                case "window.show":
                    _view.SetWindowHidden(false);
                    break;
                case "portrait.left.hide":
                    _portraits.SetVisible(DialoguePortraitSide.Left, false);
                    break;
                case "portrait.right.hide":
                    _portraits.SetVisible(DialoguePortraitSide.Right, false);
                    break;
                case "portrait.left.obscure":
                    yield return _portraits.SetObscured(DialoguePortraitSide.Left, true, 0.5f);
                    break;
                case "portrait.right.obscure":
                    yield return _portraits.SetObscured(DialoguePortraitSide.Right, true, 0.5f);
                    break;
                case "portrait.left.reveal":
                    yield return _portraits.SetObscured(DialoguePortraitSide.Left, false, 0.5f);
                    break;
                case "portrait.right.reveal":
                    yield return _portraits.SetObscured(DialoguePortraitSide.Right, false, 0.5f);
                    break;
                case "portrait.left.shake":
                    yield return _portraits.PlayEffect(
                        DialoguePortraitSide.Left,
                        ConversationView.PortraitEffect.Shake,
                        0.28f
                    );
                    break;
                case "portrait.right.shake":
                    yield return _portraits.PlayEffect(
                        DialoguePortraitSide.Right,
                        ConversationView.PortraitEffect.Shake,
                        0.28f
                    );
                    break;
                case "portrait.left.jump":
                    yield return _portraits.PlayEffect(
                        DialoguePortraitSide.Left,
                        ConversationView.PortraitEffect.Jump,
                        0.28f
                    );
                    break;
                case "portrait.right.jump":
                    yield return _portraits.PlayEffect(
                        DialoguePortraitSide.Right,
                        ConversationView.PortraitEffect.Jump,
                        0.28f
                    );
                    break;
                case "wait":
                    if (
                        float.TryParse(
                            argument,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float seconds
                        )
                    )
                        yield return new WaitForSecondsRealtime(Mathf.Clamp(seconds, 0f, 10f));
                    break;
                case "portrait.show":
                    yield return _portraits.ShowByKey(argument?.Trim());
                    break;
                case "conversation.close":
                    yield return _view.HideAnimated();
                    break;
                default:
                    IEnumerator stage = _stage.Create(command, argument);
                    if (stage != null)
                        yield return stage;
                    else
                        Debug.LogWarning($"[ConversationView] 未対応の演出コマンドです: {command}");
                    break;
            }
        }
    }
}
