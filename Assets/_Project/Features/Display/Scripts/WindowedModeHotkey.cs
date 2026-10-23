using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// F5 キーで、画面をウィンドウモードの 1280×720 に切り替える。
    /// どのシーンから起動しても使えるよう、シーンに置かずに起動時に自動生成し、シーンをまたいで残す。
    /// </summary>
    public class WindowedModeHotkey : MonoBehaviour
    {
        private const Key ToggleKey = Key.F5;
        private const int WindowWidth = 1280;
        private const int WindowHeight = 720;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            var go = new GameObject(nameof(WindowedModeHotkey));
            go.AddComponent<WindowedModeHotkey>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
                Screen.SetResolution(WindowWidth, WindowHeight, FullScreenMode.Windowed);
        }
    }
}
