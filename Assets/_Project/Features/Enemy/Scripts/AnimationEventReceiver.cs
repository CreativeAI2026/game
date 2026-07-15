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

            var enemyHitbox = _meleeHitbox.GetComponent<EnemyMeleeHitbox>();
            if (enemyHitbox != null)
            {
                enemyHitbox.EnableHitbox();
                return;
            }

            var playerHitbox = _meleeHitbox.GetComponent<PlayerMeleeHitbox>();
            if (playerHitbox != null)
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

            var enemyHitbox = _meleeHitbox.GetComponent<EnemyMeleeHitbox>();
            if (enemyHitbox != null)
            {
                enemyHitbox.DisableHitbox();
                return;
            }

            var playerHitbox = _meleeHitbox.GetComponent<PlayerMeleeHitbox>();
            if (playerHitbox != null)
            {
                playerHitbox.DisableHitbox();
                return;
            }
        }
    }
}
