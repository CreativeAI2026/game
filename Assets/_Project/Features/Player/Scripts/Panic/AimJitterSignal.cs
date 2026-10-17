using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標3: ジッター [LeDoux 1994]
    /// 交戦中に弓を構えている間のカメラのヨー角速度（°/秒）をならして計算し、急激な動きを検出する。
    /// </summary>
    [Serializable]
    public class AimJitterSignal : PanicSignal
    {
        // 角速度が満点の何割を超えたら兆候として通知するか
        private const float SignalRatio = 0.5f;

        [Tooltip("弓構え中にこの角速度（°/秒）以上のカメラ移動があればジッター満点")]
        [SerializeField]
        private float _maxAngularSpeed = 180f;

        [Tooltip(
            "角速度をならす時間（秒）。マウスはフレームごとの移動量がばらつくため、1フレームだけの跳ねで満点にならないようにする"
        )]
        [SerializeField]
        [Min(0.01f)]
        private float _smoothTime = 0.1f;

        private float _prevYaw;
        private bool _hasPrevYaw;
        private float _angularSpeed;
        private float _score;
        private bool _isSignalActive;

        public override string DisplayName => "ジッター";

        public override float Score => _score;

        /// <summary>現在のカメラ角速度（°/秒、ならした値）</summary>
        public float AngularSpeed => _angularSpeed;

        public override void Tick(PanicContext context, float deltaTime)
        {
            float currentYaw = context.CameraYaw;

            if (!context.IsInCombat || !context.IsAiming || !_hasPrevYaw)
            {
                _score = 0f;
                _angularSpeed = 0f;
                _isSignalActive = false;
                // 非構え中も前フレームのヨーを更新しておく（構えに入った直後の誤検知を防ぐ）
                _prevYaw = currentYaw;
                _hasPrevYaw = true;
                return;
            }

            // 角度のラッピング（-180〜180に正規化）して急反転を正確に計測する
            float delta = Mathf.DeltaAngle(_prevYaw, currentYaw);
            float instantSpeed = Mathf.Abs(delta) / Mathf.Max(deltaTime, 0.001f);
            _prevYaw = currentYaw;

            // フレームレートに依存しない指数平滑（_smoothTime 秒で約63%追従）
            float blend = 1f - Mathf.Exp(-deltaTime / _smoothTime);
            _angularSpeed = Mathf.Lerp(_angularSpeed, instantSpeed, blend);

            _score = Mathf.Clamp01(_angularSpeed / _maxAngularSpeed);

            UpdateActiveState(ref _isSignalActive, _angularSpeed >= _maxAngularSpeed * SignalRatio);
        }

        public override void ResetState()
        {
            _hasPrevYaw = false;
            _angularSpeed = 0f;
            _score = 0f;
            _isSignalActive = false;
        }
    }
}
