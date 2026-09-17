using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// MidBossの特殊攻撃で生成される手下のプロジェクタイル。
    /// TestEnemyNeedleProjectile をベースに、Rising フェーズを魚のような揺らぎ上昇に変更。
    ///
    /// 動作フェーズ:
    ///   Rising  : Sin/Cos を使った魚の泳ぎ動作で上昇。回転処理なし。進行方向にLookRotation。
    ///   Aiming  : 上昇完了後、プレイヤーへ向かう（TestEnemyと同じ）
    ///   Firing  : 前方へ直線飛翔（TestEnemyと同じ）
    /// </summary>
    public class MidBossMinionProjectile : MonoBehaviour
    {
        [SerializeField]
        private float _waveFrequency = 3f;

        [SerializeField]
        private float _waveAmplitude = 0.35f;

        [SerializeField]
        private float _flySpeed = 30f;

        [SerializeField]
        private float _aimWait = 0.5f;

        [SerializeField]
        private float _riseDuration = 1.5f;

        [SerializeField]
        private float _riseHeight = 5f;

        [SerializeField]
        private float _spreadRadius = 5f;
        private Transform _enemyTransform;
        private Transform _playerTransform;

        private float _fireDelay;
        private float _spawnTime;
        private int _damage;

        // 分散用 offset（上昇方向のベース）
        private Vector3 _startOffset;
        private Vector3 _endOffset;

        private enum Phase
        {
            Rising,
            Aiming,
            Firing,
        }

        private Phase _currentPhase = Phase.Rising;

        // Rising フェーズ開始時の位置（LookRotation の基準として使用）
        private Vector3 _prevPos;

        /// <summary>
        /// 初期化。TestEnemyNeedleProjectile.Initialize と同じシグネチャ。
        /// </summary>
        /// <param name="enemy">生成元（ボス）のTransform</param>
        /// <param name="player">プレイヤーのTransform</param>
        /// <param name="angle">円周分散用の角度（angleStep * i）</param>
        /// <param name="delay">発火ディレイ（Aimiingフェーズへの遅れ）</param>
        /// <param name="damage">ダメージ量</param>
        public void Initialize(
            Transform enemy,
            Transform player,
            float angle,
            float delay,
            int damage
        )
        {
            _enemyTransform = enemy;
            _playerTransform = player;
            _fireDelay = delay;
            _damage = damage;
            _spawnTime = Time.time;
            _currentPhase = Phase.Rising;

            // 上昇の始点・終点（TestEnemyと同様に円周分散）
            _startOffset = Vector3.up * 1.0f;
            _endOffset =
                Vector3.up * _riseHeight
                + (Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _spreadRadius);

            transform.position = _enemyTransform.position + _startOffset;
            _prevPos = transform.position;

            Collider existingCol = GetComponent<Collider>();
            if (existingCol == null)
            {
                SphereCollider col = gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.2f;
            }
            else
            {
                existingCol.isTrigger = true;
            }

            // KinematicRigidbody（物理演算に流されないようにする）
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        private void Update()
        {
            if (_enemyTransform == null || _playerTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            float elapsed = Time.time - _spawnTime;

            switch (_currentPhase)
            {
                case Phase.Rising:
                    UpdateRising(elapsed);
                    break;
                case Phase.Aiming:
                    UpdateAiming(elapsed);
                    break;
                case Phase.Firing:
                    UpdateFiring(elapsed);
                    break;
            }
        }

        // 魚のような揺らぎ上昇をさせる関数
        private void UpdateRising(float elapsed)
        {
            if (elapsed < _riseDuration)
            {
                float t = elapsed / _riseDuration;

                // ベース上昇位置（円周分散の方向に向かいながら上昇）
                Vector3 basePos =
                    _enemyTransform.position + Vector3.Lerp(_startOffset, _endOffset, t);

                // 魚の揺らぎ：Sin（横）と Cos（縦）でゆらゆらさせる
                // ワールド固定軸で計算することで回転の影響を受けないようにする
                float wave = elapsed * _waveFrequency;
                Vector3 rightOffset = Vector3.right * (Mathf.Sin(wave) * _waveAmplitude);
                Vector3 upOffset = Vector3.up * (Mathf.Cos(wave * 2f) * _waveAmplitude * 0.5f);

                Vector3 newPos = basePos + rightOffset + upOffset;

                // 進行方向に forward を向ける（回転処理なし・LookRotationのみ）
                Vector3 moveDir = newPos - _prevPos;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(moveDir.normalized);
                }

                transform.position = newPos;
                _prevPos = newPos;
            }
            else
            {
                _currentPhase = Phase.Aiming;
            }
        }

        // プレイヤー方向へ向かせる関数
        private void UpdateAiming(float elapsed)
        {
            float aimTime = elapsed - _riseDuration;
            float totalAimWait = _aimWait + _fireDelay;

            if (aimTime < totalAimWait)
            {
                // プレイヤーへホーミングしながら向く
                Vector3 targetPos = _playerTransform.position + Vector3.up * 1f;
                Vector3 dirToPlayer = targetPos - transform.position;
                if (dirToPlayer != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRot,
                        Time.deltaTime * 15f
                    );
                }
            }
            else
            {
                // 向きを固定して発射フェーズへ
                Vector3 targetPos = _playerTransform.position + Vector3.up * 1f;
                Vector3 dirToPlayer = targetPos - transform.position;
                if (dirToPlayer != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(dirToPlayer);
                }
                _currentPhase = Phase.Firing;
            }
        }

        // 前方に直線飛翔させる関数
        private void UpdateFiring(float elapsed)
        {
            transform.position += transform.forward * (_flySpeed * Time.deltaTime);

            // 生存時間オーバーで自動破棄
            if (elapsed > _riseDuration + _aimWait + _fireDelay + 5f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 発射フェーズ以外は当たり判定なし
            if (_currentPhase != Phase.Firing)
                return;

            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(_damage, false);
                }
                Destroy(gameObject);
            }
            else if (
                ((1 << other.gameObject.layer) & LayerMask.GetMask("Obstacle", "Ground", "Default"))
                != 0
            )
            {
                if (
                    !other.CompareTag("Enemy")
                    && _enemyTransform != null
                    && other.gameObject != _enemyTransform.gameObject
                    && other.GetComponent<MidBossMinionProjectile>() == null
                )
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
