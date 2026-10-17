using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標1: 逃避 [Yerkes-Dodson / BIS-BAS]
    /// 交戦中の敵からカメラが大きく離れたまま移動し続けた時間を計測する。
    /// </summary>
    [Serializable]
    public class FlightSignal : PanicSignal
    {
        // 逃避がこの秒数続いたら兆候として通知する（満点とは別の境目）
        private const float SignalSeconds = 1f;

        [Tooltip(
            "逃避検知を有効にするか(イベント中などに PanicDetector.SetFlightDetectionEnabled で切り替えられる)"
        )]
        [SerializeField]
        private bool _enabled = true;

        [Tooltip("この秒数以上、敵にカメラを向けず移動し続けたら満点")]
        [SerializeField]
        private float _maxDuration = 3f;

        [Tooltip("カメラが敵方向と何度以上離れていれば「敵を向いていない」と判定するか")]
        [SerializeField]
        private float _awayAngleThreshold = 80f;

        private float _timer;
        private bool _isSignalActive;

        public override string DisplayName => "逃避";

        public override float Score => Mathf.Clamp01(_timer / _maxDuration);

        /// <summary>逃避継続時間（秒）</summary>
        public float Timer => _timer;

        /// <summary>逃避検知が有効か</summary>
        public bool IsEnabled => _enabled;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _timer = 0f;
            }
        }

        public override void Tick(PanicContext context, float deltaTime)
        {
            if (!_enabled)
            {
                _timer = 0f;
                _isSignalActive = false;
                return;
            }

            if (!context.IsInCombat)
            {
                _timer = Mathf.Max(0f, _timer - deltaTime);
            }
            else if (!context.HasMoveInput)
            {
                _timer = 0f;
            }
            else
            {
                float angle = Vector3.Angle(context.CameraForwardXZ, context.ToEnemyDirectionXZ);
                if (angle >= _awayAngleThreshold)
                {
                    _timer += deltaTime;
                }
                else
                {
                    _timer = Mathf.Max(0f, _timer - deltaTime);
                }
            }

            UpdateActiveState(ref _isSignalActive, _timer >= SignalSeconds);
        }

        public override void ResetState()
        {
            _timer = 0f;
            _isSignalActive = false;
        }
    }
}
