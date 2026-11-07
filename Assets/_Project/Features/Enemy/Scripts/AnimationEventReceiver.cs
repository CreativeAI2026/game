using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// Animatorのアニメーションイベントは同一GameObject上のスクリプトしか呼び出せないため、
    /// Animatorがアタッチされたルートオブジェクトに配置し、子オブジェクトのヒットボックスへイベントを中継する。
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        [Header("武器のヒットボックス")]
        [Tooltip("子オブジェクトにある MeleeHitbox をドラッグ＆ドロップ")]
        [SerializeField]
        private GameObject _meleeHitbox;

        public void TriggerEnableHitbox()
        {
            if (_meleeHitbox == null)
            {
                return;
            }

            if (_meleeHitbox.TryGetComponent(out EnemyMeleeHitbox enemyHitbox))
            {
                enemyHitbox.EnableHitbox();
                return;
            }

            if (_meleeHitbox.TryGetComponent(out PlayerMeleeHitbox playerHitbox))
            {
                playerHitbox.EnableHitbox();
                return;
            }
        }

        public void TriggerDisableHitbox()
        {
            if (_meleeHitbox == null)
            {
                return;
            }

            if (_meleeHitbox.TryGetComponent(out EnemyMeleeHitbox enemyHitbox))
            {
                enemyHitbox.DisableHitbox();
                return;
            }

            if (_meleeHitbox.TryGetComponent(out PlayerMeleeHitbox playerHitbox))
            {
                playerHitbox.DisableHitbox();
                return;
            }
        }
    }
}
