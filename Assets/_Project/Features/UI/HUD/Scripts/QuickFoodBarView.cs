using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 即時食材使用UI(常駐)。キャラUIのロードアウト(<see cref="LoadoutPanel"/>)でセットした
    /// 最大3枠(<see cref="QuickFoodSlots"/>)を常時表示し、タップで即時使用(HP回復+消費)する。
    /// モードや所持で出し分けない。UIRoot Prefab の子として同梱され、常駐は <see cref="UIManager"/> が担う。状態は持たない。
    /// </summary>
    public sealed class QuickFoodBarView : MonoBehaviour
    {
        [Header("Slots (最大3。子の EquipmentSlot を順に割り当て)")]
        [SerializeField]
        private List<EquipmentSlot> _slots = new();

        private bool _subscribed;
        private CanvasGroup _group;
        private GameModeManager _gameMode;

        private void Awake()
        {
            _group = ResidentUIVisibility.GetOrAddGroup(gameObject);
            InitializeSlots();
        }

        private void OnEnable()
        {
            TryBind();
            RefreshSlots();
            SubscribeVisibility();
            ApplyVisibility();
        }

        // パネル(インベ/キャラ/セーブ/調合)表示中・会話中はバーを隠す。CanvasGroup で隠すので購読は生き続ける。
        // 常駐生成順ではマネージャが先に居るので Instance を取れる。
        private void SubscribeVisibility()
        {
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
        }

        private void UnsubscribeVisibility()
        {
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
        }

        private void OnModeChanged() => ApplyVisibility();

        private void ApplyVisibility()
        {
            // 会話中は使用不可なので隠す。戦闘中(会話の battle ステップも含む)は常時表示。
            bool hide = GameModeManager.MenuOpen || GameModeManager.Current == GameMode.Dialogue;
            if (_group != null)
                _group.SetShown(!hide);
        }

        private void OnDisable()
        {
            Unbind();
            UnsubscribeVisibility();
        }

        private void OnDestroy()
        {
            UnbindSlots();
            Unbind();
        }

        private void InitializeSlots()
        {
            foreach (var slot in _slots)
            {
                if (slot == null)
                    continue;
                slot.Init();
                slot.Clear();
                slot.Clicked -= OnSlotClicked;
                slot.Clicked += OnSlotClicked;
            }
        }

        private void UnbindSlots()
        {
            foreach (var slot in _slots)
                if (slot != null)
                    slot.Clicked -= OnSlotClicked;
        }

        /// <summary>枠タップで即時使用。FoodUse.TryEat が HP回復+在庫1消費まで行う(使い切って空いた枠は QuickFoodSlots.Changed 経由で反映)。</summary>
        private void OnSlotClicked(EquipmentSlot slot)
        {
            int index = _slots.IndexOf(slot);
            var stack = QuickFoodStackAt(index);
            var player = PlayerRegistry.Current;
            if (stack != null && player != null && player.TryGetComponent(out PlayerStatus status))
                FoodUse.TryEat(status, stack);
        }

        private static ItemStack QuickFoodStackAt(int index)
        {
            if (index < 0)
                return null;
            var quickFood = InventoryManager.CurrentQuickFood;
            var data = quickFood != null ? quickFood.GetSlots() : null;
            return data != null && index < data.Count ? data[index] : null;
        }

        /// <summary>データ側(最大3)の内容を各枠へ反映。単一ソースはデータ側。</summary>
        private void RefreshSlots()
        {
            var quickFood = InventoryManager.CurrentQuickFood;
            var data = quickFood != null ? quickFood.GetSlots() : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] == null)
                    continue;
                var stack = data != null && i < data.Count ? data[i] : null;
                if (stack != null)
                    _slots[i].SetStack(stack);
                else
                    _slots[i].Clear();
            }
        }

        private void TryBind()
        {
            if (
                _subscribed
                || InventoryManager.Instance == null
                || InventoryManager.CurrentQuickFood == null
            )
                return;
            InventoryManager.CurrentQuickFood.Changed -= OnQuickFoodChanged;
            InventoryManager.CurrentQuickFood.Changed += OnQuickFoodChanged;
            InventoryManager.Instance.Changed -= OnInventoryChanged;
            InventoryManager.Instance.Changed += OnInventoryChanged;
            _subscribed = true;
        }

        private void Unbind()
        {
            if (!_subscribed)
                return;
            if (InventoryManager.CurrentQuickFood != null)
                InventoryManager.CurrentQuickFood.Changed -= OnQuickFoodChanged;
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.Changed -= OnInventoryChanged;
            _subscribed = false;
        }

        private void OnQuickFoodChanged() => RefreshSlots();

        private void OnInventoryChanged()
        {
            // 使用で在庫数が変わった枠の数量表示を更新(空になった枠は所持品から消すときに外れ、QuickFoodSlots.Changed で届く)。
            foreach (var slot in _slots)
                if (slot != null && slot.Stack != null)
                    slot.UpdateCount();
        }
    }
}
