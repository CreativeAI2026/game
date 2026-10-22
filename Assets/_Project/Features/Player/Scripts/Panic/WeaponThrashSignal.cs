using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標8: 武器の空回し
    /// 交戦中、短い時間に何度も武器を切り替えた回数を数える。どの武器で戦うか決めきれない迷いの表れとみなす。
    /// 切替そのものは PanicDetector が <see cref="RecordSwitch"/> で記録する（入力による切替だけ）。
    /// </summary>
    [Serializable]
    public class WeaponThrashSignal : PanicSignal
    {
        // スコアが上がり始める切替回数（目当ての武器まで1〜2回送るのは普通の操作なので数えない）
        private const int FirstCountedSwitch = 3;

        // 目当ての武器が遠い並び順だと冷静でも3回以上送ることがあるため、低めの信頼度にする
        private const float DefaultWeight = 0.4f;

        [Tooltip("切替を数える直近の時間幅（秒）")]
        [SerializeField]
        [Min(0.1f)]
        private float _window = 4f;

        [Tooltip("直近の時間幅でこの回数以上切り替えたら満点")]
        [SerializeField]
        [Min(FirstCountedSwitch)]
        private int _maxCount = 4;

        // 判定ウィンドウ内の切替時刻（古い順）
        private readonly Queue<float> _switchTimes = new Queue<float>();

        public WeaponThrashSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Indecision;

        // デバッグUIの名前欄に収まるよう、ほかの指標と同じ4文字にしている（指標としての呼び名は「武器の空回し」）
        public override string DisplayName => "武器切替";

        public override float Score =>
            _switchTimes.Count < FirstCountedSwitch
                ? 0f
                : Mathf.Clamp01(
                    (float)(_switchTimes.Count - FirstCountedSwitch + 1)
                        / (_maxCount - FirstCountedSwitch + 1)
                );

        public override float RawValue => _switchTimes.Count;

        public override string RawValueUnit => "直近切替数";

        /// <summary>交戦中にプレイヤーの入力で武器が切り替わった時に呼ぶ。</summary>
        public void RecordSwitch()
        {
            _switchTimes.Enqueue(Time.time);
            if (_switchTimes.Count >= FirstCountedSwitch)
            {
                RaiseDetected();
            }
        }

        public override void Tick(PanicContext context, float deltaTime)
        {
            while (_switchTimes.Count > 0 && Time.time - _switchTimes.Peek() > _window)
            {
                _switchTimes.Dequeue();
            }
        }

        public override void ResetState()
        {
            _switchTimes.Clear();
        }
    }
}
