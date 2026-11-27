using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セッション常駐の UI 一式(UIRoot Prefab)の根元。UI を1つだけ生成して残し、操作で開くパネル
    /// (キャラ / インベ / セーブ / 調合 / マップ)を排他で開け閉めする。開いてよいかは <see cref="GameModeManager"/> に尋ね、
    /// 開いている間はメニュー中(<see cref="GameModeManager.IsMenuOpen"/>)と伝える。
    /// 子の会話UIを <see cref="DialogueView"/> として会話イベントに渡すため、UI の型に触れずに Gameplay に置く。
    /// Prefab 編集時の注意: 常駐UIは各自の CanvasGroup で出し分けるため、GameObject をオフで保存しない
    /// (オフで保存すると実行時に二度と表示されない)。オン/オフで開閉するのはここが管理するパネルだけ。
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        /// <summary>開け閉めするパネルの種類。</summary>
        public enum UIId
        {
            None,
            Character,
            Inventory,
            Save,
            Craft,
            Map,
        }

        /// <summary>セッション常駐のインスタンス。セッション開始前は null。</summary>
        public static UIManager Instance { get; private set; }

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

        // SetVisible(false) で消した Canvas。戻すときはこれだけ戻し、元から消えていたものは触らない。
        private readonly List<Canvas> _hiddenCanvases = new();

        private GameModeManager _gameMode;

        // 子に同梱された会話UI(ConversationPanel)。型は UI アセンブリにあるので契約で持つ
        private IDialogueView _dialogueView;

        /// <summary>会話UI。会話イベントの再生(EventPlayer)が使う。UI 一式に会話UIが無ければ null。</summary>
        public IDialogueView DialogueView => _dialogueView;

        /// <summary>いずれかのパネル(キャラ/インベ/セーブ/調合/マップ)が表示中か。</summary>
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

        /// <summary>
        /// UI 一式を1つだけ生成して常駐させる(既存ならそれを返す)。
        /// GameSession から マネージャ・所持品の生成の後に呼ぶ(UI がそれらを購読するため)。
        /// </summary>
        public static UIManager EnsureResident(GameObject uiRootPrefab)
        {
            if (Instance != null)
                return Instance;
            if (uiRootPrefab == null)
            {
                Debug.LogWarning(
                    "[UIManager] uiRootPrefab が未割当です。Resources/ResidentBootstrapConfig の uiRootPrefab に UIRoot Prefab を設定してください。"
                );
                return null;
            }

            var go = Instantiate(uiRootPrefab);
            go.name = uiRootPrefab.name; // "(Clone)" を避ける
            return go.GetComponent<UIManager>();
        }

        /// <summary>
        /// UI 全体の表示を切り替える。タイトルの暗転が降りる前に HUD が見えないよう、生成した直後に隠し、
        /// 読み込みが終わって暗転が明ける前に戻す。
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                foreach (var canvas in _hiddenCanvases)
                {
                    if (canvas != null)
                        canvas.enabled = true;
                }
                _hiddenCanvases.Clear();
                return;
            }

            foreach (var canvas in GetComponentsInChildren<Canvas>())
            {
                if (!canvas.enabled)
                    continue;
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 二重生成をガード(冪等)
                return;
            }
            Instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject); // EditMode では呼べない
            _dialogueView = GetComponentInChildren<IDialogueView>(true);

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
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            CraftStationInteractor.Requested += OpenCraft;
            _gameMode = GameModeManager.Instance;
            if (_gameMode != null)
                _gameMode.Changed += OnModeChanged;
        }

        private void OnDisable()
        {
            CraftStationInteractor.Requested -= OpenCraft;
            if (_gameMode != null)
                _gameMode.Changed -= OnModeChanged;
            _gameMode = null;
            LeaveMenu();
        }

        // メニューが閉じられた(戦闘が始まった等)のにパネルが開いていれば閉じる。
        private void OnModeChanged()
        {
            if (!_gameMode.IsMenuOpen && IsAnyPanelOpen)
                CloseAll();
        }

        // 合成装置からの依頼。調合パネルが未割当のシーンでは、他のパネルまで閉じないよう何もしない。
        private void OpenCraft()
        {
            if (_panels.ContainsKey(UIId.Craft))
                Open(UIId.Craft);
        }

        // パネルを全部閉じたらメニューを閉じたと伝える(パネルが自分の戻るボタンで閉じたときもここを通る)。
        private void SyncOpenState()
        {
            if (!IsAnyPanelOpen)
                LeaveMenu();
        }

        private static void LeaveMenu()
        {
            var mode = GameModeManager.Instance;
            if (mode != null)
                mode.CloseMenu();
        }

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

        /// <summary>対象を開き、他を全て閉じる(排他)。探索中でなければ(会話・戦闘中)開かない。</summary>
        public void Open(UIId id)
        {
            var mode = GameModeManager.Instance;
            if (mode != null && !mode.TryOpenMenu())
                return;
            // 開く先を先に出してから他を閉じる。逆だと切り替えの途中で全部閉じた瞬間ができ、メニューが閉じた扱いになる
            if (_panels.TryGetValue(id, out var target))
                target.SetActive(true);
            foreach (var kv in _panels)
            {
                if (kv.Key != id)
                    kv.Value.SetActive(false);
            }
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
