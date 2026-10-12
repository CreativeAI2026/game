using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 全アイテムの一覧。各 ItemData はこのアセットのサブアセットとして1ファイルにまとまっている。
    /// 中身は Tools > CreativeAI > アイテムを同期 で画像フォルダと items.json から作る。
    /// レシピは合成後アイテムの素材(material1/material2)から組み立てる。
    /// </summary>
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Scriptable Objects/ItemCatalog")]
    public class ItemCatalog : ScriptableObject
    {
        private static ItemCatalog _instance;
        private static ItemCatalog _injected;

        public static ItemCatalog Instance
        {
            get
            {
                if (_injected != null)
                    return _injected;
                _instance ??= Resources.Load<ItemCatalog>("ItemCatalog");
                return _instance;
            }
        }

        /// <summary>
        /// テスト専用: <see cref="Instance"/> を合成カタログに差し替える。null を渡すと解除して通常のロードに戻る。
        /// 実アセットに依存せず itemKey→ItemData の解決やレシピの検索を検証したいテストにはこの注入口が要る。
        /// </summary>
        public static void InjectForTests(IReadOnlyList<ItemData> testItems)
        {
            if (_injected != null)
                DestroyImmediate(_injected);
            _injected = null;
            if (testItems == null)
                return;

            _injected = CreateInstance<ItemCatalog>();
            _injected.items = testItems.Where(i => i != null).ToList();
        }

        [SerializeField, HideInInspector]
        private List<ItemData> items = new();

        private Dictionary<(ItemData, ItemData), CraftRecipe> _recipeByMaterials;

        public IReadOnlyList<ItemData> Items => items;

        public ItemData GetItemById(int id)
        {
            if (items == null)
                return null;
            return items.FirstOrDefault(i => i != null && i.id == id);
        }

        /// <summary>events.json の giveItem/itemKey(文字列)から ItemData を引く。未設定・未一致は null。</summary>
        public ItemData GetItemByKey(string key)
        {
            if (string.IsNullOrEmpty(key) || items == null)
                return null;
            return items.FirstOrDefault(i => i != null && i.key == key);
        }

        /// <summary>素材2つ(順不同)から完成品のレシピを引く。無ければ null。</summary>
        public CraftRecipe FindRecipe(ItemData materialA, ItemData materialB)
        {
            if (materialA == null || materialB == null)
                return null;

            _recipeByMaterials ??= BuildRecipeMap();
            return _recipeByMaterials.GetValueOrDefault((materialA, materialB));
        }

#if UNITY_EDITOR
        /// <summary>同期ツールから一覧を差し替える。</summary>
        public void SetItems(IEnumerable<ItemData> newItems)
        {
            items = newItems.ToList();
            _recipeByMaterials = null;
        }
#endif

        // 素材の並び順を問わないよう、両方の順で登録する。
        private Dictionary<(ItemData, ItemData), CraftRecipe> BuildRecipeMap()
        {
            var map = new Dictionary<(ItemData, ItemData), CraftRecipe>();
            foreach (var item in items)
            {
                if (item?.material1 == null || item.material2 == null)
                    continue;
                var recipe = new CraftRecipe
                {
                    material1 = item.material1,
                    material2 = item.material2,
                    resultItem = item,
                };
                map.TryAdd((item.material1, item.material2), recipe);
                map.TryAdd((item.material2, item.material1), recipe);
            }
            return map;
        }

        private void OnValidate()
        {
            _recipeByMaterials = null;
        }
    }
}
