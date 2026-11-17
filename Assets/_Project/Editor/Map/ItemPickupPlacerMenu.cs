using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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

            // アイテムを歩ける場所(NavMesh 上)に置くため、NavMesh のあるシーン(Field_Area01)が開いている必要がある。
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "アイテムを配置",
                    "NavMesh が見つかりません。Field_Area01 を Hierarchy に開いてから実行してください。",
                    "OK"
                );
                return;
            }

            // 小物や床のコライダーは、プレイ中に AdditiveScenes が重ねるシーンにある。
            // エディタで開いていないと避ける対象が見えず、小物の中に置いてしまうので、開いてから置く。
            var opened = OpenAreaScenes();
            try
            {
                var placed = ItemPickupPlacer.Place(scene, plan);
                Debug.Log(
                    $"[ItemPickupPlacer] {placed.Count} 個置きました。シーンを確認してから保存してください。"
                );
            }
            finally
            {
                foreach (var opening in opened)
                    if (opening.isLoaded)
                        EditorSceneManager.CloseScene(opening, true);
            }
        }

        /// <summary>
        /// 開いているシーンの <see cref="AdditiveScenes"/> が宣言する、重ねて読むシーンのうち、
        /// まだ開いていないものを開く。開いたシーンを返す(置いたあとに閉じる)。
        /// </summary>
        private static List<Scene> OpenAreaScenes()
        {
            var opened = new List<Scene>();
            foreach (var additive in Object.FindObjectsByType<AdditiveScenes>())
            {
                foreach (var name in additive.SceneNames)
                {
                    if (string.IsNullOrWhiteSpace(name) || IsLoaded(name))
                        continue;

                    var guid = AssetDatabase
                        .FindAssets($"{name} t:Scene")
                        .FirstOrDefault(g =>
                            Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))
                            == name
                        );
                    if (guid == null)
                    {
                        Debug.LogWarning($"[ItemPickupPlacer] シーン '{name}' が見つかりません。");
                        continue;
                    }

                    opened.Add(
                        EditorSceneManager.OpenScene(
                            AssetDatabase.GUIDToAssetPath(guid),
                            OpenSceneMode.Additive
                        )
                    );
                }
            }
            return opened;
        }

        private static bool IsLoaded(string name)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name == name)
                    return true;
            return false;
        }

        [MenuItem("Tools/CreativeAI/アイテムを配置(選択した計画)", true)]
        private static bool ValidatePlaceSelectedPlan() =>
            Selection.activeObject is ItemPlacementPlan;
    }
}
