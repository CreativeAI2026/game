using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Animator が指定のステートにいるか(遷移中を含む)を調べる拡張メソッド。
    /// </summary>
    public static class AnimatorStateExtensions
    {
        /// <summary>
        /// 指定のステートを再生中か、そこへ遷移中か。そのステートから抜けかけの遷移中も再生中に数える。
        /// info には、再生中なら今のステート、遷移中なら遷移先のステートの情報が入る。
        /// </summary>
        public static bool IsPlayingOrEntering(
            this Animator animator,
            int layer,
            int stateHash,
            out AnimatorStateInfo info
        )
        {
            info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == stateHash)
                return true;

            if (!animator.IsInTransition(layer))
                return false;

            info = animator.GetNextAnimatorStateInfo(layer);
            return info.shortNameHash == stateHash;
        }

        /// <summary>指定のステートを再生中か、そこへ遷移中か。抜けかけの遷移中も再生中に数える。</summary>
        public static bool IsPlayingOrEntering(this Animator animator, int layer, int stateHash) =>
            animator.IsPlayingOrEntering(layer, stateHash, out _);

        /// <summary>
        /// 遷移中は遷移先で、そうでなければ今のステートで、指定のステートかを調べる。
        /// IsPlayingOrEntering と違い、そのステートから抜けかけの遷移中は数えない。info には調べたステートの情報が入る。
        /// </summary>
        public static bool IsHeadingTo(
            this Animator animator,
            int layer,
            int stateHash,
            out AnimatorStateInfo info
        )
        {
            info = animator.IsInTransition(layer)
                ? animator.GetNextAnimatorStateInfo(layer)
                : animator.GetCurrentAnimatorStateInfo(layer);
            return info.shortNameHash == stateHash;
        }
    }
}
