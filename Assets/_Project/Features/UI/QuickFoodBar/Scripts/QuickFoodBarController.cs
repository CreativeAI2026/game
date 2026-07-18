using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.QuickFoodBar
{
    /// <summary>
    /// 即時食材使用UI(常駐)。キャラUIの即時使用食材タブ(<see cref="CreativeAI.UI.CharacterUI.QuickFoodViewController"/>)で
    /// セットした最大3枠(<see cref="InventoryManager.GetQuickFoodSlots"/>)を常時表示し、タップで即時使用(HP回復+消費)する。
    ///
    /// 仕様(Specification.md §5): 移動中・戦闘中とも常時表示(モードで出し分けない)。武器切替UIのように所持で出し分けもしない。
    /// 仕様§6のとおり <see cref="UIRoot"/> が束ねる UI レイヤーの一部で、UIRoot Prefab の子として同梱される
    /// (常駐・単一化・DontDestroyOnLoad は UIRoot が担うため、このコンポーネント自身は自己生成も DDOL もしない)。
    /// 状態は持たない(表示はデータ側の単一ソースを読むだけ)。
    /// </summary>
    public sealed class QuickFoodBarController : MonoBehaviour
    {
        [Header("Slots (最大3。子の EquipmentSlot を順に割り当て)")]
        [SerializeField]
        private List<EquipmentSlot> _slots = new();

        private bool _subscribed;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private UiRouter _router;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _router = GetComponentInParent<UiRouter>(true); // 親 UIRoot の排他ルータ
            InitializeSlots();
        }

        private void OnEnable()
        {
            TryBind();
            RefreshSlots();
            ApplyVisibility();
        }

        // パネル(インベ/キャラ/セーブ/調合)表示中・会話中はバーを隠す。canvas.enabled で隠すので購読は生き続ける。
        private void Update() => ApplyVisibility();

        private void ApplyVisibility()
        {
            bool hide =
                (_router != null && _router.IsAnyPanelOpen) || EventPlaybackService.IsPlaying;
            if (_canvas != null)
                _canvas.enabled = !hide;
            if (_raycaster != null)
                _raycaster.enabled = !hide;
        }

        private void OnDisable() => Unbind();

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
