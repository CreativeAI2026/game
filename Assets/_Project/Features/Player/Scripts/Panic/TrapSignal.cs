using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標4: 空間喪失 [Lazarus&amp;Folkman 1984]
    /// 交戦中、移動入力があるにもかかわらず実際の速度が閾値以下の状態の継続時間を計測する。
    /// 壁や地形に引っかかって動けない状態を検出する。
    /// 掴まれている間など移動自体が封じられている時は計測せず、溜まった時間を減らす。
    /// </summary>
    [Serializable]
    public class TrapSignal : PanicSignal
    {
        // 空間喪失がこの秒数続いたら兆候として通知する
        private const float SignalSeconds = 0.5f;

        // 解消した時、溜まった時間を溜まる速さの何倍で減らすか（引っかかりが外れたらすぐ下がるように）
        private const float RecoveryRate = 2f;

        [Tooltip("この秒数以上、移動入力ありで実際に動けていない状態が続いたら満点")]
        [SerializeField]
        private float _maxDuration = 3f;

        [Tooltip("「動けていない」と判定する速度の閾値（m/s）")]
        [SerializeField]
        private float _velocityThreshold = 0.5f;

        private float _timer;
        private bool _isSignalActive;

        public override string DisplayName => "空間喪失";

        public override float Score => Mathf.Clamp01(_timer / _maxDuration);

        /// <summary>空間喪失の継続時間（秒）</summary>
        public float Timer => _timer;

        public override void Tick(PanicContext context, float deltaTime)
        {
            bool isStuck = context.HorizontalSpeed < _velocityThreshold;

            if (context.IsInCombat && context.CanMove && context.HasMoveInput && isStuck)
            {
                _timer += deltaTime;
            }
            else
            {
                _timer = Mathf.Max(0f, _timer - deltaTime * RecoveryRate);
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
