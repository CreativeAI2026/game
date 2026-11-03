using System.Collections.Generic;
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
    /// <see cref="ItemPlacementPlan"/> の通りに <c>ItemPickup</c> Prefab を <c>_Items</c> シーンへまとめて置く。
    /// 置いたものには識別子も振る(<see cref="FieldItemPickupIdAssigner"/>)。
    /// </summary>
    public static class ItemPickupPlacer
    {
        public const string ItemsRootName = "Items";
        private const float SnapDistance = 4f;

        /// <summary>
        /// 計画をシーンに置く。置いた <see cref="FieldItemPickup"/> を返す。
        /// 候補が足りない行・アイテム未設定の行は警告して飛ばす。既存の配置は触らない。
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
            var random = new System.Random(plan.seed);

            foreach (var entry in plan.entries)
            {
                if (entry?.item == null)
                {
                    Debug.LogWarning("[ItemPickupPlacer] item が未設定の行を飛ばしました。");
                    continue;
                }

                var free = entry.candidates.Where(c => !used.Contains(c)).ToList();
                if (free.Count == 0)
                {
                    Debug.LogWarning(
                        $"[ItemPickupPlacer] {entry.item.itemName}: 使える場所の候補がありません。飛ばしました。"
                    );
                    continue;
                }

                var spot = free[random.Next(free.Count)];
                used.Add(spot);

                var pickup = PlaceOne(scene, root, plan.pickupPrefab, entry, Snap(spot));
                if (pickup != null)
                    placed.Add(pickup);
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

        /// <summary>候補を NavMesh 上に寄せる(壁や地面の下に埋まらないように)。NavMesh が無ければそのまま。</summary>
        private static Vector3 Snap(Vector3 position) =>
            NavMesh.SamplePosition(position, out var hit, SnapDistance, NavMesh.AllAreas)
                ? hit.position
                : position;

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
