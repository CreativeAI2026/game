using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.UI
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
            return UiInputActionMap.WasPressedThisFrame(UiInputActionMap.DialogueAdvance)
                || UiInputActionMap.WasClickPressedThisFrame();
        }

        private static bool SkipReadHeld(bool wasRead)
        {
            return wasRead && UiInputActionMap.IsPressed(UiInputActionMap.DialogueSkip);
        }

        private static float CalculateAutoDelay(TMP_Text body, float baseDelay)
        {
            int length = body != null ? body.textInfo.characterCount : 0;
            return Mathf.Max(0.1f, baseDelay + Mathf.Clamp(length * 0.025f, 0f, 2.5f));
        }
    }
}
