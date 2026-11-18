using CreativeAI.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 通常攻撃2（鞭の二段攻撃）を Edit モードで確認するデバッグ用コンポーネント。アニメを任意時刻で止めた姿勢を見られる。
    /// 腕の伸びと動きはクリップに焼いてあるため、ここで変えられるのは見る時刻だけ。攻撃判定の範囲は TBossArmSweepHitbox のギズモで確認する。
    /// </summary>
    [ExecuteAlways]
    public class TBossAttack2Tuner : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("調整対象のボス。未設定なら同じGameObjectから取得する。")]
        [SerializeField]
        private TutorialBossController boss;

        [Tooltip("プレビューするアニメーションクリップ（Attack2に割り当てたもの）。")]
        [SerializeField]
        private AnimationClip previewClip;

        [Tooltip(
            "クリップをサンプリングする基準。クリップ内のパスが 'Armature/...' のため、\n"
                + "Armatureの『親』を指定する（このモデルでは TBoss）。未設定なら自動判別する。"
        )]
        [SerializeField]
        private Transform sampleRoot;

        [Header("プレビュー")]
        [Tooltip("ONにするとEditモードでアニメーションの姿勢を適用する。OFFで元のポーズへ戻す。")]
        [SerializeField]
        private bool previewEnabled;

        [Tooltip(
            "クリップ内の再生位置（0〜1）。コンテキストメニューから各振りの時刻へジャンプできる。"
        )]
        [Range(0f, 1f)]
        [SerializeField]
        private float previewNormalizedTime;

        [Header("刃の向きの確認")]
        [Tooltip("刃の向きを測る基準ボーン（左手）。未設定なら刃の向きは表示しない。")]
        [SerializeField]
        private Transform bladeBone;

        [Tooltip(
            "bladeBoneのローカル座標で「刃が切る方向」を指すベクトル。\n"
                + "実際の刃の向きに合うよう一度だけ合わせれば、以降は矢印で向きを判断できる。"
        )]
        [SerializeField]
        private Vector3 bladeEdgeLocalDirection = Vector3.forward;

        [Tooltip("振りの方向を求めるためにクリップを先読みする時間（秒）。")]
        [SerializeField]
        private float swingSampleDelta = 0.03f;

        [Header("Playモード用")]
        [Tooltip("ONの間、Time.timeScaleを下げてスロー再生する。OFFまたは無効化で1に戻す。")]
        [SerializeField]
        private bool slowMotion;

        [Range(0.05f, 1f)]
        [SerializeField]
        private float slowMotionScale = 0.25f;

        [Header("表示")]
        [SerializeField]
        private bool drawGizmos = true;

        [Tooltip("矢印やボーンの表示サイズ（メートル）。")]
        [SerializeField]
        private float gizmoScale = 0.3f;

        // Editモードでポーズを書き換えるため、元の姿勢を保存して戻せるようにする。
        // スクリプト再コンパイル（ドメインリロード）で消えると、変形済みの姿勢を「元の姿勢」として
        // 取り直してしまい元に戻せなくなるため、シリアライズして保持する。
        [SerializeField]
        [HideInInspector]
        private Transform[] _capturedBones;

        [SerializeField]
        [HideInInspector]
        private Vector3[] _capturedPositions;

        [SerializeField]
        [HideInInspector]
        private Quaternion[] _capturedRotations;

        [SerializeField]
        [HideInInspector]
        private Vector3[] _capturedScales;

        [SerializeField]
        [HideInInspector]
        private bool _isPreviewing;

        private bool _didSetTimeScale;

        // ギズモ表示用にUpdateで算出しておく
        private Vector3 _swingDirection;
        private float _edgeToSwingAngle;

        private void OnEnable()
        {
            if (boss == null)
            {
                TryGetComponent(out boss);
            }
        }

        private void OnDisable()
        {
            StopPreview();
            RestoreTimeScale();
        }

        private void Update()
        {
            UpdateTimeScale();

            // Playモードでは本番のステートがポーズを制御するため、プレビューはEditモード専用にする
            if (Application.isPlaying)
            {
                if (_isPreviewing)
                {
                    StopPreview();
                }
                return;
            }

            if (!previewEnabled || !IsReady())
            {
                if (_isPreviewing)
                {
                    StopPreview();
                }
                return;
            }

            if (!_isPreviewing)
            {
                CaptureRestPose();
                _isPreviewing = true;
            }

            UpdatePreviewPose();
        }

        private bool IsReady()
        {
            return boss != null && previewClip != null && ResolveSampleRoot() != null;
        }

        /// <summary>
        /// クリップのパスは 'Armature/...' から始まるため、サンプリング対象はArmatureの親になる。
        /// 未設定の場合はボス配下からArmatureを探して自動判別する。
        /// </summary>
        private Transform ResolveSampleRoot()
        {
            if (sampleRoot != null)
            {
                return sampleRoot;
            }

            if (boss == null)
            {
                return null;
            }

            Transform armature = FindDescendant(boss.transform, "Armature");
            return armature != null ? armature.parent : boss.transform;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void UpdatePreviewPose()
        {
            float length = Mathf.Max(0.0001f, previewClip.length);
            float time = previewNormalizedTime * length;

            Transform blade = bladeBone;

            // 振りの方向は「少し先の時刻での刃の位置」との差分で求める。
            if (blade != null && swingSampleDelta > 0f)
            {
                ApplyPoseAt(Mathf.Min(length, time + swingSampleDelta));
                Vector3 aheadPosition = blade.position;

                ApplyPoseAt(time);
                _swingDirection = aheadPosition - blade.position;
            }
            else
            {
                ApplyPoseAt(time);
                _swingDirection = Vector3.zero;
            }

            _edgeToSwingAngle =
                blade != null && _swingDirection.sqrMagnitude > 1e-8f
                    ? Vector3.Angle(GetBladeEdgeDirection(blade), _swingDirection)
                    : -1f;
        }

        private void ApplyPoseAt(float time)
        {
            Transform root = ResolveSampleRoot();
            if (root != null)
            {
                previewClip.SampleAnimation(root.gameObject, time);
            }
        }

        private Vector3 GetBladeEdgeDirection(Transform blade)
        {
            Vector3 local =
                bladeEdgeLocalDirection.sqrMagnitude > 1e-6f
                    ? bladeEdgeLocalDirection.normalized
                    : Vector3.forward;
            return blade.TransformDirection(local);
        }

        // ────────────────────────────────────────────
        //  ポーズの保存と復元
        // ────────────────────────────────────────────

        private void CaptureRestPose()
        {
            Transform root = ResolveSampleRoot();
            if (root == null)
            {
                return;
            }

            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            _capturedBones = bones;
            _capturedPositions = new Vector3[bones.Length];
            _capturedRotations = new Quaternion[bones.Length];
            _capturedScales = new Vector3[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                _capturedPositions[i] = bones[i].localPosition;
                _capturedRotations[i] = bones[i].localRotation;
                _capturedScales[i] = bones[i].localScale;
            }
        }

        private void RestoreRestPose()
        {
            if (_capturedBones == null)
            {
                return;
            }

            for (int i = 0; i < _capturedBones.Length; i++)
            {
                Transform bone = _capturedBones[i];
                if (bone == null)
                {
                    continue;
                }

                bone.SetLocalPositionAndRotation(_capturedPositions[i], _capturedRotations[i]);
                bone.localScale = _capturedScales[i];
            }
        }

        private void StopPreview()
        {
            if (!_isPreviewing)
            {
                return;
            }

            RestoreRestPose();

            _isPreviewing = false;
            _capturedBones = null;
            _capturedPositions = null;
            _capturedRotations = null;
            _capturedScales = null;
        }

        // ────────────────────────────────────────────
        //  Playモード補助
        // ────────────────────────────────────────────

        private void UpdateTimeScale()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (slowMotion)
            {
                Time.timeScale = slowMotionScale;
                _didSetTimeScale = true;
            }
            else if (_didSetTimeScale)
            {
                // 自分が下げた場合のみ戻す。ポーズ機能など他システムのtimeScale操作を壊さないため。
                Time.timeScale = 1f;
                _didSetTimeScale = false;
            }
        }

        private void RestoreTimeScale()
        {
            if (Application.isPlaying && _didSetTimeScale)
            {
                Time.timeScale = 1f;
                _didSetTimeScale = false;
            }
        }

        // ────────────────────────────────────────────
        //  コンテキストメニュー（コンポーネント右上の「⋮」から実行）
        // ────────────────────────────────────────────

        /// <summary>
        /// クリップ内の全AnimationEventを時刻順に辿る。
        /// 「振る前に伸ばして振り切ったら戻す」を振りの回数だけ繰り返す構成では同名イベントが
        /// 複数入るため、名前で探すジャンプでは2回目以降へ到達できない。
        /// </summary>
        [ContextMenu("時刻ジャンプ/次のイベントへ")]
        private void JumpToNextEvent()
        {
            StepToAdjacentEvent(true);
        }

        [ContextMenu("時刻ジャンプ/前のイベントへ")]
        private void JumpToPreviousEvent()
        {
            StepToAdjacentEvent(false);
        }

        private void StepToAdjacentEvent(bool forward)
        {
            if (previewClip == null)
            {
                Debug.LogWarning("[TBossAttack2Tuner] previewClipが未設定です。");
                return;
            }

            AnimationEvent[] events = previewClip.events;
            if (events == null || events.Length == 0)
            {
                Debug.LogWarning(
                    $"[TBossAttack2Tuner] クリップ '{previewClip.name}' にAnimationEventがありません。"
                );
                return;
            }

            float length = Mathf.Max(0.0001f, previewClip.length);
            float currentTime = previewNormalizedTime * length;

            // 同じ時刻で止まらないよう、わずかに内側を基準にして探す
            const float timeEpsilon = 0.0005f;

            AnimationEvent best = null;
            foreach (AnimationEvent candidate in events)
            {
                bool isCandidate = forward
                    ? candidate.time > currentTime + timeEpsilon
                    : candidate.time < currentTime - timeEpsilon;
                if (!isCandidate)
                {
                    continue;
                }

                if (best == null)
                {
                    best = candidate;
                    continue;
                }

                bool isCloser = forward ? candidate.time < best.time : candidate.time > best.time;
                if (isCloser)
                {
                    best = candidate;
                }
            }

            if (best == null)
            {
                DevLog.Log(
                    forward
                        ? "[TBossAttack2Tuner] これより後にAnimationEventはありません。"
                        : "[TBossAttack2Tuner] これより前にAnimationEventはありません。"
                );
                return;
            }

            previewNormalizedTime = Mathf.Clamp01(best.time / length);
            DevLog.Log(
                $"[TBossAttack2Tuner] {best.time:F3}秒 の {best.functionName} へ移動しました。"
            );
        }

        [ContextMenu("時刻ジャンプ/腕を伸ばし始める所へ")]
        private void JumpToReachExtend()
        {
            JumpToEvent("OnAttack2ReachExtend", int.MinValue);
        }

        [ContextMenu("時刻ジャンプ/腕を戻し終える所へ")]
        private void JumpToReachRetract()
        {
            JumpToEvent("OnAttack2ReachRetract", int.MinValue);
        }

        [ContextMenu("時刻ジャンプ/判定開始へ")]
        private void JumpToHitboxEnable()
        {
            JumpToEvent("EnableArmSweepHitbox", int.MinValue);
        }

        /// <summary>
        /// クリップに仕込んだAnimationEventの時刻へ再生位置を合わせる。
        /// イベントが未設置の場合は警告を出す（無反応で悩まないようにするため）。
        /// </summary>
        private void JumpToEvent(string functionName, int intParameter)
        {
            if (previewClip == null)
            {
                Debug.LogWarning("[TBossAttack2Tuner] previewClipが未設定です。");
                return;
            }

            AnimationEvent[] events = previewClip.events;
            foreach (AnimationEvent animationEvent in events)
            {
                if (animationEvent.functionName != functionName)
                {
                    continue;
                }

                if (intParameter != int.MinValue && animationEvent.intParameter != intParameter)
                {
                    continue;
                }

                previewNormalizedTime = Mathf.Clamp01(
                    animationEvent.time / Mathf.Max(0.0001f, previewClip.length)
                );
                return;
            }

            Debug.LogWarning(
                $"[TBossAttack2Tuner] クリップ '{previewClip.name}' に "
                    + $"{functionName}"
                    + (intParameter != int.MinValue ? $"(引数{intParameter})" : string.Empty)
                    + " のAnimationEventが見つかりませんでした。Animation窓で設置してください。"
            );
        }

        [ContextMenu("元のポーズに戻す")]
        private void ForceRestore()
        {
            previewEnabled = false;
            StopPreview();
        }

        [ContextMenu("Attack2を強制発動（Play中のみ）")]
        private void ForceAttack2()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[TBossAttack2Tuner] Playモード中にのみ実行できます。");
                return;
            }

            if (boss == null)
            {
                return;
            }

            boss.IsAlerted = true;
            boss.ChangeState(new TBossNormalAttack2State(boss));
        }

        // ────────────────────────────────────────────
        //  ギズモ
        // ────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || boss == null)
            {
                return;
            }

            DrawBladeDirections();
        }

        private void DrawBladeDirections()
        {
            Transform blade = bladeBone;
            if (blade == null)
            {
                return;
            }

            Vector3 origin = blade.position;

            // 刃が切る方向
            Gizmos.color = Color.green;
            Vector3 edge = GetBladeEdgeDirection(blade) * gizmoScale;
            Gizmos.DrawLine(origin, origin + edge);
            Gizmos.DrawWireSphere(origin + edge, gizmoScale * 0.08f);

            // 実際に振っている方向
            if (_swingDirection.sqrMagnitude > 1e-8f)
            {
                Gizmos.color = Color.yellow;
                Vector3 swing = _swingDirection.normalized * gizmoScale;
                Gizmos.DrawLine(origin, origin + swing);
                Gizmos.DrawWireCube(origin + swing, Vector3.one * (gizmoScale * 0.1f));
            }

#if UNITY_EDITOR
            if (_edgeToSwingAngle >= 0f)
            {
                string judgement =
                    _edgeToSwingAngle < 45f ? "OK（刃が振り方向を向いています）"
                    : _edgeToSwingAngle > 135f ? "逆（刃の裏が振り方向を向いています）"
                    : "横向き";

                Handles.Label(
                    origin + Vector3.up * (gizmoScale * 0.6f),
                    $"刃と振りの角度: {_edgeToSwingAngle:F0}° {judgement}"
                );
            }
#endif
        }
    }
}
