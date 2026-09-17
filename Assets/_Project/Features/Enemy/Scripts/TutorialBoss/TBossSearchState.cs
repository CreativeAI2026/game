using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 一度は視認・被弾などでプレイヤーの位置を把握したが、見失った際の捜索ステート。
    /// 最後に把握した位置（または被弾時の推定位置）へ向かい、周囲を数回見回してから
    /// 諦めてパトロールへ戻る。ChaseStateやWatchStateで見失った場合、
    /// および光の外から被弾した場合（ForceAlert）の両方から遷移してくる。
    /// 捜索中は音への感度を保つため、SoundEventBusを購読して目標地点を更新する。
    /// </summary>
    public class TBossSearchState : TBossBaseState
    {
        private enum Phase
        {
            Move,
            LookAround,
        }

        private Phase _phase;
        private Vector3 _target;
        private float _timer;

        // 目標地点への移動を打ち切るまでの時間（経路が届かない・障害物で詰まった場合の保険）
        private const float MoveTimeout = 8f;

        // 到着後に見回す回数と、1方向あたりの向きを維持する時間
        private const int LookAroundCount = 3;
        private const float LookAroundStepDuration = 1.2f;
        private const float LookAroundRotateSpeed = 3f;

        private int _lookAroundStepsDone;
        private float _lookStepTimer;
        private Quaternion _lookTargetRotation;

        public TBossSearchState(TutorialBossController controller, Vector3 target)
            : base(controller)
        {
            _target = target;
        }

        public override void Enter()
        {
            Debug.Log($"[TutorialBoss] 捜索ステート開始 目標:{_target}");

            _phase = Phase.Move;
            _timer = 0f;
            _lookAroundStepsDone = 0;

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.RunSpeed;
                boss.Agent.isStopped = false;
                boss.Agent.SetDestination(_target);
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", true);
            }

            SoundEventBus.OnSoundEmitted += OnSoundHeard;
        }

        public override void Update()
        {
            // 移動中・見回し中を問わず、視界に入ったら即座に追跡へ切り替える
            if (boss.CheckInFlashlight())
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            if (_phase == Phase.Move)
            {
                UpdateMove();
            }
            else
            {
                UpdateLookAround();
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 捜索ステート終了");

            SoundEventBus.OnSoundEmitted -= OnSoundHeard;

            if (boss.Agent != null && boss.Agent.isOnNavMesh)
            {
                boss.Agent.isStopped = false;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", false);
            }
        }

        private void UpdateMove()
        {
            _timer += Time.deltaTime;

            // 経路計算前は remainingDistance が 0 を返すため、hasPath を確認しないと
            // SetDestination の直後に「到着した」と誤判定してしまう
            bool arrived =
                boss.Agent != null
                && !boss.Agent.pathPending
                && boss.Agent.hasPath
                && boss.Agent.remainingDistance <= boss.Agent.stoppingDistance + 0.5f;

            if (arrived || _timer >= MoveTimeout)
            {
                StartLookAround();
            }
        }

        private void StartLookAround()
        {
            _phase = Phase.LookAround;
            _lookAroundStepsDone = 0;

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = true;
            }
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", false);
            }

            PickNextLookAngle();
        }

        private void PickNextLookAngle()
        {
            // 左右交互に見回すことで「探している」感を出す
            float angleOffset = (_lookAroundStepsDone % 2 == 0) ? 70f : -140f;
            _lookTargetRotation =
                Quaternion.AngleAxis(angleOffset, Vector3.up) * boss.transform.rotation;
            _lookStepTimer = 0f;

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("Look");
            }
        }

        private void UpdateLookAround()
        {
            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                _lookTargetRotation,
                Time.deltaTime * LookAroundRotateSpeed
            );

            _lookStepTimer += Time.deltaTime;
            if (_lookStepTimer < LookAroundStepDuration)
            {
                return;
            }

            _lookAroundStepsDone++;
            if (_lookAroundStepsDone >= LookAroundCount)
            {
                GiveUpSearch();
            }
            else
            {
                PickNextLookAngle();
            }
        }

        private void GiveUpSearch()
        {
            Debug.Log("[TutorialBoss] 捜索を打ち切り、パトロールへ復帰");
            boss.IsAlerted = false;
            boss.ChangeState(new TBossPatrolState(boss));
        }

        private void OnSoundHeard(SoundEventData data)
        {
            float distToSound = Vector3.Distance(boss.transform.position, data.Position);
            if (distToSound > data.Radius || distToSound > boss.SoundReactRadius)
            {
                return;
            }

            float confidence = 1f - Mathf.Clamp01(distToSound / boss.SoundReactRadius);
            boss.NotifySoundHeard(confidence);

            // 捜索中に新しい音を聞いたら、そちらへ目標を更新して移動を再開する
            _target = data.Position;
            _phase = Phase.Move;
            _timer = 0f;

            if (boss.Agent != null)
            {
                boss.Agent.speed = boss.RunSpeed;
                boss.Agent.isStopped = false;
                boss.Agent.SetDestination(_target);
            }
            if (boss.Animator != null)
            {
                boss.Animator.SetBool("IsRunning", true);
            }
        }
    }
}
