using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標6: 回避失敗 [Yerkes-Dodson 注意狭隘化]
    /// 直近の一定時間に被弾した回数を数える（2回目からスコアが上がる）。
    /// 被弾そのものは PanicDetector が <see cref="RecordHit"/> で記録する。
    ///
    /// 以前は「前の被弾から3秒以内なら連鎖」と数え、2回で満点になっていた。
    /// 2回の被弾は焦っていなくても起きるため、直近の時間幅での回数にして満点を3回にしている。
    /// </summary>
    [Serializable]
    public class HitChainSignal : PanicSignal
    {
        // スコアが上がり始める被弾回数（1回の被弾は誰にでも起きるため数えない）
        private const int FirstCountedHit = 2;

        // ボスの攻撃の「結果」なので、高くすると「当たる→焦り度が上がる→さらに攻める」のハメになる。低く抑える
        private const float DefaultWeight = 0.3f;

        [Tooltip("被弾を数える直近の時間幅（秒）")]
        [SerializeField]
        [Min(0.1f)]
        private float _window = 5f;

        [Tooltip("直近の時間幅でこの回数以上被弾したら満点")]
        [SerializeField]
        [Min(FirstCountedHit)]
        private int _maxCount = 3;

        // 判定ウィンドウ内の被弾時刻（古い順）
        private readonly Queue<float> _hitTimes = new Queue<float>();

        public HitChainSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.DefenseBreakdown;

        public override string DisplayName => "回避失敗";

        public override float Score =>
            _hitTimes.Count < FirstCountedHit
                ? 0f
                : Mathf.Clamp01(
                    (float)(_hitTimes.Count - FirstCountedHit + 1) / (_maxCount - FirstCountedHit + 1)
                );

        public override float RawValue => _hitTimes.Count;

        public override string RawValueUnit => "直近被弾数";

        /// <summary>直近の時間幅での被弾回数</summary>
        public int HitCount => _hitTimes.Count;

        /// <summary>避けられたはずの被弾をした時に呼ぶ（掴み中の継続ダメージなどは呼ぶ側で除く）。</summary>
        public void RecordHit()
        {
            _hitTimes.Enqueue(Time.time);
            if (_hitTimes.Count >= FirstCountedHit)
            {
                RaiseDetected();
            }
        }

        /// <summary>判定ウィンドウより古い被弾を捨てる。</summary>
        public override void Tick(PanicContext context, float deltaTime)
        {
            while (_hitTimes.Count > 0 && Time.time - _hitTimes.Peek() > _window)
            {
                _hitTimes.Dequeue();
            }
        }

        public override void ResetState()
        {
            _hitTimes.Clear();
        }
    }
}
