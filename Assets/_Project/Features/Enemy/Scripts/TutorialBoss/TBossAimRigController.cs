using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 懐中電灯・頭のAnimation Rigging用エイムターゲットを動かすコンポーネント。
    ///
    /// このスクリプト自体はボーンを直接動かさない。Multi-Aim Constraint等のRig
    /// コンポーネントを別途Editorで追加し、その Source Objects にここで動かす
    /// ターゲットTransformを登録することで、Rig側が実際の回転を計算する構成を想定している
    /// （既存のWireRig / wireIkTargetと同じ設計パターン）。
    /// スクリプトから直接ボーンのTransformを書き換えるとアニメーションと競合して破綻することが
    /// 既に判明している（TutorialBossController削除済みのRotateFlashlightToward参照）ため、
    /// 必ずRig経由で適用すること。
    ///
    /// 参照が未設定のフィールドはそれぞれ何もしないため、Rig未セットアップの現状でも
    /// アタッチするだけなら安全（デフォルトで無害）。Editor側のセットアップ手順は
    /// クラス末尾のコメントを参照。
    /// </summary>
    public class TBossAimRigController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField]
        private TutorialBossController boss;

        [Header("懐中電灯エイム")]
        [Tooltip(
            "懐中電灯のMulti-Aim ConstraintのSource Objectsに登録する空オブジェクト。未設定なら何もしない。"
        )]
        [SerializeField]
        private Transform flashlightAimTarget;

        [Tooltip("懐中電灯エイムターゲットの追従の滑らかさ。")]
        [SerializeField]
        private float flashlightSmoothSpeed = 6f;

        [Tooltip("未発見・非警戒時に懐中電灯を左右にゆっくり振る。falseなら体の正面に固定する。")]
        [SerializeField]
        private bool sweepWhilePatrolling = true;

        [Tooltip("スイープの片側最大角度（度）。")]
        [SerializeField]
        private float sweepAngle = 35f;

        [Tooltip("スイープ1往復にかかる時間（秒）。")]
        [SerializeField]
        private float sweepPeriod = 6f;

        [Header("頭のエイム")]
        [Tooltip(
            "頭・首のMulti-Aim ConstraintのSource Objectsに登録する空オブジェクト。未設定なら何もしない。"
        )]
        [SerializeField]
        private Transform headAimTarget;

        [Tooltip("頭ボーン（可動域を制限する際の起点）。")]
        [SerializeField]
        private Transform headBone;

        [Tooltip(
            "正面から首を向ける最大角度。これを超える位置のプレイヤーは追わない（不自然な回転防止）。"
        )]
        [SerializeField]
        private float headMaxAngle = 70f;

        [Tooltip("頭エイムターゲットの追従の滑らかさ。")]
        [SerializeField]
        private float headSmoothSpeed = 5f;

        private void LateUpdate()
        {
            if (boss == null)
            {
                return;
            }

            UpdateFlashlightAim();
            UpdateHeadAim();
        }

        private void UpdateFlashlightAim()
        {
            if (flashlightAimTarget == null || boss.FlashlightTransform == null)
            {
                return;
            }

            Vector3 targetPosition;

            if (boss.IsAlerted || boss.IsSuspicious)
            {
                // 発見・警戒中はプレイヤーを狙う
                targetPosition =
                    boss.Player != null
                        ? boss.Player.transform.position + Vector3.up * 1f
                        : boss.FlashlightTransform.position + boss.FlashlightTransform.forward * 5f;
            }
            else if (sweepWhilePatrolling)
            {
                // 未発見時は「探している」印象を出すため左右にゆっくり振る
                float angle = Mathf.Sin(Time.time * (Mathf.PI * 2f / sweepPeriod)) * sweepAngle;
                Vector3 sweepDir = Quaternion.AngleAxis(angle, Vector3.up) * boss.transform.forward;
                targetPosition = boss.FlashlightTransform.position + sweepDir * 5f;
            }
            else
            {
                targetPosition = boss.FlashlightTransform.position + boss.transform.forward * 5f;
            }

            flashlightAimTarget.position = Vector3.Lerp(
                flashlightAimTarget.position,
                targetPosition,
                Time.deltaTime * flashlightSmoothSpeed
            );
        }

        private void UpdateHeadAim()
        {
            if (headAimTarget == null || headBone == null)
            {
                return;
            }

            Vector3 targetPosition;

            if (boss.Player != null && (boss.IsAlerted || boss.IsSuspicious))
            {
                Vector3 dirToPlayer = boss.Player.transform.position - headBone.position;
                float angleToPlayer = Vector3.Angle(boss.transform.forward, dirToPlayer);

                if (angleToPlayer <= headMaxAngle)
                {
                    targetPosition = boss.Player.transform.position;
                }
                else
                {
                    // 可動域を超えた位置には限界角度の境界にターゲットを留める。
                    // こうしないと首が不自然な角度まで回ってしまう（HeadLookControllerと同じ考え方）
                    Vector3 clampedDir = Vector3.RotateTowards(
                        boss.transform.forward,
                        dirToPlayer,
                        headMaxAngle * Mathf.Deg2Rad,
                        0f
                    );
                    targetPosition = headBone.position + clampedDir * 5f;
                }
            }
            else
            {
                targetPosition = headBone.position + boss.transform.forward * 5f;
            }

            headAimTarget.position = Vector3.Lerp(
                headAimTarget.position,
                targetPosition,
                Time.deltaTime * headSmoothSpeed
            );
        }
    }
}

// ────────────────────────────────────────────────────────────────
// Editorでのセットアップ手順（このスクリプトだけでは見た目は変化しません）
// ────────────────────────────────────────────────────────────────
// 1. TutorialBossプレハブ内に空のGameObjectを2つ作成する
//    （例: "FlashlightAimTarget" "HeadAimTarget"。位置は初期状態でボスの少し前方に置く）
// 2. 既存のWireRig（Rigコンポーネントが付いたGameObject）と同じ階層に、
//    新しいRig用GameObject（例: "AimRig"）を作成し、Rigコンポーネントを追加する
// 3. AimRigの子として、懐中電灯ボーンを対象にした Multi-Aim Constraint と、
//    頭/首ボーンを対象にした Multi-Aim Constraint を追加する
//    - Constrained Object: それぞれ flashlightTransform / 頭ボーン
//    - Source Objects: 手順1で作った FlashlightAimTarget / HeadAimTarget を登録
// 4. ルートのRigBuilderコンポーネントのRig Layersに、新しいAimRigを追加する
// 5. TutorialBossのGameObjectにこのTBossAimRigControllerを追加し、
//    boss / flashlightAimTarget / headAimTarget / headBone を割り当てる
//
// Rigのパス解決で問題が起きた場合は、既存の RigDebugger コンポーネントで
// パスやConstraintの参照状況を確認できます（Console出力を参照）。
