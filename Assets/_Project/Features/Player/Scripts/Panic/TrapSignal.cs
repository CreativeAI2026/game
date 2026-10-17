using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標4: 空間喪失 [Lazarus&amp;Folkman 1984]
    /// 交戦中の敵が近くにいる時、移動入力をしているのに思うように動けない状態の継続時間を計測する。
    /// 壁や地形に追い詰められて身動きが取れない状態を検出する。
    ///
    /// 除外するもの:
    ///   - 敵が遠い時（探索中に壁へ引っかかるのは焦りではない）
    ///   - 敵の体に押し当てているだけの時（敵へ向かって押しているのは追い詰められているのではない）
    ///   - 掴まれている間など、移動自体が封じられている時
    /// </summary>
    [Serializable]
    public class TrapSignal : PanicSignal
    {
        // 空間喪失がこの秒数続いたら兆候として通知する
        private const float SignalSeconds = 0.5f;

        // 解消した時、溜まった時間を溜まる速さの何倍で減らすか（引っかかりが外れたらすぐ下がるように）
        private const float RecoveryRate = 2f;

        // 狭い場所で小刻みに位置を直しているだけの場合も含まれうるため中程度にする
        private const float DefaultWeight = 0.5f;

        [Tooltip("この秒数以上、動けない状態が続いたら満点")]
        [SerializeField]
        private float _maxDuration = 3f;

        [Tooltip("実際の速さが、入力から期待される速さのこの割合未満なら「動けていない」とみなす")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _stuckSpeedRatio = 0.3f;

        [Tooltip("交戦中の敵がこの水平距離（m）以内にいる時だけ数える")]
        [SerializeField]
        private float _nearEnemyDistance = 8f;

        private float _timer;
        private bool _isSignalActive;

        public TrapSignal()
            : base(DefaultWeight) { }

        public override PanicCategory Category => PanicCategory.Avoidance;

        public override string DisplayName => "空間喪失";

        public override float Score => Mathf.Clamp01(_timer / _maxDuration);

        public override float RawValue => _timer;

        public override string RawValueUnit => "継続秒";

        /// <summary>空間喪失の継続時間（秒）</summary>
        public float Timer => _timer;

        public override void Tick(PanicContext context, float deltaTime)
        {
            if (IsStuckNearEnemy(context))
            {
                // 満点で頭打ちにして、抜け出した後に下がり切るまでの時間を一定にする
                _timer = Mathf.Min(_maxDuration, _timer + deltaTime);
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

        private bool IsStuckNearEnemy(PanicContext context)
        {
            if (!context.IsInCombat || !context.CanMove || context.IsTouchingEnemy)
                return false;

            if (context.EnemyDistance > _nearEnemyDistance || context.DesiredSpeed <= 0f)
                return false;

            return context.HorizontalSpeed < context.DesiredSpeed * _stuckSpeedRatio;
        }
    }
}
