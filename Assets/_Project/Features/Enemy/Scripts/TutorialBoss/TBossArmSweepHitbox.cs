using System;
using System.Collections.Generic;
using SoundKit;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 伸びる腕全体を攻撃判定にする。肩から鎌の先端までの折れ線に太さを持たせ、前フレームからの動きを刻んで掃く。
    /// 先端の球だけの判定では、腕の途中（ボスの手前）にいる相手に当たらないため、通常攻撃2ではこちらを使う。
    /// 有効/無効は AnimationEvent（TutorialBossController.EnableArmSweepHitbox / DisableArmSweepHitbox）で切り替える。
    /// </summary>
    public sealed class TBossArmSweepHitbox : MonoBehaviour
    {
        // 1フレームの移動を刻む回数の上限。鞭の先端は1フレームで数m動くため刻むが、問い合わせ回数が膨らまないよう抑える
        private const int MaxSubsteps = 8;

        [Header("判定の形")]
        [Tooltip("根元から先端の順に並べた腕の節。例: LeftArm → LeftForeArm → LeftHand → 鎌の先端")]
        [SerializeField]
        private Transform[] _chain;

        [Tooltip("腕の太さ（半径、メートル）")]
        [SerializeField]
        private float _radius = 0.35f;

        [Tooltip("判定するレイヤー（Player のレイヤーを指定する）")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        [Header("ダメージ")]
        [Tooltip("この攻撃を行っている敵自身のステータス")]
        [SerializeField]
        private EnemyStatus _enemyStatus;

        [Tooltip("敵の攻撃力に掛ける倍率（1.0 = 100%）")]
        [SerializeField]
        private float _meleeMultiplier = 1.0f;

        [Header("演出")]
        [SerializeField]
        private GameObject _hitEffect;

        [Tooltip("プレイヤーにダメージを与えた時の音。ガード・パリィ成立時は鳴らさない")]
        [SerializeField]
        private SoundCue _hitSound;

        private readonly HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();
        private readonly Collider[] _overlapHits = new Collider[16];
        private Vector3[] _previousPoints;
        private Vector3[] _currentPoints;
        private bool _isActive;

        /// <summary>
        /// 腕がプレイヤーに触れた瞬間に発火する（ガード成立・ダメージ無効時も発火する）。引数はヒット座標。
        /// </summary>
        public event Action<Vector3> OnHitLanded;

        private void Awake()
        {
            Assert.IsNotNull(
                _enemyStatus,
                $"{name}: TBossArmSweepHitbox の EnemyStatus が未設定です"
            );
            Assert.IsTrue(
                _chain != null && _chain.Length >= 2,
                $"{name}: TBossArmSweepHitbox の節は2つ以上必要です"
            );

            _previousPoints = new Vector3[_chain.Length];
            _currentPoints = new Vector3[_chain.Length];
        }

        /// <summary>判定を始める。1回の有効化につき同じ相手へは1度だけ当たる。</summary>
        public void EnableHitbox()
        {
            _alreadyHitTargets.Clear();
            ReadChain(_previousPoints);
            _isActive = true;
        }

        /// <summary>判定を止める。</summary>
        public void DisableHitbox()
        {
            _isActive = false;
            _alreadyHitTargets.Clear();
        }

        // アニメーションが骨を動かした後の姿勢で判定するため LateUpdate で行う
        private void LateUpdate()
        {
            if (!_isActive)
            {
                return;
            }

            ReadChain(_currentPoints);

            float maxTravel = 0f;
            for (int i = 0; i < _currentPoints.Length; i++)
            {
                maxTravel = Mathf.Max(
                    maxTravel,
                    Vector3.Distance(_previousPoints[i], _currentPoints[i])
                );
            }

            // 隣り合う刻みの間に半径ぶんの隙間ができないだけ刻む
            int steps = Mathf.Clamp(Mathf.CeilToInt(maxTravel / _radius), 1, MaxSubsteps);
            for (int step = 1; step <= steps; step++)
            {
                float t = (float)step / steps;
                for (int i = 0; i < _currentPoints.Length - 1; i++)
                {
                    Vector3 start = Vector3.Lerp(_previousPoints[i], _currentPoints[i], t);
                    Vector3 end = Vector3.Lerp(_previousPoints[i + 1], _currentPoints[i + 1], t);
                    CheckSegment(start, end);
                }
            }

            Array.Copy(_currentPoints, _previousPoints, _currentPoints.Length);
        }

        private void ReadChain(Vector3[] points)
        {
            for (int i = 0; i < _chain.Length; i++)
            {
                points[i] = _chain[i].position;
            }
        }

        private void CheckSegment(Vector3 start, Vector3 end)
        {
            int count = Physics.OverlapCapsuleNonAlloc(
                start,
                end,
                _radius,
                _overlapHits,
                _targetLayer
            );
            for (int i = 0; i < count; i++)
            {
                ProcessHit(_overlapHits[i], start, end);
            }
        }

        private void ProcessHit(Collider other, Vector3 start, Vector3 end)
        {
            if (_alreadyHitTargets.Contains(other) || !other.CompareTag("Player"))
            {
                return;
            }

            _alreadyHitTargets.Add(other);

            // 重なり判定は接触点を返さないため、腕の線上で相手に最も近い点から求める
            Vector3 pointOnArm = ClosestPointOnSegment(start, end, other.bounds.center);
            Vector3 hitPoint = other.ClosestPoint(pointOnArm);
            Vector3 hitNormal = (pointOnArm - hitPoint).normalized;

            // ガードされても腕が触れたこと自体は変わらないため、ダメージ判定より前に知らせる
            OnHitLanded?.Invoke(hitPoint);

            float damage = _enemyStatus.CurrentAttackPower * _meleeMultiplier;
            if (EnemyAttackHit.ApplyToPlayer(other, damage, _enemyStatus.transform, hitPoint))
            {
                SpawnHitEffect(hitPoint, hitNormal);
                WorldSoundPlayer.Play(_hitSound, hitPoint, this);
            }
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (_hitEffect == null)
            {
                return;
            }

            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            EffectPool.Play(_hitEffect, position, rotation);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 1e-8f)
            {
                return start;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }

        private void OnDrawGizmosSelected()
        {
            if (_chain == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            for (int i = 0; i < _chain.Length; i++)
            {
                if (_chain[i] == null)
                {
                    continue;
                }

                Gizmos.DrawWireSphere(_chain[i].position, _radius);
                if (i + 1 < _chain.Length && _chain[i + 1] != null)
                {
                    Gizmos.DrawLine(_chain[i].position, _chain[i + 1].position);
                }
            }
        }
    }
}
