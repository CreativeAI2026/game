using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 音を検知した際の調査ステート。
    /// バイオRE4ガラドールのように、音源の距離に応じて確信度を変化させ、
    /// 近距離（高確信）→その場所まで歩いて向かう
    /// 遠距離（低確信）→その方向に振り返るだけで動かない
    /// 音源の近くまで来たらパトロールに戻る。移動中に視認したらChaseへ。
    /// </summary>
    public class TBossSoundInvestigateState : TBossBaseState
    {
        // 確信度：近いほど1に近い（1=必ず向かう / 0=振り返るだけ）
        private float _confidence;

        // この確信度以上なら音源へ向かう（以下なら振り返るだけ）
        private const float WalkThreshold = 0.5f;

        private float _timeoutTimer;
        private const float Timeout = 12f;

        // 振り返り中のタイマー（LookOnly モード時）
        private float _lookOnlyTimer;
        private const float LookOnlyDuration = 3f; // 振り返るだけで3秒後にパトロールへ

        // 到着後の待機タイマー（Arrived モード時）
        private float _arrivedTimer;
        private const float ArrivedWaitDuration = 3f; // 到着後3秒間周囲を見回す

        private enum Mode
        {
            Walk, // 音源へ歩いて向かう
            LookOnly, // その方向を向くだけ
            Arrived, // 到着して周囲を見回す
        }

        private Mode _mode;

        public TBossSoundInvestigateState(TutorialBossController controller, float confidence)
            : base(controller)
        {
            _confidence = confidence;
        }

        public override void Enter()
        {
            Debug.Log($"[TutorialBoss] 音調査ステート開始 確信度:{_confidence:F2}");

            _mode = _confidence >= WalkThreshold ? Mode.Walk : Mode.LookOnly;
            _timeoutTimer = 0f;
            _lookOnlyTimer = 0f;
            _arrivedTimer = 0f;

            if (_mode == Mode.Walk)
            {
                // 確信度が高い：歩いて向かう（走らない。不意打ちでないぶん理不尽感を減らす）
                if (boss.Agent != null)
                {
                    boss.Agent.speed = boss.WalkSpeed;
                    boss.Agent.isStopped = false;
                    boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                }

                if (boss.Animator != null)
                {
                    boss.Animator.SetBool("IsRunning", false);
                }
            }
            else
            {
                // 確信度が低い：その場で向くだけ
                if (boss.Agent != null)
                {
                    boss.Agent.isStopped = true;
                }
            }

            // 音イベント購読（別の音が鳴ったら確信度を更新）
            SoundEventBus.OnSoundEmitted += OnSoundHeard;
        }

        public override void Update()
        {
            // 視認できたら即追跡
            if (boss.CheckInFlashlight())
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            if (_mode == Mode.LookOnly)
            {
                // 音源方向に振り返るだけ
                RotateTowardSound();

                _lookOnlyTimer += Time.deltaTime;
                if (_lookOnlyTimer >= LookOnlyDuration)
                {
                    boss.ChangeState(new TBossPatrolState(boss));
                }
                return;
            }

            if (_mode == Mode.Arrived)
            {
                // 到着後はその場で少し待機し、周囲を見回す
                _arrivedTimer += Time.deltaTime;
                if (_arrivedTimer >= ArrivedWaitDuration)
                {
                    boss.ChangeState(new TBossPatrolState(boss));
                }
                return;
            }

            // Walk モード：ボス本体を音源方向に向けながら歩く
            // ※ ここでRotateFlashlightTowardを呼ばないことで、懐中電灯がボス体の向きと連動し、
            //   プレイヤーが光に入ったときに CheckInFlashlight() が正常に true を返せるようになる。
            Vector3 dirToSound = (boss.LastHeardSoundPosition - boss.transform.position);
            dirToSound.y = 0f;
            if (dirToSound.sqrMagnitude > 0.001f)
            {
                boss.transform.rotation = Quaternion.Slerp(
                    boss.transform.rotation,
                    Quaternion.LookRotation(dirToSound.normalized),
                    Time.deltaTime * 2f
                );
            }

            _timeoutTimer += Time.deltaTime;

            // 音源の近く（2m以内）まで来たら調査完了
            float distToTarget = Vector3.Distance(
                boss.transform.position,
                boss.LastHeardSoundPosition
            );
            bool arrived =
                distToTarget <= 2f
                || (
                    boss.Agent != null
                    && !boss.Agent.pathPending
                    && boss.Agent.remainingDistance <= boss.Agent.stoppingDistance + 0.5f
                );

            bool timedOut = _timeoutTimer >= Timeout;

            if (arrived || timedOut)
            {
                OnArrived();
            }
        }

        public override void Exit()
        {
            Debug.Log("[TutorialBoss] 音調査ステート終了");

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

        private void RotateTowardSound()
        {
            Vector3 dir = (boss.LastHeardSoundPosition - boss.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                targetRot,
                Time.deltaTime * 3f
            );

            // LookOnlyモード時のみ懐中電灯を音源に向ける。
            // Walkモードで呼ぶとプレイヤーが光円錐に入れなくなるため呼ばない。
            // （懐中電灯はボス本体の向きに連動させる）
            boss.RotateFlashlightToward(boss.LastHeardSoundPosition);
        }

        private void OnArrived()
        {
            // 音源に到達。スポットライトで周囲を確認（すでにCheckInFlashlightで判定済み）
            // プレイヤーが攻撃範囲内（特殊攻撃射程の半分、または通常攻撃の長い方）なら攻撃
            if (boss.Player != null)
            {
                float dist = Vector3.Distance(
                    boss.transform.position,
                    boss.Player.transform.position
                );
                float attackThreshold = Mathf.Max(boss.AttackRange, boss.SpecialAttackRange * 0.5f);
                if (dist <= attackThreshold)
                {
                    boss.TransitionToAttack();
                    return;
                }
            }

            // 攻撃圏外ならArrivedモードに移行して周囲を警戒
            _mode = Mode.Arrived;
            _arrivedTimer = 0f;

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = true;
            }
        }

        private void OnSoundHeard(SoundEventData data)
        {
            // 音よりもスポットライトでの視認を最優先する
            if (boss.CheckInFlashlight())
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            float distToSound = Vector3.Distance(boss.transform.position, data.Position);
            if (distToSound > data.Radius || distToSound > boss.SoundReactRadius)
            {
                return;
            }

            // 新しい音の確信度を計算し、今より高ければ目標を更新
            float newConfidence = 1f - Mathf.Clamp01(distToSound / boss.SoundReactRadius);
            if (newConfidence > _confidence)
            {
                _confidence = newConfidence;
                boss.LastHeardSoundPosition = data.Position;

                if (_mode == Mode.LookOnly && _confidence >= WalkThreshold)
                {
                    // 確信度が上がったので歩きモードに切り替え
                    _mode = Mode.Walk;
                    if (boss.Agent != null)
                    {
                        boss.Agent.speed = boss.WalkSpeed;
                        boss.Agent.isStopped = false;
                        boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                    }
                }
                else if (_mode == Mode.Walk && boss.Agent != null)
                {
                    boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                }
            }
        }
    }
}
