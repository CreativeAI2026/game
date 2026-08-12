using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>スクリプトエンジン由来の演出コマンドを会話UI操作へ変換する。</summary>
    internal sealed class DialoguePresentationCommandRouter
    {
        private readonly Action<bool> _setWindowHidden;
        private readonly Action<DialoguePortraitSide, bool> _setPortraitVisible;
        private readonly Func<DialoguePortraitSide, bool, float, IEnumerator> _setObscured;
        private readonly Func<
            DialoguePortraitSide,
            ConversationView.PortraitEffect,
            float,
            IEnumerator
        > _playEffect;
        private readonly Func<IEnumerator> _close;
        private readonly Action<string, string> _external;

        public DialoguePresentationCommandRouter(
            Action<bool> setWindowHidden,
            Action<DialoguePortraitSide, bool> setPortraitVisible,
            Func<DialoguePortraitSide, bool, float, IEnumerator> setObscured,
            Func<
                DialoguePortraitSide,
                ConversationView.PortraitEffect,
                float,
                IEnumerator
            > playEffect,
            Func<IEnumerator> close,
            Action<string, string> external
        )
        {
            _setWindowHidden = setWindowHidden;
            _setPortraitVisible = setPortraitVisible;
            _setObscured = setObscured;
            _playEffect = playEffect;
            _close = close;
            _external = external;
        }

        public IEnumerator Execute(string command, string argument)
        {
            switch (command?.Trim().ToLowerInvariant())
            {
                case "window.hide":
                    _setWindowHidden(true);
                    break;
                case "window.show":
                    _setWindowHidden(false);
                    break;
                case "portrait.left.hide":
                    _setPortraitVisible(DialoguePortraitSide.Left, false);
                    break;
                case "portrait.right.hide":
                    _setPortraitVisible(DialoguePortraitSide.Right, false);
                    break;
                case "portrait.left.obscure":
                    yield return _setObscured(DialoguePortraitSide.Left, true, 0.5f);
                    break;
                case "portrait.right.obscure":
                    yield return _setObscured(DialoguePortraitSide.Right, true, 0.5f);
                    break;
                case "portrait.left.reveal":
                    yield return _setObscured(DialoguePortraitSide.Left, false, 0.5f);
                    break;
                case "portrait.right.reveal":
                    yield return _setObscured(DialoguePortraitSide.Right, false, 0.5f);
                    break;
                case "portrait.left.shake":
                    yield return _playEffect(
                        DialoguePortraitSide.Left,
                        ConversationView.PortraitEffect.Shake,
                        0.28f
                    );
                    break;
                case "portrait.right.shake":
                    yield return _playEffect(
                        DialoguePortraitSide.Right,
                        ConversationView.PortraitEffect.Shake,
                        0.28f
                    );
                    break;
                case "portrait.left.jump":
                    yield return _playEffect(
                        DialoguePortraitSide.Left,
                        ConversationView.PortraitEffect.Jump,
                        0.28f
                    );
                    break;
                case "portrait.right.jump":
                    yield return _playEffect(
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
                case "conversation.close":
                    yield return _close();
                    break;
                default:
                    if (
                        command != null
                        && (
                            command.StartsWith("camera.", StringComparison.OrdinalIgnoreCase)
                            || command.StartsWith("background.", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                        _external(command, argument);
                    else
                        Debug.LogWarning($"[ConversationView] 未対応の演出コマンドです: {command}");
                    break;
            }
        }
    }
}
