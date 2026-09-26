using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 懐中電灯・頭の Animation Rigging 用エイムターゲットを動かす。ボーンは直接動かさず、Multi-Aim Constraint 等の Source Objects に登録した
    /// ターゲットを動かして Rig 側に回転を計算させる（WireRig / wireIkTarget と同じ方式。ボーンを直接書き換えるとアニメと競合して破綻する）。
    /// 未設定の参照は何もしないので Rig 未セットアップでも安全。セットアップ手順はクラス末尾を参照。
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

// Editor セットアップ手順（このスクリプトだけでは見た目は変わらない）
// 1. TutorialBoss プレハブに空の "FlashlightAimTarget" "HeadAimTarget" を作り、ボスの少し前方に置く
// 2. WireRig と同階層に Rig 付きの "AimRig" を作り、子に懐中電灯ボーン用・頭/首ボーン用の Multi-Aim Constraint を追加して
//    Source Objects に手順1のターゲットを登録し、ルートの RigBuilder の Rig Layers に AimRig を追加する
// 3. このコンポーネントを追加し boss / flashlightAimTarget / headAimTarget / headBone を割り当てる（問題時は RigDebugger で確認）
