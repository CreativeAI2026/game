using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セッション常駐の所持品。アイテムの束を持ち、追加・消費・検索を行う。
    /// 束に依存する装備の枠(<see cref="Equipment"/>)と即時食材の枠(<see cref="QuickFood"/>)もここが持ち、
    /// 束を所持品から消すときは、同じ操作の中で各枠からも外す(枠に消えた束が残ることはない)。
    /// 所持品と枠を両方変える操作では、すべて変え終えてから通知する(通知を受けた側が途中の状態を見ないように)。
    /// 食材を食べる処理は <see cref="FoodUse"/>、調合のルールは <see cref="CraftingService"/> 側に置く。
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour, IItemGiver
    {
        public static InventoryManager Instance { get; private set; }

        public event Action Changed;

        private readonly List<ItemStack> _items = new();
        private EquipmentSlots _equipment;
        private QuickFoodSlots _quickFood;

        /// <summary>今の所持品の装備の枠。所持品がまだ無ければ null。</summary>
        public static EquipmentSlots CurrentEquipment =>
            Instance != null ? Instance.Equipment : null;

        /// <summary>今の所持品の即時食材の枠。所持品がまだ無ければ null。</summary>
        public static QuickFoodSlots CurrentQuickFood =>
            Instance != null ? Instance.QuickFood : null;

        /// <summary>装備の枠。所持品にある装備品だけを入れられる。</summary>
        public EquipmentSlots Equipment => _equipment ??= new EquipmentSlots(this);

        /// <summary>即時食材の枠。所持品にある食材だけを入れられる。</summary>
        public QuickFoodSlots QuickFood => _quickFood ??= new QuickFoodSlots(this);

        /// <summary>
        /// セッション常駐の Inventory を1つだけ生成する(既存ならそれを返す)。
        /// GameSession から マネージャ生成の後・プレイヤー生成の前に呼ぶ。
        /// </summary>
        public static InventoryManager EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(InventoryManager)).AddComponent<InventoryManager>();
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>count 個を既存スタックへ積み、溢れた分は新しいスタックにする。最後に積んだスタックを返す。</summary>
        public ItemStack AddItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return null;

            int remaining = count;
            int maxStack = data.MaxStack;
            ItemStack lastStack = null;

            foreach (var stack in GetStacksWithRoom(data, maxStack))
            {
                int addCount = Math.Min(remaining, maxStack - stack.Count);
                stack.Count += addCount;
                remaining -= addCount;
                lastStack = stack;

                if (remaining <= 0)
                    break;
            }

            while (remaining > 0)
            {
                int stackCount = Math.Min(remaining, maxStack);
                lastStack = new ItemStack(data, stackCount);
                _items.Add(lastStack);
                remaining -= stackCount;
            }

            Changed?.Invoke();
            return lastStack;
        }

        /// <summary>
        /// 調合でロールした装備品/武器を「個体」として追加する(数量1・他とマージしない)。
        /// 同じ素材ペアでも個体差が出るため、1個ずつ別スタックで保持する。
        /// </summary>
        public ItemStack AddInstance(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            if (data == null)
                return null;

            var stack = new ItemStack(data, rolledStats);
            _items.Add(stack);
            Changed?.Invoke();
            return stack;
        }

        /// <summary>所持品を全消去する(はじめから)。装備と即時食材の枠も空になる。</summary>
        public void Clear()
        {
            if (_items.Count == 0)
                return;

            var removed = new SlotsChange();
            foreach (var stack in _items.ToList())
                removed.Add(RemoveStack(stack));
            NotifyChanged(removed);
        }

        /// <summary>
        /// ロードで所持品と装備・即時食材の枠をまるごと差し替える。全部差し替えてから通知するので、
        /// 通知を受けた側が「所持品はあるのに装備がまだ」のような途中の状態を見ることはない。
        /// 枠に入れられない束(所持品に無い・種類が違う)は入れない。
        /// </summary>
        public void Restore(
            IEnumerable<ItemStack> stacks,
            IReadOnlyList<ItemStack> equipment,
            IReadOnlyList<ItemStack> quickFood
        )
        {
            _items.Clear();
            if (stacks != null)
                _items.AddRange(stacks.Where(stack => stack?.Data != null && stack.Count > 0));
            Equipment.Restore(equipment);
            QuickFood.Restore(quickFood);
            NotifyChanged(new SlotsChange { Equipment = true, QuickFood = true });
        }

        /// <summary>
        /// IItemGiver。EventPlayer の giveItem ステップから文字列キーで呼ばれ、1個追加する。
        /// キーが ItemCatalog に無ければ警告して無視(打ち間違い検出は Importer 側が本命)。
        /// </summary>
        public void Give(string itemKey)
        {
            var data =
                ItemCatalog.Instance != null ? ItemCatalog.Instance.GetItemByKey(itemKey) : null;
            if (data == null)
            {
                Debug.LogWarning(
                    $"[InventoryManager] Give: itemKey '{itemKey}' が ItemCatalog に見つかりません。"
                );
                return;
            }
            AddAcquired(data, 1);
        }

        /// <summary>
        /// 入手したアイテムを追加する(フィールドで拾う・イベントでもらう共通)。装備品は1個ずつ能力値を抽選した個体、それ以外は数量ぶん積む。
        /// </summary>
        public void AddAcquired(ItemData data, int count)
        {
            if (data.category != ItemCategory.Equipment)
            {
                AddItem(data, count);
                return;
            }

            var random = new SystemRandomSource();
            for (int i = 0; i < count; i++)
                AddInstance(data, RolledStat.FromVector(DropStatRoller.RollDropStats(random)));
        }

        /// <summary>
        /// IItemGiver。hasItem 条件から呼ばれ、itemKey の「大事なもの」を1つ以上持つかを返す。
        /// itemKey を ItemCatalog で引き、カテゴリが 大事なもの のときだけ所持数を見る。
        /// 未登録キー・大事なもの以外(装備品/食材/武器)は対象外で false(警告つき)。
        /// </summary>
        public bool HasImportantItem(string itemKey)
        {
            var data =
                ItemCatalog.Instance != null ? ItemCatalog.Instance.GetItemByKey(itemKey) : null;
            if (data == null)
            {
                Debug.LogWarning(
                    $"[InventoryManager] HasImportantItem: itemKey '{itemKey}' が ItemCatalog に見つかりません。"
                );
                return false;
            }
            if (data.category != ItemCategory.Important)
            {
                Debug.LogWarning(
                    $"[InventoryManager] HasImportantItem: itemKey '{itemKey}' は大事なものではありません"
                        + $"(category={data.category})。hasItem 条件は大事なもの専用です。"
                );
                return false;
            }
            return HasItem(data, 1);
        }

        /// <summary>
        /// IItemGiver。key のアイテムの所持数(未装備のスタックの合計)。装備中のものは数えない。key が空なら 0。
        /// </summary>
        public int CountItem(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            return UsableStacks().Where(stack => stack.Data.key == key).Sum(stack => stack.Count);
        }

        /// <summary>
        /// useItem で渡せる持ち物を key ごとに合計した一覧(最初に出た順)。数え方は <see cref="CountItem"/> と同じ。
        /// </summary>
        public List<(ItemData Data, int Count)> GetUsableItemCounts() =>
            UsableStacks()
                .GroupBy(stack => stack.Data.key)
                .Select(group => (Data: group.First().Data, Count: group.Sum(stack => stack.Count)))
                .Where(item => item.Count > 0)
                .ToList();

        // useItem の対象になる束(未装備で key のあるもの)。数える・並べる・消費するのはすべてここから。
        private IEnumerable<ItemStack> UsableStacks() =>
            _items.Where(stack =>
                !Equipment.Contains(stack)
                && stack.Data != null
                && !string.IsNullOrEmpty(stack.Data.key)
            );

        /// <summary>
        /// IItemGiver。key の未装備のアイテムを count 個消費する。足りなければ何も変えず false。
        /// 複数のスタック(装備品の個体など)にまたがっていても、未装備のものから順に消費する。
        /// </summary>
        public bool Consume(string key, int count)
        {
            if (count <= 0 || CountItem(key) < count)
                return false;

            var remaining = count;
            var candidates = UsableStacks().Where(stack => stack.Data.key == key).ToList();
            foreach (var stack in candidates)
            {
                var take = Math.Min(stack.Count, remaining);
                ConsumeFromStack(stack, take);
                remaining -= take;
                if (remaining == 0)
                    break;
            }
            return true;
        }

        public bool ConsumeFromStack(ItemStack stack, int count = 1)
        {
            if (stack == null || count <= 0 || stack.Count < count)
                return false;

            if (!_items.Contains(stack))
                return false;

            // 個体(ロール済み)は Count が常に 1 で減らせないので、1 つ消費した時点で丸ごと取り除く。
            stack.Count -= count;
            var removed = new SlotsChange();
            if (stack.IsInstance || stack.Count <= 0)
                removed = RemoveStack(stack);

            NotifyChanged(removed);
            return true;
        }

        public void RemoveItem(ItemData data, int count = 1)
        {
            if (data == null || count <= 0)
                return;

            int remaining = count;
            bool removedAny = false;
            var removed = new SlotsChange();
            for (int i = _items.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var stack = _items[i];
                if (stack.Data != data)
                    continue;

                int removeCount = Math.Min(stack.Count, remaining);
                stack.Count -= removeCount;
                remaining -= removeCount;
                removedAny = true;

                if (stack.Count <= 0)
                    removed.Add(RemoveStack(stack));
            }

            if (removedAny)
                NotifyChanged(removed);
        }

        // 束を所持品から消し、同じ操作の中で装備と即時食材の枠からも外す。通知はしない(呼んだ側が最後にまとめて通知する)。
        private SlotsChange RemoveStack(ItemStack stack)
        {
            _items.Remove(stack);
            return new SlotsChange
            {
                Equipment = Equipment.Release(stack),
                QuickFood = QuickFood.Release(stack),
            };
        }

        // 所持品と枠を変え終えてから、所持品 → 変わった枠の順に通知する。
        private void NotifyChanged(SlotsChange slots)
        {
            Changed?.Invoke();
            if (slots.Equipment)
                Equipment.NotifyChanged();
            if (slots.QuickFood)
                QuickFood.NotifyChanged();
        }

        // 1回の操作で、どちらの枠が変わったか。
        private struct SlotsChange
        {
            public bool Equipment;
            public bool QuickFood;

            public void Add(SlotsChange other)
            {
                Equipment |= other.Equipment;
                QuickFood |= other.QuickFood;
            }
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return data != null && count > 0 && GetItemCount(data) >= count;
        }

        public int GetItemCount(ItemData data)
        {
            if (data == null)
                return 0;

            return _items.Where(stack => stack.Data == data).Sum(stack => stack.Count);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return _items.FindAll(stack => stack.Data != null && stack.Data.category == category);
        }

        public bool ContainsStack(ItemStack stack)
        {
            return stack != null && _items.Contains(stack);
        }

        public List<ItemStack> GetAllItems() => new(_items);

        private IEnumerable<ItemStack> GetStacksWithRoom(ItemData data, int maxStack)
        {
            return _items.Where(stack =>
                stack.Data == data && !stack.IsInstance && stack.Count < maxStack
            );
        }
    }
}
