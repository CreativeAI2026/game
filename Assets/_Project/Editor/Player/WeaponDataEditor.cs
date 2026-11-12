using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.Editor.UI
{
    [CustomEditor(typeof(WeaponData))]
    [CanEditMultipleObjects]
    public sealed class WeaponDataEditor : UnityEditor.Editor
    {
        private SerializedProperty _icon;
        private SerializedProperty _id;
        private SerializedProperty _itemName;
        private SerializedProperty _description;
        private SerializedProperty _attack;
        private SerializedProperty _criticalRate;
        private SerializedProperty _criticalDamage;

        private void OnEnable()
        {
            _icon = serializedObject.FindProperty("icon");
            _id = serializedObject.FindProperty("id");
            _itemName = serializedObject.FindProperty("itemName");
            _description = serializedObject.FindProperty("description");
            _attack = serializedObject.FindProperty("attack");
            _criticalRate = serializedObject.FindProperty("criticalRate");
            _criticalDamage = serializedObject.FindProperty("criticalDamage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Script",
                    MonoScript.FromScriptableObject((WeaponData)target),
                    typeof(MonoScript),
                    false
                );
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("基本情報", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_icon, new GUIContent("Icon"));
            EditorGUILayout.PropertyField(_id, new GUIContent("ID"));
            EditorGUILayout.PropertyField(_itemName, new GUIContent("Name"));
            EditorGUILayout.PropertyField(_description, new GUIContent("Description"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("武器ステータス", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_attack, new GUIContent("ATK"));
            EditorGUILayout.PropertyField(_criticalRate, new GUIContent("CRTR"));
            EditorGUILayout.PropertyField(_criticalDamage, new GUIContent("CRTD"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
