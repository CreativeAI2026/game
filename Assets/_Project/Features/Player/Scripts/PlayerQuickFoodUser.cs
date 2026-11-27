using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 即時食材キー(1/2/3)で、キャラUIの即時使用食材タブでセットした食材をその場で使う。
    /// セット内容の正は <see cref="QuickFoodSlots"/>。回復と在庫1消費は
    /// <see cref="FoodUse.TryEat"/> が行い、使い切った束は InventoryManager が食材の枠からも外す。
    /// 会話中・パネル表示中は <see cref="PlayerInputHandler"/> が入力を捨てるので、ここでは見ない。
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public sealed class PlayerQuickFoodUser : MonoBehaviour
    {
        private PlayerInputHandler _input;
        private PlayerStatus _status;

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            TryGetComponent(out _status);
        }

        private void OnEnable() => _input.QuickFoodPressed += Use;

        private void OnDisable() => _input.QuickFoodPressed -= Use;

        /// <summary>枠 slot(0始まり)にセットされた食材を1つ使う。空き枠・死亡中は何もしない。</summary>
        public void Use(int slot)
        {
            var quickFood = InventoryManager.CurrentQuickFood;
            if (quickFood == null || _status == null)
                return;

            var slots = quickFood.GetSlots();
            if (slot < 0 || slot >= slots.Count || slots[slot] == null)
                return;

            FoodUse.TryEat(_status, slots[slot]);
        }
    }
}
