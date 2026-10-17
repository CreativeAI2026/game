using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標6: 回避失敗 [Yerkes-Dodson 注意狭隘化]
    /// 短時間に連続被弾した回数を数える（2回目以降の連続被弾からスコアが上がる）。
    /// 被弾そのものは PanicDetector が <see cref="RecordHit"/> で記録する。
    /// </summary>
    [Serializable]
    public class HitChainSignal : PanicSignal
    {
        [Tooltip("この秒数以内に連続被弾したらカウントを加算する判定ウィンドウ（秒）")]
        [SerializeField]
        private float _chainWindow = 3f;

        [Tooltip("この連続被弾数以上で満点")]
        [SerializeField]
        private int _maxCount = 2;

        private float _lastHitTime = float.NegativeInfinity;
        private int _chainCount;

        public override string DisplayName => "回避失敗";

        public override float Score =>
            _chainCount <= 1 || _maxCount <= 1
                ? 0f
                : Mathf.Clamp01((float)(_chainCount - 1) / (_maxCount - 1));

        /// <summary>連続被弾回数</summary>
        public int ChainCount => _chainCount;

        /// <summary>避けられたはずの被弾をした時に呼ぶ（掴み中の継続ダメージなどは呼ぶ側で除く）。</summary>
        public void RecordHit()
        {
            float now = Time.time;

            // 前回の被弾から一定時間以内なら連続被弾としてカウント
            if (now - _lastHitTime <= _chainWindow)
            {
                _chainCount++;
                RaiseDetected();
            }
            else
            {
                // 単発被弾（連続被弾チェーンの1回目スタート）
                _chainCount = 1;
            }

            _lastHitTime = now;
        }

        /// <summary>最後の被弾から一定時間経過したら連鎖を切る。</summary>
        public override void Tick(PanicContext context, float deltaTime)
        {
            if (_chainCount > 0 && Time.time - _lastHitTime > _chainWindow)
            {
                _chainCount = 0;
            }
        }

        public override void ResetState()
        {
            _chainCount = 0;
            _lastHitTime = float.NegativeInfinity;
        }
    }
}
