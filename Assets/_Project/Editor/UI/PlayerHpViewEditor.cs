using CreativeAI.UI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// プレイヤー HP バーの確認用 Inspector。
    /// ダメージを受けなくても、HP 割合を変えたときの色・心電図を確かめられるよう、
    /// プレイ中に表示だけ HP 割合を差し替えるスライダーと、HP 帯ごとのボタンを出す(実際の HP は変えない)。
    /// </summary>
    [CustomEditor(typeof(PlayerHpView))]
    internal class PlayerHpViewEditor : UnityEditor.Editor
    {
        // プレビューのスライダー位置(Inspector を閉じ直しても残るよう static)
        private static float s_previewRatio = 1f;

        // HP 帯のボタンで、境目ちょうどではなく帯の内側を見せるためにずらす量
        private const float PhaseButtonInset = 0.01f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("確認用: HP 表示のプレビュー", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "プレイ中に使えます。実際の HP は変えず、表示だけを指定の HP 割合にします。",
                    MessageType.Info
                );
                return;
            }

            var view = (PlayerHpView)target;

            EditorGUI.BeginChangeCheck();
            s_previewRatio = EditorGUILayout.Slider("HP 割合", s_previewRatio, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                view.SetPreviewHpRatio(s_previewRatio);

            DrawPhaseButtons(view);

            using (new EditorGUI.DisabledScope(!view.IsPreviewing))
            {
                if (GUILayout.Button("プレビューをやめて実際の HP に戻す"))
                    view.SetPreviewHpRatio(null);
            }

            if (view.IsPreviewing)
                EditorGUILayout.HelpBox("プレビュー中(表示のみ差し替え)", MessageType.None);
        }

        /// <summary>HP 帯(EKG フェーズ)ごとに、その帯の上端の少し内側へ飛ぶボタンを並べる。</summary>
        private static void DrawPhaseButtons(PlayerHpView view)
        {
            float[] thresholds = view.GetPhaseThresholds();
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (float threshold in thresholds)
                {
                    float ratio = Mathf.Clamp01(threshold - PhaseButtonInset);
                    if (threshold >= 1f)
                        ratio = 1f; // 最初の帯は満タンで見せる
                    if (GUILayout.Button($"{ratio * 100f:0}%"))
                    {
                        s_previewRatio = ratio;
                        view.SetPreviewHpRatio(ratio);
                    }
                }
                if (GUILayout.Button("0%"))
                {
                    s_previewRatio = 0f;
                    view.SetPreviewHpRatio(0f);
                }
            }
        }
    }
}
