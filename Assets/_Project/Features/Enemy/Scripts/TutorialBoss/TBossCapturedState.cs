using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class TBossCapturedState : TBossBaseState
    {
        private const string GrabbedTrigger = "Grabbed";
        private const string EscapedTrigger = "Escaped";
        private const string NeckBoneName = "Neck";

        private const float WaitBeforePullDuration = 0.5f;
        private const float WaitBeforeDamageDuration = 0.5f;

        private enum Phase
        {
            WaitBeforePull,
            Pull,
            WaitBeforeDamage,
            Captured,
            Escape,
        }

        private Phase _phase;
        private float _timer;
        private float _damageTimer;
        private float _escapeGauge;
        private Vector2 _prevMoveInput;
        private Vector3 _knockbackVelocity;
        private ParticleSystem _electricEffect;

        private PlayerController _playerController;
        private PlayerInputHandler _playerInput;
        private PlayerStatus _playerStatus;
        private Animator _playerAnimator;
        private CharacterController _playerCC;
        private Rigidbody _playerRb;
        private HeadLookController _headLookController;
        private Transform _neckBone;

        private Quaternion _initialLocalRot;
        private Vector3 _escapeStartLocalPos;
        private Quaternion _escapeStartLocalRot;

        public TBossCapturedState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 捕獲ステート開始: WaitBeforePull フェーズ");

            if (boss.Player == null)
            {
                boss.ChangeState(new TBossWatchState(boss));
                return;
            }

            _playerController = boss.Player.GetComponent<PlayerController>();
            _playerInput = boss.Player.GetComponent<PlayerInputHandler>();
            _playerStatus = boss.Player.GetComponent<PlayerStatus>();
            _playerAnimator = boss.Player.GetComponent<Animator>();
            _playerCC = boss.Player.GetComponent<CharacterController>();
            _playerRb = boss.Player.GetComponent<Rigidbody>();
            _headLookController = boss.Player.GetComponent<HeadLookController>();

            _neckBone = FindBone(boss.Player.transform, NeckBoneName);

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (_playerController != null)
            {
                _playerController.IsGrabbed = true;
                _playerController.CanMove = false;
                _playerController.CanChangeWeapon = false;
            }
            if (_playerInput != null)
            {
                _playerInput.cursorInputForLook = false;
                _playerInput.LookInput(Vector2.zero);
            }

            if (_playerRb != null)
            {
                _playerRb.linearVelocity = Vector3.zero;
                _playerRb.angularVelocity = Vector3.zero;
            }

            if (_headLookController != null)
                _headLookController.enabled = false;

            if (_playerAnimator != null)
            {
                _playerAnimator.SetTrigger(GrabbedTrigger);
            }

            _timer = 0f;
            _damageTimer = 0f;
            _escapeGauge = 0f;
            _prevMoveInput = Vector2.zero;

            _phase = Phase.WaitBeforePull;

            GrabEscapeEvents.OnCameraPull?.Invoke();

            if (boss.WireRig != null)
            {
                boss.WireRig.weight = 1f;
            }
            if (boss.WireBone != null)
            {
                // 特殊攻撃の射出でWireBoneの回転は既に上書きされているため、
                // 現在値ではなくAwakeで保存した本来の初期姿勢を復元先とする。
                // 現在値を使うとリトラクトのたびに姿勢のずれが蓄積する。
                _initialLocalRot = boss.InitialWireBoneLocalRotation;
            }
        }

        public override void Update()
        {
            switch (_phase)
            {
                case Phase.WaitBeforePull:
                    _timer += Time.deltaTime;
                    if (_timer >= WaitBeforePullDuration)
                    {
                        _phase = Phase.Pull;
                        _timer = 0f;
                        Debug.Log("[TutorialBoss] Pull フェーズへ");
                    }
                    break;
                case Phase.Pull:
                    UpdatePull();
                    break;
                case Phase.WaitBeforeDamage:
                    _timer += Time.deltaTime;
                    if (_timer >= WaitBeforeDamageDuration)
                    {
                        _phase = Phase.Captured;
                        _timer = 0f;

                        SpawnElectricEffect();
                        GrabEscapeEvents.OnShowGauge?.Invoke(0f, boss.GrabEscapeThreshold);

                        // 電撃フェーズのカメラへ切り替える。
                        // 毎フレーム呼ぶと引き寄せカメラ(vcamPull)を即座に上書きしてしまうため、
                        // フェーズ遷移のこの一度だけ発火させる。
                        GrabEscapeEvents.OnCameraDamage?.Invoke();

                        Debug.Log("[TutorialBoss] Captured フェーズへ (電撃開始)");
                    }
                    break;
                case Phase.Captured:
                    UpdateCaptured();
                    break;
                case Phase.Escape:
                    UpdateEscape();
                    break;
            }
        }

        public override void LateUpdate()
        {
            if (_phase != Phase.Escape)
            {
                UpdateTentacleIkTarget();
                ForcePlayerLookAtBoss();
            }
            else
            {
                // Escape中のボーンリトラクト処理
                float t = Mathf.Clamp01(_timer / boss.GrabRetractDuration);
                if (boss.WireBone != null)
                {
                    boss.WireBone.localPosition = Vector3.Lerp(
                        _escapeStartLocalPos,
                        boss.InitialWireBoneLocalPosition,
                        t
                    );
                    boss.WireBone.localRotation = Quaternion.Slerp(
                        _escapeStartLocalRot,
                        _initialLocalRot,
                        t
                    );
                }
            }
        }

        public override void Exit()
        {
            StopElectricEffect();

            GrabEscapeEvents.OnHideGauge?.Invoke();
            GrabEscapeEvents.OnCameraEnd?.Invoke();

            if (_playerCC != null)
            {
                _playerCC.enabled = true;
            }

            if (_playerController != null)
            {
                _playerController.IsGrabbed = false;
                _playerController.CanMove = true;
                _playerController.CanChangeWeapon = true;
            }
            if (_playerInput != null)
            {
                _playerInput.cursorInputForLook = true;
            }
            if (_headLookController != null)
                _headLookController.enabled = true;

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }

            if (boss.WireRig != null)
            {
                boss.WireRig.weight = 0f;
            }
            if (boss.WireBone != null)
            {
                boss.WireBone.localPosition = boss.InitialWireBoneLocalPosition;
                boss.WireBone.localRotation = _initialLocalRot;
            }
        }

        private void UpdatePull()
        {
            _timer += Time.deltaTime;

            if (boss.Player != null)
            {
                Vector3 targetPos =
                    boss.transform.position
                    + boss.transform.forward * boss.GrabPullDistance
                    + boss.transform.right * boss.GrabPullLateralOffset;
                targetPos.y = boss.Player.transform.position.y;

                float dist = Vector3.Distance(boss.Player.transform.position, targetPos);

                if (dist < 0.1f || _timer >= boss.GrabPullDuration)
                {
                    _phase = Phase.WaitBeforeDamage;
                    _timer = 0f;
                    Debug.Log("[TutorialBoss] WaitBeforeDamage フェーズへ");
                    return;
                }

                if (_playerRb != null)
                {
                    float speed = dist / Mathf.Max(0.01f, boss.GrabPullDuration - _timer);
                    Vector3 moveDir = (targetPos - boss.Player.transform.position).normalized;
                    _playerRb.MovePosition(_playerRb.position + moveDir * speed * Time.deltaTime);
                }
                else if (_playerCC != null && _playerCC.enabled)
                {
                    Vector3 moveDir = (targetPos - boss.Player.transform.position).normalized;
                    float speed = dist / Mathf.Max(0.01f, boss.GrabPullDuration - _timer);
                    _playerCC.Move(moveDir * speed * Time.deltaTime);
                }
            }
            else
            {
                _phase = Phase.WaitBeforeDamage;
                _timer = 0f;
            }
        }

        private void UpdateCaptured()
        {
            // プレイヤーが電撃で力尽きた場合、掴んだまま停止し続けないよう拘束を解除する
            if (_playerStatus != null && _playerStatus.CurrentHp <= 0f)
            {
                Debug.Log("[TutorialBoss] プレイヤーが力尽きたため拘束を解除します");
                boss.ChangeState(new TBossWatchState(boss));
                return;
            }

            _damageTimer += Time.deltaTime;
            if (_damageTimer >= boss.GrabDamageInterval)
            {
                _damageTimer -= boss.GrabDamageInterval;
                ApplyElectricDamage();
            }

            if (_playerInput != null)
            {
                Vector2 currentMove = _playerInput.move;
                bool movedX = HasDirectionChanged(_prevMoveInput.x, currentMove.x);
                bool movedY = HasDirectionChanged(_prevMoveInput.y, currentMove.y);

                if (movedX || movedY)
                {
                    _escapeGauge += boss.GrabEscapePerInput;
                    _escapeGauge = Mathf.Min(_escapeGauge, boss.GrabEscapeThreshold);

                    GrabEscapeEvents.OnUpdateGauge?.Invoke(_escapeGauge, boss.GrabEscapeThreshold);

                    if (_escapeGauge >= boss.GrabEscapeThreshold)
                    {
                        StartEscapeSequence();
                        _prevMoveInput = currentMove;
                        return;
                    }
                }
                _prevMoveInput = currentMove;
            }
        }

        private void StartEscapeSequence()
        {
            Debug.Log("[TutorialBoss] 脱出シーケンス開始");
            _phase = Phase.Escape;
            _timer = 0f;

            // 脱出後は怯みステートを経由するため、警戒を落とすと目の前のプレイヤーを見失って徘徊に戻ってしまう
            boss.IsAlerted = true;

            if (_playerAnimator != null)
                _playerAnimator.SetTrigger(EscapedTrigger);

            GrabEscapeEvents.OnCameraEscape?.Invoke();
            StopElectricEffect();

            if (boss.Player != null)
            {
                if (_playerCC != null)
                    _playerCC.enabled = true;

                Vector3 knockbackDir = (
                    boss.Player.transform.position - boss.transform.position
                ).normalized;
                knockbackDir.y = 0f;
                knockbackDir.Normalize();

                _knockbackVelocity = knockbackDir * boss.GrabEscapeKnockbackForce;
            }

            if (boss.WireRig != null)
            {
                boss.WireRig.weight = 0f;
            }
            if (boss.WireBone != null)
            {
                _escapeStartLocalPos = boss.WireBone.localPosition;
                _escapeStartLocalRot = boss.WireBone.localRotation;
            }
        }

        private void UpdateEscape()
        {
            _timer += Time.deltaTime;

            if (_playerCC != null && _playerCC.enabled && _knockbackVelocity.sqrMagnitude > 0.01f)
            {
                _knockbackVelocity = Vector3.Lerp(
                    _knockbackVelocity,
                    Vector3.zero,
                    Time.deltaTime * 5f
                );
                Vector3 move = _knockbackVelocity;
                move.y -= 9.81f;
                _playerCC.Move(move * Time.deltaTime);
            }

            float t = Mathf.Clamp01(_timer / boss.GrabRetractDuration);
            if (t >= 1f)
            {
                boss.ChangeState(new TBossFlinchState(boss));
            }
        }

        private void UpdateTentacleIkTarget()
        {
            if (_neckBone != null)
            {
                // Rig (Position Constraint) を使う場合のターゲット移動
                if (boss.WireIkTarget != null)
                {
                    boss.WireIkTarget.position = _neckBone.position;
                }

                // スクリプトで強制的に追従させるモード（Rigを使用しない場合）
                if (!boss.ForceStraightWireByRig && boss.WireBone != null)
                {
                    // ワイヤーをプレイヤーの首元に直接移動
                    boss.WireBone.position = _neckBone.position;
                    // ワイヤーの向きをボスの正面（プレイヤーのいる方向）へ向ける
                    boss.WireBone.rotation = Quaternion.FromToRotation(Vector3.up, boss.transform.forward);
                }
            }
        }

        private void ApplyElectricDamage()
        {
            if (_playerStatus == null)
                return;
            _playerStatus.TakeDamage(boss.GrabDamagePerTick, false);
            Debug.Log($"[TutorialBoss] 電撃ダメージ {boss.GrabDamagePerTick} を付与");
        }

        private void SpawnElectricEffect()
        {
            if (boss.ElectricEffectPrefab == null || boss.Player == null)
                return;
            _electricEffect = Object.Instantiate(boss.ElectricEffectPrefab, boss.Player.transform);
            Vector3 localNeckPos =
                _neckBone != null
                    ? boss.Player.transform.InverseTransformPoint(_neckBone.position)
                    : new Vector3(0f, 1.4f, 0f);
            _electricEffect.transform.localPosition = localNeckPos;
            _electricEffect.Play();
        }

        private void StopElectricEffect()
        {
            if (_electricEffect == null)
                return;
            _electricEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(_electricEffect.gameObject, 2f);
            _electricEffect = null;
        }

        private void ForcePlayerLookAtBoss()
        {
            if (boss.Player == null)
                return;

            Vector3 dirToBoss = boss.transform.position - boss.Player.transform.position;
            dirToBoss.y = 0f;
            if (dirToBoss.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToBoss.normalized);
                boss.Player.transform.rotation = Quaternion.Slerp(
                    boss.Player.transform.rotation,
                    targetRot,
                    Time.deltaTime * 10f
                );
            }
        }

        private static bool HasDirectionChanged(float prev, float current)
        {
            if (Mathf.Approximately(prev, 0f) && Mathf.Approximately(current, 0f))
                return false;
            if (Mathf.Approximately(prev, 0f) && !Mathf.Approximately(current, 0f))
                return true;
            if (prev > 0f && current < 0f)
                return true;
            if (prev < 0f && current > 0f)
                return true;
            return false;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            if (root.name == boneName)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindBone(child, boneName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
