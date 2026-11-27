using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.DevTools
{
    /// <summary>
    /// 指定キー（既定: F8）で、プレイヤーの歩き・走りを速くするデバッグ用の「ファストダッシュ」を切り替える。
    /// 広いマップを確かめるときの一時的な機能で、あとで消す前提。このファイルを消すだけで外せる。
    /// 起動時に自動で常駐するので、シーンへ置く必要はない。開発用ビルドとエディタでだけ動く。
    /// プレイヤーが作り直されても、入れたままなら新しいプレイヤーにも掛け直す。
    /// </summary>
    public sealed class FastDashDebugToggle : MonoBehaviour
    {
        private const Key ToggleKey = Key.F8;

        // 歩き・走りに掛ける倍率
        private const float SpeedScale = 3f;

        private bool _enabled;
        private PlayerController _target;
        private float _originalMoveSpeed;
        private float _originalSprintSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Debug.isDebugBuild)
                return;

            var go = new GameObject(nameof(FastDashDebugToggle));
            DontDestroyOnLoad(go);
            go.AddComponent<FastDashDebugToggle>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
            {
                _enabled = !_enabled;
                if (!_enabled)
                    Restore();
                Debug.Log(
                    $"[{nameof(FastDashDebugToggle)}] ファストダッシュを{(_enabled ? "オン" : "オフ")}にしました"
                );
            }

            if (_enabled)
                Apply(PlayerRegistry.Current);
        }

        // プレイヤーが入れ替わったら、前のプレイヤーの速度を元に戻してから、新しいプレイヤーへ掛ける。
        private void Apply(PlayerController player)
        {
            if (player == null || player == _target)
                return;

            Restore();
            _target = player;
            _originalMoveSpeed = player.MoveSpeed;
            _originalSprintSpeed = player.SprintSpeed;
            player.MoveSpeed = _originalMoveSpeed * SpeedScale;
            player.SprintSpeed = _originalSprintSpeed * SpeedScale;
        }

        private void Restore()
        {
            if (_target != null)
            {
                _target.MoveSpeed = _originalMoveSpeed;
                _target.SprintSpeed = _originalSprintSpeed;
            }
            _target = null;
        }

        private void OnGUI()
        {
            if (_enabled)
                GUI.Label(new Rect(8f, 8f, 260f, 24f), $"FAST DASH ON ({ToggleKey})");
        }
    }
}
