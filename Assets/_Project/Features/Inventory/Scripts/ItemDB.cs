using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "ItemDB", menuName = "Scriptable Objects/ItemDB")]
    public class ItemDB : ScriptableObject
    {
        private static ItemDB _instance;
        public static ItemDB Instance
        {
            get { return _instance ??= Resources.Load<ItemDB>("ItemDB"); }
        }

        public List<ItemData> items;

        public ItemData GetItemById(int id)
        {
            if (items == null)
                return null;
            return items.FirstOrDefault(i => i != null && i.id == id);
        }
    }
}
