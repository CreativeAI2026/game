using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    [CreateAssetMenu(fileName = "ItemDB", menuName = "Scriptable Objects/ItemDB")]
    public class ItemDB : ScriptableObject
    {
        private static ItemDB _instance;
        public static ItemDB Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<ItemDB>("ItemDB");
                return _instance;
            }
        }
        public List<ItemData> items;

        public ItemData GetItemById(int id)
        {
            if (items == null)
                return null;

            foreach (var item in items)
            {
                if (item != null && item.id == id)
                    return item;
            }

            return null;
        }
    }
}
