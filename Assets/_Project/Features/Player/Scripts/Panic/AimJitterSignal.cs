using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標3: ジッター [LeDoux 1994]
    /// 交戦中に弓を構えている間、カメラの左右の向きが「行き過ぎては戻す」を繰り返した回数を数える。
    ///
    /// 以前は角速度の大きさで判定していたが、動き回るボスを素早く滑らかに追う正常な操作でも上がり、
    /// マウスとパッドで値の出方も大きく違った。慌てた照準は「狙いすぎて戻す」の切り返しとして現れるので、
    /// 一定以上振ってから逆向きに切り返した回数で判定する。
    /// </summary>
    [Serializable]
    public class AimJitterSignal : PanicSignal
    {
        // スコアがこの値に達したら兆候として通知する
        private const float SignalScore = 0.5f;

        // これより小さい1フレームの回転は手ぶれ・入力の揺れとして無視する（度）
        private const float NegligibleDeltaDegrees = 0.01f;

        // 正常な追従でも切り返しは起きうるため、ほかの指標より低めの信頼度にする
        private const float DefaultWeight = 0.4f;

        [Tooltip("一方向にこの角度（度）以上振ってから逆向きに戻したら「切り返し」1回と数える。細かな照準調整を除くため")]
        [SerializeField]
        [Min(0f)]
        private float _minSwingDegrees = 3f;

        [Tooltip("切り返しを数える直近の時間幅（秒）")]
        [SerializeField]
        [Min(0.1f)]
        private float _window = 1.5f;

        [Tooltip("直近の時間幅でこの回数以上切り返したら満点")]
        [SerializeField]
        [Min(1)]
        private int _maxReversals = 4;

        // 判定ウィンドウ内の切り返し時刻（古い順）
        private readonly Queue<float> _reversalTimes = new Queue<float>();

        private float _prevYaw;
        private bool _hasPrevYaw;
        private int _swingDirection; // 今振っている向き（+1 / -1、まだ動いていなければ 0）
        private float _swingDegrees; // 今の向きに振った角度の合計
        private bool _isSignalActive;

        public AimJitterSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Indecision;

        public override string DisplayName => "ジッター";

        public override float Score => Mathf.Clamp01((float)_reversalTimes.Count / _maxReversals);

        public override float RawValue => _reversalTimes.Count;

        public override string RawValueUnit => "切り返し回数";

        /// <summary>直近の時間幅での切り返し回数</summary>
        public int ReversalCount => _reversalTimes.Count;

        public override void Tick(PanicContext context, float deltaTime)
        {
            float currentYaw = context.CameraYaw;

            if (!context.IsInCombat || !context.IsAiming || !_hasPrevYaw)
            {
                ClearSwing();
                _isSignalActive = false;
                // 非構え中も前フレームのヨーを更新しておく（構えに入った直後の誤検知を防ぐ）
                _prevYaw = currentYaw;
                _hasPrevYaw = true;
                return;
            }

            // 角度のラッピング（-180〜180に正規化）して、0度/360度をまたぐ回転を正しく扱う
            float delta = Mathf.DeltaAngle(_prevYaw, currentYaw);
            _prevYaw = currentYaw;
            TrackSwing(delta);

            while (_reversalTimes.Count > 0 && Time.time - _reversalTimes.Peek() > _window)
            {
                _reversalTimes.Dequeue();
            }

            UpdateActiveState(ref _isSignalActive, Score >= SignalScore);
        }

        public override void ResetState()
        {
            _hasPrevYaw = false;
            ClearSwing();
            _isSignalActive = false;
        }

        /// <summary>1フレームの回転を、今の振りに足すか、切り返しとして数える。</summary>
        private void TrackSwing(float deltaDegrees)
        {
            if (Mathf.Abs(deltaDegrees) < NegligibleDeltaDegrees)
                return;

            int direction = deltaDegrees > 0f ? 1 : -1;
            if (direction == _swingDirection)
            {
                _swingDegrees += Mathf.Abs(deltaDegrees);
                return;
            }

            if (_swingDirection != 0 && _swingDegrees >= _minSwingDegrees)
            {
                _reversalTimes.Enqueue(Time.time);
            }
            _swingDirection = direction;
            _swingDegrees = Mathf.Abs(deltaDegrees);
        }

        // 構えを解いたら照準の迷いは終わったとみなし、切り返しの記録ごと捨てる
        private void ClearSwing()
        {
            _reversalTimes.Clear();
            _swingDirection = 0;
            _swingDegrees = 0f;
        }
    }
}
