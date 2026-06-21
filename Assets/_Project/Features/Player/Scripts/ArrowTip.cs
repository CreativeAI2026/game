using UnityEngine;

namespace CreativeAI.Gameplay
{
    [RequireComponent(typeof(CapsuleCollider))]
    public class ArrowTip : MonoBehaviour
    {
        [Header("弓のダメージ倍率(1.0 = 100%)")]
        [SerializeField]
        private float _bowMultiplier = 0.8f;

        [Header("衝突設定")]
        [Tooltip("Enemy レイヤー名（プロジェクトの設定に合わせる）")]
        [SerializeField]
        private string _enemyLayerName = "Enemy";

        [Tooltip("Obstacle レイヤー名（プロジェクトの設定に合わせる）")]
        [SerializeField]
        private string _obstacleLayerName = "Obstacle";

        [Header("ヒットエフェクト")]
        [Tooltip("敵に命中した時のエフェクトPrefab")]
        [SerializeField]
        private GameObject _hitEffect;

        private PlayerStatus _playerStatus;
        private int _enemyLayer;
        private int _obstacleLayer;
        private ArrowProjectile _projectile;

        void Awake()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerStatus = player.GetComponent<PlayerStatus>();
            }

            _enemyLayer = LayerMask.NameToLayer(_enemyLayerName);
            _obstacleLayer = LayerMask.NameToLayer(_obstacleLayerName);
            _projectile = GetComponentInParent<ArrowProjectile>();
        }

        public void ProcessCollision(Collision collision)
        {
            if (_projectile != null && !_projectile.IsFlying)
                return;

            int layer = collision.gameObject.layer;

            if (layer == _enemyLayer)
            {
                HitEnemy(collision);
            }
            else if (collision.gameObject.TryGetComponent(out IArrowHittable hittable))
            {
                // IArrowHittable を実装したオブジェクト（スポーンボタンなど）に命中
                hittable.OnArrowHit();
                // 矢は貫通させず消滅
                if (_projectile != null)
                {
                    Destroy(_projectile.gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else if (
                layer == _obstacleLayer
                || (
                    !collision.gameObject.CompareTag("Player")
                    && layer != LayerMask.NameToLayer("Ignore Raycast")
                )
            )
            {
                // PlayerとIgnoreRaycast以外は基本的に刺さるようにする
                StickToSurface(collision);
            }
        }

        private void HitEnemy(Collision collision)
        {
            // 敵のIDamageableを取得
            if (collision.gameObject.TryGetComponent(out IDamageable enemy))
            {
                if (_playerStatus != null)
                {
                    // プレイヤーのStatusに倍率を渡して、最終ダメージを計算してもらう
                    float finalDamage = _playerStatus.RollDamage(
                        _bowMultiplier,
                        out bool isCritical
                    );
                    // 敵にダメージを与える
                    enemy.TakeDamage(finalDamage, isCritical);
                }
            }

            ContactPoint contact = collision.contacts[0];
            SpawnHitEffect(contact.point, contact.normal);

            if (_projectile != null)
            {
                Destroy(_projectile.gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void StickToSurface(Collision collision)
        {
            if (_projectile != null)
            {
                _projectile.StopFlying();
                _projectile.transform.SetParent(collision.transform, true);
            }
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (_hitEffect == null)
                return;
            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            Instantiate(_hitEffect, position, rotation);
        }
    }
}
