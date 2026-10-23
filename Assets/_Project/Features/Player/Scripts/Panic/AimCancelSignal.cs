using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標5: 意思崩壊 [Starcke&amp;Brand 2012]
    /// 交戦中、弓を構えたのに撃たず自分で解いた直近の回数を数える。
    /// キャンセルそのものは PanicDetector が <see cref="RecordCancel"/> で記録する。
    /// </summary>
    [Serializable]
    public class AimCancelSignal : PanicSignal
    {
        [Tooltip("直近の判定ウィンドウ内でこの回数以上、弓をキャンセルしたら満点")]
        [SerializeField]
        private int _maxCount = 3;

        [Tooltip("弓キャンセルを数える直近の時間幅（秒）。これより前のキャンセルは数えない")]
        [SerializeField]
        private float _window = 10f;

        // 判定ウィンドウ内のキャンセル時刻（古い順）
        private readonly Queue<float> _cancelTimes = new Queue<float>();

        // 狙い直しのための意図的なキャンセルも含まれるため中程度にする
        private const float DefaultWeight = 0.5f;

        public AimCancelSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Indecision;

        public override string DisplayName => "意思崩壊";

        public override float Score => Mathf.Clamp01((float)_cancelTimes.Count / _maxCount);

        public override float RawValue => _cancelTimes.Count;

        public override string RawValueUnit => "直近回数";

        /// <summary>直近の判定ウィンドウ内の弓キャンセル回数</summary>
        public int Count => _cancelTimes.Count;

        /// <summary>弓キャンセルを数える直近の時間幅（秒）</summary>
        public float Window => _window;

        /// <summary>交戦中にプレイヤーが自分で弓の構えを解いた時に呼ぶ。</summary>
        public void RecordCancel()
        {
            _cancelTimes.Enqueue(Time.time);
            RaiseDetected();
        }

        /// <summary>判定ウィンドウより古いキャンセルを捨てる（累計で数えると一度満点になったまま下がらないため）。</summary>
        public override void Tick(PanicContext context, float deltaTime)
        {
            while (_cancelTimes.Count > 0 && Time.time - _cancelTimes.Peek() > _window)
            {
                _cancelTimes.Dequeue();
            }
        }

        public override void ResetState()
        {
            _cancelTimes.Clear();
        }
    }
}
