using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 音を検知した際の調査（RE4 ガラドール風）。音源が近い（高確信）なら歩いて向かい、遠い（低確信）なら振り返るだけ。
    /// 音源付近に着いたらパトロールへ、移動中に視認したら Chase へ。
    /// </summary>
    public class TBossSoundInvestigateState : TBossBaseState
    {
        // 確信度：近いほど1に近い（1=必ず向かう / 0=振り返るだけ）
        private float _confidence;
        private bool _hasTriggeredLook;

        // この確信度以上なら音源へ向かう（以下なら振り返るだけ）
        private const float WalkThreshold = 0.5f;

        // この確信度以上なら音源へ走る（以下なら歩く）。
        // 走り速度はプレイヤーのダッシュと同等なので、遠めの物音にまで全力で駆けつけると逃げ場が無くなる
        private const float RunThreshold = 0.8f;

        private float _timeoutTimer;
        private const float Timeout = 12f;

        // 音源に着いたとみなす距離と、経路残距離の余裕（スケール1換算。BodyScaleを掛けて使う）
        private const float SoundArriveDistance = 2f;
        private const float ArriveMargin = 0.5f;

        private TBossStuckDetector _stuckDetector;

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
            _hasTriggeredLook = false;

            if (_mode == Mode.Walk)
            {
                // 音源へ向かう（確信度が高ければ走り、そうでなければ歩く）
                if (boss.Agent != null)
                {
                    boss.Agent.speed = SpeedForConfidence();
                    boss.SetAgentStopped(false);
                    boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                }
            }
            else
            {
                // 確信度が低い：その場で向くだけ
                if (boss.Agent != null)
                {
                    boss.SetAgentStopped(true);
                }
            }

            if (boss.Agent != null)
            {
                _stuckDetector = new TBossStuckDetector(boss.Agent);
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

                // 向きが概ね一致したら Look トリガーを引く
                Vector3 dir = (boss.LastHeardSoundPosition - boss.transform.position);
                dir.y = 0f;
                if (!_hasTriggeredLook && dir.sqrMagnitude > 0.001f)
                {
                    float angle = Vector3.Angle(boss.transform.forward, dir.normalized);
                    if (angle < 5f)
                    {
                        if (boss.Animator != null)
                        {
                            boss.PlayLookAround();
                        }
                        _hasTriggeredLook = true;
                    }
                }

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

            // Walk モード：ボス本体を音源方向に向けながら歩く（懐中電灯は体の向きに追従する）
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

            // 音源の近くまで来たら調査完了
            float distToTarget = Vector3.Distance(
                boss.transform.position,
                boss.LastHeardSoundPosition
            );
            // 経路計算前は remainingDistance が 0 を返すため、hasPath を確認しないと
            // SetDestination の直後に「到着した」と誤判定してしまう
            bool arrived =
                distToTarget <= SoundArriveDistance * boss.BodyScale
                || (
                    boss.Agent != null
                    && !boss.Agent.pathPending
                    && boss.Agent.hasPath
                    && boss.Agent.remainingDistance
                        <= boss.Agent.stoppingDistance + ArriveMargin * boss.BodyScale
                );

            // 音源がNavMesh外・到達不能だと着かないまま止まるため、詰まったらそこで調査を切り上げる
            bool stuck = _stuckDetector != null && _stuckDetector.Tick(Time.deltaTime);
            bool timedOut = _timeoutTimer >= Timeout;

            if (arrived || stuck || timedOut)
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
                boss.SetAgentStopped(false);
            }
        }

        private float SpeedForConfidence()
        {
            return _confidence >= RunThreshold ? boss.RunSpeed : boss.WalkSpeed;
        }

        private void RotateTowardSound()
        {
            Vector3 dir = (boss.LastHeardSoundPosition - boss.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                return;
            }

            // 懐中電灯はボス本体の向きに固定されているため、体を音源へ向けるだけで
            // 懐中電灯も追従する（別途懐中電灯だけを回す処理は不要）。
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            boss.transform.rotation = Quaternion.Slerp(
                boss.transform.rotation,
                targetRot,
                Time.deltaTime * 3f
            );
        }

        private void OnArrived()
        {
            // 音源に到達。光の中にいれば既にUpdateで追跡へ移っている。
            // 光の外でも、すぐ近くで遮蔽物なく見通せる位置にいれば気づいて交戦に入る。
            // 以前は距離だけで攻撃していたため、壁の向こうや背後の見えていないプレイヤーにも攻撃していた
            if (boss.CheckPlayerTracked())
            {
                boss.IsAlerted = true;
                boss.ChangeState(new TBossChaseState(boss));
                return;
            }

            // 気づけなければArrivedモードに移行して周囲を警戒
            _mode = Mode.Arrived;
            _arrivedTimer = 0f;

            if (boss.Agent != null)
            {
                boss.SetAgentStopped(true);
            }

            if (!_hasTriggeredLook && boss.Animator != null)
            {
                boss.PlayLookAround();
                _hasTriggeredLook = true;
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

            if (!boss.TryHearSound(data, out float newConfidence))
            {
                return;
            }

            // 新しい音の確信度が今より高ければ目標を更新
            boss.NotifySoundHeard(newConfidence);
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
                        // Enterで設定する移動速度と揃える
                        boss.Agent.speed = SpeedForConfidence();
                        boss.SetAgentStopped(false);
                        boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                    }
                }
                else if (_mode == Mode.Walk && boss.Agent != null)
                {
                    boss.Agent.speed = SpeedForConfidence();
                    boss.Agent.SetDestination(boss.LastHeardSoundPosition);
                }

                // 目的地が変わったので、進み具合の計測をやり直す
                _stuckDetector?.Reset();
            }
        }
    }
}
