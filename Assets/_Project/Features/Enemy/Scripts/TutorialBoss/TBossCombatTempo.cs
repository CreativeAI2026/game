using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 交戦中の「次に攻撃を仕掛ける時刻」を、追跡・様子見の状態をまたいで保持する。
    /// 様子見ステートごとに待ち時間を持たせると、プレイヤーが離れて追跡に戻るたびに待ち時間がやり直しになり、
    /// 走って逃げ続けるだけで一度も攻撃されなくなるため、交戦全体で1本の時計として持つ。
    /// TBossPerception と同じく TutorialBossController が所有するプレーンなクラス。
    /// </summary>
    public class TBossCombatTempo
    {
        // 攻撃間隔にランダム幅を持たせ、読まれやすい一定間隔での攻撃を避ける（基準間隔に対する倍率）
        private const float IntervalMinScale = 0.6f;
        private const float IntervalMaxScale = 1.4f;

        private float _nextAttackTime;
        private bool _isScheduled;

        /// <summary>次の攻撃を仕掛けてよい時刻を過ぎたか。交戦に入っていなければ false。</summary>
        public bool IsAttackDue => _isScheduled && Time.time >= _nextAttackTime;

        /// <summary>
        /// 交戦に入った時に呼ぶ。既に時計が動いていれば何もしない（追跡⇔様子見の往復で待ち時間を延ばさない）。
        /// </summary>
        public void EnsureScheduled(float baseInterval)
        {
            if (!_isScheduled)
            {
                Schedule(baseInterval);
            }
        }

        /// <summary>攻撃を仕掛けた時に呼び、次の攻撃までの待ち時間を決め直す。</summary>
        public void Schedule(float baseInterval)
        {
            _nextAttackTime =
                Time.time + Random.Range(baseInterval * IntervalMinScale, baseInterval * IntervalMaxScale);
            _isScheduled = true;
        }

        /// <summary>交戦が終わった時（見失って巡回へ戻る等）に呼ぶ。次の交戦では待ち時間を新しく決める。</summary>
        public void Clear()
        {
            _isScheduled = false;
        }
    }
}
