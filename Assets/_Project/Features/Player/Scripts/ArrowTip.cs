using SoundKit;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の先端オブジェクトにアタッチする当たり判定コンポーネント。
    /// Rigidbodyはルート（ArrowProjectile）が持つため、衝突イベントはルートから転送される。
    /// 先端を別オブジェクトにすることで、ヒット位置の精度を保ちつつ
    /// ルート側の物理制御と当たり判定ロジックを分離している。
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class ArrowTip : MonoBehaviour
    {
        [Header("弓のダメージ倍率(1.0 = 100%)")]
        [SerializeField]
        private float _bowMultiplier = 1.0f;

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

        [Header("着弾音設定")]
        [Tooltip("敵に当たった時の音")]
        [SerializeField]
        private SoundCue _enemyHitSound;

        [Tooltip("壁などに刺さった時の音")]
        [SerializeField]
        private SoundCue _stickSound;

        [Tooltip("壁などに刺さった音がAIに届く半径（メートル）。")]
        [SerializeField]
        private float _impactRadius = 15f;

        private int _enemyLayer;
        private int _obstacleLayer;
        private ArrowProjectile _projectile;

        void Awake()
        {
            _enemyLayer = LayerMask.NameToLayer(_enemyLayerName);
            _obstacleLayer = LayerMask.NameToLayer(_obstacleLayerName);
            _projectile = GetComponentInParent<ArrowProjectile>();
            Assert.IsNotNull(_projectile, $"{name}: 親に ArrowProjectile がありません");
        }

        public void ProcessCollision(Collision collision)
        {
            if (!_projectile.IsFlying)
                return;

            int layer = collision.gameObject.layer;

            if (layer == _enemyLayer)
            {
                HitEnemy(collision);
            }
            else if (collision.gameObject.TryGetComponent(out IArrowHittable hittable))
            {
                // IArrowHittable を実装したオブジェクト（スポーンボタン等のギミック）に命中した場合の処理
                hittable.OnArrowHit();
                _projectile.ReturnToPool();
            }
            else if (
                layer == _obstacleLayer
                || (
                    !collision.gameObject.CompareTag("Player")
                    && layer != LayerMask.NameToLayer("Ignore Raycast")
                )
            )
            {
                // PlayerとIgnoreRaycast以外の全オブジェクトに刺さる仕様。
                // 自分自身（Player）や非物理オブジェクト（Ignore Raycast）への誤刺さりを防ぐ
                StickToSurface(collision);
            }
        }

        private void HitEnemy(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out IDamageable enemy))
            {
                // ダメージ計算はPlayerStatusに委譲し、装備やバフの影響を一元管理する
                float finalDamage = _projectile.Owner.RollDamage(
                    _bowMultiplier,
                    out bool isCritical
                );
                enemy.TakeDamage(finalDamage, isCritical);
            }

            ContactPoint contact = collision.contacts[0];
            SpawnHitEffect(contact.point, contact.normal);
            WorldSoundPlayer.Play(_enemyHitSound, contact.point, this);

            _projectile.ReturnToPool();
        }

        private void StickToSurface(Collision collision)
        {
            _projectile.StopFlying();
            _projectile.transform.SetParent(collision.transform, true);

            Vector3 impactPoint = collision.contacts[0].point;
            WorldSoundPlayer.Play(_stickSound, impactPoint, this);

            // 着弾位置をSoundEventBusで敵AIに通知する
            SoundEventBus.Emit(new SoundEventData(SoundType.ArrowHit, impactPoint, _impactRadius));
        }

        // TODO : 現在はダメージを与えたときにしか生成されないが、のちに壁に当たったときにも別のエフェクトを生成させる
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
