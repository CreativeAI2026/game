using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 未発見時の徘徊。SoundEventBus で音を検知したら SoundInvestigateState へ。光に入っても即発見せず、発見度（Awareness）が上限に達して ChaseState へ遷移する。
    /// ちらっと見えた程度（Suspicious）の間は立ち止まってプレイヤー方向を向くだけに留める。
    /// </summary>
    public class TBossPatrolState : TBossBaseState
    {
        // 次のランダム徘徊目標までの待機タイマー
        private float _wanderTimer;
        private const float WanderInterval = 4f;
        private const float WanderRadius = 8f;

        // Suspicious状態に入った際に一度だけLookトリガーを引くためのフラグ
        private bool _hasTriggeredLook;

        // プレイヤーへ向き直る補間速度
        private const float NoticeRotateSpeed = 3f;

        // 怪しんでいる間にプレイヤーが光から消えて、この時間（秒）見えなければ、見えた場所を確かめに行く
        private const float GlimpseCheckDelay = 1f;

        // 今回の「怪しい」の間に実際にプレイヤーを光で捉えたか。
        // 音の調査から戻った直後など、見てもいないのに古い既知位置へ確かめに行かないための区別
        private bool _sawPlayerWhileSuspicious;
        private float _glimpseLostTimer;

        public TBossPatrolState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] パトロールステート開始");

            // 交戦が終わったので、次に見つけた時は攻撃までの待ち時間を新しく決める
            boss.EndCombatTempo();

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.WalkSpeed;
                boss.Agent.isStopped = false;
            }

            _wanderTimer = WanderInterval; // 即座に最初の目標を設定させる
            _hasTriggeredLook = false;

            // 音イベント購読
            SoundEventBus.OnSoundEmitted += OnSoundHeard;
        }

        public override void Update()
        {
            // 発見度が上限に達したら完全発見
            if (boss.IsFullyAware)
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            // ちらっと見えた等で怪しんでいる間は、立ち止まって見えた場所を向く
            if (boss.IsSuspicious)
            {
                if (!_hasTriggeredLook)
                {
                    _hasTriggeredLook = true;
                    _sawPlayerWhileSuspicious = false;
                    _glimpseLostTimer = 0f;
                    if (boss.Agent != null)
                    {
                        boss.Agent.isStopped = true;
                    }
                    if (boss.Animator != null)
                    {
                        boss.Animator.SetTrigger("Look");
                    }
                }

                if (UpdateGlimpse())
                {
                    return;
                }

                FaceTowardLastKnown();
                return;
            }

            if (_hasTriggeredLook)
            {
                // 疑いが晴れたので徘徊を再開する
                _hasTriggeredLook = false;
                if (boss.Agent != null)
                {
                    boss.Agent.isStopped = false;
                }
            }

            // ランダム徘徊
            _wanderTimer += Time.deltaTime;
            if (_wanderTimer >= WanderInterval)
            {
                _wanderTimer = 0f;
                SetRandomWanderTarget();
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] パトロールステート終了");

            // 音イベント解除
            SoundEventBus.OnSoundEmitted -= OnSoundHeard;
        }

        private void SetRandomWanderTarget()
        {
            if (boss.Agent == null)
            {
                return;
            }

            // 現在地の周辺のランダムな地点をNavMesh上で探す
            Vector3 randomDir = Random.insideUnitSphere * WanderRadius;
            randomDir += boss.transform.position;
            randomDir.y = boss.transform.position.y;

            if (
                NavMesh.SamplePosition(
                    randomDir,
                    out NavMeshHit hit,
                    WanderRadius,
                    NavMesh.AllAreas
                )
            )
            {
                boss.Agent.SetDestination(hit.position);
            }
        }

        /// <summary>
        /// 怪しんでいる間にプレイヤーが光から消えたら、少し待ってから見えた場所を歩いて確かめに行く。
        /// 遷移した場合は true を返す。
        /// </summary>
        private bool UpdateGlimpse()
        {
            if (boss.CheckInFlashlight())
            {
                _sawPlayerWhileSuspicious = true;
                _glimpseLostTimer = 0f;
                return false;
            }

            if (!_sawPlayerWhileSuspicious || !boss.HasKnownPlayerPosition)
            {
                return false;
            }

            _glimpseLostTimer += Time.deltaTime;
            if (_glimpseLostTimer < GlimpseCheckDelay)
            {
                return false;
            }

            Debug.Log("[TutorialBoss] 見えた気がした場所を確かめに行く");
            boss.ChangeState(new TBossSearchState(boss, boss.LastKnownPlayerPosition, cautious: true));
            return true;
        }

        /// <summary>
        /// 怪しんでいる間、その場で最後に見えた場所へ向き直る（懐中電灯は体の向きに追従する）。
        /// 実際のプレイヤー位置を向くと、見えていないのに物陰の移動を目で追う透視になってしまう。
        /// </summary>
        private void FaceTowardLastKnown()
        {
            if (!boss.HasKnownPlayerPosition)
            {
                return;
            }

            Vector3 dir = boss.LastKnownPlayerPosition - boss.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                return;
            }

            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                Quaternion.LookRotation(dir.normalized),
                Time.deltaTime * NoticeRotateSpeed
            );
        }

        private void OnSoundHeard(SoundEventData data)
        {
            // 音よりもスポットライトでの視認を最優先する（完全発見のみ即座に切り替える）
            if (boss.IsFullyAware)
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            // 半径フィルタ：音の届く範囲内かチェック
            float distToSound = Vector3.Distance(boss.transform.position, data.Position);
            if (distToSound > data.Radius)
            {
                return;
            }

            // 自分の反応半径内かチェック
            if (distToSound > boss.SoundReactRadius)
            {
                return;
            }

            boss.LastHeardSoundPosition = data.Position;

            // 距離が近いほど確信度が高い（远ければ振り返るだけ、近ければ歩いて向かう）
            float confidence = 1f - Mathf.Clamp01(distToSound / boss.SoundReactRadius);
            boss.NotifySoundHeard(confidence);
            boss.ChangeState(new TBossSoundInvestigateState(boss, confidence));
        }
    }
}
