using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 巡回・捜索・音の調査で周りを見渡すモーション（Animator の Look）を出し、見渡し終わるまでその場に止めておく。
    /// Look のクリップは約4.4秒あり、各ステートはそれより早く移動を再開するため、見渡しの間は動かさないことをここで保証する。
    /// 追跡・攻撃などに移った時の見渡しの打ち切りは Animator 側の遷移（動き出したら Look を抜ける・攻撃や怯みは Any State から入る）に任せ、
    /// ここでは足止めを解くだけにする。
    /// TBossPerception と同じく TutorialBossController が所有するプレーンなクラス。
    /// </summary>
    public class TBossLookAround
    {
        private const string LookTrigger = "Look";
        private const string LookStateName = "Look";

        // 見渡しに入れなかった場合などに、止めっぱなしにしないための保険（クリップ長＋遷移の余裕。秒）
        private const float HoldTimeout = 6f;

        private readonly Animator _animator;
        private float _holdTimer;

        /// <summary>見渡しモーションの最中（または入る直前）で、移動を止めておくべきか。</summary>
        public bool IsHolding { get; private set; }

        public TBossLookAround(Animator animator)
        {
            _animator = animator;
        }

        /// <summary>見渡しモーションを始め、終わるまで足止めする。既に見渡し中ならトリガーを積まない。</summary>
        public void Play()
        {
            if (_animator == null)
            {
                return;
            }

            if (!IsInLookState())
            {
                _animator.SetTrigger(LookTrigger);
            }
            IsHolding = true;
            _holdTimer = 0f;
        }

        /// <summary>
        /// 毎フレーム呼ぶ。lookAllowed が false（追跡・攻撃など）になったら足止めを解く。
        /// 見渡しが終わった時も足止めを解く。足止めの状態が変わったら true を返す。
        /// </summary>
        public bool Tick(bool lookAllowed, float deltaTime)
        {
            if (!IsHolding)
            {
                return false;
            }

            _holdTimer += deltaTime;
            // トリガーを引いた直後は、Animator がまだ Look へ切り替わり始めていない。
            // その間はトリガーが使われずに残っているので、それも見渡し中とみなす（時間で待つとフレームが重い時に外れる）
            bool stillLooking = IsInLookState() || _animator.GetBool(LookTrigger);
            if (lookAllowed && stillLooking && _holdTimer < HoldTimeout)
            {
                return false;
            }

            IsHolding = false;
            return true;
        }

        private bool IsInLookState()
        {
            return _animator.GetCurrentAnimatorStateInfo(0).IsName(LookStateName)
                || _animator.GetNextAnimatorStateInfo(0).IsName(LookStateName);
        }
    }
}
