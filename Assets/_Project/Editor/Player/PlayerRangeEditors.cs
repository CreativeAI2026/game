using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    // プレイヤーの索敵・射程などの半径を、そのコンポーネントを選択している間だけシーンビューに表示する。
    // Inspector の表示は既定のまま(OnInspectorGUI は上書きしない)。

    /// <summary>剣・鎌・ハンマー共通: ダッシュ接近の索敵半径と射程(これより遠い敵にはダッシュで接近)。</summary>
    [CustomEditor(typeof(MeleeWeaponController), true)]
    [CanEditMultipleObjects]
    internal class MeleeWeaponControllerEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var weapon = (MeleeWeaponController)target;
            // 距離判定はプレイヤーのルート基準なので、そこを中心に描く
            var player = weapon.GetComponentInParent<PlayerController>();
            Transform origin = player != null ? player.transform : weapon.transform;

            // 複数選択時も武器ごとに自分の値を描くため、対象1つ分の SerializedObject を使う
            var so = new SerializedObject(weapon);
            so.Update();
            SceneRadiusHandle.Draw(
                so.FindProperty(nameof(MeleeWeaponController.dashSearchRadius)),
                origin.position,
                origin.forward,
                Color.yellow,
                $"{weapon.name} ダッシュ接近の索敵"
            );
            SceneRadiusHandle.Draw(
                so.FindProperty(nameof(MeleeWeaponController.attackRange)),
                origin.position,
                origin.right,
                Color.red,
                $"{weapon.name} 射程(外ならダッシュ)"
            );
            DrawWeaponSpecificRanges(so, origin, weapon.name);
            so.ApplyModifiedProperties();
        }

        /// <summary>武器固有の半径(投擲・叩きつけ等)を描く。共通の円と点が重ならないよう、後ろ・左側に置く。</summary>
        protected virtual void DrawWeaponSpecificRanges(
            SerializedObject so,
            Transform origin,
            string weaponName
        ) { }
    }

    /// <summary>鎌: 投擲で狙う敵の索敵半径。</summary>
    [CustomEditor(typeof(ScytheController))]
    [CanEditMultipleObjects]
    internal class ScytheControllerEditor : MeleeWeaponControllerEditor
    {
        protected override void DrawWeaponSpecificRanges(
            SerializedObject so,
            Transform origin,
            string weaponName
        )
        {
            SceneRadiusHandle.Draw(
                so.FindProperty("_throwSearchRadius"),
                origin.position,
                -origin.forward,
                Color.magenta,
                $"{weaponName} 投擲の索敵"
            );
        }
    }

    /// <summary>ハンマー: 叩きつけで跳びかかる敵の索敵半径と、1回の最大跳躍距離。</summary>
    [CustomEditor(typeof(HammerController))]
    [CanEditMultipleObjects]
    internal class HammerControllerEditor : MeleeWeaponControllerEditor
    {
        protected override void DrawWeaponSpecificRanges(
            SerializedObject so,
            Transform origin,
            string weaponName
        )
        {
            SceneRadiusHandle.Draw(
                so.FindProperty("_slamSearchRadius"),
                origin.position,
                -origin.forward,
                Color.magenta,
                $"{weaponName} 叩きつけの索敵"
            );
            SceneRadiusHandle.Draw(
                so.FindProperty("_maxLeapDistance"),
                origin.position,
                -origin.right,
                Color.white,
                $"{weaponName} 最大跳躍距離"
            );
        }
    }

    /// <summary>視線・焦り検知が追う「最寄りの敵」の索敵半径。</summary>
    [CustomEditor(typeof(PlayerTargetManager))]
    [CanEditMultipleObjects]
    internal class PlayerTargetManagerEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var manager = (PlayerTargetManager)target;
            var so = new SerializedObject(manager);
            so.Update();
            SceneRadiusHandle.Draw(
                so.FindProperty(nameof(PlayerTargetManager.searchRadius)),
                manager.transform.position,
                -manager.transform.forward,
                Color.cyan,
                "視線・焦り検知の索敵"
            );
            so.ApplyModifiedProperties();
        }
    }

    /// <summary>足音が敵AIに届く半径(歩き・走り)。</summary>
    [CustomEditor(typeof(PlayerFootstep))]
    [CanEditMultipleObjects]
    internal class PlayerFootstepEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var footstep = (PlayerFootstep)target;
            Transform origin = footstep.transform;
            var so = new SerializedObject(footstep);
            so.Update();
            SceneRadiusHandle.Draw(
                so.FindProperty("_walkRadius"),
                origin.position,
                -origin.right,
                Color.green,
                "足音(歩き)が敵に届く"
            );
            SceneRadiusHandle.Draw(
                so.FindProperty("_runRadius"),
                origin.position,
                (-origin.right - origin.forward).normalized,
                new Color(1f, 0.5f, 0f),
                "足音(走り)が敵に届く"
            );
            so.ApplyModifiedProperties();
        }
    }
}
