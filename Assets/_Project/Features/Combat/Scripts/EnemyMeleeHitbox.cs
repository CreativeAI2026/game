using System;
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

        private readonly MeleeSweep _sweep = new MeleeSweep();

        // 毎フレームの割り当てを避けるため、当たったときの処理を一度だけデリゲートにしておく
        private MeleeSweep.HitHandler _onHit;

        /// <summary>
        /// プレイヤーに武器が接触した瞬間に発火する（ガード成立・ダメージ無効時も発火する）。
        /// 攻撃側が「当たったこと自体」に対する演出（引き寄せ等）を行いたい場合に使う。
        /// 引数はヒット座標。
        /// </summary>
        public event Action<Vector3> OnHitLanded;

        private void Awake()
        {
            _onHit = ProcessHit;
            if (_enemyStatus == null)
            {
                _enemyStatus = GetComponentInParent<EnemyStatus>();
            }
        }

        private void Update()
        {
            _sweep.Sweep(transform.position, _hitboxRadius, _targetLayer, _onHit);
        }

        private void ProcessHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (other.CompareTag("Player"))
            {
                // 1スイングにつき同一対象へは1回だけヒットさせる
                _sweep.MarkHit(other);

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
            Quaternion rotation =
                normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
            EffectPool.Play(_hitEffect, position, rotation);
        }

        private void OnDrawGizmosSelected()
        {
            MeleeSweep.DrawGizmo(transform.position, _hitboxRadius);
        }
    }
}
