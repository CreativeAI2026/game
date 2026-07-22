using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 操作で開く UI(キャラ / インベ / セーブ / 調合)の排他表示を管理する薄いルート。
    /// 常駐 <see cref="UIRoot"/> にアタッチする。入口は Open / Toggle の1本で、開くのは常に1つ
    /// (別のを開くと前のが閉じる=排他)。「今どれが開いているか」を持つだけで、
    /// 全システムが叩く重い UIManager は作らない(spec §5「UI / オーバーレイ」の設計判断)。
    /// documents/UIImplementation.md §2 参照。
    /// </summary>
    public sealed class UiRouter : MonoBehaviour
    {
        public enum UiId
        {
            None,
            Character,
            Inventory,
            Save,
            Craft,
        }

        [SerializeField]
        private GameObject _characterUI;

        [SerializeField]
        private GameObject _inventoryUI;

        [SerializeField]
        private GameObject _saveUI;

        [SerializeField]
        private GameObject _craftUI; // 調合場所でのみ開く。未割当可

        private readonly Dictionary<UiId, GameObject> _panels = new();

        /// <summary>いずれかのパネル(キャラ/インベ/セーブ/調合)が表示中か。常駐の即時食材使用UI等の出し分けに使う。</summary>
        public bool IsAnyPanelOpen
        {
            get
            {
                foreach (var kv in _panels)
                    if (kv.Value != null && kv.Value.activeSelf)
                        return true;
                return false;
            }
        }

        private void Awake()
        {
            Register(UiId.Character, _characterUI);
            Register(UiId.Inventory, _inventoryUI);
            Register(UiId.Save, _saveUI);
            Register(UiId.Craft, _craftUI);

            CloseAll(); // 起動時は全て隠しておき、操作で開く
        }

        private void Register(UiId id, GameObject panel)
        {
            if (panel != null)
                _panels[id] = panel;
        }

        /// <summary>対象を開き、他を全て閉じる(排他)。</summary>
        public void Open(UiId id)
        {
            foreach (var kv in _panels)
                kv.Value.SetActive(kv.Key == id);
        }

        /// <summary>全て閉じる。</summary>
        public void CloseAll()
        {
            foreach (var kv in _panels)
                kv.Value.SetActive(false);
        }

        /// <summary>
        /// 同じものが開いていれば閉じ、そうでなければ排他で開く(右上アイコン押下用)。
        /// 実際の表示状態(activeSelf)を見るので、パネル側の戻るボタンで閉じた後でも整合する。
        /// </summary>
        public void Toggle(UiId id)
        {
            if (_panels.TryGetValue(id, out var panel) && panel.activeSelf)
                panel.SetActive(false);
            else
                Open(id);
        }
    }
}
