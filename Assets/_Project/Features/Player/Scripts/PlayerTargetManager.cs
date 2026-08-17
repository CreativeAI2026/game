using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤー周囲の最も近い敵を毎フレーム検出し、一元管理する。
    /// HeadLookControllerなど複数のシステムが同じターゲットを参照できるよう、
    /// 索敵ロジックをここに集約している。
    /// </summary>
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
