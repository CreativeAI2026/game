#if UNITY_EDITOR
using CreativeAI.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 再生中に F1 で、会話の全スキップ(DialogueDebugSkip)をオン/オフする開発用のホットキー。
    /// イベントの確認で毎回送り入力を連打しなくて済むようにするもの。選択肢は手で選ぶ。
    /// Editor にだけ置くので、ビルドにはキー判定も表示も入らない。再生を止めるとオフに戻る。
    /// </summary>
    [InitializeOnLoad]
    public static class DialogueDebugSkipHotkey
    {
        static DialogueDebugSkipHotkey()
        {
            // 入力の更新ごとに見る(EditorApplication.update だと、1フレームに複数回呼ばれて二重に切り替わる)。
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnAfterInputUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f1Key.wasPressedThisFrame)
                return;

            DialogueDebugSkip.Active = !DialogueDebugSkip.Active;
            ShowNotification(
                DialogueDebugSkip.Active ? "会話スキップ ON(F1 で解除)" : "会話スキップ OFF"
            );
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                DialogueDebugSkip.Active = false;
        }

        // Game ビューの上に一言出す(GameView は内部の型なので名前で探す)。
        private static void ShowNotification(string message)
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
                return;
            foreach (var view in Resources.FindObjectsOfTypeAll(gameViewType))
                if (view is EditorWindow window)
                    window.ShowNotification(new GUIContent(message), 1.5);
        }
    }
}
#endif
