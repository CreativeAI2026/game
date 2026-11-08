using System;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CreativeAI.Playtest
{
    /// <summary>
    /// ボス体験用ビルドで、キーでボスのシーンを行き来する・今のシーンをやり直す。
    /// 体験用ビルドにはタイトルも死亡後の処理も無いため、倒れた後もここから続けられるようにする。
    /// 戦闘中以外(フィールド中・倒れた後)だけ、画面上部にキーの案内を出す。
    /// </summary>
    public sealed class PlaytestSceneSwitcher : MonoBehaviour
    {
        [Serializable]
        private struct Destination
        {
            public Key key;
            public string sceneName;
            public string label;
        }

        private const float HintTopMargin = 8f;
        private const int HintFontSize = 16;

        [SerializeField]
        private Destination[] _destinations =
        {
            new Destination
            {
                key = Key.F1,
                sceneName = "TBossDemo",
                label = "TBossへ",
            },
            new Destination
            {
                key = Key.F2,
                sceneName = "MidBossDemo",
                label = "クラゲへ",
            },
        };

        [SerializeField]
        private Key _retryKey = Key.F5;

        private PlayerStatus _playerStatus;
        private GUIContent _hintContent;

        // GUIStyle は保存できる型なので、付けないとエディターの再読み込みで空のスタイルが入り、下の ??= で作り直されなくなる
        [NonSerialized]
        private GUIStyle _hintStyle;

        private void Awake()
        {
            var parts = new string[_destinations.Length + 1];
            for (int i = 0; i < _destinations.Length; i++)
                parts[i] = $"{_destinations[i].key}: {_destinations[i].label}";
            parts[^1] = $"{_retryKey}: やり直す";
            _hintContent = new GUIContent(string.Join("    ", parts));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[_retryKey].wasPressedThisFrame)
            {
                Load(SceneManager.GetActiveScene().name);
                return;
            }
            foreach (var destination in _destinations)
            {
                if (keyboard[destination.key].wasPressedThisFrame)
                {
                    Load(destination.sceneName);
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (!ShouldShowHint())
                return;

            _hintStyle ??= new GUIStyle(GUI.skin.box) { fontSize = HintFontSize };
            Vector2 size = _hintStyle.CalcSize(_hintContent);
            var rect = new Rect((Screen.width - size.x) * 0.5f, HintTopMargin, size.x, size.y);
            GUI.Box(rect, _hintContent, _hintStyle);
        }

        private bool ShouldShowHint()
        {
            var mode = GameModeManager.Instance;
            bool inBattle = mode != null && mode.CurrentMode == GameMode.Battle;
            return !inBattle || IsPlayerDead();
        }

        private bool IsPlayerDead()
        {
            if (_playerStatus == null && PlayerRegistry.Current != null)
                _playerStatus = PlayerRegistry.Current.GetComponent<PlayerStatus>();
            return _playerStatus != null && _playerStatus.IsDead;
        }

        private void Load(string sceneName)
        {
            if (SceneController.CanLoad(sceneName))
            {
                // タイトルを通らない体験用ビルドには SceneController(暗転つきの遷移)が無いので、無ければそのまま読む。
                if (SceneController.Instance != null)
                    SceneController.Instance.LoadScene(sceneName);
                else
                    SceneManager.LoadScene(sceneName);
                return;
            }

#if UNITY_EDITOR
            // エディターでは共有の Build Settings に無いシーンも、パスを探して読む(Build Settings は他の人も使うので足さない)。
            if (TryLoadInEditor(sceneName))
                return;
#endif
            Debug.LogError($"{name}: シーン '{sceneName}' を読み込めません", this);
        }

#if UNITY_EDITOR
        private static bool TryLoadInEditor(string sceneName)
        {
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != sceneName)
                    continue;
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    path,
                    new LoadSceneParameters(LoadSceneMode.Single)
                );
                return true;
            }
            return false;
        }
#endif
    }
}
