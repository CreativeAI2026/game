using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーを発見した状態での追跡ステート。
    /// 捕捉が切れたら最後に把握した位置へ向かい続け、一定時間捕捉できなければ捜索ステートへ移行する。
    /// 攻撃範囲内に入るか、到達できない位置にいて近づけなくなったら様子見へ遷移する。
    /// </summary>
    public class TBossChaseState : TBossBaseState
    {
        // 見失い後、最後に把握した位置に着いたとみなす距離（スケール1換算）
        private const float LastKnownArriveDistance = 1f;

        private TBossStuckDetector _stuckDetector;
        private bool _isHeadingToLastKnown;
        private readonly bool _continueLostSightTimer;

        /// <param name="continueLostSightTimer">
        /// true なら見失い時間を引き継ぐ。様子見中に物陰へ隠れられて追跡へ戻る場合に使い、見失いの猶予が二重に延びないようにする。
        /// </param>
        public TBossChaseState(TutorialBossController controller, bool continueLostSightTimer = false)
            : base(controller)
        {
            _continueLostSightTimer = continueLostSightTimer;
        }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 追跡ステート開始");

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.RunSpeed;
                boss.Agent.isStopped = false;
                _stuckDetector = new TBossStuckDetector(boss.Agent);
            }

            if (!_continueLostSightTimer)
            {
                boss.LostSightTimer = 0f;
            }
            _isHeadingToLastKnown = false;
            boss.BeginCombatTempo();

            SoundEventBus.OnSoundEmitted += OnSoundHeard;
        }

        public override void Update()
        {
            if (boss.Player == null)
            {
                boss.ChangeState(new TBossPatrolState(boss));
                return;
            }

            if (boss.CheckPlayerTracked())
            {
                UpdateTracking();
            }
            else
            {
                UpdateLostSight();
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 追跡ステート終了");

            SoundEventBus.OnSoundEmitted -= OnSoundHeard;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.ResetPath();
            }
        }

        private void UpdateTracking()
        {
            boss.LostSightTimer = 0f;
            _isHeadingToLastKnown = false;

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
                boss.Agent.SetDestination(boss.Player.transform.position);
            }

            // 攻撃時刻が来ていれば、追いかけている最中でも仕掛ける（走り込み・触手）。
            // これが無いと、離れては追跡に戻るを繰り返すだけで一度も攻撃されなくなる
            if (boss.IsAttackDue && boss.TryStartAttack())
            {
                return;
            }

            float distance = Vector3.Distance(boss.transform.position, boss.Player.transform.position);

            // 様子見の間合いに入ったら様子見へ。
            // ただし間に壁・穴・段差があって直線で詰められない場合、様子見の直線的な間合い管理は成立しないため、
            // 経路探索での追跡を続けて回り込む
            if (distance <= boss.WatchDistanceMax && boss.AttackFeasibility.HasDirectApproach())
            {
                // 毎回様子見から入る一定パターンを崩すため、一定確率で様子見を挟まずそのまま突っ込む
                if (Random.value < boss.DirectAssaultChance && boss.TryStartAttack())
                {
                    return;
                }
                boss.ChangeState(new TBossWatchState(boss));
                return;
            }

            // 見えているのに近づけない（段差の上・NavMesh外など到達不能な位置）。
            // 走り続けて立ち往生せず、様子見で向き合い、射程内なら特殊攻撃（ワイヤー）で引きずり出させる。
            if (_stuckDetector != null && _stuckDetector.Tick(Time.deltaTime))
            {
                Debug.Log("[TutorialBoss] プレイヤーへ到達できないため様子見へ切り替え");
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        private void UpdateLostSight()
        {
            boss.LostSightTimer += Time.deltaTime;

            if (!_isHeadingToLastKnown)
            {
                // 見失った瞬間に立ち止まると間抜けに見えるため、最後に把握した位置までは走り続ける
                _isHeadingToLastKnown = true;
                if (boss.Agent != null)
                {
                    boss.Agent.isStopped = false;
                    boss.Agent.SetDestination(boss.LastKnownPlayerPosition);
                }
                _stuckDetector?.Reset();
            }

            bool reachedLastKnown =
                Vector3.Distance(boss.transform.position, boss.LastKnownPlayerPosition)
                <= LastKnownArriveDistance * boss.BodyScale;
            bool stuck = _stuckDetector != null && _stuckDetector.Tick(Time.deltaTime);

            if (reachedLastKnown || stuck || boss.LostSightTimer >= boss.LostSightDuration)
            {
                boss.LostSightTimer = 0f;
                // 即座にパトロールへ戻さず、最後に見た移動方向から逃げた先を読んで捜索してから諦めさせる
                boss.ChangeState(new TBossSearchState(boss, boss.PredictPlayerPosition()));
            }
        }

        /// <summary>
        /// 見失っている間に、プレイヤーの居場所で鳴る音（足音・ハンマー）を聞いたら、その位置へ向かい直して見失いの猶予も戻す。
        /// 光から外れて走り去るプレイヤーを音で追い続けられる一方、歩けば足音の届く範囲が狭いため振り切れる。
        /// </summary>
        private void OnSoundHeard(SoundEventData data)
        {
            if (boss.CheckPlayerTracked() || !TBossPerception.IsEmittedAtPlayer(data.Type))
            {
                return;
            }

            float distToSound = Vector3.Distance(boss.transform.position, data.Position);
            if (distToSound > data.Radius || distToSound > boss.SoundReactRadius)
            {
                return;
            }

            boss.NotifyPlayerHeardAt(data.Position);
            boss.LostSightTimer = 0f;

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
                boss.Agent.SetDestination(data.Position);
            }
            _isHeadingToLastKnown = true;
            _stuckDetector?.Reset();
        }
    }
}
