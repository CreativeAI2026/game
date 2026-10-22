using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標2: スパム [Christou&amp;Bhatt 2012]
    /// 直近の一定時間に剣の防御ボタンを押した回数から頻度（回/秒）を算出する。
    /// 押下そのものは PanicDetector が <see cref="RecordPress"/> で記録する。
    ///
    /// ボスが攻撃している最中の押下は数えない。パリィは攻撃が当たる直前に押して成立するため、
    /// 攻撃中の連打は「パリィを狙った有効な操作」と区別できないからである。
    /// 何も来ていない時に押し続けるのを「焦りの連打」とみなす。
    /// </summary>
    [Serializable]
    public class GuardSpamSignal : PanicSignal
    {
        // 連打頻度（回/秒）がこれ以上なら兆候として通知する
        private const float SignalRate = 1f;

        // 1回だけの押下は冷静な防御・パリィの可能性があるので、この回数から連打とみなす
        private const int MinPressesForSpam = 2;

        // 攻撃ステートに入る直前から身構えて押すパリィ狙いは除けないため、中程度の信頼度にする
        private const float DefaultWeight = 0.5f;

        [Tooltip("この頻度（回/秒）以上で防御ボタンを押したら満点")]
        [SerializeField]
        private float _maxRate = 3f;

        [Tooltip("頻度を数える直近の時間幅（秒）")]
        [SerializeField]
        private float _windowDuration = 1.5f;

        // 判定ウィンドウ内の押下時刻（古い順）
        private readonly Queue<float> _pressTimes = new Queue<float>();
        private bool _isSignalActive;

        public GuardSpamSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.DefenseBreakdown;

        public override string DisplayName => "スパム";

        public override float Score =>
            _pressTimes.Count < MinPressesForSpam ? 0f : Mathf.Clamp01(Rate / _maxRate);

        public override float RawValue => Rate;

        public override string RawValueUnit => "回毎秒";

        /// <summary>直近の判定ウィンドウ内の押下頻度（回/秒）</summary>
        public float Rate => _pressTimes.Count / _windowDuration;

        /// <summary>交戦中に剣の防御ボタンが押された時に呼ぶ。ボスの攻撃中の押下はパリィ狙いとして数えない。</summary>
        public void RecordPress(PanicContext context)
        {
            if (context.IsEnemyAttacking)
                return;

            _pressTimes.Enqueue(Time.time);
        }

        public override void Tick(PanicContext context, float deltaTime)
        {
            while (_pressTimes.Count > 0 && Time.time - _pressTimes.Peek() > _windowDuration)
            {
                _pressTimes.Dequeue();
            }

            UpdateActiveState(
                ref _isSignalActive,
                _pressTimes.Count >= MinPressesForSpam && Rate >= SignalRate
            );
        }

        public override void ResetState()
        {
            _pressTimes.Clear();
            _isSignalActive = false;
        }
    }
}
