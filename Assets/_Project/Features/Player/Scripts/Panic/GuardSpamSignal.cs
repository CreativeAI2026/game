using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標2: スパム [Christou&amp;Bhatt 2012]
    /// 一定幅のウィンドウごとに、剣の防御ボタンの押下回数から頻度（回/秒）を算出する。
    /// 押下そのものは PanicDetector が <see cref="RecordPress"/> で記録する。
    /// </summary>
    [Serializable]
    public class GuardSpamSignal : PanicSignal
    {
        // 連打頻度（回/秒）がこれ以上なら兆候として通知する
        private const float SignalRate = 1f;

        [Tooltip("この頻度（回/秒）以上で防御ボタンを押したら満点")]
        [SerializeField]
        private float _maxRate = 3f;

        [Tooltip("スパム頻度計測のウィンドウ幅（秒）")]
        [SerializeField]
        private float _windowDuration = 1.5f;

        private int _pressCount;
        private float _elapsed;
        private float _score;

        public override string DisplayName => "スパム";

        public override float Score => _score;

        /// <summary>現在のスパム頻度（回/秒）</summary>
        public float Rate { get; private set; }

        /// <summary>交戦中に剣の防御ボタンが押された時に呼ぶ。</summary>
        public void RecordPress()
        {
            _pressCount++;
        }

        public override void Tick(PanicContext context, float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < _windowDuration)
                return;

            if (_pressCount > 1)
            {
                Rate = _pressCount / _elapsed;
                _score = Mathf.Clamp01(Rate / _maxRate);

                if (Rate >= SignalRate)
                {
                    RaiseDetected();
                }
            }
            else
            {
                // 1回だけの押下は冷静な防御・パリィの可能性があるので連打とみなさない。
                // 前のウィンドウの値を残すと、連打をやめた後もスコアが下がらなくなるため0に戻す
                Rate = 0f;
                _score = 0f;
            }

            _pressCount = 0;
            _elapsed = 0f;
        }

        public override void ResetState()
        {
            _pressCount = 0;
            _elapsed = 0f;
            _score = 0f;
            Rate = 0f;
        }
    }
}
