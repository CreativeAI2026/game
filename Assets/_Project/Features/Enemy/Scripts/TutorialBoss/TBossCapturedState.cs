using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class TBossCapturedState : TBossBaseState
    {
        private const int TentaclePointCount = 20;
        private const float NeckWrapRadius = 0.12f;
        private const int NeckWrapPoints = 6;
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
        private Vector3 _retractTargetPos;
        private Vector3 _knockbackVelocity;
        private readonly Vector3[][] _retractFromPoints;
        private ParticleSystem _electricEffect;

        private PlayerController _playerController;
        private PlayerInputHandler _playerInput;
        private PlayerStatus _playerStatus;
        private Animator _playerAnimator;
        private CharacterController _playerCC;
        private Rigidbody _playerRb;
        private HeadLookController _headLookController;
        private Transform _neckBone;
        private int _tentacleCount;

        public TBossCapturedState(TutorialBossController controller)
            : base(controller)
        {
            int maxTentacles =
                controller.TentacleLineRenderers != null
                    ? controller.TentacleLineRenderers.Count
                    : 0;
            _retractFromPoints = new Vector3[maxTentacles][];
            for (int i = 0; i < maxTentacles; i++)
                _retractFromPoints[i] = new Vector3[TentaclePointCount];
        }

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
            _tentacleCount =
                boss.TentacleLineRenderers != null ? boss.TentacleLineRenderers.Count : 0;

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

            // Rigidbody があればそちらで速度をゼロにする。
            // CharacterController は動かしたままにする（無効化するとY=0にスナップするバグの原因）。
            if (_playerRb != null)
            {
                _playerRb.linearVelocity = Vector3.zero;
                _playerRb.angularVelocity = Vector3.zero;
            }

            // HeadLookController は playerRoot.forward を参照するため、
            // ForcePlayerLookAtBoss による rotation 書き換えと競合して首が回転し続ける。
            // 掴み中は無効化して競合を防ぐ。
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

            // 掴み成功と同時にvcamPullへ切り替え
            GrabEscapeEvents.OnCameraPull?.Invoke();
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
                    GrabEscapeEvents.OnCameraDamage?.Invoke();
                    break;
                case Phase.WaitBeforeDamage:
                    _timer += Time.deltaTime;
                    if (_timer >= WaitBeforeDamageDuration)
                    {
                        _phase = Phase.Captured;
                        _timer = 0f;

                        SpawnElectricEffect();
                        GrabEscapeEvents.OnShowGauge?.Invoke(0f, boss.GrabEscapeThreshold);

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

            if (_phase != Phase.Escape)
            {
                UpdateTentacleWrap();
                ForcePlayerLookAtBoss();
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
        }

        private void UpdatePull()
        {
            _timer += Time.deltaTime;

            if (boss.Player != null)
            {
                // 正面方向 + 横方向オフセット（ボスのright方向。Inspector で調整可能）
                Vector3 targetPos =
                    boss.transform.position
                    + boss.transform.forward * boss.GrabPullDistance
                    + boss.transform.right * boss.GrabPullLateralOffset;
                // XZ のみ引き寄せ。Y はプレイヤー自身の位置を保持（地面への埋め込み防止）
                targetPos.y = boss.Player.transform.position.y;

                float dist = Vector3.Distance(boss.Player.transform.position, targetPos);

                if (dist < 0.1f || _timer >= boss.GrabPullDuration)
                {
                    _phase = Phase.WaitBeforeDamage;
                    _timer = 0f;
                    Debug.Log("[TutorialBoss] WaitBeforeDamage フェーズへ");
                    return;
                }

                // Rigidbody があれば MovePosition で物理的に引き寄せ（transform 直接書き換えなし）
                if (_playerRb != null)
                {
                    float speed = dist / Mathf.Max(0.01f, boss.GrabPullDuration - _timer);
                    Vector3 moveDir = (targetPos - boss.Player.transform.position).normalized;
                    _playerRb.MovePosition(_playerRb.position + moveDir * speed * Time.deltaTime);
                }
                else if (_playerCC != null && _playerCC.enabled)
                {
                    // Rigidbody がない場合のフォールバック
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
            _retractTargetPos =
                boss.TentacleOrigin != null
                    ? boss.TentacleOrigin.position
                    : boss.transform.position;

            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Count && i < _retractFromPoints.Length; i++)
                {
                    LineRenderer lr = renderers[i];
                    if (lr == null)
                        continue;
                    int count = Mathf.Min(lr.positionCount, TentaclePointCount);
                    for (int j = 0; j < count; j++)
                        _retractFromPoints[i][j] = lr.GetPosition(j);
                    for (int j = count; j < TentaclePointCount; j++)
                        _retractFromPoints[i][j] =
                            count > 0 ? _retractFromPoints[i][count - 1] : _retractTargetPos;
                }
            }

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

                // ノックバック初速を設定（UpdateEscape でフレームごとに減衰させながら CharacterController で移動）
                _knockbackVelocity = knockbackDir * boss.GrabEscapeKnockbackForce;
            }

            // ここではまだ操作可能にしない（アニメーション中の移動を防ぐため）。
            // 操作可能になるのは Exit() が呼ばれた時点。
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
                move.y -= 9.81f; // 簡易重力
                _playerCC.Move(move * Time.deltaTime);
            }

            float t = Mathf.Clamp01(_timer / boss.GrabRetractDuration);

            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers != null && boss.TentacleOrigin != null)
            {
                Vector3 origin = boss.TentacleOrigin.position;
                for (int i = 0; i < renderers.Count && i < _retractFromPoints.Length; i++)
                {
                    LineRenderer lr = renderers[i];
                    if (lr == null)
                        continue;
                    lr.positionCount = TentaclePointCount;
                    Vector3[] from = _retractFromPoints[i];
                    for (int j = 0; j < TentaclePointCount; j++)
                    {
                        float pointT = Mathf.Clamp01(t + (float)j / TentaclePointCount * 0.5f);
                        lr.SetPosition(j, Vector3.Lerp(from[j], origin, pointT));
                    }
                }
            }

            if (t >= 1f)
            {
                if (renderers != null)
                {
                    foreach (var lr in renderers)
                        if (lr != null)
                            lr.enabled = false;
                }
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        private void UpdateTentacleWrap()
        {
            if (
                boss.Player == null
                || boss.TentacleLineRenderers == null
                || boss.TentacleOrigin == null
            )
                return;

            Vector3 neckPos =
                _neckBone != null
                    ? _neckBone.position
                    : boss.Player.transform.position + Vector3.up * 1.4f;

            Vector3 origin = boss.TentacleOrigin.position;
            Vector3 dirToNeck = (neckPos - origin);
            float dist = dirToNeck.magnitude;

            if (dist < 0.01f)
            {
                dirToNeck = boss.transform.forward;
                dist = 0.01f;
            }
            Vector3 dir = dirToNeck.normalized;

            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            int count = Mathf.Min(renderers.Count, _tentacleCount);

            float waveFreq = 2.0f;
            float waveAmp = 0.15f;
            float waveSpeed = 3.0f;

            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(dir, up).normalized;
            if (right == Vector3.zero)
                right = Vector3.right;
            up = Vector3.Cross(right, dir).normalized;

            int straightPoints = TentaclePointCount - NeckWrapPoints;

            for (int i = 0; i < count; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null || lr.positionCount < TentaclePointCount)
                    continue;

                float phaseOffset = (float)i / count * Mathf.PI * 2f;
                float timeOffset = Time.time * waveSpeed + phaseOffset;

                for (int j = 0; j < straightPoints; j++)
                {
                    float t = (float)j / Mathf.Max(1, straightPoints - 1);
                    Vector3 basePos = origin + dir * (dist * t);

                    float waveU = Mathf.Sin(t * waveFreq * Mathf.PI + timeOffset) * waveAmp;
                    float waveV =
                        Mathf.Sin(t * waveFreq * Mathf.PI + timeOffset + Mathf.PI * 0.5f)
                        * waveAmp
                        * 0.5f;
                    float envelope = Mathf.Sin(t * Mathf.PI);

                    Vector3 offset = right * (waveU * envelope) + up * (waveV * envelope);
                    lr.SetPosition(j, basePos + offset);
                }

                for (int j = 0; j < NeckWrapPoints; j++)
                {
                    int ptIndex = straightPoints + j;
                    float angle =
                        phaseOffset
                        + (float)j / NeckWrapPoints * Mathf.PI * 2f * 0.8f
                        + Time.time * 2.0f;
                    float radius = NeckWrapRadius * (1f - (float)j / NeckWrapPoints * 0.3f);

                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle) * radius,
                        ((float)j / NeckWrapPoints) * 0.15f - 0.075f,
                        Mathf.Sin(angle) * radius
                    );
                    lr.SetPosition(ptIndex, neckPos + offset);
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

        /// <summary>
        /// 掴み中、毎フレームプレイヤーをボス方向に向かせ続ける。
        /// 操作が無効化されているのでユーザー入力で向きが変わることはない。
        /// </summary>
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
