using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 触手のような長いボーン連鎖全体の当たり判定。隣り合うボーンの間をカプセルで調べる。
    /// 先端だけの球(<see cref="EnemyMeleeHitbox"/>)では、地面から突き出す・横に薙ぎ払うといった
    /// 胴の部分で当たる攻撃を拾えないため分けている。当たった後の扱い(ガード・ダメージ)は <see cref="EnemyAttackHit"/> で共通。
    /// </summary>
    public class TentacleChainHitbox : MonoBehaviour
    {
        // 前フレームから今フレームの間を何段に分けて調べるか。速く振った時にカプセルがプレイヤーを飛び越えないため
        private const int SweepSubSteps = 3;

        [Tooltip("根元から先端の順に並べたボーン")]
        [SerializeField]
        private Transform[] _bones;

        [Tooltip("判定の太さ（半径・m）。触手を包むくらいにする")]
        [SerializeField]
        private float _radius = 0.6f;

        [Tooltip("判定するレイヤー")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        [Header("ヒット時")]
        [SerializeField]
        private GameObject _hitEffect;

        [Tooltip("プレイヤーにダメージを与えた時の音。ガード・パリィ成立時は鳴らさない")]
        [SerializeField]
        private SoundCue _hitSound;

        private readonly Collider[] _overlapBuffer = new Collider[8];
        private Vector3[] _previousPositions;
        private EnemyStatus _attacker;
        private float _damageMultiplier;
        private bool _isActive;
        private bool _hasHit;

        /// <summary>判定を入れる。1回入れている間は同じプレイヤーに1度だけ当たる。</summary>
        /// <param name="attacker">攻撃している敵。攻撃力とパリィの相手に使う</param>
        /// <param name="damageMultiplier">攻撃力に掛ける倍率</param>
        public void Activate(EnemyStatus attacker, float damageMultiplier)
        {
            _attacker = attacker;
            _damageMultiplier = damageMultiplier;
            _hasHit = false;
            _isActive = true;
            RecordPositions();
        }

        public void Deactivate() => _isActive = false;

        private void LateUpdate()
        {
            // ボーンはアニメーションやスクリプトが LateUpdate までに動かすので、その後の位置で調べる
            if (!_isActive || _hasHit)
            {
                RecordPositions();
                return;
            }

            for (int step = 1; step <= SweepSubSteps && !_hasHit; step++)
            {
                float t = (float)step / SweepSubSteps;
                for (int i = 0; i + 1 < _bones.Length && !_hasHit; i++)
                {
                    Vector3 a = Vector3.Lerp(_previousPositions[i], _bones[i].position, t);
                    Vector3 b = Vector3.Lerp(_previousPositions[i + 1], _bones[i + 1].position, t);
                    CheckSegment(a, b);
                }
            }

            RecordPositions();
        }

        private void CheckSegment(Vector3 a, Vector3 b)
        {
            int count = Physics.OverlapCapsuleNonAlloc(
                a,
                b,
                _radius,
                _overlapBuffer,
                _targetLayer,
                QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapBuffer[i];
                if (!other.CompareTag("Player"))
                    continue;

                _hasHit = true;
                Vector3 segmentCenter = (a + b) * 0.5f;
                Vector3 hitPoint = other.ClosestPoint(segmentCenter);
                float damage = _attacker.CurrentAttackPower * _damageMultiplier;
                if (EnemyAttackHit.ApplyToPlayer(other, damage, _attacker.transform, hitPoint))
                {
                    SpawnHitEffect(hitPoint, (hitPoint - segmentCenter).normalized);
                    WorldSoundPlayer.Play(_hitSound, hitPoint, this);
                }
                return;
            }
        }

        private void RecordPositions()
        {
            if (_previousPositions == null || _previousPositions.Length != _bones.Length)
                _previousPositions = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
                _previousPositions[i] = _bones[i].position;
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (_hitEffect == null)
                return;
            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            EffectPool.Play(_hitEffect, position, rotation);
        }

        private void OnDrawGizmosSelected()
        {
            if (_bones == null)
                return;
            Gizmos.color = _isActive ? new Color(1f, 0f, 0f, 0.6f) : new Color(1f, 0.6f, 0f, 0.4f);
            for (int i = 0; i + 1 < _bones.Length; i++)
            {
                if (_bones[i] == null || _bones[i + 1] == null)
                    continue;
                Gizmos.DrawWireSphere(_bones[i].position, _radius);
                Gizmos.DrawLine(_bones[i].position, _bones[i + 1].position);
            }
        }
    }
}
