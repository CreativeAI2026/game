using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class InventoryManager : MonoBehaviour, IItemGiver
    {
        private const int InitialEquippedTestItemCountPerCategory = 2;
        private const int InitialTestItemMinCount = 5;
        private const int InitialTestItemMaxCountExclusive = 16;

        public static InventoryManager Instance { get; private set; }

        // EnsureResident() 経由(本番Title フロー)で生成中は true。
        // この間に Awake したインスタンスはデバッグ用テストアイテムを積まない(新規はまっさら。spec §6.1)。
        // シーン直置き(開発時に Field を直接 Play)では false のままなので従来どおりテスト品が入る。
        private static bool _creatingResident;

        public event System.Action InventoryChanged;

        /// <summary>即時使用食材スロット(最大3)の内容が変わったときに発火。即時食材使用UI / 即時使用食材タブが購読する。</summary>
        public event System.Action QuickFoodChanged;

        /// <summary>
        /// 装備の着脱で発火(静的:PlayerStatus は先に生成され得るため、インスタンス無しでも購読できる)。
        /// PlayerStatus がこれを受けて装備補正を再計算する。
        /// </summary>
        public static event System.Action EquipmentChanged;

        [SerializeField]
        private bool _addTestItemsOnAwake = true;

        private readonly InventoryStorage _storage = new();
        private InventoryService _inventoryService;
        private RecipeCraftingService _recipeCraftingService;
        private ItemUseService _itemUseService;

        public InventoryService InventoryService => _inventoryService ??= CreateInventoryService();

        public RecipeCraftingService RecipeCraftingService =>
            _recipeCraftingService ??= new RecipeCraftingService(InventoryService);

        public ItemUseService ItemUseService =>
            _itemUseService ??= new ItemUseService(InventoryService);

        /// <summary>
        /// セッション常駐の Inventory を「はじめる/続きから」時に1つだけ生成する(spec §6.1: 生成はTitleが担う)。
        /// 既に在ればそれを返す。Core は Gameplay を参照できない(循環)ため SessionBootstrap ではなくここに置き、
        /// Title フロー(UI 層)から マネージャ生成の後・プレイヤー生成の前に呼ぶ。
        /// コード生成なのでシーン配置に依存せず、どのエリアから開始しても Inventory が必ず存在する。
        /// </summary>
        public static InventoryManager EnsureResident()
        {
            if (Instance != null)
                return Instance;

            _creatingResident = true;
            try
            {
                // AddComponent は Awake を同期実行する。_creatingResident 中の Awake はテスト品を積まない。
                return new GameObject(nameof(InventoryManager)).AddComponent<InventoryManager>();
            }
            finally
            {
                _creatingResident = false;
            }
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

            // giveItem ステップの seam に自身を登録(EventPlayer が Inspector 未配線時に拾う)。
            ItemGiverService.Current = this;

            if (_addTestItemsOnAwake && !_creatingResident)
            {
                AddTestItems();
                EquipInitialTestItems();
            }

            // 初期装備を PlayerStatus に反映させる(購読済みの PlayerStatus が居れば再計算)。
            EquipmentChanged?.Invoke();
        }

        private bool _testItemsSeeded;

        /// <summary>
        /// 開発シーン(<c>FieldDevBootstrap</c>)用: テスト品を積み初期装備する。既に積んでいれば何もしない。
        /// 本番 Title フローでは呼ばない(所持品はまっさらで始まる)。
        /// </summary>
        public void SeedTestItems()
        {
            if (_testItemsSeeded)
                return;
            _testItemsSeeded = true;
            AddTestItems();
            EquipInitialTestItems();
            EquipmentChanged?.Invoke();
        }

        public void AddItem(ItemData data, int count = 1)
        {
            InventoryService.AddItem(data, count);
        }

        public void AddEquipmentItem(EquipmentData data, EquipmentInstance instance)
        {
            InventoryService.AddEquipmentItem(data, instance);
        }

        /// <summary>
        /// 調合でロールした装備品/武器を「個体」として追加する(数量1・他とマージしない)。
        /// 同じ素材ペアでも個体差が出るため、1個ずつ別スタックで保持する。
        /// </summary>
        public ItemStack AddInstance(ItemData data, IReadOnlyList<RolledStat> rolledStats)
        {
            return InventoryService.AddInstance(data, rolledStats);
        }

        /// <summary>所持品を全消去する(ロード時の再構築用)。</summary>
        public void Clear()
        {
            InventoryService.Clear();
        }

        /// <summary>
        /// IItemGiver。EventPlayer の giveItem ステップから文字列キーで呼ばれ、1個追加する。
        /// キーが ItemDB に無ければ警告して無視(打ち間違い検出は Importer 側が本命)。
        /// </summary>
        public void Give(string itemKey)
        {
            var data = ItemDB.Instance != null ? ItemDB.Instance.GetItemByKey(itemKey) : null;
            if (data == null)
            {
                Debug.LogWarning(
                    $"[InventoryManager] Give: itemKey '{itemKey}' が ItemDB に見つかりません。"
                );
                return;
            }
            AddItem(data, 1);
        }

        /// <summary>
        /// IItemGiver。hasItem 条件から呼ばれ、itemKey の「大事なもの」を1つ以上持つかを返す。
        /// itemKey を ItemDB で引き、カテゴリが 大事なもの のときだけ所持数を見る。
        /// 未登録キー・大事なもの以外(装備品/食材/武器)は対象外で false(警告つき)。
        /// </summary>
        public bool HasImportantItem(string itemKey)
        {
            var data = ItemDB.Instance != null ? ItemDB.Instance.GetItemByKey(itemKey) : null;
            if (data == null)
            {
                Debug.LogWarning(
                    $"[InventoryManager] HasImportantItem: itemKey '{itemKey}' が ItemDB に見つかりません。"
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

        public void RemoveItem(ItemData data, int count = 1)
        {
            InventoryService.RemoveItem(data, count);
        }

        public bool ConsumeItem(ItemData data, int count = 1)
        {
            return InventoryService.ConsumeItem(data, count);
        }

        public bool TryUse(ItemStack stack)
        {
            return ItemUseService.TryUse(stack);
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return InventoryService.HasItem(data, count);
        }

        public int GetItemCount(ItemData data)
        {
            return InventoryService.GetItemCount(data);
        }

        public bool CanCraft(CraftRecipeData recipe, int quantity = 1)
        {
            return RecipeCraftingService.CanCraft(recipe, quantity);
        }

        public bool CanCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return RecipeCraftingService.CanCraft(recipe, materialA, materialB);
        }

        public int GetMaximumCraftable(CraftRecipeData recipe)
        {
            return RecipeCraftingService.GetMaximumCraftable(recipe);
        }

        public bool TryCraft(CraftRecipeData recipe, int quantity)
        {
            return RecipeCraftingService.TryCraft(recipe, quantity);
        }

        public bool TryCraft(CraftRecipeData recipe, ItemStack materialA, ItemStack materialB)
        {
            return RecipeCraftingService.TryCraft(recipe, materialA, materialB);
        }

        /// <summary>装備品の同時装備上限(仕様 §2.1「装備品 最大3つ」)。</summary>
        public const int MaxEquippedEquipment = 3;

        public void SetEquipped(ItemStack stack, bool equipped)
        {
            if (stack == null || stack.IsEquipped == equipped)
                return;
            // 装備品は最大3つまで。上限に達していたら装着を拒否する(武器は在庫外なので数えない)。
            if (
                equipped
                && stack.Data is EquipmentData
                && CountEquippedEquipment() >= MaxEquippedEquipment
            )
            {
                Debug.LogWarning(
                    $"[InventoryManager] 装備品は最大 {MaxEquippedEquipment} つまでです。装着をスキップしました。"
                );
                return;
            }
            stack.IsEquipped = equipped;
            EquipmentChanged?.Invoke(); // 最終ステータス再計算のトリガー
        }

        /// <summary>現在装備中の装備品(EquipmentData)の数。武器は在庫外なので数えない。</summary>
        private int CountEquippedEquipment() =>
            InventoryService
                .GetAllItems()
                .Count(s => s != null && s.IsEquipped && s.Data is EquipmentData);

        /// <summary>
        /// 装備中(IsEquipped)の装備品の補正合計。素の値に足すと最終ステータス。
        /// 武器は在庫外(仕様 L30・3本固定切替)なのでここでは扱わない。選択中武器の補正は
        /// WeaponManager.GetSelectedBonus() から PlayerStatus が別ルートで合算する。
        /// 調合で作られた個体(stack.RolledStats あり)は端末でロールした個体差を持つので、そのロール値を
        /// 使う(CraftStatBridge 経由)。素材の固定 SO(RolledStats 無し)は EquipmentData の値を使う。
        /// </summary>
        public EquipmentBonus GetEquippedBonus()
        {
            var b = new EquipmentBonus();
            foreach (var stack in InventoryService.GetAllItems())
            {
                if (stack == null || !stack.IsEquipped)
                    continue;
                // 武器(WeaponData)は在庫外・WeaponManager 管理なので意図的に加算しない。
                if (stack.Data is not EquipmentData e)
                    continue;

                if (stack.RolledStats != null && stack.RolledStats.Count > 0)
                {
                    // 調合でロールされた個体(個体差あり)。
                    CraftStatBridge.Accumulate(ref b, stack.RolledStats);
                }
                else
                {
                    // 固定 SO の装備品(素材など未ロール)。
                    b.attackPct += e.attack;
                    b.defensePct += e.defense;
                    b.maxHpPct += e.maxHP;
                    b.criticalChance += e.criticalRate;
                    b.criticalDamage += e.criticalDamage;
                }
            }
            return b;
        }

        public bool IsEquipped(ItemStack stack) => stack?.IsEquipped ?? false;

        public bool IsItemEquipped(ItemData data)
        {
            return data != null
                && InventoryService
                    .GetAllItems()
                    .Any(stack => stack.Data == data && stack.IsEquipped);
        }

        public bool HasEquippedMaterial(IEnumerable<ItemData> materials)
        {
            return materials != null && materials.Any(IsItemEquipped);
        }

        /// <summary>stack が即時使用食材スロットにセットされているか(調合の素材から除外・警告に使う)。</summary>
        public bool IsInQuickFood(ItemStack stack) => InventoryService.IsInQuickFood(stack);

        /// <summary>data の在庫スタックのいずれかが即時使用食材にセットされているか。</summary>
        public bool IsItemInQuickFood(ItemData data)
        {
            if (data == null)
                return false;
            foreach (var slot in InventoryService.GetQuickFoodSlots())
                if (slot != null && slot.Data == data)
                    return true;
            return false;
        }

        /// <summary>materials のいずれかが即時使用食材にセットされているか(レシピ調合の可否判定用)。</summary>
        public bool HasQuickFoodMaterial(IEnumerable<ItemData> materials)
        {
            return materials != null && materials.Any(IsItemInQuickFood);
        }

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return InventoryService.GetItemsByCategory(category);
        }

        public List<ItemStack> GetAllItems() => InventoryService.GetAllItems();

        // --- 即時使用食材スロット(最大3)。即時食材使用UIにセットする食材の選択状態(spec §1.2) ---

        /// <summary>即時使用食材スロットの内容(食材スタック or null)。即時食材使用UI / 即時使用食材タブが読む。</summary>
        public IReadOnlyList<ItemStack> GetQuickFoodSlots() => InventoryService.GetQuickFoodSlots();

        /// <summary>スロット slot に食材をセットする(CharacterUI 即時使用食材タブから)。食材以外・在庫外は false。</summary>
        public bool SetQuickFood(int slot, ItemStack stack) =>
            InventoryService.SetQuickFood(slot, stack);

        /// <summary>スロット slot を空にする。</summary>
        public void ClearQuickFood(int slot) => InventoryService.ClearQuickFood(slot);

        private void AddTestItems()
        {
            if (ItemDB.Instance == null)
                return;

            // 武器はインベントリ管理の対象外(仕様 §2)。ItemDB はフォルダ一括同期で武器も拾うため、ここで除外する。
            var testItems = ItemDB
                .Instance.Items.Where(item =>
                    item != null && !(item is WeaponData) && HasZeroSecondDigit(item)
                )
                .ToList();
            foreach (var item in testItems)
            {
                int count =
                    item.MaxStack > 1
                        ? Random.Range(InitialTestItemMinCount, InitialTestItemMaxCountExclusive)
                        : 1;
                AddItem(item, count);
            }
        }

        private void EquipInitialTestItems()
        {
            // 食材は装備の概念を持たない(仕様 §2.1)。装備扱いにするのは装備品のみ。
            EquipInitialTestItems(ItemCategory.Equipment);
        }

        private void EquipInitialTestItems(ItemCategory category)
        {
            var items = InventoryService.GetAllItems();
            if (items.Any(stack => stack.Data.category == category && stack.IsEquipped))
                return;

            foreach (
                var stack in items
                    .Where(stack => stack.Data.category == category)
                    .Take(InitialEquippedTestItemCountPerCategory)
            )
            {
                stack.IsEquipped = true;
            }
        }

        private InventoryService CreateInventoryService()
        {
            var service = new InventoryService(_storage);
            service.InventoryChanged += OnInventoryServiceChanged;
            service.QuickFoodChanged += OnQuickFoodChanged;
            return service;
        }

        private void OnInventoryServiceChanged()
        {
            InventoryChanged?.Invoke();
        }

        private void OnQuickFoodChanged()
        {
            QuickFoodChanged?.Invoke();
        }

        private static bool HasZeroSecondDigit(ItemData item)
        {
            if (item == null)
                return false;

            string id = Mathf.Abs(item.id).ToString();
            return id.Length >= 2 && id[1] == '0';
        }
    }
}
