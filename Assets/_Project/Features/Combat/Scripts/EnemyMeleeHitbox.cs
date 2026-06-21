using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class EnemyMeleeHitbox : MonoBehaviour
    {
        [Header("敵のダメージ倍率(1.0 = 100%)")]
        [SerializeField]
        private float _meleeMultiplier = 1.0f;

        [Header("参照")]
        [Tooltip("この攻撃を行っている敵自身のステータス")]
        [SerializeField]
        private EnemyStatus _enemyStatus;

        [Header("ヒットエフェクト")]
        [SerializeField]
        private GameObject _hitEffect;

        [Header("判定設定(SphereCast)")]
        [Tooltip("判定の太さ（半径）。武器を包み込むくらいのサイズにする")]
        [SerializeField]
        private float _hitboxRadius = 0.5f;

        [Tooltip("判定するレイヤー(Playerのレイヤーを指定すると処理が軽くなります)")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        private bool _isHitboxActive = false;
        private Vector3 _previousPosition;
        private HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();

        private void Awake()
        {
            if (_enemyStatus == null)
            {
                _enemyStatus = GetComponentInParent<EnemyStatus>();
            }
        }

        private void Update()
        {
            // 判定がONの時（攻撃モーション中）だけ毎フレーム実行
            if (!_isHitboxActive)
                return;

            Vector3 currentPosition = transform.position;
            Vector3 direction = currentPosition - _previousPosition;
            float distance = direction.magnitude;

            // ① 軌跡の判定（すり抜け防止）
            // 前のフレームから今のフレームまでの移動ルート上にプレイヤーがいなかったかチェック
            if (distance > 0)
            {
                RaycastHit[] hits = Physics.SphereCastAll(
                    _previousPosition,
                    _hitboxRadius,
                    direction.normalized,
                    distance,
                    _targetLayer
                );
                foreach (var hit in hits)
                {
                    // SphereCastAll の hit.point は始点とコライダーが重なっている場合に
                    // Vector3.zero を返すことがある。その場合はコライダー上の最近傍点を使う。
                    Vector3 resolvedPoint =
                        (hit.point == Vector3.zero)
                            ? hit.collider.ClosestPoint(currentPosition)
                            : hit.point;
                    ProcessHit(hit.collider, resolvedPoint, hit.normal);
                }
            }

            // ② 現在位置の判定（めり込み防止）
            // 敵が踏み込みすぎて、すでにプレイヤーが武器の中心(currentPosition)にいる場合用
            Collider[] overlaps = Physics.OverlapSphere(
                currentPosition,
                _hitboxRadius,
                _targetLayer
            );
            foreach (var col in overlaps)
            {
                // OverlapSphereは正確なヒットポイントが取れないため、一番近い表面の座標を取得
                Vector3 hitPoint = col.ClosestPoint(currentPosition);
                Vector3 hitNormal = (currentPosition - hitPoint).normalized;
                ProcessHit(col, hitPoint, hitNormal);
            }

            // 次のフレームでの軌跡計算のために、今の位置を保存しておく
            _previousPosition = currentPosition;
        }

        // ダメージとエフェクトの処理をまとめた関数
        private void ProcessHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_alreadyHitTargets.Contains(other))
                return;

            if (other.CompareTag("Player"))
            {
                _alreadyHitTargets.Add(other); // 多段ヒット防止
                float finalDamage = _enemyStatus.CurrentAttackPower * _meleeMultiplier;

                // ─── 追加：ダメージを与える前にガード・パリィ判定を行う ───
                // プレイヤー側から SwordController を探す（ルートや子オブジェクトにあっても取得可能）
                SwordController sword = other.GetComponentInChildren<SwordController>();

                // 剣を装備中（アクティブ）かチェック
                if (sword != null && sword.gameObject.activeInHierarchy)
                {
                    // SwordControllerに攻撃の情報を渡し、ガードできたか(isBlocked)を受け取る
                    bool isBlocked = sword.ReceiveAttack(
                        finalDamage,
                        true,
                        _enemyStatus.transform,
                        hitPoint
                    );

                    if (isBlocked)
                    {
                        // ガード・ジャストパリィ成功！
                        // ダメージ処理を行わずにここで終了する（敵の剣が弾かれたSEなどを鳴らしても良い）
                        return;
                    }
                }
                // ──────────────────────────────────────────────

                // ガードしていなかった、もしくは弓などを装備していて無防備だった場合の通常の被弾処理
                if (other.TryGetComponent(out IDamageable target))
                {
                    target.TakeDamage(finalDamage, false);

                    // 取得した「実際に当たった正確な座標」で流血などの被弾エフェクトを出す！
                    SpawnHitEffect(hitPoint, hitNormal);
                }
            }
        }

        public void EnableHitbox()
        {
            _alreadyHitTargets.Clear();
            _previousPosition = transform.position; // 判定開始時の位置を初期位置として記録
            _isHitboxActive = true;
        }

        public void DisableHitbox()
        {
            _isHitboxActive = false;
            _alreadyHitTargets.Clear();
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (_hitEffect == null)
                return;
            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            Instantiate(_hitEffect, position, rotation);
        }

        // 調整用：エディタ上で判定の大きさを赤い球で可視化する
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, _hitboxRadius);
        }
    }
}
