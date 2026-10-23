using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 指標4: 空間喪失 [Lazarus&amp;Folkman 1984]
    /// 交戦中、移動入力をしているのに、入力した方向へ思うように進めない状態の継続時間を計測する。
    /// 壁や地形・小物に追い詰められて身動きが取れない状態を検出する。
    ///
    /// 「動けていない」は速さの大きさではなく、入力した方向へどれだけ進めたかで判定する。
    /// 壁に斜めに当たって横へ滑っている時や、段差で小刻みに動いている時は、速さはあっても狙った方向へは進めていないため。
    ///
    /// 怯み・掴み・自分の攻撃などで移動自体が封じられている間は、溜まった時間を保つ（減らさない）。
    /// 壁際で斬られて怯むたびに値が消えると、いちばん追い詰められた場面ほど溜まらなくなるため（2026-10-23 の再現で確認）。
    ///
    /// 除外するもの:
    ///   - 交戦中の敵が遠い時（探索中に壁へ引っかかるのは焦りではない）
    ///   - 敵の体へ向かって押しているだけの時（敵へ踏み込んでいるのは追い詰められているのではない）
    /// </summary>
    [Serializable]
    public class TrapSignal : PanicSignal
    {
        // 空間喪失がこの秒数続いたら兆候として通知する
        private const float SignalSeconds = 0.5f;

        // 入力方向と敵方向がこの内積以上なら「敵へ向かって押している」とみなす（約60度以内）
        private const float TowardEnemyDot = 0.5f;

        // 狭い場所で小刻みに位置を直しているだけの場合も含まれうるため中程度にする
        private const float DefaultWeight = 0.5f;

        [Tooltip("この秒数以上、動けない状態が続いたら満点")]
        [SerializeField]
        private float _maxDuration = 3f;

        [Tooltip(
            "入力した方向へ進めた速さが、入力から期待される速さのこの割合未満なら「動けていない」とみなす"
        )]
        [SerializeField]
        [Range(0f, 1f)]
        private float _stuckProgressRatio = 0.4f;

        [Tooltip(
            "交戦中の敵がこの水平距離（m）以内にいる時だけ数える。ボスの様子見の間合い（最大9m）より広く、触手の射程程度にする"
        )]
        [SerializeField]
        private float _threatDistance = 15f;

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
            if (context.IsInCombat && !context.CanMove)
            {
                // 怯み・掴み・攻撃モーション中は判定できないので、溜まった時間を保つ
            }
            else if (IsStuckNearEnemy(context))
            {
                // 満点で頭打ちにして、抜け出した後に下がり切るまでの時間を一定にする
                _timer = Mathf.Min(_maxDuration, _timer + deltaTime);
            }
            else
            {
                _timer = Mathf.Max(0f, _timer - deltaTime);
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
            if (!context.IsInCombat || context.DesiredSpeed <= 0f)
                return false;

            if (context.EnemyDistance > _threatDistance)
                return false;

            bool isPushingTowardEnemy =
                Vector3.Dot(context.DesiredMoveDirection, context.ToEnemyDirectionXZ) >= TowardEnemyDot;
            if (context.IsTouchingEnemy && isPushingTowardEnemy)
                return false;

            float progressSpeed = Vector3.Dot(context.HorizontalVelocity, context.DesiredMoveDirection);
            return progressSpeed < context.DesiredSpeed * _stuckProgressRatio;
        }
    }
}
