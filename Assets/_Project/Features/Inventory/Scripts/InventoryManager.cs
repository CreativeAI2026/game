using System.Collections.Generic;
using System.Linq;
using CreativeAI.Core;
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
        // この間に Awake したインスタンスはデバッグ用テストアイテムを積まない(新規はまっさら)。
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

        private InventoryService _inventoryService;
        private CraftingService _craftingService;
        private PlayerStatus _playerStatus;

        public InventoryService InventoryService => _inventoryService ??= CreateInventoryService();

        public CraftingService CraftingService =>
            _craftingService ??= new CraftingService(InventoryService);

        /// <summary>
        /// セッション常駐の Inventory を1つだけ生成する(既存ならそれを返す)。Core から Gameplay は参照できないため Core ではなくここに置き、
        /// Title フローから マネージャ生成の後・プレイヤー生成の前に呼ぶ。
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
            if (_playerStatus == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                _playerStatus = player != null ? player.GetComponent<PlayerStatus>() : null;
            }
            return InventoryService.TryUseFood(stack, _playerStatus);
        }

        public bool HasItem(ItemData data, int count = 1)
        {
            return InventoryService.HasItem(data, count);
        }

        public int GetItemCount(ItemData data)
        {
            return InventoryService.GetItemCount(data);
        }

        public bool CanCraft(CraftRecipe recipe, ItemStack materialA, ItemStack materialB)
        {
            return CraftingService.CanCraft(recipe, materialA, materialB);
        }

        public bool TryCraft(
            CraftRecipe recipe,
            ItemStack materialA,
            ItemStack materialB,
            out ItemStack crafted
        )
        {
            return CraftingService.TryCraft(recipe, materialA, materialB, out crafted);
        }

        /// <summary>装備品の同時装備上限。</summary>
        public const int MaxEquippedEquipment = 3;

        public void SetEquipped(ItemStack stack, bool equipped)
        {
            if (stack == null || stack.IsEquipped == equipped)
                return;
            // 装備品は最大3つまで。上限に達していたら装着を拒否する(武器は在庫外なので数えない)。
            if (
                equipped
                && stack.Data.category == ItemCategory.Equipment
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

        /// <summary>現在装備中の装備品の数。武器は在庫外なので数えない。</summary>
        private int CountEquippedEquipment() =>
            InventoryService
                .GetAllItems()
                .Count(s => s != null && s.IsEquipped && s.Data.category == ItemCategory.Equipment);

        /// <summary>
        /// 装備中の装備品の補正合計(武器は WeaponManager 側で別合算)。
        /// 装備品の能力値は個体ごとのロール値(RolledStats)だけ。ロールされていない装備品は補正なし。
        /// </summary>
        public EquipmentBonus GetEquippedBonus()
        {
            var b = new EquipmentBonus();
            foreach (var stack in InventoryService.GetAllItems())
            {
                if (stack == null || !stack.IsEquipped)
                    continue;
                // 武器(WeaponData)は在庫外・WeaponManager 管理なので意図的に加算しない。
                if (stack.Data.category == ItemCategory.Equipment)
                    b.Add(stack.RolledStats);
            }
            return b;
        }

        /// <summary>stack が即時使用食材スロットにセットされているか(調合の素材から除外・警告に使う)。</summary>
        public bool IsInQuickFood(ItemStack stack) => InventoryService.IsInQuickFood(stack);

        public bool SwapQuickFood(int firstSlot, int secondSlot) =>
            InventoryService.SwapQuickFood(firstSlot, secondSlot);

        public List<ItemStack> GetItemsByCategory(ItemCategory category)
        {
            return InventoryService.GetItemsByCategory(category);
        }

        public List<ItemStack> GetAllItems() => InventoryService.GetAllItems();

        // --- 即時使用食材スロット(最大3)。即時食材使用UIにセットする食材の選択状態 ---

        /// <summary>即時使用食材スロットの内容(食材スタック or null)。即時食材使用UI / 即時使用食材タブが読む。</summary>
        public IReadOnlyList<ItemStack> GetQuickFoodSlots() => InventoryService.GetQuickFoodSlots();

        /// <summary>スロット slot に食材をセットする(CharacterUI 即時使用食材タブから)。食材以外・在庫外は false。</summary>
        public bool SetQuickFood(int slot, ItemStack stack) =>
            InventoryService.SetQuickFood(slot, stack);

        /// <summary>スロット slot を空にする。</summary>
        public void ClearQuickFood(int slot) => InventoryService.ClearQuickFood(slot);

        private void AddTestItems()
        {
            if (ItemCatalog.Instance == null)
                return;

            var testItems = ItemCatalog
                .Instance.Items.Where(item => item != null && HasZeroSecondDigit(item))
                .ToList();
            foreach (var item in testItems)
            {
                int count =
                    item.MaxStack > 1
                        ? Random.Range(InitialTestItemMinCount, InitialTestItemMaxCountExclusive)
                        : 1;
                AddAcquired(item, count);
            }
        }

        private void EquipInitialTestItems()
        {
            // 食材は装備の概念を持たない。装備扱いにするのは装備品のみ。
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
            var service = new InventoryService();
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
