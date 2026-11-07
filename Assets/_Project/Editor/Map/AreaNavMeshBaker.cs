#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CreativeAI.Gameplay;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// Field_Area01 の NavMesh(敵が歩ける範囲)を焼く。Map の当たり(Ground / Obstacle レイヤーの Collider)から作るので、
    /// 当たりの無い小物は対象外(プレイヤーも擦り抜けるのと揃う)。扉板は開閉するので通れる扱いにする。
    /// 小物(机・椅子など)は別の小物シーンにあるので、ベイクの間だけ Additive で重ねて当たりを含める
    /// (含めないと NavMesh が小物の上を通ってしまい、道筋の線や敵の経路が小物を貫く)。
    /// NavMeshSurface は Map の外の `NavMesh` ルートに置く(Rebuild で Map ごと消えないように)。
    /// </summary>
    public static class AreaNavMeshBaker
    {
        const string SurfaceRootName = "NavMesh";

        [MenuItem("Tools/CreativeAI/Map/Bake NavMesh Field_Area01")]
        public static void BakeNavMeshArea01()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = MapLayoutBuilder.FindOpenScene(MapLayoutBuilder.MapScenePath);
            if (!scene.IsValid())
                scene = EditorSceneManager.OpenScene(
                    MapLayoutBuilder.MapScenePath,
                    OpenSceneMode.Single
                );
            Bake(scene);
        }

        /// <summary>そのシーンの NavMesh を焼いて、データを シーン名/NavMesh.asset に保存しシーンも保存する。</summary>
        public static bool Bake(Scene scene)
        {
            var map = FindRoot(scene, MapLayoutBuilder.MapRoot);
            if (map == null)
            {
                Debug.LogError(
                    $"[AreaNavMeshBaker] {scene.path} に {MapLayoutBuilder.MapRoot} がありません。"
                );
                return false;
            }

            var mask = LayerMask.GetMask("Ground", "Obstacle");
            if (mask == 0)
            {
                Debug.LogError(
                    "[AreaNavMeshBaker] レイヤー Ground / Obstacle が Tags and Layers にありません。"
                );
                return false;
            }

            IgnoreDoorLeaves(map);

            var surface = EnsureSurface(scene);
            surface.agentTypeID = 0; // Humanoid(敵 Prefab の NavMeshAgent と同じ)
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = mask;
            var openedPropScenes = OpenPropScenes(scene);
            try
            {
                surface.BuildNavMesh();
            }
            finally
            {
                foreach (var propScene in openedPropScenes)
                    EditorSceneManager.CloseScene(propScene, true);
            }
            if (surface.navMeshData == null)
            {
                Debug.LogError("[AreaNavMeshBaker] NavMesh のベイクに失敗しました。");
                return false;
            }

            // 焼いたデータは メモリ上にしか無いので、シーンと同名フォルダ(オクルージョンと同じ場所)へ保存する
            var dir = Path.Combine(
                Path.GetDirectoryName(scene.path)!,
                Path.GetFileNameWithoutExtension(scene.path)
            );
            Directory.CreateDirectory(dir);
            var assetPath = Path.Combine(dir, "NavMesh.asset").Replace('\\', '/');
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
            EditorUtility.SetDirty(surface);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var tri = NavMesh.CalculateTriangulation();
            Debug.Log(
                $"[AreaNavMeshBaker] NavMesh を焼きました({tri.indices.Length / 3} 三角形)→ {assetPath}"
            );
            return true;
        }

        /// <summary>
        /// 小物シーンのうち開いていないものを Additive で開いて返す(閉じるのは呼び出し側)。
        /// すでに開いているものは触らない。
        /// </summary>
        static List<Scene> OpenPropScenes(Scene mapScene)
        {
            var opened = new List<Scene>();
            foreach (var part in PropSceneWorkflow.Parts)
            {
                var path = PropSceneWorkflow.ScenePath(part);
                if (!File.Exists(path) || SceneManager.GetSceneByPath(path).isLoaded)
                    continue;
                opened.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
            }
            if (mapScene.IsValid())
                SceneManager.SetActiveScene(mapScene);
            return opened;
        }

        /// <summary>扉板は開閉するので焼く対象から外す(閉じた形で焼くと戸口が通れなくなる)。</summary>
        static void IgnoreDoorLeaves(GameObject map)
        {
            foreach (var door in map.GetComponentsInChildren<SwingDoor>(true))
            {
                var leaf = door.Leaf != null ? door.Leaf : SwingDoor.FindLeaf(door.transform);
                if (leaf == null)
                    continue;
                if (!leaf.TryGetComponent<NavMeshModifier>(out var modifier))
                    modifier = leaf.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
                // Prefab インスタンスは記録しないとシーン保存時に上書きが残らない
                PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);
            }
        }

        static NavMeshSurface EnsureSurface(Scene scene)
        {
            var root = FindRoot(scene, SurfaceRootName);
            if (root == null)
            {
                root = new GameObject(SurfaceRootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            return root.TryGetComponent<NavMeshSurface>(out var surface)
                ? surface
                : root.AddComponent<NavMeshSurface>();
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name)
                    return go;
            return null;
        }
    }
}
#endif
