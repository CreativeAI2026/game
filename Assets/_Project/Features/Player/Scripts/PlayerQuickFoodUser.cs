using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 即時食材キー(1/2/3)で、キャラUIの即時使用食材タブでセットした食材をその場で使う。
    /// セット内容の正は <see cref="InventoryManager.GetQuickFoodSlots"/>。回復と在庫1消費は
    /// <see cref="InventoryManager.TryUse"/> が行い、使い切った枠は InventoryService 側で空になる。
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
            _status = GetComponent<PlayerStatus>();
        }

        private void OnEnable() => _input.QuickFoodPressed += Use;

        private void OnDisable() => _input.QuickFoodPressed -= Use;

        /// <summary>枠 slot(0始まり)にセットされた食材を1つ使う。空き枠・死亡中は何もしない。</summary>
        public void Use(int slot)
        {
            // 死亡中は回復しないのに在庫だけ減るので使わせない
            if (_status != null && _status.IsDead)
                return;

            var inventory = InventoryManager.Instance;
            if (inventory == null)
                return;

            var slots = inventory.GetQuickFoodSlots();
            if (slot < 0 || slot >= slots.Count || slots[slot] == null)
                return;

            inventory.TryUse(slots[slot]);
        }
    }
}
