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

        [Tooltip(
            "敵のレイヤー。Enemy のみにする(敵の攻撃判定・弾のレイヤーを含めると、それを敵として狙ってしまう)"
        )]
        public LayerMask enemyLayer;

        /// <summary>現在の最寄りの敵(searchRadius 以内)。いなければ null。</summary>
        public Transform CurrentTarget =>
            CurrentTargetCollider != null ? CurrentTargetCollider.transform : null;

        /// <summary>現在の最寄りの敵のコライダー。</summary>
        public Collider CurrentTargetCollider { get; private set; }

        // 「見る位置」の高さ。コライダーの下端から上端までのうち、この割合の位置を見る。
        // 中心(0.5)だと腰のあたりになり、背の高い敵では足元を見ているように見えたため、顔の高さに寄せている
        // (TutorialBoss ではこの高さが頭のボーンとほぼ一致する)
        private const float LookHeightRatio = 0.75f;

        /// <summary>
        /// 現在の最寄りの敵の「見る位置」。コライダーの下から <see cref="LookHeightRatio"/> の高さ(顔のあたり)。
        /// ターゲットがいなければ null。
        /// </summary>
        public Vector3? CurrentTargetPoint
        {
            get
            {
                if (CurrentTargetCollider == null)
                    return null;

                Bounds bounds = CurrentTargetCollider.bounds;
                return new Vector3(
                    bounds.center.x,
                    bounds.min.y + bounds.size.y * LookHeightRatio,
                    bounds.center.z
                );
            }
        }

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
                float distance = Vector3.Distance(
                    transform.position,
                    hitCollider.transform.position
                );
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
