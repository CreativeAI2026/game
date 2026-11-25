using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 操作で開く UI(キャラ / インベ / セーブ / 調合 / マップ)の排他表示を管理する薄いルート。常駐 <see cref="UIRoot"/> にアタッチする。
    /// 入口は Open / Toggle で、別のを開くと前のが閉じる。「今どれが開いているか」だけを持つ。
    /// 開いているかどうかは <see cref="UIPanelService"/> に伝え、プレイヤー側が操作を止める・カーソルを出すのに使う。
    /// </summary>
    public sealed class UIRouter : MonoBehaviour
    {
        public enum UIId
        {
            None,
            Character,
            Inventory,
            Save,
            Craft,
            Map,
        }

        [SerializeField]
        private GameObject _characterUI;

        [SerializeField]
        private GameObject _inventoryUI;

        [SerializeField]
        private GameObject _saveUI;

        [SerializeField]
        private GameObject _craftUI; // 調合場所でのみ開く。未割当可

        [SerializeField]
        private GameObject _mapUI; // 全画面マップ。未割当可

        private readonly Dictionary<UIId, GameObject> _panels = new();

        /// <summary>いずれかのパネル(キャラ/インベ/セーブ/調合/マップ)が表示中か。常駐の即時食材使用UI等の出し分けに使う。</summary>
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
            Register(UIId.Character, _characterUI);
            Register(UIId.Inventory, _inventoryUI);
            Register(UIId.Save, _saveUI);
            Register(UIId.Craft, _craftUI);
            Register(UIId.Map, _mapUI);

            CloseAll(); // 起動時は全て隠しておき、操作で開く
        }

        private void OnDestroy()
        {
            foreach (var kv in _panels)
                if (kv.Value != null && kv.Value.TryGetComponent(out UIPanelActivityRelay relay))
                    relay.Changed -= SyncOpenState;
        }

        private void OnEnable() => CraftUIRequestService.Requested += OpenCraft;

        private void OnDisable()
        {
            CraftUIRequestService.Requested -= OpenCraft;
            UIPanelService.SetOpen(false);
        }

        // 合成装置からの依頼。調合パネルが未割当のシーンでは、他のパネルまで閉じないよう何もしない。
        private void OpenCraft()
        {
            if (_panels.ContainsKey(UIId.Craft))
                Open(UIId.Craft);
        }

        private void SyncOpenState() => UIPanelService.SetOpen(IsAnyPanelOpen);

        private void Register(UIId id, GameObject panel)
        {
            if (panel == null)
                return;
            _panels[id] = panel;

            // パネルは自分の戻るボタンなどで SetActive(false) して閉じることがあり、ここを通らない。
            // オン・オフの通知を受けて、開いているかどうかを伝え直す。
            if (!panel.TryGetComponent(out UIPanelActivityRelay relay))
                relay = panel.AddComponent<UIPanelActivityRelay>();
            relay.Changed += SyncOpenState;
        }

        /// <summary>対象を開き、他を全て閉じる(排他)。</summary>
        public void Open(UIId id)
        {
            foreach (var kv in _panels)
                kv.Value.SetActive(kv.Key == id);
            SyncOpenState();
        }

        /// <summary>全て閉じる。</summary>
        public void CloseAll()
        {
            foreach (var kv in _panels)
                kv.Value.SetActive(false);
            SyncOpenState();
        }

        /// <summary>
        /// 同じものが開いていれば閉じ、そうでなければ排他で開く(右上アイコン押下用)。
        /// 実際の表示状態(activeSelf)を見るので、パネル側の戻るボタンで閉じた後でも整合する。
        /// </summary>
        public void Toggle(UIId id)
        {
            if (_panels.TryGetValue(id, out var panel) && panel.activeSelf)
            {
                panel.SetActive(false);
                SyncOpenState();
            }
            else
                Open(id);
        }
    }
}
