using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの近接攻撃ヒットボックス。
    /// 武器オブジェクトにアタッチし、アニメーションイベントから有効/無効を切り替えて使用する。
    /// SphereCastによる軌跡判定とOverlapSphereによる重なり判定の二段構えで、
    /// 高速スイング時のすり抜けと密着時の検出漏れの両方を防ぐ。
    /// </summary>
    public class PlayerMeleeHitbox : MonoBehaviour
    {
        [Header("近接のダメージ倍率(1.0 = 100%)")]
        [SerializeField]
        private float _meleeMultiplier = 1.5f;

        [Header("ヒットエフェクト")]
        [SerializeField]
        private GameObject _hitEffect;

        [Header("判定設定(SphereCast)")]
        [Tooltip("判定の太さ（半径）。武器を包み込むくらいのサイズにする")]
        [SerializeField]
        private float _hitboxRadius = 0.5f;

        [Tooltip("判定するレイヤー(Enemyのレイヤー等を指定すると処理が軽くなる)")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        private PlayerStatus _playerStatus;
        private bool _isHitboxActive = false;
        private Vector3 _previousPosition;
        private HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();

        private void Awake()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerStatus = player.GetComponent<PlayerStatus>();
            }
        }

        private void Update()
        {
            if (!_isHitboxActive)
                return;

            Vector3 currentPosition = transform.position;
            Vector3 direction = currentPosition - _previousPosition;
            float distance = direction.magnitude;

            // 高速スイング時に武器がターゲットをすり抜けないよう、
            // 前フレームから現フレームまでの軌跡全体を太いレイで判定する
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
                    // SphereCastAll は始点でコライダーと既に重なっている場合
                    // hit.point に Vector3.zero を返す仕様上の問題があるため、
                    // その場合はClosestPointで代替する
                    Vector3 resolvedPoint =
                        (hit.point == Vector3.zero)
                            ? hit.collider.ClosestPoint(currentPosition)
                            : hit.point;
                    ProcessHit(hit.collider, resolvedPoint, hit.normal);
                }
            }

            // SphereCastは始点内部の重なりを検出できないため、
            // 密着・めり込み状態をOverlapSphereで補完する
            Collider[] overlaps = Physics.OverlapSphere(
                currentPosition,
                _hitboxRadius,
                _targetLayer
            );
            foreach (var col in overlaps)
            {
                // OverlapSphereは接触点を返さないため、エフェクト表示用にClosestPointで近似する
                Vector3 hitPoint = col.ClosestPoint(currentPosition);
                Vector3 hitNormal = (currentPosition - hitPoint).normalized;
                ProcessHit(col, hitPoint, hitNormal);
            }

            _previousPosition = currentPosition;
        }

        private void ProcessHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_alreadyHitTargets.Contains(other))
                return;

            // 武器がプレイヤー自身のコライダーに触れてもヒット扱いにしない
            if (other.CompareTag("Player"))
                return;

            if (other.TryGetComponent(out IDamageable enemy))
            {
                // 1スイングにつき同一対象へは1回だけヒットさせる
                _alreadyHitTargets.Add(other);

                float finalDamage = _playerStatus.RollDamage(_meleeMultiplier, out bool isCritical);
                enemy.TakeDamage(finalDamage, isCritical);

                CameraShakeManager.Instance?.Shake(0.3f);

                SpawnHitEffect(hitPoint, hitNormal);
            }
        }

        public void EnableHitbox()
        {
            _alreadyHitTargets.Clear();
            // 軌跡判定の起点を記録し、最初のフレームで正しくSphereCastできるようにする
            _previousPosition = transform.position;
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, _hitboxRadius);
        }
    }
}
