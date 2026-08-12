using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>手動送り、既読スキップ、AUTO送りと進捗率の計算を担当する。</summary>
    internal sealed class DialogueAdvanceController
    {
        public float Progress { get; private set; }

        public IEnumerator Wait(
            TMP_Text body,
            bool currentLineWasRead,
            Func<bool> autoMode,
            Func<bool> skipMode,
            Func<bool> paused,
            Func<bool> historyOpen,
            float baseAutoDelay
        )
        {
            yield return null;
            float autoElapsed = 0f;
            Progress = 0f;
            while (true)
            {
                if (paused() || historyOpen())
                {
                    autoElapsed = 0f;
                    Progress = 0f;
                    yield return null;
                    continue;
                }
                if (
                    AdvancePressed()
                    || SkipReadHeld(currentLineWasRead)
                    || (currentLineWasRead && skipMode())
                )
                    break;
                if (autoMode())
                {
                    float delay = CalculateAutoDelay(body, baseAutoDelay);
                    autoElapsed += Time.unscaledDeltaTime;
                    Progress = Mathf.Clamp01(autoElapsed / delay);
                    if (autoElapsed >= delay)
                        break;
                }
                else
                {
                    autoElapsed = 0f;
                    Progress = 0f;
                }
                yield return null;
            }
            Progress = 0f;
        }

        public static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (
                keyboard != null
                && (
                    keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame
                    || keyboard.zKey.wasPressedThisFrame
                )
            )
                return true;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;
            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private static bool SkipReadHeld(bool wasRead) =>
            wasRead && Keyboard.current != null && Keyboard.current.sKey.isPressed;

        private static float CalculateAutoDelay(TMP_Text body, float baseDelay)
        {
            int length = body != null ? body.textInfo.characterCount : 0;
            return Mathf.Max(0.1f, baseDelay + Mathf.Clamp(length * 0.025f, 0f, 2.5f));
        }
    }
}
