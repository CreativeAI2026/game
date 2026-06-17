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
            get
            {
                return _instance ??= Resources.Load<ItemDB>("ItemDB");
                /*
                if (_instance == null)
                    _instance = Resources.Load<ItemDB>("ItemDB");
                return _instance;
            */
            }
        }
        public List<ItemData> items;

        public ItemData GetItemById(int id)
        {
            if (items == null)
                return null;

            return (
                from item in items
                where item != null && item.id == id
                select item
            ).FirstOrDefault();
            /*
                        foreach (var item in items)
                        {
                            if (item != null && item.id == id)
                                return item;
                        }
            
                        return null;
                        */
        }
    }
}
