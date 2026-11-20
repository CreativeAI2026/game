using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TBoss の防御の判断と、プレイヤーの攻撃の受け止め。
    /// プレイヤーの攻撃が当たるたびに攻撃側から問い合わせを受け（<see cref="IPlayerAttackGuard"/>）、
    /// 防御中で正面から来た攻撃なら受け止めて火花と音を出す。受け止めたかどうかに関係なく殴られた回数を数え、
    /// 連打されているか（はじき返すか）をコントローラーへ返す。
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

        private EnemyHpPhaseTracker _phase;
        private TutorialBossSounds _sounds;
        private readonly TBossMashCounter _mashCounter = new TBossMashCounter();

        /// <summary>防御・はじき返しの調整値。</summary>
        public TBossGuardSettings Settings => _settings;

        /// <summary>今、正面からの攻撃を受け止めるか。防御・はじき返しのステートが切り替える。</summary>
        public bool IsGuarding { get; set; }

        /// <summary>直近に連打されていて、はじき返す条件を満たしているか。</summary>
        public bool IsMashed =>
            _mashCounter.IsMashed(Time.time, _settings.RepelWindow, _settings.RepelHitCount);

        private void Awake()
        {
            _phase = GetComponent<EnemyHpPhaseTracker>();
            _sounds = GetComponent<TutorialBossSounds>();
            Assert.AreEqual(
                _phase.PhaseCount,
                _settings.PostAttackGuardPhaseCount,
                $"{name}: 攻撃後の防御確率の数が、HPの段階の数と合っていません"
            );
            Assert.IsNotNull(_blockEffect, $"{name}: 受け止めた時の火花（_blockEffect）が未設定です");
        }

        /// <inheritdoc/>
        public bool ReceivePlayerAttack(PlayerAttackKind kind, Vector3 hitPoint)
        {
            // ハンマーの範囲攻撃は武器の実体が当たっていないので、はじき返す相手として数えない
            if (kind != PlayerAttackKind.Slam)
            {
                _mashCounter.Register(Time.time);
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

            Vector3 outward = hitPoint - transform.position;
            outward.y = 0f;
            Quaternion rotation =
                outward.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(outward)
                    : transform.rotation;
            EffectPool.Play(_blockEffect, hitPoint, rotation);
            _sounds.PlayGuardBlock(hitPoint);
            return true;
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
