using CreativeAI.UI;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 装備バフのアイコン列の確認用 Inspector。
    /// 装備を整えなくても並び方を確かめられるよう、登録した全アイコンを並べるボタンを出す。
    /// 編集中(プレイしていない時)でも使え、そのとき作るアイコンは保存されない。
    /// </summary>
    [CustomEditor(typeof(EquipmentBuffIconBarView))]
    internal class EquipmentBuffIconBarViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "確認用: アイコンの並びのプレビュー",
                EditorStyles.boldLabel
            );

            var view = (EquipmentBuffIconBarView)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全アイコンを並べる"))
                    view.SetPreview(true);

                using (new EditorGUI.DisabledScope(!view.IsPreviewing))
                {
                    if (GUILayout.Button("プレビューをやめる"))
                        view.SetPreview(false);
                }
            }

            if (view.IsPreviewing)
                EditorGUILayout.HelpBox(
                    "プレビュー中(装備に関係なく全アイコンを表示)。上下は「並べ方」で切り替えられます。",
                    MessageType.None
                );
        }
    }
}
