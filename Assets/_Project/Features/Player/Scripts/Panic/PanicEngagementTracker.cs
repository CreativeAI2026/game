using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度の計測対象となる「交戦相手」を決めて保持する。
    /// 最寄りの敵（PlayerTargetManager の索敵半径内）を毎フレーム選び直すだけだと、半径の外へ逃げた瞬間に
    /// 交戦相手がいなくなり逃避を測れないため、一度交戦した敵は交戦半径の外へ離れるか警戒を解くまで持ち続ける。
    /// </summary>
    [Serializable]
    public class PanicEngagementTracker
    {
        [Tooltip(
            "一度交戦した敵を、この水平距離（m）まで離れても交戦相手として持ち続ける。索敵半径（PlayerTargetManager）より大きくする"
        )]
        [SerializeField]
        private float _engagementRadius = 30f;

        // 最寄りの敵のコライダーが変わった時だけ親の EnemyBaseController を引き直す
        private Collider _cachedNearestCollider;
        private EnemyBaseController _cachedNearestEnemy;

        /// <summary>現在の交戦相手。いなければ null。</summary>
        public EnemyBaseController EngagedEnemy { get; private set; }

        /// <summary>
        /// 最寄りの敵のコライダーとプレイヤー位置から、交戦相手を更新する。
        /// 最寄りの敵が交戦可能ならそれに切り替え、そうでなければ今の交戦相手を保持できるか確かめる。
        /// </summary>
        public void Refresh(Collider nearestEnemyCollider, Vector3 playerPosition)
        {
            EnemyBaseController nearest = ResolveEnemy(nearestEnemyCollider);
            if (IsEngageable(nearest))
            {
                EngagedEnemy = nearest;
                return;
            }

            if (
                !IsEngageable(EngagedEnemy)
                || HorizontalDistance(EngagedEnemy.transform.position, playerPosition) > _engagementRadius
            )
            {
                EngagedEnemy = null;
            }
        }

        /// <summary>交戦相手を外す（戦闘終了時のリセット用）。</summary>
        public void Clear()
        {
            EngagedEnemy = null;
        }

        private EnemyBaseController ResolveEnemy(Collider enemyCollider)
        {
            if (enemyCollider != _cachedNearestCollider)
            {
                _cachedNearestCollider = enemyCollider;
                _cachedNearestEnemy =
                    enemyCollider != null
                        ? enemyCollider.GetComponentInParent<EnemyBaseController>()
                        : null;
            }
            return _cachedNearestEnemy;
        }

        /// <summary>
        /// プレイヤーに気付いていて（警戒中）、生きている敵か。
        /// 敵の操作クラスを持たない対象は、気付いているか分からないため交戦相手にしない。
        /// </summary>
        private static bool IsEngageable(EnemyBaseController enemy)
        {
            return enemy != null
                && enemy.isActiveAndEnabled
                && enemy.IsAlerted
                && !enemy.Status.IsDead;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
