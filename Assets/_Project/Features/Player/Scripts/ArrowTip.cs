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

        [Tooltip("壁などに刺さった時のエフェクトPrefab")]
        [SerializeField]
        private GameObject _stickEffect;

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
            ContactPoint contact = collision.GetContact(0);

            // 敵が受け止めたら、ダメージもヒットの演出も出さずに矢を消す(受け止めた演出は敵側が出す)
            if (
                collision.gameObject.TryGetComponent(out IPlayerAttackGuard guard)
                && guard.ReceivePlayerAttack(PlayerAttackKind.Arrow, contact.point)
            )
            {
                _projectile.ReturnToPool();
                return;
            }

            if (collision.gameObject.TryGetComponent(out IDamageable enemy))
            {
                // ダメージ計算はPlayerStatusに委譲し、装備やバフの影響を一元管理する
                float finalDamage = _projectile.Owner.RollDamage(
                    _bowMultiplier,
                    out bool isCritical
                );
                enemy.TakeDamage(finalDamage, isCritical);
            }

            SpawnEffect(_hitEffect, contact.point, contact.normal);
            WorldSoundPlayer.Play(_enemyHitSound, contact.point, this);

            _projectile.ReturnToPool();
        }

        private void StickToSurface(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 impactPoint = contact.point;

            // 衝突相手の子にすると非一様スケールで矢のメッシュが歪むため、ワールド空間のまま着弾点・入射角で固定する
            _projectile.StickAt(impactPoint);

            SpawnEffect(_stickEffect, impactPoint, contact.normal);
            WorldSoundPlayer.Play(_stickSound, impactPoint, this);

            // 着弾位置をNoiseEventBusで敵AIに通知する
            NoiseEventBus.Emit(new NoiseEvent(NoiseType.ArrowHit, impactPoint, _impactRadius));
        }

        /// <summary>エフェクトの前方(+Z)を、当たった面の外向きに合わせて再生する。</summary>
        private static void SpawnEffect(GameObject effect, Vector3 position, Vector3 normal)
        {
            if (effect == null)
                return;
            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            EffectPool.Play(effect, position, rotation);
        }
    }
}
