#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// Floor.glb を敷き詰めた新しいフィールドシーン Field_Area02 を生成する。
    /// （外周のガラス壁は一旦廃止。床のみ。）
    /// Tools > CreativeAI > Create Field Scene から実行。
    /// バッチ実行: Unity -batchmode -quit -executeMethod CreativeAI.EditorTools.CreateFieldScene.Run
    /// </summary>
    public static class CreateFieldScene
    {
        private const string FloorModelPath =
            "Assets/_Project/Art/Models/Environment/Floor.glb";
        private const string ScenePath = "Assets/_Project/Scenes/Field/Field_Area02.unity";

        // 床を1辺あたり何枚敷くか＝マップの広さ。大きくすればマップが広くなる。
        private const int FloorTilesX = 15;
        private const int FloorTilesZ = 15;

        [MenuItem("Tools/CreativeAI/Create Field Scene")]
        public static void Run()
        {
            // Editor から手動実行したときに未保存シーンを失わないよう確認する（バッチでは無害）
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // glb が未インポートの場合に備えて取り込みを促す
            AssetDatabase.ImportAsset(FloorModelPath, ImportAssetOptions.ForceSynchronousImport);

            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorModelPath);
            if (floorPrefab == null)
            {
                Debug.LogError(
                    $"[CreateFieldScene] Floor モデルが読み込めませんでした: {FloorModelPath}"
                );
                return;
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single
            );

            // --- 床 ---
            var floorSize = MeasureSize(floorPrefab);
            float stepX = floorSize.x > 0.0001f ? floorSize.x : 1f;
            float stepZ = floorSize.z > 0.0001f ? floorSize.z : 1f;
            Debug.Log($"[CreateFieldScene] Floor タイルサイズ x={stepX:F3} z={stepZ:F3}");

            int gridX = Mathf.Max(1, FloorTilesX);
            int gridZ = Mathf.Max(1, FloorTilesZ);
            Debug.Log(
                $"[CreateFieldScene] 床 {gridX}x{gridZ} 枚 → 全幅 x={stepX * gridX:F2} z={stepZ * gridZ:F2}"
            );

            var floorRoot = new GameObject("Floor");
            float offsetX = -stepX * (gridX - 1) * 0.5f;
            float offsetZ = -stepZ * (gridZ - 1) * 0.5f;

            for (int ix = 0; ix < gridX; ix++)
            {
                for (int iz = 0; iz < gridZ; iz++)
                {
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                    tile.name = $"Floor_{ix}_{iz}";
                    tile.transform.SetParent(floorRoot.transform, false);
                    tile.transform.localPosition = new Vector3(
                        offsetX + ix * stepX,
                        0f,
                        offsetZ + iz * stepZ
                    );

                    // モデル原点が中心でも床の上面を y=0 に揃える（床本体は y=0 より下＝一番底）。
                    AlignTopToY(tile, 0f);
                }
            }

            float areaW = stepX * gridX; // X方向の床全幅
            float areaD = stepZ * gridZ; // Z方向の床全奥行

            // --- カメラ ---
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera != null)
            {
                float span = Mathf.Max(areaW, areaD);
                camera.transform.position = new Vector3(0, span * 0.7f, -span * 0.85f);
                camera.transform.rotation = Quaternion.Euler(35, 0, 0);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            if (saved)
            {
                Debug.Log(
                    $"[CreateFieldScene] シーンを保存しました: {ScenePath} "
                        + $"(床 {gridX}x{gridZ}={gridX * gridZ}枚)"
                );
            }
            else
            {
                Debug.LogError($"[CreateFieldScene] シーン保存に失敗: {ScenePath}");
            }
        }

        /// <summary>インスタンスのメッシュ上面（bounds.max.y）が targetY になるよう垂直方向に移動する。</summary>
        private static void AlignTopToY(GameObject instance, float targetY)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            var pos = instance.transform.position; // parent は原点なので world=local
            pos.y += targetY - bounds.max.y;
            instance.transform.position = pos;
        }

        /// <summary>インスタンスの全 Renderer を合成したワールドバウンズのサイズを返す。</summary>
        private static Vector3 MeasureSize(GameObject prefab)
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var renderers = probe.GetComponentsInChildren<Renderer>();
            Vector3 size = Vector3.one;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                size = bounds.size;
            }
            Object.DestroyImmediate(probe);
            return size;
        }
    }
}
#endif
