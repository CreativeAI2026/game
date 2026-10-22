using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 半径(m)の設定値を、シーンビューに地面の円として描き、縁の点をドラッグして編集できるようにする。
    /// OnDrawGizmos で常時描くとシーンが円だらけになるため、各 Editor の OnSceneGUI(そのオブジェクトの選択中のみ呼ばれる)から使う。
    /// 半径はワールド座標の m で描く(OverlapSphere / Vector3.Distance と同じ基準。プレイヤーの Scale には影響されない)。
    /// </summary>
    internal static class SceneRadiusHandle
    {
        // 円の内側の塗りの不透明度。円が重なっても地形が見える程度に薄くする
        private const float FillAlpha = 0.06f;

        private const float LineThickness = 2f;

        // ドラッグ用の点の大きさ(画面上の大きさに対する比率)
        private const float HandleSizeRatio = 0.08f;

        /// <summary>
        /// 半径を描画し、ドラッグされたら <paramref name="radius"/> を更新する。
        /// 反映(Undo・Prefab の上書き含む)は呼び出し側の ApplyModifiedProperties で行う。
        /// </summary>
        /// <param name="handleDirection">ドラッグ点とラベルを置く方向。複数の円で点が重ならないよう方向を変える。</param>
        public static void Draw(
            SerializedProperty radius,
            Vector3 center,
            Vector3 handleDirection,
            Color color,
            string label
        )
        {
            float r = radius.floatValue;

            Handles.color = new Color(color.r, color.g, color.b, FillAlpha);
            Handles.DrawSolidDisc(center, Vector3.up, r);
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, r, LineThickness);

            Vector3 edge = center + handleDirection * r;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(
                edge,
                handleDirection,
                HandleUtility.GetHandleSize(edge) * HandleSizeRatio,
                Handles.DotHandleCap,
                0f
            );
            if (EditorGUI.EndChangeCheck())
                radius.floatValue = Mathf.Max(0f, Vector3.Dot(moved - center, handleDirection));

            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(edge, $"{label} {radius.floatValue:0.##}m", style);
        }
    }
}
