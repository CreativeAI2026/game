using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 触手を使った特殊攻撃ステート。LineRendererで5本の触手を演出する。
    /// フェーズ1（狙い）：各触手をサイン波＋ノイズでランダムにうねらせながらターゲットを向き続ける
    /// フェーズ2（射出）：全触手を一気に直線伸展し、先端付近の複数点でOverlapSphereによる当たり判定を行う
    /// ヒット時はCapturedStateへ遷移する。
    /// 外れた場合は伸ばしきり→引っ込めの後、クリップを最後まで再生しきってからWatchStateへ遷移する
    /// （この間もAgentは停止したままなので、モーションが終わるまで移動しない）。
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
            Finish, // クリップの残りを再生しきるのを待つ（この間も移動しない）
        }

        private Phase _currentPhase;

        // 狙いフェーズ中の最大伸長距離（演出）。
        // 射出フェーズは0からSpecialAttackRangeまで伸ばし直すため、ここを長くすると
        // 射出の瞬間にワイヤーが縮んでから飛ぶ見た目の破綻が起きる。構える程度の短さに留める。
        private const float AimTentacleLength = 0.5f;

        // 射出フェーズの当たり判定半径
        private const float TentacleHitRadius = 0.25f;

        private const float AimNoiseStrength = 90f; // ノイズによるランダム成分（度数）

        // キープと引っ込みフェーズの時間設定
        private const float KeepDuration = 0.5f; // 伸ばしきった状態でキープする時間
        private const float RetractDuration = 0.3f; // 引っ込むのにかかる時間

        // AnimationEvent(OnPoseReady)の取りこぼしで永久にAimのまま固まるのを防ぐ保険の倍率
        private const float AimTimeoutMultiplier = 2f;

        // クリップの再生完了待ちが何らかの理由で終わらない場合に打ち切る時間
        private const float FinishTimeout = 3f;

        // 初期状態
        private Quaternion _initialLocalRot;

        // 現在のワイヤー伸長量。壁に阻まれた位置からリトラクトさせるために保持する
        private float _currentLength;
        private float _retractStartLength;

        // 前フレームの先端位置。高速伸展時の素抜けを防ぐ掃引判定に使う
        private Vector3 _prevTipPosition;
        private bool _hasPrevTip;

        // 当たり判定に使う先端位置。腕のしなりに影響されないようApplyWireTransformで算出して保持する
        private Vector3 _deterministicTipPosition;

        public TBossSpecialAttackState(TutorialBossController controller)
            : base(controller) { }

        public override void Enter()
        {
            _timer = 0f;
            _hasHit = false;
            _currentPhase = Phase.Aim;
            _currentLength = 0f;
            _retractStartLength = 0f;
            _hasPrevTip = false;

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
                // 直前の攻撃で上書きされた姿勢ではなく、Awakeで保存した本来の初期姿勢を復元先とする
                _initialLocalRot = boss.InitialWireBoneLocalRotation;
            }
            else
            {
                Debug.LogWarning("[TutorialBoss] WireBone が未設定です。");
            }

            if (boss.ForceStraightWireByRig && boss.WireRig != null)
            {
                boss.WireRig.weight = 1f;
            }

            // 前回の攻撃で残ったバネの速度・姿勢を破棄し、開始フレームのガクつきを防ぐ
            boss.WireChainDriver?.ResetState();
        }

        public override void Update()
        {
            _timer += Time.deltaTime;

            // Aimフェーズは通常 AnimationEvent (OnPoseReady) で終了するが、
            // イベント未設定やトリガー取りこぼしで永久に固まるのを防ぐため保険のタイムアウトを持たせる
            if (_currentPhase == Phase.Aim)
            {
                if (_timer >= boss.SpecialAttackAimDuration * AimTimeoutMultiplier)
                {
                    Debug.LogWarning(
                        "[TutorialBoss] OnSpecialAttackPoseReady が呼ばれませんでした。"
                            + "SpecialAttackクリップのAnimationEventを確認してください。タイムアウトで射出します。"
                    );
                    StartShootPhase();
                }
                return;
            }

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
                    StartFinishPhase();
                }
            }
            else if (_currentPhase == Phase.Finish)
            {
                UpdateFinishPhase();
            }
        }

        public override void LateUpdate()
        {
            // 腕のしなりを先に適用し、その後で各フェーズがワイヤー先端の姿勢を上書きする。
            // この順序により「腕はしなるが、先端は狙った位置に決定的に届く」状態を保てる。
            ApplyWireChainFlex();

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
            boss.WireChainDriver?.RestoreExtension();

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

            // OnSpecialAttackPoseReady で 0 にしたアニメーション再生速度を必ず戻す。
            // 怯み・死亡などでフェーズ途中に中断された場合、ここが最後の砦になる。
            //
            // なお空振り時にLocomotionへ強制的に切り替える処理は削除した。
            // Finishフェーズでクリップを最後まで再生しきってからWatchへ抜けるため、
            // ここで切り替えるとモーションが途中で途切れてしまう。
            if (boss.Animator != null)
            {
                boss.Animator.speed = 1f;
            }

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

        /// <summary>
        /// 腕の連鎖に「しなり」（ラグ・波・ドループ）を適用する。
        /// 伸長はワイヤー先端の既存計算（ApplyWireTransform）が担うため、ここでは0を渡して
        /// 見た目のしなりだけを乗せる。これによりリーチと当たり判定は従来どおり保たれる。
        /// </summary>
        private void ApplyWireChainFlex()
        {
            if (boss.WireChainDriver == null || !boss.WireChainDriver.IsValid)
            {
                return;
            }

            float wavePower =
                boss.SpecialAttackRange > 0.0001f
                    ? Mathf.Clamp01(_currentLength / boss.SpecialAttackRange)
                    : 0f;

            boss.WireChainDriver.Apply(0f, wavePower, Time.deltaTime);
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

            // 当たり判定用の先端位置は、ボーンの実際の位置から読み戻さずここで確定させる。
            // 腕をしならせると親ボーンの遅れでWireBone.positionがぶれてしまい、
            // 見た目のしなり具合で判定が変わる（＝理不尽になる）ため、判定は常にこの直線上の点で行う。
            _deterministicTipPosition = originPos + forward * currentLength;

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

            float aimProgress = Mathf.Clamp01(_timer / boss.SpecialAttackAimDuration);
            _currentLength = AimTentacleLength * aimProgress;

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
                boss.InitialWireBoneLocalPosition + Vector3.up * _currentLength;
        }

        private void StartShootPhase()
        {
            _currentPhase = Phase.Shoot;
            _timer = 0f;

            // 掃引判定の起点を射出開始時の先端位置に合わせる
            _hasPrevTip = false;
        }

        private void UpdateShootPhase()
        {
            if (_hasHit)
                return;

            float shootProgress = Mathf.Clamp01(_timer / boss.SpecialAttackShootDuration);
            _currentLength = boss.SpecialAttackRange * shootProgress;

            ApplyWireTransform(_currentLength);

            CheckHitAtTip();
        }

        private void StartKeepPhase()
        {
            _currentPhase = Phase.Keep;
            _timer = 0f;
            _currentLength = boss.SpecialAttackRange;
        }

        private void UpdateKeepPhase()
        {
            if (_hasHit)
                return;

            ApplyWireTransform(_currentLength);
        }

        private void StartRetractPhase()
        {
            _currentPhase = Phase.Retract;
            _timer = 0f;

            // 壁に阻まれて途中で止まった場合でも、その位置から自然に縮むようにする
            _retractStartLength = _currentLength;
        }

        private void UpdateRetractPhase()
        {
            if (_hasHit)
                return;

            float retractProgress = Mathf.Clamp01(_timer / RetractDuration);
            _currentLength = _retractStartLength * (1f - retractProgress);

            ApplyWireTransform(_currentLength);
        }

        /// <summary>
        /// ワイヤーを戻し終えた後、クリップの残りを再生しきるのを待つフェーズへ移る。
        /// 以前はここで即座にWatchへ遷移し、Exitでアニメーションを強制的にLocomotionへ
        /// 切り替えていたため、空振り時にモーションが途中で途切れていた。
        /// </summary>
        private void StartFinishPhase()
        {
            _currentPhase = Phase.Finish;
            _timer = 0f;

            // OnSpecialAttackPoseReadyで0にした再生速度をここで戻す。
            // 戻さないとnormalizedTimeが進まず、完了待ちが永久に終わらない。
            if (boss.Animator != null)
            {
                boss.Animator.speed = 1f;
            }
        }

        /// <summary>
        /// クリップが最後まで再生されるのを待つ。この間もAgentは停止したままなので移動しない。
        /// </summary>
        private void UpdateFinishPhase()
        {
            if (boss.Animator == null)
            {
                boss.ChangeState(new TBossWatchState(boss));
                return;
            }

            AnimatorStateInfo stateInfo = boss.Animator.GetCurrentAnimatorStateInfo(0);
            bool clipFinished =
                stateInfo.IsName("SpecialAttack") && stateInfo.normalizedTime >= 1.0f;

            // 別ステートへ差し替わった場合や、何らかの理由で進行しない場合の保険
            if (clipFinished || _timer >= FinishTimeout)
            {
                boss.ChangeState(new TBossWatchState(boss));
            }
        }

        /// <summary>
        /// ワイヤー先端の当たり判定。
        /// 射出速度は「射程 ÷ 射出時間」で秒速数十mに達するため、単発のOverlapSphereでは
        /// 1フレームの移動量が判定直径を上回ってプレイヤーを素抜けてしまう。
        /// そのため前フレームの先端位置から現在位置までを掃引して判定する。
        /// </summary>
        private void CheckHitAtTip()
        {
            if (boss.WireBone == null)
                return;

            Vector3 tipPosition = _deterministicTipPosition;

            if (!_hasPrevTip)
            {
                _prevTipPosition = tipPosition;
                _hasPrevTip = true;
            }

            Vector3 delta = tipPosition - _prevTipPosition;
            _prevTipPosition = tipPosition;

            if (delta.sqrMagnitude > 1e-6f)
            {
                RaycastHit[] sweep = Physics.SphereCastAll(
                    tipPosition - delta,
                    TentacleHitRadius,
                    delta.normalized,
                    delta.magnitude,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

                // 掃引結果は距離順ではないため、手前に当たったものを優先して評価する
                System.Array.Sort(sweep, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit in sweep)
                {
                    if (EvaluateCollider(hit.collider))
                        return;
                }
            }

            // 密着状態は掃引で検出できないため重なり判定で補完する
            Collider[] overlaps = Physics.OverlapSphere(
                tipPosition,
                TentacleHitRadius,
                ~0,
                QueryTriggerInteraction.Ignore
            );
            foreach (Collider col in overlaps)
            {
                if (EvaluateCollider(col))
                    return;
            }
        }

        /// <summary>
        /// 先端が触れたコライダーを評価する。
        /// </summary>
        /// <returns>判定を打ち切るべきならtrue</returns>
        private bool EvaluateCollider(Collider col)
        {
            if (col == null)
                return false;

            // 自分自身の体やワイヤーを拾わないよう除外する
            if (col.transform.IsChildOf(boss.transform))
                return false;

            if (col.CompareTag("Player"))
            {
                _hasHit = true;
                Debug.Log("[TutorialBoss] 特殊攻撃 ヒット！ 捕獲ステートへ遷移");
                boss.ChangeState(new TBossCapturedState(boss));
                return true;
            }

            // 壁越しに掴めてしまうのを防ぐ。障害物に当たった時点で空振り確定とする
            if ((boss.ObstacleLayer.value & (1 << col.gameObject.layer)) != 0)
            {
                Debug.Log("[TutorialBoss] 特殊攻撃 障害物に阻まれました");
                StartRetractPhase();
                return true;
            }

            return false;
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
