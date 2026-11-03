using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// フィールドに置くアイテムの計画。アイテム・個数・場所の候補を並べておくと、
    /// <see cref="ItemPickupPlacer"/> が <c>ItemPickup</c> Prefab を1行につき1つ、候補のどれかへ置く。
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
            [Tooltip("拾える個数。")]
            public int count = 1;

            [Tooltip(
                "置く場所の候補(ワールド座標)。置くたびに、まだ使っていない候補から1つ選ぶ。NavMesh 上に寄せる。"
            )]
            public List<Vector3> candidates = new();
        }

        [Tooltip("ItemPickup Prefab。")]
        public GameObject pickupPrefab;

        [Tooltip("候補の選び方を固定する種。同じ種なら同じ配置になる。")]
        public int seed = 1;

        public List<Entry> entries = new();
    }
}
