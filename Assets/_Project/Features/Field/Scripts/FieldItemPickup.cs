using CreativeAI.Core;
using CreativeAI.Core.EventSystem;
using CreativeAI.Crafting;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドに置かれた取得可能アイテム(documents/Specification.md §0「キラキラとしたエフェクトを伴って
    /// 地面に配置される」/ §2「拾得」)。プレイヤーが触れると在庫へ入れて自分を消す。
    ///
    /// 拾えるのは<b>移動中(Field)だけ</b>。戦闘モード中・会話イベント再生中は触れても拾わない
    /// (操作不能な間に在庫が動くのを防ぐ。セーブ/食材使用と同じ判断軸)。
    ///
    /// 装備品は<b>拾った瞬間に付与ステータスをロールする</b>(§2.1.1: 型は重み付き非復元抽出 /
    /// 量は CraftingStatAlgorithm のロールモデル)。同じ装備品でも拾うたびに違う個体になるため、
    /// 数量でまとめず1個ずつ別スタックで持つ。食材・大事なものは固定ルールなのでそのまま数量ぶん積む。
    ///
    /// 配置は「シーン上に手で置く」流儀(EventTrigger / SceneExit と同じ): 空の GameObject に
    /// Collider(Is Trigger = ON)とこのコンポーネントを付け、Item にアセットを、Sparkle に
    /// キラキラエフェクトをアサインする。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FieldItemPickup : MonoBehaviour
    {
        [Tooltip(
            "拾えるアイテム(装備品 / 食材 / 大事なもの)。武器は在庫外なのでここには置けない。"
        )]
        [SerializeField]
        private ItemData _item;

        [Tooltip("拾える個数。装備品は1個ずつ別個体としてロールする。")]
        [SerializeField, Min(1)]
        private int _count = 1;

        [Tooltip("キラキラエフェクト(任意)。拾った瞬間に消す。")]
        [SerializeField]
        private GameObject _sparkle;

        [SerializeField]
        private string _playerTag = "Player";

        [Tooltip("OFF にすると拾っても GameObject を残す(再配置・演出を配置側で管理したいとき)。")]
        [SerializeField]
        private bool _destroyOnPickup = true;

        private bool _picked;

        /// <summary>すでに拾われたか(二重取得防止)。</summary>
        public bool IsPicked => _picked;

        /// <summary>拾えるアイテム。配置ツール・テストからの確認用。</summary>
        public ItemData Item => _item;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag))
                return;
            TryPickup();
        }

        /// <summary>
        /// 在庫へ入れて自分を片付ける。拾えたら true。
        /// 拾えない条件(既に拾われた / 未設定 / 戦闘中 / 会話イベント中 / 在庫が無い)では
        /// <b>何も消費・変更しない</b>。
        /// </summary>
        public bool TryPickup()
        {
            if (_picked)
                return false;

            if (_item == null)
            {
                Debug.LogWarning(
                    $"[FieldItemPickup] '{name}': Item が未設定のため拾得をスキップしました。"
                );
                return false;
            }

            // 移動中(Field)のみ。マネージャ未生成(開発シーン直 Play)は Field 扱いで許可する。
            var mode = GameModeManager.Instance;
            if (mode != null && mode.CurrentMode != GameMode.Field)
                return false;
            if (EventPlaybackService.IsPlaying)
                return false;

            var inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                Debug.LogWarning(
                    $"[FieldItemPickup] '{name}': InventoryManager が無いため拾得をスキップしました。"
                );
                return false;
            }

            if (_item is EquipmentData equipment)
            {
                // 装備品は拾った瞬間にロール(§2.1.1)。個体差があるので1個ずつ別スタック。
                var rng = new SystemRandomSource();
                for (int i = 0; i < _count; i++)
                    inventory.AddInstance(equipment, CraftStatBridge.RollDrop(equipment, rng));
            }
            else
            {
                // 食材(HP即時回復の固定ルール)・大事なもの。ロールは通さない。
                inventory.AddItem(_item, _count);
            }

            _picked = true;
            if (_sparkle != null)
                _sparkle.SetActive(false);

            if (!_destroyOnPickup)
                return true;

            if (Application.isPlaying)
                Destroy(gameObject);
            else
                gameObject.SetActive(false); // EditMode(テスト)では Destroy を呼べない
            return true;
        }
    }
}
