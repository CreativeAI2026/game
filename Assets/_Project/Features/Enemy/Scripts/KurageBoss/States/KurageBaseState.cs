using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの全ステートが共有する基底クラス。
    /// アニメーションイベント(<see cref="KurageAnimationEvent"/>)は今のステートへ届くので、使うステートだけ受け取る。
    /// </summary>
    public abstract class KurageBaseState : IEnemyState
    {
        protected readonly KurageBossController boss;

        private bool _enteredAnimationState;

        protected KurageBaseState(KurageBossController controller)
        {
            boss = controller;
        }

        public virtual void Enter() { }

        public virtual void Update() { }

        public virtual void LateUpdate() { }

        public virtual void Exit() { }

        public virtual void OnAnimationEvent(KurageAnimationEvent animationEvent) { }

        /// <summary>正面をステージへ向ける。プレイヤーを目で追うことはしない(移動のクリップは正面を向いたまま左右へ動く作り)。</summary>
        protected void FaceStage() => boss.Mover.FaceStage(boss.TurnSpeed);

        /// <summary>
        /// 指定した Animator ステートの再生が終わったか。
        /// Trigger を立てた直後はまだ遷移前の Move ステートにいるため、一度対象ステートに入ったことを確認してから、
        /// Move へ戻り切った（または遷移なしで最後まで再生し切った）時点で完了とする。
        /// </summary>
        protected bool IsAnimationFinished(int stateHash)
        {
            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == stateHash)
            {
                _enteredAnimationState = true;
                return !boss.Animator.IsInTransition(0) && stateInfo.normalizedTime >= 1.0f;
            }

            return _enteredAnimationState;
        }
    }
}
