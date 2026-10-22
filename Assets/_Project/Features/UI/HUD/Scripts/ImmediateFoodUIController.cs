using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 画面左下の即時食材UI(常駐、UIRoot の ImmediateFoodUI)。
    /// キャラUIの即時使用食材タブでセットした最大3枠(<see cref="InventoryManager.GetQuickFoodSlots"/>)を表示し、
    /// 使用(1/2/3キー、<see cref="PlayerQuickFoodUser"/>)で減った所持数や、使い切って空いた枠を追従させる。表示だけで使用はしない。
    /// </summary>
    public sealed class ImmediateFoodUIController : MonoBehaviour
    {
        [Tooltip("左から 1/2/3 キーに対応する順で並べる")]
        [SerializeField]
        private List<ImmediateFoodSlotView> _slots = new();

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private UiRouter _router;
        private InventoryManager _inventory;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _router = GetComponentInParent<UiRouter>(true);
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
            ApplyVisibility();
        }

        private void OnDisable() => Unbind();

        // 常駐生成順では InventoryManager が先に居るが、居ないシーン(UI確認用など)で後から生成された場合にも追従する。
        private void Update()
        {
            if (_inventory == null && InventoryManager.Instance != null)
            {
                Bind();
                Refresh();
            }
            ApplyVisibility();
        }

        private void Bind()
        {
            if (_inventory != null || InventoryManager.Instance == null)
                return;
            _inventory = InventoryManager.Instance;
            _inventory.QuickFoodChanged += Refresh;
            _inventory.InventoryChanged += Refresh;
        }

        private void Unbind()
        {
            if (_inventory == null)
                return;
            _inventory.QuickFoodChanged -= Refresh;
            _inventory.InventoryChanged -= Refresh;
            _inventory = null;
        }

        private void Refresh()
        {
            var data = _inventory != null ? _inventory.GetQuickFoodSlots() : null;
            for (int i = 0; i < _slots.Count; i++)
            {
                var stack = data != null && i < data.Count ? data[i] : null;
                if (stack != null)
                    _slots[i].Show(stack);
                else
                    _slots[i].Clear();
            }
        }

        // QuickFoodBarController と同じ出し分け: パネル表示中・会話中は隠す(戦闘中は常時表示)。
        // canvas.enabled で隠すので購読は生き続ける。
        private void ApplyVisibility()
        {
            bool inBattle =
                GameModeManager.Instance != null
                && GameModeManager.Instance.CurrentMode == GameMode.Battle;
            bool inDialogue = EventPlaybackService.IsPlaying && !inBattle;
            bool hide = (_router != null && _router.IsAnyPanelOpen) || inDialogue;
            if (_canvas != null)
                _canvas.enabled = !hide;
            if (_raycaster != null)
                _raycaster.enabled = !hide;
        }
    }
}
