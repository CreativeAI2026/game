using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// <see cref="ItemPlacementPlan"/> の通りに <c>ItemPickup</c> Prefab を <c>_Items</c> シーンへまとめて置く。
    /// 置いたものには識別子も振る(<see cref="FieldItemPickupIdAssigner"/>)。
    /// </summary>
    public static class ItemPickupPlacer
    {
        public const string ItemsRootName = "Items";

        /// <summary>
        /// 計画をシーンに置く。置いた <see cref="FieldItemPickup"/> を返す。
        /// アイテム未設定の行は警告して飛ばす。既存の配置は触らない(2回実行するとその分重なる)。
        /// </summary>
        public static List<FieldItemPickup> Place(Scene scene, ItemPlacementPlan plan)
        {
            var placed = new List<FieldItemPickup>();
            if (!scene.IsValid() || !scene.isLoaded || plan == null)
                return placed;
            if (plan.pickupPrefab == null)
            {
                Debug.LogWarning("[ItemPickupPlacer] pickupPrefab が未設定です。");
                return placed;
            }

            var root = FindOrCreateRoot(scene);
            var used = new HashSet<Vector3>();
            var taken = new List<Vector3>(); // 今回置いた位置(近すぎる場所を避ける)

            foreach (var entry in plan.entries)
            {
                if (entry?.item == null)
                {
                    Debug.LogWarning("[ItemPickupPlacer] item が未設定の行を飛ばしました。");
                    continue;
                }

                foreach (var spot in entry.candidates)
                {
                    if (!used.Add(spot))
                        continue; // 同じ場所に重ねて置かない

                    // 床に合わせ、小物や壁と重なるなら近くの空いた場所へずらす。
                    if (!ItemSpotFinder.TryFind(spot, taken, out var ground))
                    {
                        Debug.LogWarning(
                            $"[ItemPickupPlacer] {entry.item.itemName}: {spot} の近くに置ける場所が見つからず、飛ばしました。({ItemSpotFinder.LastFailure})"
                        );
                        continue;
                    }

                    var pickup = PlaceOne(scene, root, plan.pickupPrefab, entry, ground);
                    if (pickup == null)
                        continue;
                    placed.Add(pickup);
                    taken.Add(ground);
                }
            }

            FieldItemPickupIdAssigner.AssignInScene(scene);
            if (placed.Count > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            return placed;
        }

        private static FieldItemPickup PlaceOne(
            Scene scene,
            Transform root,
            GameObject prefab,
            ItemPlacementPlan.Entry entry,
            Vector3 position
        )
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            go.name = $"ItemPickup_{entry.item.itemName}";

            var pickup = go.GetComponent<FieldItemPickup>();
            if (pickup == null)
            {
                Debug.LogWarning("[ItemPickupPlacer] Prefab に FieldItemPickup がありません。");
                Object.DestroyImmediate(go);
                return null;
            }

            var so = new SerializedObject(pickup);
            so.FindProperty("_item").objectReferenceValue = entry.item;
            so.FindProperty("_count").intValue = Mathf.Max(1, entry.count);
            so.ApplyModifiedPropertiesWithoutUndo();
            return pickup;
        }

        private static Transform FindOrCreateRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == ItemsRootName)
                    return go.transform;

            var created = new GameObject(ItemsRootName);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created.transform;
        }
    }
}
