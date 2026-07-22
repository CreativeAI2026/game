using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 未発見状態での徘徊ステート。
    /// SoundEventBusを購読し、音を検知したらSoundInvestigateStateへ遷移する。
    /// プレイヤーが懐中電灯の光に入ったらChaseStateへ遷移する。
    /// </summary>
    public class TBossPatrolState : TBossBaseState
    {
        // 次のランダム徘徊目標までの待機タイマー
        private float _wanderTimer;
        private const float WanderInterval = 4f;
        private const float WanderRadius = 8f;

        public TBossPatrolState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] パトロールステート開始");

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.WalkSpeed;
                boss.Agent.isStopped = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", false);
            }

            _wanderTimer = WanderInterval; // 即座に最初の目標を設定させる

            // 音イベント購読
            SoundEventBus.OnSoundEmitted += OnSoundHeard;
        }

        public override void Update()
        {
            // プレイヤーが光に入ったら発見
            if (boss.CheckInFlashlight())
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
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

        private void OnSoundHeard(SoundEventData data)
        {
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
            boss.ChangeState(new TBossSoundInvestigateState(boss, confidence));
        }
    }
}
