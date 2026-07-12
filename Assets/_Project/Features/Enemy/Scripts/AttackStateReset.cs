using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 攻撃アニメーションが怯みや死亡によって中断された場合、ヒットボックスが有効なまま残り続けるのを防ぐ。
    /// 攻撃ステートのAnimatorStateに付与して使用する。
    /// </summary>
    public class AttackStateReset : StateMachineBehaviour
    {
        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex
        )
        {
            var hitbox = animator.GetComponentInChildren<EnemyMeleeHitbox>();
            if (hitbox != null)
            {
                hitbox.DisableHitbox();
            }
        }
    }
}
