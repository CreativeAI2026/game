using System;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    public sealed class PreviewWeaponTestControls : EditorWindow
    {
        [MenuItem("Tools/CreativeAI/UI/Preview Test Controls")]
        private static void Open() => GetWindow<PreviewWeaponTestControls>("Preview Tests");

        [MenuItem("Tools/CreativeAI/UI/Preview Weapons/None")]
        public static void NoWeapons() => SetOwned(false);

        [MenuItem("Tools/CreativeAI/UI/Preview Weapons/All")]
        public static void AllWeapons() => SetOwned(true);

        private static void SetOwned(bool all)
        {
            var manager = Manager;
            if (manager == null)
                return;
            var keys = all ? new string[WeaponManager.WeaponKeys.Count] : Array.Empty<string>();
            if (all)
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = WeaponManager.WeaponKeys[i];
            manager.RestoreWeapons(keys, all ? 0 : WeaponManager.NoWeapon);
        }

        private static WeaponManager Manager =>
            Application.isPlaying && PlayerRegistry.Current != null
                ? PlayerRegistry.Current.GetComponent<WeaponManager>()
                : null;

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Play Mode中のテスト用です。セーブファイルや起動設定は変更しません。",
                MessageType.Info
            );
            var manager = Manager;
            EditorGUILayout.LabelField(
                "プレイヤー",
                PlayerRegistry.Current != null ? PlayerRegistry.Current.name : "未生成"
            );
            EditorGUILayout.LabelField(
                "所持数",
                manager != null ? manager.OwnedCount.ToString() : "—"
            );
            using (new EditorGUI.DisabledScope(manager == null))
            {
                if (GUILayout.Button("武器なし（所持0・素手）", GUILayout.Height(32)))
                    NoWeapons();
                if (GUILayout.Button("全部あり（4種所持・刀を装備）", GUILayout.Height(32)))
                    AllWeapons();
                if (GUILayout.Button("所持を維持して素手にする"))
                    manager.Unequip();
            }
            if (!Application.isPlaying)
                EditorGUILayout.LabelField("UI_SandboxをPlayしてから切り替えてください。");
        }
    }
}
