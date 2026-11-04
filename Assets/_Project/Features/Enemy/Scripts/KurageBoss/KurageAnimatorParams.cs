using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの AnimatorController(KurageBossAnimator)のパラメータ名。
    /// 文字列の直書きを散らさず、ここを唯一の対応表にする。Animator 側で名前を変えたらここも変える。
    /// </summary>
    public static class KurageAnimatorParams
    {
        /// <summary>移動の向き(-1 で左移動、+1 で右移動のクリップ)。</summary>
        public static readonly int MoveDirection = Animator.StringToHash("MoveDirection");

        public static readonly int Stab = Animator.StringToHash("Stab");
        public static readonly int Drill = Animator.StringToHash("Drill");
        public static readonly int Ring = Animator.StringToHash("Ring");

        /// <summary>怯み・死亡はクリップが後から来る。ステートとパラメータだけ先に用意してある。</summary>
        public static readonly int Flinch = Animator.StringToHash("Flinch");
        public static readonly int Die = Animator.StringToHash("Die");
    }
}
