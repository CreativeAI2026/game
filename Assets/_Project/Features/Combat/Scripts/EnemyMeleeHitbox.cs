using System;
using System.Collections.Generic;
using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵の近接攻撃ヒットボックス。武器にアタッチし、アニメーションイベントで有効/無効を切り替える。
    /// SphereCast（軌跡）と OverlapSphere（重なり）の二段構えで、高速スイングのすり抜けと密着時の検出漏れを防ぐ。
    /// </summary>
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

        [Header("被弾音")]
        [Tooltip(
            "プレイヤーにダメージを与えた時の音。攻撃の大分類(斬撃など)ごとの音を割り当てる。ガード・パリィ成立時は鳴らさない"
        )]
        [SerializeField]
        private SoundCue _hitSound;

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

        // 物理クエリ結果の再利用バッファ(_targetLayer が広い場合も取りこぼさないよう大きめ)
        private readonly RaycastHit[] _castHits = new RaycastHit[64];
        private readonly Collider[] _overlapHits = new Collider[64];

        /// <summary>
        /// プレイヤーに武器が接触した瞬間に発火する（ガード成立・ダメージ無効時も発火する）。
        /// 攻撃側が「当たったこと自体」に対する演出（引き寄せ等）を行いたい場合に使う。
        /// 引数はヒット座標。
        /// </summary>
        public event Action<Vector3> OnHitLanded;

        /// <summary>判定半径。デバッグ表示で実際の判定範囲を描くために公開する。</summary>
        public float HitboxRadius => _hitboxRadius;

        private void Awake()
        {
            if (_enemyStatus == null)
            {
                _enemyStatus = GetComponentInParent<EnemyStatus>();
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
            // 前フレームから現フレームまでの軌跡全体を太いRayで判定する
            if (distance > 0)
            {
                int hitCount = Physics.SphereCastNonAlloc(
                    _previousPosition,
                    _hitboxRadius,
                    direction.normalized,
                    _castHits,
                    distance,
                    _targetLayer
                );
                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _castHits[i];
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
            int overlapCount = Physics.OverlapSphereNonAlloc(
                currentPosition,
                _hitboxRadius,
                _overlapHits,
                _targetLayer
            );
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = _overlapHits[i];
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

            if (other.CompareTag("Player"))
            {
                // 1スイングにつき同一対象へは1回だけヒットさせる
                _alreadyHitTargets.Add(other);

                // ガード成立でダメージが無効化される場合も、武器が触れたこと自体は変わらないため
                // ダメージ判定より前に発火する
                OnHitLanded?.Invoke(hitPoint);

                float finalDamage = _enemyStatus.CurrentAttackPower * _meleeMultiplier;

                if (
                    EnemyAttackHit.ApplyToPlayer(
                        other,
                        finalDamage,
                        _enemyStatus.transform,
                        hitPoint
                    )
                )
                {
                    SpawnHitEffect(hitPoint, hitNormal);
                    WorldSoundPlayer.Play(_hitSound, hitPoint, this);
                }
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
