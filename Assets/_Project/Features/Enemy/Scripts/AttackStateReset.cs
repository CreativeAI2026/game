using UnityEngine;

namespace CreativeAI.Gameplay
{
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
