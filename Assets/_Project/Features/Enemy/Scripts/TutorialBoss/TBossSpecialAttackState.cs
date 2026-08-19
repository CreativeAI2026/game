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

        // 触手の本数
        private const int TentacleCount = 5;

        // 触手の分割数（多いほど滑らか）
        private const int TentaclePointCount = 20;

        // 狙いフェーズ中の触手の最大伸長距離（演出）
        private const float AimTentacleLength = 3f;

        // 射出フェーズの当たり判定半径
        private const float TentacleHitRadius = 0.5f;

        // うねりパラメータ（狙いフェーズ）
        private const float AimWaveFrequency = 2.5f; // 波の周期数
        private const float AimWaveAmplitude = 0.35f; // 波の振れ幅最大値
        private const float AimWaveSpeed = 3.0f; // 波の流れる速さ
        private const float AimNoiseStrength = 0.2f; // ノイズによるランダム成分

        // うねりパラメータ（射出フェーズ：収束して直線に近づく）
        private const float ShootWaveFrequency = 3.0f;
        private const float ShootWaveAmplitude = 0.08f; // 射出時は振れが小さい
        private const float ShootWaveSpeed = 10.0f;

        // キープと引っ込みフェーズの時間設定
        private const float KeepDuration = 0.5f; // 伸ばしきった状態でキープする時間
        private const float RetractDuration = 0.3f; // 引っ込むのにかかる時間

        // 射出方向（全触手共通。StartShootPhaseでスナップ）
        private Vector3 _shootDirection;

        // 射出開始時点で固定した触手の起点座標
        private Vector3 _shootOrigin;

        // 各触手ごとのランダムオフセット（うねりの個性を出す）
        // x=位相オフセット, y=ノイズシード
        private readonly Vector2[] _tentacleRandoms = new Vector2[TentacleCount];

        // 各フレームで計算したポイント列（当たり判定にも流用）
        // [触手インデックス][ポイントインデックス]
        private readonly Vector3[][] _tentaclePoints = new Vector3[TentacleCount][];

        public TBossSpecialAttackState(TutorialBossController controller)
            : base(controller)
        {
            // 各触手のポイント配列を初期化
            for (int i = 0; i < TentacleCount; i++)
            {
                _tentaclePoints[i] = new Vector3[TentaclePointCount];
            }
        }

        public override void Enter()
        {
            Debug.Log("[TutorialBoss] 特殊攻撃ステート開始");

            _timer = 0f;
            _hasHit = false;
            _currentPhase = Phase.Aim;

            // 各触手のランダムパラメータを決定
            for (int i = 0; i < TentacleCount; i++)
            {
                _tentacleRandoms[i] = new Vector2(
                    Random.Range(0f, Mathf.PI * 2f), // 位相オフセット
                    Random.Range(0f, 100f) // Perlinノイズシード
                );
            }

            if (boss.Agent != null)
            {
                boss.Agent.ResetPath();
                boss.Agent.isStopped = true;
            }

            if (boss.Animator != null)
            {
                boss.Animator.SetTrigger("SpecialAttack");
            }

            // 全LineRendererを初期化して有効化
            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers == null || renderers.Count == 0 || boss.TentacleOrigin == null)
            {
                Debug.LogWarning(
                    "[TutorialBoss] TentacleLineRenderers または TentacleOrigin が未設定です。"
                );
                return;
            }

            for (int i = 0; i < renderers.Count && i < TentacleCount; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null)
                    continue;

                lr.enabled = true;
                lr.positionCount = TentaclePointCount;
                lr.useWorldSpace = true;

                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;

                // 全点を起点に初期化
                for (int j = 0; j < TentaclePointCount; j++)
                {
                    lr.SetPosition(j, boss.TentacleOrigin.position);
                }
            }
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            if (_currentPhase == Phase.Aim)
            {
                UpdateAimPhase();

                if (_timer >= boss.SpecialAttackAimDuration)
                {
                    StartShootPhase();
                }
            }
            else if (_currentPhase == Phase.Shoot)
            {
                UpdateShootPhase();

                if (_timer >= boss.SpecialAttackAimDuration + boss.SpecialAttackShootDuration)
                {
                    if (!_hasHit)
                    {
                        StartKeepPhase();
                    }
                }
            }
            else if (_currentPhase == Phase.Keep)
            {
                UpdateKeepPhase();

                if (
                    _timer
                    >= boss.SpecialAttackAimDuration
                        + boss.SpecialAttackShootDuration
                        + KeepDuration
                )
                {
                    StartRetractPhase();
                }
            }
            else if (_currentPhase == Phase.Retract)
            {
                UpdateRetractPhase();

                float totalDuration =
                    boss.SpecialAttackAimDuration
                    + boss.SpecialAttackShootDuration
                    + KeepDuration
                    + RetractDuration;
                if (_timer >= totalDuration)
                {
                    // タイムアウト（ハズレて完全に引っ込んだ後）
                    boss.ChangeState(new TBossWatchState(boss));
                }
            }
        }

        public override void Exit()
        {
            // ヒットして CapturedState へ遷移した場合は触手を消さない。
            // 触手の LineRenderer の無効化は TBossCapturedState の脱出完了時に行う。
            if (!_hasHit)
            {
                List<LineRenderer> renderers = boss.TentacleLineRenderers;
                if (renderers != null)
                {
                    foreach (var lr in renderers)
                    {
                        if (lr != null)
                            lr.enabled = false;
                    }
                }
            }

            if (boss.Agent != null)
            {
                boss.Agent.isStopped = false;
            }

            Debug.Log("[TutorialBoss] 特殊攻撃ステート終了");
        }

        // ── 狙いフェーズ ──

        private void UpdateAimPhase()
        {
            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers == null || renderers.Count == 0 || boss.TentacleOrigin == null)
            {
                return;
            }

            // ターゲット方向を決定（発見時はプレイヤー、未発見は音源）
            Vector3 targetPos = GetTargetPosition();

            // ボス本体をターゲット方向に向ける
            // 狙いフェーズ中は高速に向かせる（10f）ことで、射出時にずれにくくする
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

            // 懐中電灯もターゲット方向へ向ける
            boss.RotateFlashlightToward(targetPos);

            // 触手を徐々に伸ばしながうねらせる演出
            float aimProgress = Mathf.Clamp01(_timer / boss.SpecialAttackAimDuration);
            float currentLength = AimTentacleLength * aimProgress;

            Vector3 origin = boss.TentacleOrigin.position;
            // TentacleOriginからターゲットへの方向を計算
            // ボス本体の向きではなく、targetPosへの実ベクトルを使うことで
            // Slerpの遅延に関わらず触手だけは常に正確なターゲット方向を向く
            Vector3 direction = (targetPos - origin);
            direction.y = 0f; // 高さ差は無視して水平射出
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = boss.transform.forward;
            }
            direction = direction.normalized;

            for (int i = 0; i < renderers.Count && i < TentacleCount; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null)
                    continue;

                BuildWavedTentacle(
                    i,
                    origin,
                    direction,
                    currentLength,
                    AimWaveFrequency,
                    AimWaveAmplitude * aimProgress, // 徐々に振れ幅が増す
                    AimWaveSpeed,
                    AimNoiseStrength * aimProgress
                );
                ApplyPointsToLineRenderer(i, lr);
            }
        }

        private void StartShootPhase()
        {
            _currentPhase = Phase.Shoot;
            _timer = boss.SpecialAttackAimDuration; // タイマーをリセットせず継続

            // 射出時点でTentacleOriginのワールド座標を固定する
            _shootOrigin = boss.TentacleOrigin.position;

            // 射出方向はターゲットへの現在ベクトルで再スナップ（高さ差無視）
            Vector3 targetPos = GetTargetPosition();
            Vector3 dir = (targetPos - _shootOrigin);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = boss.transform.forward;
            }
            _shootDirection = dir.normalized;

            Debug.Log("[TutorialBoss] 特殊攻撃 射出フェーズ開始");
        }

        // ── 射出フェーズ ──

        private void UpdateShootPhase()
        {
            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers == null || renderers.Count == 0 || boss.TentacleOrigin == null)
            {
                return;
            }

            if (_hasHit)
            {
                return;
            }

            // 射出進行度（0→1）
            float shootElapsed = _timer - boss.SpecialAttackAimDuration;
            float shootProgress = Mathf.Clamp01(shootElapsed / boss.SpecialAttackShootDuration);

            float currentLength = boss.SpecialAttackRange * shootProgress;

            // 射出フェーズはうねりを収束させる（振れ幅が急速に小さくなる）
            float waveAmplitude = ShootWaveAmplitude * (1f - shootProgress * 0.7f);

            for (int i = 0; i < renderers.Count && i < TentacleCount; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null)
                    continue;

                BuildWavedTentacle(
                    i,
                    _shootOrigin,
                    _shootDirection,
                    currentLength,
                    ShootWaveFrequency,
                    waveAmplitude,
                    ShootWaveSpeed,
                    0f // ノイズなし（射出は素早く真っ直ぐに近づける）
                );
                ApplyPointsToLineRenderer(i, lr);
            }

            // 当たり判定：全触手の先端付近3点でOverlapSphere
            CheckHitAtTip();
        }

        // ── キープフェーズ ──

        private void StartKeepPhase()
        {
            _currentPhase = Phase.Keep;
            _timer = boss.SpecialAttackAimDuration + boss.SpecialAttackShootDuration;
        }

        private void UpdateKeepPhase()
        {
            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers == null || renderers.Count == 0 || boss.TentacleOrigin == null || _hasHit)
            {
                return;
            }

            float currentLength = boss.SpecialAttackRange;
            float waveAmplitude = ShootWaveAmplitude * 0.3f; // キープ時は少しだけうねる

            for (int i = 0; i < renderers.Count && i < TentacleCount; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null)
                    continue;

                BuildWavedTentacle(
                    i,
                    _shootOrigin,
                    _shootDirection,
                    currentLength,
                    ShootWaveFrequency,
                    waveAmplitude,
                    ShootWaveSpeed,
                    0f
                );
                ApplyPointsToLineRenderer(i, lr);
            }
        }

        // ── 引っ込みフェーズ ──

        private void StartRetractPhase()
        {
            _currentPhase = Phase.Retract;
            _timer = boss.SpecialAttackAimDuration + boss.SpecialAttackShootDuration + KeepDuration;
        }

        private void UpdateRetractPhase()
        {
            List<LineRenderer> renderers = boss.TentacleLineRenderers;
            if (renderers == null || renderers.Count == 0 || boss.TentacleOrigin == null || _hasHit)
            {
                return;
            }

            float retractElapsed =
                _timer
                - (boss.SpecialAttackAimDuration + boss.SpecialAttackShootDuration + KeepDuration);
            float retractProgress = Mathf.Clamp01(retractElapsed / RetractDuration);

            // 引っ込む時は長さを0に向かって縮める
            float currentLength = boss.SpecialAttackRange * (1f - retractProgress);
            float waveAmplitude = ShootWaveAmplitude * 0.1f;

            for (int i = 0; i < renderers.Count && i < TentacleCount; i++)
            {
                LineRenderer lr = renderers[i];
                if (lr == null)
                    continue;

                BuildWavedTentacle(
                    i,
                    _shootOrigin,
                    _shootDirection,
                    currentLength,
                    ShootWaveFrequency,
                    waveAmplitude,
                    ShootWaveSpeed * 1.5f, // 引っ込む時は少し動きを早く
                    0f
                );
                ApplyPointsToLineRenderer(i, lr);
            }
        }

        // ── 触手の点列を構築 ──

        /// <summary>
        /// サイン波＋Perlinノイズで触手の各ポイント座標を _tentaclePoints[tentacleIndex] に書き込む。
        /// 各触手は _tentacleRandoms により位相・ノイズが異なり、個性的なうねりになる。
        /// </summary>
        private void BuildWavedTentacle(
            int tentacleIndex,
            Vector3 origin,
            Vector3 direction,
            float length,
            float waveFreq,
            float waveAmp,
            float waveSpeed,
            float noiseStrength
        )
        {
            // 触手に垂直な2軸を計算（up方向とright方向）
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(direction, up).normalized;
            if (right == Vector3.zero)
            {
                right = Vector3.right;
            }
            up = Vector3.Cross(right, direction).normalized;

            float phaseOffset = _tentacleRandoms[tentacleIndex].x;
            float noiseSeed = _tentacleRandoms[tentacleIndex].y;
            float timeOffset = Time.time * waveSpeed + phaseOffset;

            Vector3[] points = _tentaclePoints[tentacleIndex];

            for (int i = 0; i < TentaclePointCount; i++)
            {
                float t = i / (float)(TentaclePointCount - 1);
                Vector3 basePos = origin + direction * (length * t);

                // サイン波成分（水平・垂直を少しずらして立体感を出す）
                float waveU = Mathf.Sin(t * waveFreq * Mathf.PI + timeOffset) * waveAmp;
                float waveV =
                    Mathf.Sin(t * waveFreq * Mathf.PI + timeOffset + Mathf.PI * 0.5f)
                    * waveAmp
                    * 0.5f;

                // Perlinノイズ成分（ランダムなうねり）
                float noiseU =
                    (Mathf.PerlinNoise(t * 3f + noiseSeed, timeOffset * 0.5f) - 0.5f)
                    * 2f
                    * noiseStrength;
                float noiseV =
                    (Mathf.PerlinNoise(t * 3f + noiseSeed + 50f, timeOffset * 0.5f) - 0.5f)
                    * 2f
                    * noiseStrength;

                // 根元・先端付近はうねりを抑える（自然な境界）
                float envelope = Mathf.Sin(t * Mathf.PI); // 0→1→0 の包絡線

                Vector3 offset =
                    right * ((waveU + noiseU) * envelope) + up * ((waveV + noiseV) * envelope);

                points[i] = basePos + offset;
            }
        }

        private void ApplyPointsToLineRenderer(int tentacleIndex, LineRenderer lr)
        {
            Vector3[] points = _tentaclePoints[tentacleIndex];
            lr.positionCount = TentaclePointCount;
            for (int i = 0; i < TentaclePointCount; i++)
            {
                lr.SetPosition(i, points[i]);
            }
        }

        /// <summary>
        /// 全触手の先端付近の点でOverlapSphereを行い、プレイヤーへのヒットを確認する。
        /// </summary>
        private void CheckHitAtTip()
        {
            int checkCount = 3;
            int startIndex = TentaclePointCount - checkCount;

            for (int t = 0; t < TentacleCount; t++)
            {
                Vector3[] points = _tentaclePoints[t];

                for (int i = startIndex; i < TentaclePointCount; i++)
                {
                    Collider[] hits = Physics.OverlapSphere(points[i], TentacleHitRadius);
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
            }
        }

        private Vector3 GetTargetPosition()
        {
            // 攻撃ステートに入る時点でプレイヤーが近くにいるはずなので、
            // IsAlerted の状態に関わらず、プレイヤーが存在すれば現在位置を直接使う。
            // IsAlerted=false のまま TransitionToAttack() が呼ばれるケース
            // （音調査→攻撃遷移）でも正しくプレイヤーを向けるようにするため。
            if (boss.Player != null)
            {
                return boss.Player.transform.position;
            }

            return boss.LastHeardSoundPosition;
        }
    }
}
