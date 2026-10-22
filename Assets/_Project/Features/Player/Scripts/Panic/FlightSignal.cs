using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標1: 逃避 [Yerkes-Dodson / BIS-BAS]
    /// 交戦中の敵から遠ざかり続けた時間を計測する。
    /// 「カメラが敵を向いていないか」だけでは、敵を向いたまま後ろへ下がる逃げを拾えず、
    /// 横へ回り込むだけの移動まで拾ってしまうため、敵から離れる向きの速さで判定する。
    ///
    /// 離れ方による重み:
    ///   背を向けて離れる          → 1.0（典型的な逃げ）
    ///   敵を向いたまま後ろへ下がる → <see cref="_backpedalFactor"/>（距離を取り直す冷静な後退も含まれる）
    ///   弓を構えたまま下がる      → 0（引き撃ちという正しい戦法）
    /// </summary>
    [Serializable]
    public class FlightSignal : PanicSignal
    {
        // 逃避がこの秒数続いたら兆候として通知する（満点とは別の境目）
        private const float SignalSeconds = 1f;

        // 交戦中に背を向けて離れ続ける理由は焦り以外に少ないため、信頼度を高めにする
        private const float DefaultWeight = 0.7f;

        [Tooltip(
            "逃避検知を有効にするか(イベント中などに PanicDetector.SetFlightDetectionEnabled で切り替えられる)"
        )]
        [SerializeField]
        private bool _enabled = true;

        [Tooltip("この秒数以上、背を向けて離れ続けたら満点")]
        [SerializeField]
        private float _maxDuration = 3f;

        [Tooltip("カメラが敵方向と何度以上離れていれば「背を向けている」と判定するか")]
        [SerializeField]
        private float _awayAngleThreshold = 80f;

        [Tooltip("敵から離れる向きの速さが、歩き速度のこの割合以上なら「離れている」とみなす")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _retreatSpeedRatio = 0.4f;

        [Tooltip(
            "敵を向いたまま後ろへ下がっている時の溜まり方（背を向けて離れる時を 1 とした割合）"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float _backpedalFactor = 0.5f;

        private float _timer;
        private bool _isSignalActive;

        public FlightSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Avoidance;

        public override string DisplayName => "逃避";

        public override float Score => Mathf.Clamp01(_timer / _maxDuration);

        public override float RawValue => _timer;

        public override string RawValueUnit => "継続秒";

        /// <summary>逃避継続時間（秒、背を向けて離れた時間に換算した値）</summary>
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

            float factor = context.IsInCombat ? EvaluateRetreatFactor(context) : 0f;
            if (factor > 0f)
            {
                // 満点で頭打ちにして、離れるのをやめた後に下がり切るまでの時間を一定にする
                _timer = Mathf.Min(_maxDuration, _timer + deltaTime * factor);
            }
            else
            {
                // 立ち止まった瞬間に0へ戻すと、止まっては逃げるを繰り返す逃げ方を拾えないため、ゆっくり減らす
                _timer = Mathf.Max(0f, _timer - deltaTime);
            }

            UpdateActiveState(ref _isSignalActive, _timer >= SignalSeconds);
        }

        public override void ResetState()
        {
            _timer = 0f;
            _isSignalActive = false;
        }

        /// <summary>今フレームの離れ方の重み（0 = 離れていない、1 = 背を向けて離れている）。</summary>
        private float EvaluateRetreatFactor(PanicContext context)
        {
            if (context.IsAiming)
                return 0f;

            float retreatSpeed = Vector3.Dot(
                context.HorizontalVelocity,
                -context.ToEnemyDirectionXZ
            );
            if (retreatSpeed < context.WalkSpeed * _retreatSpeedRatio)
                return 0f;

            float cameraAngle = Vector3.Angle(context.CameraForwardXZ, context.ToEnemyDirectionXZ);
            return cameraAngle >= _awayAngleThreshold ? 1f : _backpedalFactor;
        }
    }
}
