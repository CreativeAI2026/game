using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 液体攻撃で発射される球形プロジェクタイル。
    /// Rigidbodyによる水平投射＋重力落下で飛び、プレイヤーに直撃したら直接ダメージ＋毒を付与し、
    /// 地面・障害物に着弾したら毒付与エリアを生成する。
    /// コライダーは isTrigger = true で運用する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MidBossLiquidProjectile : MonoBehaviour
    {
        private Rigidbody _rb;
        private GameObject _damageAreaPrefab;
        private float _damageAreaDuration;
        private float _damageAreaInterval;
        private float _directDamage;
        private float _poisonDuration;
        private float _poisonDamagePerTick;
        private float _poisonTickInterval;
        private bool _hasLanded;

        // 着弾しないまま消えるまでの最大生存時間（秒）
        private const float MaxLifetime = 8f;
        private float _lifetime;

        [SerializeField]
        [Tooltip(
            "プレイヤーのレイヤー。ここで指定したレイヤーに衝突するとプレイヤーへの直接ダメージ＋毒付与を行う。"
        )]
        private LayerMask _playerLayer = ~0;

        [SerializeField]
        [Tooltip("ここで指定したレイヤーに衝突すると毒付与エリアを生成する。")]
        private LayerMask _obstacleLayer = ~0;

        [SerializeField]
        [Tooltip("プレイヤーに直撃したときに生成するヒットエフェクト。")]
        private GameObject _playerHitEffect;

        [SerializeField]
        [Tooltip("地面・障害物に着弾したときに落下点で生成する衝突エフェクト。")]
        private GameObject _landEffect;

        /// <summary>
        /// 液体弾を初期化して発射する。
        /// 水平投射の初速は spawnPos → targetPos の落下時間から逆算して計算する。
        /// </summary>
        /// <param name="spawnPos">発射位置</param>
        /// <param name="targetPos">着弾目標（地面上の点）</param>
        /// <param name="directDamage">プレイヤー直撃時のダメージ量</param>
        /// <param name="damageAreaPrefab">着弾後に生成する毒付与エリアプレハブ</param>
        /// <param name="damageAreaDuration">毒付与エリア持続時間（秒）</param>
        /// <param name="damageAreaInterval">毒付与エリアの判定間隔（秒）</param>
        /// <param name="poisonDuration">毒の持続時間（秒）</param>
        /// <param name="poisonDamagePerTick">毒の1tick あたりのダメージ量</param>
        /// <param name="poisonTickInterval">毒のダメージ間隔（秒）</param>
        public void Initialize(
            Vector3 spawnPos,
            Vector3 targetPos,
            float directDamage,
            GameObject damageAreaPrefab,
            float damageAreaDuration,
            float damageAreaInterval,
            float poisonDuration,
            float poisonDamagePerTick,
            float poisonTickInterval
        )
        {
            _damageAreaPrefab = damageAreaPrefab;
            _damageAreaDuration = damageAreaDuration;
            _damageAreaInterval = damageAreaInterval;
            _directDamage = directDamage;
            _poisonDuration = poisonDuration;
            _poisonDamagePerTick = poisonDamagePerTick;
            _poisonTickInterval = poisonTickInterval;
            _hasLanded = false;
            _lifetime = 0f;

            transform.position = spawnPos;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _rb.isKinematic = false;

            // 水平投射の初速計算
            // 高低差 h = 発射位置.y - 着弾位置.y（ボスのほうが高い前提）
            float h = spawnPos.y - targetPos.y;
            float g = Mathf.Abs(Physics.gravity.y);

            Vector3 horizontalDiff = targetPos - spawnPos;
            horizontalDiff.y = 0f;
            float horizontalDist = horizontalDiff.magnitude;

            // h <= 0（発射位置が着弾点より低い）の場合はデフォルト速度で投げる
            float horizontalSpeed;
            if (h > 0.01f)
            {
                float fallTime = Mathf.Sqrt(2f * h / g);
                horizontalSpeed = fallTime > 0.001f ? horizontalDist / fallTime : 5f;
            }
            else
            {
                // 発射位置が低い場合は放物線投射（斜め45度相当）
                horizontalSpeed = Mathf.Sqrt(g * horizontalDist * 0.5f);
            }

            Vector3 horizontalDir =
                horizontalDiff.sqrMagnitude > 0.001f ? horizontalDiff.normalized : Vector3.forward;

            _rb.linearVelocity = horizontalDir * horizontalSpeed;
        }

        private void Update()
        {
            if (_hasLanded)
                return;

            _lifetime += Time.deltaTime;
            if (_lifetime >= MaxLifetime)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// isTrigger = true のコライダーが他のコライダーに侵入したときに呼ばれる。
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_hasLanded)
                return;

            int hitLayer = 1 << other.gameObject.layer;

            if ((hitLayer & _playerLayer.value) != 0)
            {
                // プレイヤー直撃：直接ダメージ＋ヒットエフェクト＋毒付与
                // 毒付与エリアは生成しない
                if (other.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(_directDamage, false);
                }
                else
                {
                    // コライダーが子オブジェクトにある場合も考慮
                    var damageableInParent = other.GetComponentInParent<IDamageable>();
                    if (damageableInParent != null)
                    {
                        damageableInParent.TakeDamage(_directDamage, false);
                    }
                }

                if (_playerHitEffect != null)
                {
                    // OnTriggerEnter では接触点が取れないため transform.position で代替
                    Instantiate(_playerHitEffect, transform.position, Quaternion.identity);
                }

                var effectHandler = other.GetComponentInParent<PlayerStatusEffectHandler>();
                if (effectHandler != null)
                {
                    effectHandler.ApplyPoison(
                        _poisonDuration,
                        _poisonDamagePerTick,
                        _poisonTickInterval
                    );
                }

                Destroy(gameObject);
            }
            else if ((hitLayer & _obstacleLayer.value) != 0)
            {
                // 地面・障害物に着弾したら毒付与エリアを生成
                _hasLanded = true;

                if (_landEffect != null)
                {
                    Instantiate(_landEffect, transform.position, Quaternion.identity);
                }

                // Triggerでは法線が取れないため、移動方向にRaycastを飛ばして着弾面の法線を取得する
                Vector3 moveDir = _rb.linearVelocity.normalized;
                Vector3 normal = Vector3.up; // デフォルトは上向き（床）
                Vector3 spawnPos = transform.position;

                // 念のため少し後ろからRayを飛ばす
                if (
                    Physics.Raycast(
                        transform.position - moveDir * 0.5f,
                        moveDir,
                        out RaycastHit hit,
                        2f,
                        _obstacleLayer
                    )
                )
                {
                    normal = hit.normal;
                    spawnPos = hit.point;
                }

                SpawnPoisonArea(spawnPos, normal);
                Destroy(gameObject);
            }
        }

        private void SpawnPoisonArea(Vector3 position, Vector3 normal)
        {
            if (_damageAreaPrefab == null)
                return;

            // 地面（上向き）を基準に作られたプレハブを、着弾した壁や床の角度（法線）に合わせる
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

            // 壁の中や床下に埋まらないよう、法線方向にわずかに浮かせて生成
            Vector3 spawnPos = position + normal * 0.05f;
            GameObject areaObj = Instantiate(_damageAreaPrefab, spawnPos, rotation);

            var area = areaObj.GetComponent<MidBossPoisonArea>();
            if (area == null)
            {
                area = areaObj.AddComponent<MidBossPoisonArea>();
            }

            area.Initialize(
                _damageAreaDuration,
                _damageAreaInterval,
                _poisonDuration,
                _poisonDamagePerTick,
                _poisonTickInterval,
                _playerLayer
            );
        }
    }
}
