using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// フィールドに置くアイテムの計画。アイテム・個数・場所を並べておくと、
    /// <see cref="ItemPickupPlacer"/> が <c>ItemPickup</c> Prefab を、1行につき場所の数だけ置く。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemPlacementPlan", menuName = "CreativeAI/アイテム配置計画")]
    public sealed class ItemPlacementPlan : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("置くアイテム(装備品 / 食材 / 大事なもの)。")]
            public ItemData item;

            [Min(1)]
            [Tooltip("1か所で拾える個数。置く数は Candidates の数で決まる。")]
            public int count = 1;

            [Tooltip(
                "置く場所(ワールド座標)。ここに並べた場所すべてに1個ずつ置く。NavMesh 上に寄せる。"
            )]
            public List<Vector3> candidates = new();
        }

        [Tooltip("ItemPickup Prefab。")]
        public GameObject pickupPrefab;

        public List<Entry> entries = new();
    }
}
