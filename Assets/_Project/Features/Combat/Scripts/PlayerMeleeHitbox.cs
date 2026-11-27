using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの近接攻撃ヒットボックス。武器にアタッチし、アニメーションイベントで有効/無効を切り替える。
    /// SphereCast（軌跡）と OverlapSphere（重なり）の二段構えで、高速スイングのすり抜けと密着時の検出漏れを防ぐ。
    /// </summary>
    public class PlayerMeleeHitbox : MonoBehaviour
    {
        [Header("近接のダメージ倍率(1.0 = 100%)")]
        [SerializeField]
        private float _meleeMultiplier = 1.0f;

        [Tooltip(
            "この判定の攻撃の種類。敵が防御・はじき返しの扱いを変えるのに使う(ハンマーの範囲攻撃は Slam、投げた鎌は Thrown)"
        )]
        [SerializeField]
        private PlayerAttackKind _attackKind = PlayerAttackKind.Melee;

        [Header("ヒットエフェクト")]
        [SerializeField]
        private GameObject _hitEffect;

        [Tooltip(
            "ON: エフェクトの前方(+Z)を武器を振った向き、上(+Y)を当たった面の外向きに合わせる(斬撃用)。"
                + "OFF: エフェクトの前方を当たった面の外向きに合わせる"
        )]
        [SerializeField]
        private bool _alignHitEffectToSwing;

        [Header("ヒット音")]
        [Tooltip("敵に当たった時の音。武器(判定)ごとに割り当てる")]
        [SerializeField]
        private SoundCue _hitSound;

        [Header("判定設定(SphereCast)")]
        [Tooltip("判定の太さ（半径）。武器を包み込むくらいのサイズにする")]
        [SerializeField]
        private float _hitboxRadius = 0.5f;

        [Tooltip("判定するレイヤー(Enemyのレイヤー等を指定すると処理が軽くなる)")]
        [SerializeField]
        private LayerMask _targetLayer = ~0;

        private PlayerStatus _playerStatus;
        private readonly MeleeSweep _sweep = new MeleeSweep();

        // 毎フレームの割り当てを避けるため、当たったときの処理を一度だけデリゲートにしておく
        private MeleeSweep.HitHandler _onHit;

        private void Awake()
        {
            _onHit = ProcessHit;
            // 武器に付いている判定は、親階層のプレイヤーが持ち主。
            // 投げた鎌のようにプレイヤーの外にある判定は、投げる側が SetOwner で渡す
            _playerStatus = GetComponentInParent<PlayerStatus>();
        }

        /// <summary>ダメージ計算に使う持ち主を設定する(プレイヤーの階層の外で使う判定用)。</summary>
        public void SetOwner(PlayerStatus owner)
        {
            _playerStatus = owner;
        }

        private void Update()
        {
            _sweep.Sweep(transform.position, _hitboxRadius, _targetLayer, _onHit);
        }

        private void ProcessHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            // 武器がプレイヤー自身のコライダーに触れてもヒット扱いにしない
            if (other.CompareTag("Player"))
                return;

            if (other.TryGetComponent(out IDamageable enemy))
            {
                // 1スイングにつき同一対象へは1回だけヒットさせる
                _sweep.MarkHit(other);

                // 敵が受け止めたら、ダメージもヒットの演出も出さない(受け止めた演出は敵側が出す)
                if (
                    other.TryGetComponent(out IPlayerAttackGuard guard)
                    && guard.ReceivePlayerAttack(_attackKind, hitPoint)
                )
                    return;

                float finalDamage = _playerStatus.RollDamage(_meleeMultiplier, out bool isCritical);
                enemy.TakeDamage(finalDamage, isCritical);

                if (CameraShakeManager.Instance != null)
                    CameraShakeManager.Instance.Shake(0.3f);

                SpawnHitEffect(hitPoint, hitNormal);
                WorldSoundPlayer.Play(_hitSound, hitPoint, this);
            }
        }

        public void EnableHitbox()
        {
            Assert.IsNotNull(
                _playerStatus,
                $"{name}: 持ち主の PlayerStatus がありません(親階層に無い場合は SetOwner で渡す)"
            );
            _sweep.Begin(transform.position);
        }

        public void DisableHitbox()
        {
            _sweep.End();
        }

        private void SpawnHitEffect(Vector3 position, Vector3 normal)
        {
            if (_hitEffect == null)
                return;
            EffectPool.Play(_hitEffect, position, ResolveHitEffectRotation(normal));
        }

        private Quaternion ResolveHitEffectRotation(Vector3 normal)
        {
            // 判定がまだ動いていない最初のフレームで当たった場合は、振った向きが分からないので面の向きに合わせる
            Vector3 swingDirection = _sweep.SwingDirection;
            if (_alignHitEffectToSwing && swingDirection != Vector3.zero)
            {
                Vector3 up = normal != Vector3.zero ? normal : Vector3.up;
                return Quaternion.LookRotation(swingDirection, up);
            }
            return normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
        }

        private void OnDrawGizmosSelected()
        {
            MeleeSweep.DrawGizmo(transform.position, _hitboxRadius);
        }
    }
}
