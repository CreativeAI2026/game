using CreativeAI.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 選んだ <see cref="ItemPlacementPlan"/> を、開いている <c>Field_Area01_Items</c> へ置くメニュー。
    /// シーンは自動では保存しない(置いた結果を確認してから保存する)。
    /// </summary>
    public static class ItemPickupPlacerMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area01_Items.unity";

        [MenuItem("Tools/CreativeAI/アイテムを配置(選択した計画)")]
        public static void PlaceSelectedPlan()
        {
            var plan = Selection.activeObject as ItemPlacementPlan;
            if (plan == null)
            {
                EditorUtility.DisplayDialog(
                    "アイテムを配置",
                    "Project ビューで ItemPlacementPlan を選んでから実行してください。",
                    "OK"
                );
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                EditorUtility.DisplayDialog(
                    "アイテムを配置",
                    "Field_Area01_Items が開かれていません。Hierarchy に重ねて開いてから実行してください。",
                    "OK"
                );
                return;
            }

            var placed = ItemPickupPlacer.Place(scene, plan);
            Debug.Log(
                $"[ItemPickupPlacer] {placed.Count} 個置きました。シーンを確認してから保存してください。"
            );
        }

        [MenuItem("Tools/CreativeAI/アイテムを配置(選択した計画)", true)]
        private static bool ValidatePlaceSelectedPlan() =>
            Selection.activeObject is ItemPlacementPlan;
    }
}
