using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 即時食材使用UI(常駐)。キャラUIのロードアウト(<see cref="CharacterUI.LoadoutPanelController"/>)でセットした
    /// 最大3枠(<see cref="InventoryManager.GetQuickFoodSlots"/>)を常時表示し、タップで即時使用(HP回復+消費)する。
    /// モードや所持で出し分けない。UIRoot Prefab の子として同梱され、常駐は <see cref="UIRoot"/> が担う。状態は持たない。
    /// </summary>
    public sealed class QuickFoodBarView : MonoBehaviour
    {
        [Header("Slots (最大3。子の EquipmentSlot を順に割り当て)")]
        [SerializeField]
        private List<EquipmentSlot> _slots = new();

        private bool _subscribed;
        private CanvasGroup _group;
        private UiRouter _router;
        private GameModeManager _gameMode;

        private void Awake()
        {
            _group = ResidentUiVisibility.GetOrAddGroup(gameObject);
            _router = GetComponentInParent<UiRouter>(true); // 親 UIRoot の排他ルータ
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
                _gameMode.OnModeChanged += OnModeChanged;
            EventPlaybackService.PlayingChanged += OnPlaybackChanged;
            UiPanelService.OpenChanged += OnPanelOpenChanged;
        }

        private void UnsubscribeVisibility()
        {
            if (_gameMode != null)
                _gameMode.OnModeChanged -= OnModeChanged;
            EventPlaybackService.PlayingChanged -= OnPlaybackChanged;
            UiPanelService.OpenChanged -= OnPanelOpenChanged;
        }

        private void OnModeChanged(GameMode mode) => ApplyVisibility();

        private void OnPlaybackChanged(bool playing) => ApplyVisibility();

        private void OnPanelOpenChanged(bool open) => ApplyVisibility();

        private void ApplyVisibility()
        {
            // 会話(line/choice)中は使用不可なので隠す。ただし戦闘中は常時表示。
            // battle ステップも EventPlaybackService.IsPlaying の内側で走るため、モードで戦闘を除外する。
            bool inBattle =
                GameModeManager.Instance != null
                && GameModeManager.Instance.CurrentMode == GameMode.Battle;
            bool inDialogue = EventPlaybackService.IsPlaying && !inBattle;
            bool hide = (_router != null && _router.IsAnyPanelOpen) || inDialogue;
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

        /// <summary>枠タップで即時使用。TryUse が HP回復+在庫1消費+通知まで行う(消費で空いた枠は QuickFoodChanged 経由で反映)。</summary>
        private void OnSlotClicked(EquipmentSlot slot)
        {
            int index = _slots.IndexOf(slot);
            var stack = QuickFoodStackAt(index);
            if (stack != null)
                InventoryManager.Instance?.TryUse(stack);
        }

        private static ItemStack QuickFoodStackAt(int index)
        {
            if (index < 0)
                return null;
            var data = InventoryManager.Instance?.GetQuickFoodSlots();
            return data != null && index < data.Count ? data[index] : null;
        }

        /// <summary>データ側(最大3)の内容を各枠へ反映。単一ソースはデータ側。</summary>
        private void RefreshSlots()
        {
            var data = InventoryManager.Instance?.GetQuickFoodSlots();
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
            if (_subscribed || InventoryManager.Instance == null)
                return;
            InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;
            InventoryManager.Instance.QuickFoodChanged += OnQuickFoodChanged;
            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
            _subscribed = true;
        }

        private void Unbind()
        {
            if (!_subscribed)
                return;
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.QuickFoodChanged -= OnQuickFoodChanged;
                InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
            }
            _subscribed = false;
        }

        private void OnQuickFoodChanged() => RefreshSlots();

        private void OnInventoryChanged()
        {
            // 使用で在庫数が変わった枠の数量表示を更新(空になった枠は QuickFoodChanged 側で Clear)。
            foreach (var slot in _slots)
                if (slot?.Stack != null)
                    slot.UpdateCount();
        }
    }
}
