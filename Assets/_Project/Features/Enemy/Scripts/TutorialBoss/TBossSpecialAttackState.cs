using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 触手を使った特殊攻撃ステート。LineRendererで5本の触手を演出する。
    /// フェーズ1（狙い）：各触手をサイン波＋ノイズでランダムにうねらせながらターゲットを向き続ける
    /// フェーズ2（射出）：全触手を一気に直線伸展し、先端付近の複数点でOverlapSphereによる当たり判定を行う
    /// ヒット時はCapturedStateへ、外れ/タイムアウトはWatchStateへ遷移する。
    /// </summary>
    public class TBossSpecialAttackState : TBossBaseState
    {
        private float _timer;
        private bool _hasHit;

        // フェーズ管理
        private enum Phase
        {
            Aim,
            Shoot,
            Keep, // 伸びきった状態を維持
            Retract, // 引っ込む
        }

        private Phase _currentPhase;

        // 狙いフェーズ中の最大伸長距離（演出）
        private const float AimTentacleLength = 20f;

        // 射出フェーズの当たり判定半径
        private const float TentacleHitRadius = 0.25f;

        private const float AimNoiseStrength = 90f; // ノイズによるランダム成分（度数）

        // キープと引っ込みフェーズの時間設定
        private const float KeepDuration = 0.5f; // 伸ばしきった状態でキープする時間
        private const float RetractDuration = 0.3f; // 引っ込むのにかかる時間

        // 射出開始時点で固定したターゲット座標
        private Vector3 _shootOrigin;

        // 初期状態
        private Quaternion _initialLocalRot;

        public TBossSpecialAttackState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            _hasHit = false;
            _currentPhase = Phase.Aim;

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("SpecialAttack");
            }

            if (boss.WireRig != null)
            {
                boss.WireRig.weight = 0f;
            }

            if (boss.WireBone != null)
            {
                _initialLocalRot = boss.WireBone.localRotation;
            }
            else
            {
                Debug.LogWarning("[TutorialBoss] WireBone が未設定です。");
            }

            if (boss.ForceStraightWireByRig && boss.WireRig != null)
            {
                boss.WireRig.weight = 1f;
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            // Aimフェーズは AnimationEvent (OnPoseReady) で終了するため、Update内でのタイマー遷移は行わない

            if (_currentPhase == Phase.Shoot)
            {
                if (_timer >= boss.SpecialAttackShootDuration)
                {
                    if (!_hasHit)
                    {
                        StartKeepPhase();
                    }
                }
            }
            else if (_currentPhase == Phase.Keep)
            {
                if (_timer >= KeepDuration)
                {
                    StartRetractPhase();
                }
            }
            else if (_currentPhase == Phase.Retract)
            {
                if (_timer >= RetractDuration)
                {
                    boss.ChangeState(new TBossWatchState(boss));
                }
            }
        }

        public override void LateUpdate()
        {
            if (_currentPhase == Phase.Aim)
            {
                UpdateAimPhase();
            }
            else if (_currentPhase == Phase.Shoot)
            {
                UpdateShootPhase();
            }
            else if (_currentPhase == Phase.Keep)
            {
                UpdateKeepPhase();
            }
            else if (_currentPhase == Phase.Retract)
            {
                UpdateRetractPhase();
            }
        }

        public override void Exit()
        {
            if (!_hasHit && boss.WireBone != null)
            {
                // リセット
                boss.WireBone.localPosition = boss.InitialWireBoneLocalPosition;
                boss.WireBone.localRotation = _initialLocalRot;
            }

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }

            boss.Animator.speed = 1f;

            if (boss.WireRig != null)
            {
                boss.WireRig.weight = 0f;
            }
        }

        public void OnPoseReady()
        {
            if (_currentPhase == Phase.Aim)
            {
                StartShootPhase();
            }
        }

        private void ApplyWireTransform(float currentLength, float aimProgress = 0f)
        {
            if (boss.WireBone == null)
                return;

            Vector3 forward = boss.transform.forward;
            Vector3 originPos =
                boss.WireBone.parent != null
                    ? boss.WireBone.parent.TransformPoint(boss.InitialWireBoneLocalPosition)
                    : boss.transform.position;

            // ノイズ（Aimフェーズ用）
            float noiseX = 0f;
            float noiseZ = 0f;
            if (aimProgress > 0f)
            {
                float timeOffset = Time.time * 5f;
                noiseX =
                    (Mathf.PerlinNoise(timeOffset, 0f) - 0.5f) * AimNoiseStrength * aimProgress;
                noiseZ =
                    (Mathf.PerlinNoise(0f, timeOffset) - 0.5f) * AimNoiseStrength * aimProgress;
            }

            // ボスの真正面（forward）に向かせる基本回転にノイズを加える
            Quaternion finalRot =
                Quaternion.FromToRotation(Vector3.up, forward)
                * Quaternion.Euler(noiseX, 0f, noiseZ);

            if (boss.ForceStraightWireByRig)
            {
                if (boss.WireIkTarget != null)
                {
                    // IKターゲットを真正面に移動させてRig(Position Constraint)に引っ張らせる
                    boss.WireIkTarget.position = originPos + forward * currentLength;
                }
                // 回転はスクリプトで直接真正面に補正し、アニメーションのねじれを打ち消す
                boss.WireBone.rotation = finalRot;
            }
            else
            {
                // スクリプトで強制的にワールド座標系で位置と回転を真正面に上書きする
                boss.WireBone.position = originPos + forward * currentLength;
                boss.WireBone.rotation = finalRot;
            }
        }

        private void UpdateAimPhase()
        {
            if (boss.WireBone == null)
                return;

            Vector3 targetPos = GetTargetPosition();

            Vector3 dirToTarget = (targetPos - boss.transform.position);
            dirToTarget.y = 0f;
            if (dirToTarget.sqrMagnitude > 0.001f)
            {
                boss.transform.rotation = Quaternion.Slerp(
                    boss.transform.rotation,
                    Quaternion.LookRotation(dirToTarget.normalized),
                    Time.deltaTime * 10f
                );
            }

            boss.RotateFlashlightToward(targetPos);

            float aimProgress = Mathf.Clamp01(_timer / boss.SpecialAttackAimDuration);
            float currentLength = AimTentacleLength * aimProgress;

            // Aimフェーズ（ポーズ完了前）は強制上書き(ApplyWireTransform)を行わず、アニメーションの腕に追従させる
            float timeOffset = Time.time * 5f;
            float noiseX =
                (Mathf.PerlinNoise(timeOffset, 0f) - 0.5f) * AimNoiseStrength * aimProgress;
            float noiseZ =
                (Mathf.PerlinNoise(0f, timeOffset) - 0.5f) * AimNoiseStrength * aimProgress;

            // 回転リセットしつつノイズ追加
            boss.WireBone.localEulerAngles = new Vector3(noiseX, 0f, noiseZ);

            // 親（腕）のローカルY方向に伸ばすことで、腕の動きに追従させる
            boss.WireBone.localPosition =
                boss.InitialWireBoneLocalPosition + Vector3.up * currentLength;
        }

        private void StartShootPhase()
        {
            _currentPhase = Phase.Shoot;
            _timer = 0f;
        }

        private void UpdateShootPhase()
        {
            if (_hasHit)
                return;

            float shootProgress = Mathf.Clamp01(_timer / boss.SpecialAttackShootDuration);
            float currentLength = boss.SpecialAttackRange * shootProgress;

            ApplyWireTransform(currentLength);

            CheckHitAtTip();
        }

        private void StartKeepPhase()
        {
            _currentPhase = Phase.Keep;
            _timer = 0f;
        }

        private void UpdateKeepPhase()
        {
            if (_hasHit)
                return;

            float currentLength = boss.SpecialAttackRange;
            ApplyWireTransform(currentLength);
        }

        private void StartRetractPhase()
        {
            _currentPhase = Phase.Retract;
            _timer = 0f;
        }

        private void UpdateRetractPhase()
        {
            if (_hasHit)
                return;

            float retractProgress = Mathf.Clamp01(_timer / RetractDuration);
            float currentLength = boss.SpecialAttackRange * (1f - retractProgress);

            ApplyWireTransform(currentLength);
        }

        private void CheckHitAtTip()
        {
            if (boss.WireBone == null)
                return;

            // 先端位置はWireBoneの現在座標
            Vector3 tipPosition = boss.WireBone.position;

            Collider[] hits = Physics.OverlapSphere(tipPosition, TentacleHitRadius);
            foreach (var col in hits)
            {
                if (col.CompareTag("Player"))
                {
                    _hasHit = true;
                    Debug.Log("[TutorialBoss] 特殊攻撃 ヒット！ 捕獲ステートへ遷移");
                    boss.ChangeState(new TBossCapturedState(boss));
                    return;
                }
            }
        }

        private Vector3 GetTargetPosition()
        {
            if (boss.Player != null)
            {
                return boss.Player.transform.position + Vector3.up * 1f; // 首〜胸元あたりを狙う
            }
            return boss.LastHeardSoundPosition;
        }
    }
}
