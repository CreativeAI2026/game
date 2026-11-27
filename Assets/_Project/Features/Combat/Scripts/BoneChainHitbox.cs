using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 腕や触手のような、骨の連なり全体を敵の攻撃判定にする。隣り合う骨の間に太さを持たせ、前フレームからの動きを刻んで掃く。
    /// 先端だけの球(<see cref="EnemyMeleeHitbox"/>)では、途中の部分で当たる攻撃(伸びた腕の手前・地面から突き出す触手など)を拾えないため分けている。
    /// 当たった後の扱い(ガード・ダメージ)は <see cref="EnemyAttackHit"/> で共通。
    /// </summary>
    public sealed class BoneChainHitbox : MonoBehaviour
    {
        // 動いた距離から刻むときの回数の上限。鞭の先端は1フレームで数m動くため刻むが、問い合わせ回数が膨らまないよう抑える
        private const int MaxSubsteps = 8;

        [Header("判定の形")]
        [Tooltip("根元から先端の順に並べた骨。例: LeftArm → LeftForeArm → LeftHand → 鎌の先端")]
        [SerializeField]
        private Transform[] _chain;

        [Tooltip("判定の太さ（半径、メートル）")]
        [SerializeField]
        private float _radius = 0.35f;

        [Tooltip("判定するレイヤー（Player のレイヤーを指定すると処理が軽くなる）")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        [Tooltip(
            "前フレームから今フレームの間を何段に分けて調べるか。0 なら骨が動いた距離から決める(半径ぶんごとに1段、最大8段)"
        )]
        [SerializeField]
        private int _fixedSubsteps;

        [Header("ダメージ")]
        [Tooltip(
            "この攻撃を行っている敵自身のステータス。引数なしの EnableHitbox で使う(引数で渡す場合は空でよい)"
        )]
        [SerializeField]
        private EnemyStatus _enemyStatus;

        [Tooltip("敵の攻撃力に掛ける倍率（1.0 = 100%）。引数なしの EnableHitbox で使う")]
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
        private EnemyStatus _attacker;
        private float _damageMultiplier;
        private bool _isActive;

        /// <summary>
        /// 骨の連なりがプレイヤーに触れた瞬間に発火する（ガード成立・ダメージ無効時も発火する）。引数はヒット座標。
        /// </summary>
        public event Action<Vector3> OnHitLanded;

        /// <summary>Inspector の EnemyStatus と倍率で判定を始める。1回の有効化につき同じ相手へは1度だけ当たる。</summary>
        public void EnableHitbox() => EnableHitbox(_enemyStatus, _meleeMultiplier);

        /// <summary>判定を始める。1回の有効化につき同じ相手へは1度だけ当たる。</summary>
        /// <param name="attacker">攻撃している敵。攻撃力とパリィの相手に使う</param>
        /// <param name="damageMultiplier">攻撃力に掛ける倍率</param>
        public void EnableHitbox(EnemyStatus attacker, float damageMultiplier)
        {
            Assert.IsNotNull(attacker, $"{name}: BoneChainHitbox の攻撃している敵がありません");
            Assert.IsTrue(
                _chain != null && _chain.Length >= 2,
                $"{name}: BoneChainHitbox の骨は2つ以上必要です"
            );

            _attacker = attacker;
            _damageMultiplier = damageMultiplier;
            _alreadyHitTargets.Clear();
            if (_previousPoints == null || _previousPoints.Length != _chain.Length)
            {
                _previousPoints = new Vector3[_chain.Length];
                _currentPoints = new Vector3[_chain.Length];
            }
            ReadChain(_previousPoints);
            _isActive = true;
        }

        /// <summary>判定を止める。</summary>
        public void DisableHitbox()
        {
            _isActive = false;
            _alreadyHitTargets.Clear();
        }

        // アニメーションやスクリプトが骨を動かした後の姿勢で判定するため LateUpdate で行う
        private void LateUpdate()
        {
            if (!_isActive)
            {
                return;
            }

            ReadChain(_currentPoints);

            int steps = ResolveSubsteps();
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

        private int ResolveSubsteps()
        {
            if (_fixedSubsteps > 0)
            {
                return _fixedSubsteps;
            }

            float maxTravel = 0f;
            for (int i = 0; i < _currentPoints.Length; i++)
            {
                maxTravel = Mathf.Max(
                    maxTravel,
                    Vector3.Distance(_previousPoints[i], _currentPoints[i])
                );
            }

            // 隣り合う刻みの間に半径ぶんの隙間ができないだけ刻む
            return Mathf.Clamp(Mathf.CeilToInt(maxTravel / _radius), 1, MaxSubsteps);
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
                _targetLayer,
                QueryTriggerInteraction.Ignore
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

            // 重なり判定は接触点を返さないため、骨の線上で相手に最も近い点から求める
            Vector3 pointOnChain = ClosestPointOnSegment(start, end, other.bounds.center);
            Vector3 hitPoint = other.ClosestPoint(pointOnChain);
            Vector3 hitNormal = (pointOnChain - hitPoint).normalized;

            // ガードされても触れたこと自体は変わらないため、ダメージ判定より前に知らせる
            OnHitLanded?.Invoke(hitPoint);

            float damage = _attacker.CurrentAttackPower * _damageMultiplier;
            if (EnemyAttackHit.ApplyToPlayer(other, damage, _attacker.transform, hitPoint))
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

            Gizmos.color = _isActive ? new Color(1f, 0f, 0f, 0.6f) : new Color(1f, 0.6f, 0f, 0.4f);
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
