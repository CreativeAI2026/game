using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    // 敵の遠距離攻撃で生成される針のテスト用スクリプト。
    public class EnemyNeedleProjectile : MonoBehaviour
    {
        private Transform enemyTransform;
        private Transform playerTransform;

        [SerializeField]
        private float spreadRadius = 5.0f;

        [SerializeField]
        private float riseHeight = 5.0f;

        private Vector3 startOffset;
        private Vector3 endOffset;

        [SerializeField]
        private float riseDuration = 1.5f;

        private float _fireDelay;
        private float _spawnTime;

        [SerializeField]
        private float flySpeed = 30f;

        [SerializeField]
        private int damage = 10;

        [SerializeField]
        private float spinSpeed = 1080f;

        private enum Phase
        {
            Rising,
            Aiming,
            Firing,
        }

        private Phase currentPhase = Phase.Rising;

        public void Initialize(Transform enemy, Transform player, float angle, float delay, int dmg)
        {
            enemyTransform = enemy;
            playerTransform = player;
            _fireDelay = delay;
            damage = dmg;
            _spawnTime = Time.time;

            startOffset = Vector3.up * 1.0f;
            endOffset =
                Vector3.up * riseHeight
                + (Quaternion.Euler(0, angle, 0) * Vector3.forward * spreadRadius);

            transform.position = enemyTransform.position + startOffset;

            if (transform.childCount == 0)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                // UnityのCylinderは初期状態でY軸方向を向いているため、進行方向(Z軸)と視覚的な向きを一致させる
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(0.05f, 0.4f, 0.05f);

                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.black;
                }

                Destroy(visual.GetComponent<Collider>());
            }

            Collider existingCol = gameObject.GetComponent<Collider>();
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

            // 外部の物理演算（重力や他のオブジェクトとの衝突応答）による意図しない軌道逸脱を防ぐためKinematicに設定
            Rigidbody rb = gameObject.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
        }

        private void Update()
        {
            if (enemyTransform == null || playerTransform == null)
            {
                Destroy(gameObject);
                return;
            }

            float elapsedTime = Time.time - _spawnTime;

            if (currentPhase == Phase.Rising)
            {
                if (elapsedTime < riseDuration)
                {
                    float t = elapsedTime / riseDuration;
                    // 複数生成された針同士が重なって視認性が低下するのを防ぐため、円周上に散開させながら上昇させる
                    Vector3 currentOffset = Vector3.Lerp(startOffset, endOffset, t);
                    transform.position = enemyTransform.position + currentOffset;

                    transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);
                }
                else
                {
                    currentPhase = Phase.Aiming;
                }
            }
            else if (currentPhase == Phase.Aiming)
            {
                // 全ての針が同時に追従を完了できるよう、エイム期間に最低0.5秒の猶予を持たせる
                float aimTime = elapsedTime - riseDuration;
                float totalAimWait = 0.5f + _fireDelay;

                if (aimTime < totalAimWait)
                {
                    Vector3 targetPos = playerTransform.position + Vector3.up * 1f; // 足元ではなく胴体（被弾しやすい部位）を狙うためのオフセット
                    Vector3 dirToPlayer = targetPos - transform.position;
                    if (dirToPlayer != Vector3.zero)
                    {
                        Quaternion targetRot =
                            Quaternion.LookRotation(dirToPlayer) * Quaternion.Euler(90f, 0f, 0f);
                        // 15f: プレイヤーの移動に対して十分な追従速度を確保しつつ、フレーム間のジッターを抑えるための補間値
                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            targetRot,
                            Time.deltaTime * 15f
                        );
                    }
                }
                else
                {
                    Vector3 targetPos = playerTransform.position + Vector3.up * 1f;
                    Vector3 dirToPlayer = targetPos - transform.position;
                    if (dirToPlayer != Vector3.zero)
                    {
                        transform.rotation =
                            Quaternion.LookRotation(dirToPlayer) * Quaternion.Euler(90f, 0f, 0f);
                    }
                    currentPhase = Phase.Firing;
                }
            }
            else if (currentPhase == Phase.Firing)
            {
                transform.position += transform.up * (flySpeed * Time.deltaTime);

                // メモリリークおよび無限に飛び続けることによる不具合を防ぐためのフォールバック処理（5秒で寿命）
                if (elapsedTime > riseDuration + 0.5f + _fireDelay + 5f)
                {
                    Destroy(gameObject);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 展開フェーズ中に敵自身や周囲の障害物と誤って衝突判定されるのを防ぐ
            if (currentPhase != Phase.Firing)
                return;

            if (other.CompareTag("Player"))
            {
                // Playerへの直接ダメージより先に、ガード可能武器(Sword)が防御を成立させていないか優先して判定する
                SwordController sword = other.GetComponentInChildren<SwordController>();
                if (sword != null && sword.gameObject.activeInHierarchy)
                {
                    // 遠距離攻撃（Projectile）であることを通知し、パリィ等の近接限定カウンター処理を弾く
                    bool isBlocked = sword.ReceiveAttack(
                        damage,
                        false,
                        enemyTransform,
                        transform.position
                    );
                    if (isBlocked)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }

                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damage, false);
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
                    && other.gameObject != enemyTransform.gameObject
                    && other.GetComponent<EnemyNeedleProjectile>() == null
                )
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
