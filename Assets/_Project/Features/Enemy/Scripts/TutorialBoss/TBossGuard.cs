using System;
using UnityEngine;
using UnityEngine.Assertions;
using Random = UnityEngine.Random;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の防御の判断と、プレイヤーの攻撃の受け止め。
    /// プレイヤーの攻撃が当たるたびに攻撃側から問い合わせを受け（<see cref="IPlayerAttackGuard"/>）、
    /// 防御中で正面から来た攻撃なら受け止めて火花と音を出す。受け止めたかどうかに関係なく殴られた回数を数え、
    /// 連打の最後の一撃が当たったその瞬間に、その攻撃をはじく（<see cref="Repelled"/> を出し、パリィと同じ火花・音・揺れを出す）。
    /// プレイヤーののけぞりとボスの払いは、この通知を受けたその場で同時に始める。
    /// 防御に入る・出る・はじき返すといった行動の切り替えは、コントローラーとステートが行う。
    /// </summary>
    [RequireComponent(typeof(EnemyHpPhaseTracker))]
    [RequireComponent(typeof(TutorialBossSounds))]
    public sealed class TBossGuard : MonoBehaviour, IPlayerAttackGuard
    {
        [SerializeField]
        private TBossGuardSettings _settings = new TBossGuardSettings();

        [Header("受け止めた時の演出（音は TutorialBossSounds が持つ）")]
        [Tooltip("攻撃を受け止めた位置に出す火花")]
        [SerializeField]
        private GameObject _blockEffect;

        [Header("はじき返した時の演出（プレイヤーのパリィと同じ。音は TutorialBossSounds が持つ）")]
        [Tooltip("はじいた攻撃が当たった位置に出す火花")]
        [SerializeField]
        private GameObject _repelEffect;

        [Tooltip("はじいた瞬間のカメラの揺れの強さ。プレイヤーのパリィと同じ値")]
        [SerializeField]
        private float _repelCameraShake = 0.4f;

        private EnemyHpPhaseTracker _phase;
        private TutorialBossSounds _sounds;
        private readonly TBossMashCounter _mashCounter = new TBossMashCounter();

        /// <summary>防御・はじき返しの調整値。</summary>
        public TBossGuardSettings Settings => _settings;

        /// <summary>今、正面からの攻撃を受け止めるか。防御・はじき返しのステートが切り替える。</summary>
        public bool IsGuarding { get; set; }

        /// <summary>今、連打されたらはじき返してよいか。コントローラーが毎フレーム決める。</summary>
        public bool CanRepel { get; set; }

        /// <summary>
        /// 連打の最後の一撃をはじいた瞬間（攻撃が当たったその場）に呼ばれる。引数は攻撃の種類と当たった位置。
        /// </summary>
        public event Action<PlayerAttackKind, Vector3> Repelled;

        private void Awake()
        {
            _phase = GetComponent<EnemyHpPhaseTracker>();
            _sounds = GetComponent<TutorialBossSounds>();
            Assert.AreEqual(
                _phase.PhaseCount,
                _settings.PostAttackGuardPhaseCount,
                $"{name}: 攻撃後の防御確率の数が、HPの段階の数と合っていません"
            );
            Assert.AreEqual(
                _phase.PhaseCount,
                _settings.RangedGuardPhaseCount,
                $"{name}: 遠距離の防御確率の数が、HPの段階の数と合っていません"
            );
            Assert.IsNotNull(
                _blockEffect,
                $"{name}: 受け止めた時の火花（_blockEffect）が未設定です"
            );
            Assert.IsNotNull(
                _repelEffect,
                $"{name}: はじき返した時の火花（_repelEffect）が未設定です"
            );
        }

        /// <inheritdoc/>
        public bool ReceivePlayerAttack(PlayerAttackKind kind, Vector3 hitPoint)
        {
            // ハンマーの範囲攻撃は武器の実体が当たっていないので、はじき返す相手として数えない
            if (kind != PlayerAttackKind.Slam)
            {
                _mashCounter.Register(Time.time);
                if (
                    CanRepel
                    && _mashCounter.IsMashed(
                        Time.time,
                        _settings.RepelWindow,
                        _settings.RepelHitCount
                    )
                )
                {
                    // 最後の一撃はダメージを通さずにはじく。ボスとプレイヤーの反応は通知先がこの場で始める
                    _mashCounter.Clear();
                    PlayRepelImpact(hitPoint);
                    Repelled?.Invoke(kind, hitPoint);
                    return true;
                }
            }

            if (
                !IsGuarding
                || !IsInGuardArc(
                    transform.position,
                    transform.forward,
                    hitPoint,
                    _settings.GuardHalfAngle
                )
            )
            {
                return false;
            }

            EffectPool.Play(_blockEffect, hitPoint, OutwardRotation(hitPoint));
            _sounds.PlayGuardBlock(hitPoint);
            return true;
        }

        private void PlayRepelImpact(Vector3 hitPoint)
        {
            EffectPool.Play(_repelEffect, hitPoint, OutwardRotation(hitPoint));
            _sounds.PlayRepel(hitPoint);
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.Shake(_repelCameraShake);
            }
        }

        /// <summary>当たった位置からボスの外へ向く向き。火花をプレイヤー側へ散らすのに使う。</summary>
        private Quaternion OutwardRotation(Vector3 hitPoint)
        {
            Vector3 outward = hitPoint - transform.position;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(outward)
                : transform.rotation;
        }

        /// <summary>殴られた回数の記録を消す。はじき返した後や、数えない行動（攻撃・怯みなど）の間に呼ぶ。</summary>
        public void ClearMash() => _mashCounter.Clear();

        /// <summary>プレイヤーが攻撃ボタンを押した瞬間に、反応して防御するかを引く。</summary>
        public bool RollReactiveGuard() => Random.value < _settings.ReactiveGuardChance;

        /// <summary>様子見に入った時に、読みで防御するかを引く。当たれば防御の長さも決める。</summary>
        public bool RollReadGuard(out float duration)
        {
            Vector2 range = _settings.ReadGuardDurationRange;
            duration = Random.Range(range.x, range.y);
            return Random.value < _settings.ReadGuardChance;
        }

        /// <summary>攻撃の後に防御するかを、今のHPの段階の確率で引く。</summary>
        public bool RollPostAttackGuard() =>
            Random.value < _settings.GetPostAttackGuardChance(_phase.CurrentPhaseIndex);

        /// <summary>距離を取った遠距離攻撃に対して、構えてにじり寄るかを今のHPの段階の確率で引く。</summary>
        public bool RollRangedGuard() =>
            Random.value < _settings.GetRangedGuardChance(_phase.CurrentPhaseIndex);

        /// <summary>
        /// 当たった位置が、ボスの正面から左右 halfAngle 度以内か（水平方向だけで見る）。
        /// </summary>
        public static bool IsInGuardArc(
            Vector3 bossPosition,
            Vector3 bossForward,
            Vector3 hitPoint,
            float halfAngle
        )
        {
            Vector3 toHit = hitPoint - bossPosition;
            toHit.y = 0f;
            bossForward.y = 0f;
            if (toHit.sqrMagnitude < 0.0001f || bossForward.sqrMagnitude < 0.0001f)
            {
                // 体の中心に当たった（向きが決まらない）時は受け止める側に倒す
                return true;
            }
            return Vector3.Angle(bossForward, toHit) <= halfAngle;
        }
    }
}
