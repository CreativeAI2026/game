using System.Text;
using UnityEngine;
using UnityEngine.Animations.Rigging;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    public class RigDebugger : MonoBehaviour
    {
        private void Start()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== [RigDebugger] 調査開始 ===");

            Animator anim = GetComponent<Animator>();
            RigBuilder rb = GetComponent<RigBuilder>();

            sb.AppendLine($"1. Animator取得: {(anim != null ? "成功" : "失敗")}");
            if (anim != null)
            {
                sb.AppendLine($"   - Avatar: {(anim.avatar != null ? "あり" : "なし")}");
                if (anim.avatar != null)
                {
                    sb.AppendLine(
                        $"   - Avatarタイプ: {(anim.avatar.isHuman ? "Humanoid" : "Generic")}"
                    );
                    sb.AppendLine($"   - AvatarのValid状態: {anim.avatar.isValid}");
                }
            }

            sb.AppendLine($"2. RigBuilder取得: {(rb != null ? "成功" : "失敗")}");
            if (rb != null)
            {
                sb.AppendLine($"   - RigLayers数: {rb.layers.Count}");
                for (int i = 0; i < rb.layers.Count; i++)
                {
                    var layer = rb.layers[i];
                    if (layer.rig != null)
                    {
                        sb.AppendLine($"   - Layer {i}: Rig名 [{layer.rig.name}]");

                        // RigオブジェクトがAnimatorの子としてどう認識されるかをテスト
                        if (anim != null)
                        {
#if UNITY_EDITOR
                            string path = AnimationUtility.CalculateTransformPath(
                                layer.rig.transform,
                                anim.transform
                            );
                            sb.AppendLine($"     -> Animatorからの相対パス計算結果: '{path}'");

                            // MultiPositionConstraintなどのコンポーネントがあればそのパスも計算
                            var constraint =
                                layer.rig.GetComponentInChildren<MultiPositionConstraint>();
                            if (constraint != null)
                            {
                                string cPath = AnimationUtility.CalculateTransformPath(
                                    constraint.transform,
                                    anim.transform
                                );
                                sb.AppendLine(
                                    $"     -> Constraintからの相対パス計算結果: '{cPath}'"
                                );
                            }
#endif
                        }
                    }
                    else
                    {
                        sb.AppendLine($"   - Layer {i}: RigはNullです。");
                    }
                }
            }

            sb.AppendLine("3. 階層の直接検索テスト");
            Transform wireRigTransform = transform.Find("WireRig");
            sb.AppendLine(
                $"   - transform.Find(\"WireRig\"): {(wireRigTransform != null ? "発見" : "未発見")}"
            );

            Debug.LogWarning(sb.ToString());
        }
    }
}
