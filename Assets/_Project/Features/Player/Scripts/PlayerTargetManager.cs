using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤー周囲の敵の索敵を一元管理する。敵のレイヤー指定はここ1か所だけに持つ。
    /// - 視線(HeadLookController)・焦り検知(PanicDetector)用に、最寄りの敵を毎フレーム更新する
    /// - 武器(接近・投擲の狙い)は <see cref="FindNearest"/> に自分の索敵半径を渡して使う
    /// </summary>
    public class PlayerTargetManager : MonoBehaviour
    {
        [Header("設定")]
        [Tooltip("毎フレーム追う「最寄りの敵」の索敵半径（m）")]
        public float searchRadius = 10f;

        [Tooltip("敵のレイヤー。Enemy のみにする(敵の攻撃判定・弾のレイヤーを含めると、それを敵として狙ってしまう)")]
        public LayerMask enemyLayer;

        /// <summary>現在の最寄りの敵(searchRadius 以内)。いなければ null。</summary>
        public Transform CurrentTarget => CurrentTargetCollider != null ? CurrentTargetCollider.transform : null;

        /// <summary>現在の最寄りの敵のコライダー。</summary>
        public Collider CurrentTargetCollider { get; private set; }

        /// <summary>
        /// 現在の最寄りの敵の「見る位置」。足元(Transform の原点)ではなくコライダーの中心。
        /// ターゲットがいなければ null。
        /// </summary>
        public Vector3? CurrentTargetPoint =>
            CurrentTargetCollider != null ? CurrentTargetCollider.bounds.center : (Vector3?)null;

        private void Update()
        {
            CurrentTargetCollider = FindNearestCollider(searchRadius);
        }

        /// <summary>指定した半径内で最寄りの敵を返す。いなければ null。</summary>
        public Transform FindNearest(float radius)
        {
            Collider nearest = FindNearestCollider(radius);
            return nearest != null ? nearest.transform : null;
        }

        private Collider FindNearestCollider(float radius)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, radius, enemyLayer);
            Collider nearest = null;
            float minDistance = float.MaxValue;

            foreach (var hitCollider in hitColliders)
            {
                float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = hitCollider;
                }
            }

            return nearest;
        }
    }
}
