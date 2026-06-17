using UnityEngine;

namespace CreativeAI.Gameplay
{
    // プレイヤーが「どの敵を認識しているか」を一元管理するクラス
    public class PlayerTargetManager : MonoBehaviour
    {
        [Header("設定")]
        public float searchRadius = 10f;
        public LayerMask enemyLayer;

        [Header("現在のターゲット (Read Only)")]
        [HideInInspector]
        public Transform currentTarget;

        private void Update()
        {
            // 常に最も近い敵を検索して更新する（パフォーマンスが気になる場合は数フレームに1回に間引いてもOKです）
            FindNearestEnemy();
        }

        private void FindNearestEnemy()
        {
            Collider[] hitColliders = Physics.OverlapSphere(
                transform.position,
                searchRadius,
                enemyLayer
            );
            Transform nearest = null;
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
                    nearest = hitCollider.transform;
                }
            }

            currentTarget = nearest;
        }
    }
}
