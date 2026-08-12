using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>本文のタイプライター表示、文字ウェイト、早送りとタイプ音を担当する。</summary>
    internal sealed class DialogueTextPlayer
    {
        public IEnumerator Play(
            TMP_Text body,
            string source,
            ConversationView.TextSpeed speed,
            float characterInterval,
            float punctuationDelay,
            float fastForwardMultiplier,
            bool wasRead,
            AudioSource audioSource,
            AudioClip typingSound,
            Func<bool> skipMode,
            Func<bool> paused
        )
        {
            if (body == null)
                yield break;

            var parsed = DialogueMarkupParser.Parse(source);
            body.text = parsed.Text;
            body.ForceMeshUpdate();
            int total = body.textInfo.characterCount;
            body.maxVisibleCharacters = 0;
            Vector2 basePosition = body.rectTransform.anchoredPosition;

            for (int shown = 1; shown <= total; shown++)
            {
                while (paused())
                    yield return null;
                if (
                    AdvancePressed()
                    || SkipReadHeld(wasRead)
                    || (wasRead && skipMode())
                    || speed == ConversationView.TextSpeed.Instant
                )
                {
                    body.maxVisibleCharacters = total;
                    break;
                }

                body.maxVisibleCharacters = shown;
                if (audioSource != null && typingSound != null && shown % 2 == 0)
                    audioSource.PlayOneShot(typingSound);
                body.rectTransform.anchoredPosition = parsed.IsShaking(shown - 1)
                    ? basePosition + UnityEngine.Random.insideUnitCircle * 2.5f
                    : basePosition;

                float delay = Mathf.Max(0f, characterInterval) * SpeedMultiplier(speed);
                if (IsPunctuation(body.textInfo.characterInfo[shown - 1].character))
                    delay += Mathf.Max(0f, punctuationDelay);
                delay += parsed.GetWaitAfter(shown - 1);
                if (FastForwardHeld())
                    delay *= Mathf.Clamp(fastForwardMultiplier, 0.05f, 1f);
                if (delay > 0f)
                    yield return new WaitForSecondsRealtime(delay);
                else
                    yield return null;
            }

            body.rectTransform.anchoredPosition = basePosition;
            body.maxVisibleCharacters = total;
        }

        private static float SpeedMultiplier(ConversationView.TextSpeed speed) =>
            speed switch
            {
                ConversationView.TextSpeed.Slow => 1.6f,
                ConversationView.TextSpeed.Fast => 0.45f,
                ConversationView.TextSpeed.Instant => 0f,
                _ => 1f,
            };

        private static bool IsPunctuation(char character) =>
            character is '。' or '、' or '！' or '？' or '!' or '?' or '…' or '・' or ',';

        private static bool SkipReadHeld(bool wasRead) =>
            wasRead && Keyboard.current != null && Keyboard.current.sKey.isPressed;

        private static bool FastForwardHeld()
        {
            var keyboard = Keyboard.current;
            if (
                keyboard != null
                && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            )
                return true;
            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.rightShoulder.isPressed;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (
                keyboard != null
                && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            )
                return true;
            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }
    }
}
