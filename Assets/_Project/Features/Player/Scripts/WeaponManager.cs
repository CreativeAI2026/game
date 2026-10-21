using System;
using System.Collections.Generic;
using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの武器の<b>所持状態</b>と<b>選択中の1本</b>を管理する。
    ///
    /// 仕様:
    /// - 主人公は最初1本も持たない。イベントの giveWeapon(<see cref="IWeaponGiver"/>)で入手する。
    /// - 切り替えられるのは<b>入手ずみの武器だけ</b>。未入手はスキップされる(押しても変化なし)。
    /// - 最終ステータスに乗るのは<b>選択中の1本の補正だけ</b>。0本なら補正は 0。
    /// - 所持本数の変化は <see cref="OnOwnedCountChanged"/> で武器切替UIへ通知する(0本で非表示)。
    ///
    /// 武器切り替え時に武器ごとの AnimatorController へ差し替えてから Animator.Rebind() で状態を完全リセットし、
    /// 前の武器のアニメーショントリガーやステートが残留するのを防ぐ。
    /// </summary>
    public class WeaponManager : MonoBehaviour, IWeaponGiver
    {
        /// <summary>武器が1本も選ばれていない状態の index。</summary>
        public const int NoWeapon = -1;

        /// <summary>
        /// events.json の weaponKey(documents/ScenarioReference.md「武器カタログ」)と
        /// <see cref="_slots"/> の index の対応。並び順が契約。
        /// </summary>
        public static readonly IReadOnlyList<string> WeaponKeys = new[]
        {
            "sword",
            "bow",
            "scythe",
            "hammer",
        };

        private static readonly IReadOnlyList<int> WeaponIds = new[] { 1001, 1002, 1003, 1004 };

        /// <summary>
        /// 切り替えの順番(E = 次の武器 で進む向き)。Q はこの逆順。
        /// WeaponKeys の番号はセーブ・events.json との契約なので並べ替えず、切り替え順はここで別に持つ。
        /// 武器切替 HUD の輪の並びもこの順(選択中を上に置き、反時計回りに次の武器)。
        /// </summary>
        private static readonly string[] SwitchOrderKeys = { "sword", "scythe", "hammer", "bow" };

        /// <summary>武器1本分の設定。以前は3本の配列に分かれていて、並びを揃え損ねると別の武器の値が使われていた。</summary>
        [Serializable]
        private struct WeaponSlot
        {
            [Tooltip("武器のルートオブジェクト(Player/Weapons の下)。モデル未配線なら空欄")]
            public GameObject weapon;

            [Tooltip(
                "ステータス補正。選択中の1本の補正のみが最終ステータスに乗る(Specification.md「アイテムカテゴリと付与ステータス」)"
            )]
            public WeaponData stats;

            [Tooltip(
                "選択時に Animator へ差し替える Controller(ベース Controller または Animator Override Controller)。"
                    + "空欄なら Animator に最初から設定されている Controller を使う"
            )]
            public RuntimeAnimatorController animatorController;
        }

        [Header("武器の一覧(並び順 = WeaponKeys: 0 剣 / 1 弓 / 2 鎌 / 3 ハンマー)")]
        [Tooltip("ステータス補正の Key が並び順と合っていないと、Inspector 編集時に警告が出る")]
        [SerializeField]
        private WeaponSlot[] _slots = Array.Empty<WeaponSlot>();

        [Header("プロトタイプ用")]
        [Tooltip(
            "ON にすると開始時に全武器を入手ずみにする。戦闘検証シーン専用の抜け道で、"
                + "仕様(初期0本・イベントで入手)は OFF 側。製品フローでは OFF のままにすること。"
        )]
        [SerializeField]
        private bool _startWithAllWeapons;

        // 入手ずみ武器の index。切り替え順(SwitchOrderKeys = 剣→鎌→ハンマー→弓)に並べておく。
        private readonly List<int> _owned = new();
        private int _currentWeaponIndex = NoWeapon;

        private PlayerInputHandler _input;
        private Animator _animator;
        private RuntimeAnimatorController _defaultAnimatorController;
        private PlayerController _playerController;

        /// <summary>武器を切り替えたときに通知。true: prev(左回転) / false: next(右回転)。</summary>
        public event Action<bool> OnWeaponSwitched;

        /// <summary>
        /// 所持本数が変わったときに通知(引数は変化後の本数)。武器切替UIが 0本→非表示 / 1本以上→表示 に使う。
        /// </summary>
        public event Action<int> OnOwnedCountChanged;

        /// <summary>選択中の武器 index。1本も持っていなければ <see cref="NoWeapon"/>。</summary>
        public int CurrentWeaponIndex => _currentWeaponIndex;

        /// <summary>
        /// 選択中の武器のルートオブジェクト。1本も持っていない・未登録なら null。
        /// PlayerAnimationEventRelay が AnimationEvent の届け先を探すのに使う。
        /// </summary>
        public GameObject CurrentWeaponObject =>
            _currentWeaponIndex >= 0 && _currentWeaponIndex < _slots.Length
                ? _slots[_currentWeaponIndex].weapon
                : null;

        /// <summary>
        /// 装備中の武器の行動を強制的に打ち切って中立状態へ戻す(怯み・死亡時)。
        /// しまっている武器は OnDisable で後始末済みなので対象外。
        /// </summary>
        public void ResetEquippedWeapon()
        {
            GameObject weapon = CurrentWeaponObject;
            if (weapon != null && weapon.TryGetComponent(out IResettableWeapon resettable))
                resettable.ForceReset();
        }

        /// <summary>登録されている武器の総数。WeaponHUDController がパネル数の決定に使用する。</summary>
        public int WeaponCount => _slots.Length;

        public GameObject GetWeaponObject(int dataId)
        {
            foreach (var slot in _slots)
                if (slot.stats != null && slot.stats.id == dataId)
                    return slot.weapon;
            return null;
        }

        public void Unequip()
        {
            ApplySelection(NoWeapon);
            OnWeaponSwitched?.Invoke(false);
        }

        /// <summary>
        /// 武器のアイコン(ステータス補正アセット WeaponData の Icon)。未設定なら null。
        /// HUD はこれを使ってパネルとアイコンを武器ごとに対応させる。
        /// </summary>
        public Sprite GetIcon(int index)
        {
            if (index < 0 || index >= _slots.Length)
                return null;
            WeaponData stats = _slots[index].stats;
            return stats != null ? stats.icon : null; // ステータス補正が未設定の枠
        }

        /// <summary>入手ずみの本数。</summary>
        public int OwnedCount => _owned.Count;

        /// <summary>入手ずみ武器の index。切り替え順(E で進む順)に並んでいる。</summary>
        public IReadOnlyList<int> OwnedIndices => _owned;

        /// <summary>入手ずみの一覧を切り替え順に並べ直す。</summary>
        private void SortOwnedBySwitchOrder()
        {
            _owned.Sort((a, b) => SwitchOrderOf(a).CompareTo(SwitchOrderOf(b)));
        }

        private static int SwitchOrderOf(int weaponIndex)
        {
            int order = Array.IndexOf(SwitchOrderKeys, WeaponKeys[weaponIndex]);
            // 切り替え順に載っていない武器は末尾(番号順)に回す
            return order >= 0 ? order : SwitchOrderKeys.Length + weaponIndex;
        }

        public bool IsOwned(int index) => _owned.Contains(index);

        public bool IsOwned(string weaponKey) => IsOwned(IndexOfKey(weaponKey));

        /// <summary>weaponKey → index。未知のキーは -1。</summary>
        public static int IndexOfKey(string weaponKey)
        {
            if (string.IsNullOrEmpty(weaponKey))
                return -1;
            for (int i = 0; i < WeaponKeys.Count; i++)
            {
                if (WeaponKeys[i] == weaponKey)
                    return i;
            }
            return -1;
        }

        // --- IWeaponGiver: イベントの giveWeapon から呼ばれる ---

        /// <summary>
        /// weaponKey(WeaponKeys のいずれか)の武器を1本入手する。既に所持なら何もしない。
        /// 最初の1本なら自動で選択状態にし、補正を最終ステータスへ反映させる。
        /// </summary>
        public void GiveWeapon(string weaponKey)
        {
            int index = IndexOfKey(weaponKey);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[WeaponManager] 未知の weaponKey '{weaponKey}'({string.Join(" / ", WeaponKeys)} のみ)。入手をスキップしました。"
                );
                return;
            }
            GiveWeaponAt(index);
        }

        private void GiveWeaponAt(int index)
        {
            if (index < 0 || index >= WeaponKeys.Count)
                return;
            if (_owned.Contains(index))
                return; // 既に所持

            _owned.Add(index);
            SortOwnedBySwitchOrder();

            bool isFirst = _owned.Count == 1;
            if (isFirst)
                ApplySelection(index);

            OnOwnedCountChanged?.Invoke(_owned.Count);
            if (isFirst)
                OnWeaponSwitched?.Invoke(false); // PlayerStatus に武器補正を引き直させる
        }

        /// <summary>
        /// 選択中の武器の補正を装備品と同じ <see cref="EquipmentBonus"/> 形式で返す。
        /// PlayerStatus が「装備補正 + 武器補正」として最終ステータスに合算する
        /// (装備品:InventoryManager と対称。選択の情報源はここ 1 箇所)。
        /// 選択中の 1 本の補正のみが乗り、<b>0本のときは 0</b>。
        /// 移動速度/攻撃速度は PlayerStatus の対象外なので含めない。
        /// </summary>
        public EquipmentBonus GetSelectedBonus()
        {
            var b = new EquipmentBonus();
            if (_currentWeaponIndex < 0 || _currentWeaponIndex >= _slots.Length)
            {
                return b;
            }

            var w = _slots[_currentWeaponIndex].stats;
            if (w == null)
            {
                return b; // ステータス補正が未設定の枠
            }

            b.attackPct += w.attack;
            b.criticalChance += w.criticalRate;
            b.criticalDamage += w.criticalDamage;
            return b;
        }

        // --- セーブ(SaveService から呼ばれる) ---

        public int CaptureSelectedWeaponIndex() => _currentWeaponIndex;

        /// <summary>保存用に入手ずみ武器のキーを返す(index ではなくキーで持つ = 並び替えに強い)。</summary>
        public IReadOnlyList<string> CaptureOwnedWeaponKeys()
        {
            var keys = new List<string>(_owned.Count);
            foreach (int i in _owned)
                keys.Add(WeaponKeys[i]);
            return keys;
        }

        /// <summary>
        /// 入手ずみ武器と選択中の武器を復元する。選択が未所持・範囲外なら所持の先頭に寄せる。
        /// PlayerStatus が購読して武器補正を再計算する(bool は HUD 回転向きの区別用。復元は次向き扱い)。
        /// </summary>
        public void RestoreWeapons(IReadOnlyList<string> ownedWeaponKeys, int selectedWeaponIndex)
        {
            _owned.Clear();
            if (ownedWeaponKeys != null)
            {
                foreach (var key in ownedWeaponKeys)
                {
                    int index = IndexOfKey(key);
                    if (index >= 0 && !_owned.Contains(index))
                        _owned.Add(index);
                }
            }
            SortOwnedBySwitchOrder();

            int selected = _owned.Contains(selectedWeaponIndex)
                ? selectedWeaponIndex
                : (_owned.Count > 0 ? _owned[0] : NoWeapon);

            ApplySelection(selected);
            OnOwnedCountChanged?.Invoke(_owned.Count);
            OnWeaponSwitched?.Invoke(false);
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            // EditMode テストでは WeaponManager 単体で生成されモデル層が無いため、無ければ Animator 操作をしない
            PlayerModel model = GetComponentInChildren<PlayerModel>(true);
            if (model != null)
            {
                _animator = model.Animator;
                _defaultAnimatorController = _animator.runtimeAnimatorController;
            }
            _playerController = GetComponent<PlayerController>();

            // giveWeapon の届け先として自身を登録する(IItemGiver / InventoryManager と同じ思想)。
            WeaponGiverService.Current = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(WeaponGiverService.Current, this))
                WeaponGiverService.Current = null;
        }

        private void Start()
        {
            if (_startWithAllWeapons)
            {
                // モデルが配線されている武器だけを持たせる(未配線の枠を選ぶと武器が消えて見えるため)。
                int slots = Mathf.Min(_slots.Length, WeaponKeys.Count);
                for (int i = 0; i < slots; i++)
                {
                    if (_slots[i].weapon != null)
                        GiveWeaponAt(i);
                }
            }
            else
            {
                // 初期0本: どの武器モデルも出さない。
                ApplySelection(_currentWeaponIndex);
                OnOwnedCountChanged?.Invoke(_owned.Count);
            }
        }

        private void Update()
        {
            if (!_playerController.CanChangeWeapon)
            {
                return;
            }

            if (_input.weaponNext)
            {
                _input.weaponNext = false;
                SelectNext();
            }

            if (_input.weaponPrev)
            {
                _input.weaponPrev = false;
                SelectPrevious();
            }
        }

        /// <summary>入手ずみ武器の並びで次の1本へ。切り替わったら true。</summary>
        public bool SelectNext()
        {
            if (!TryStep(+1))
                return false;
            OnWeaponSwitched?.Invoke(false);
            return true;
        }

        /// <summary>入手ずみ武器の並びで前の1本へ。切り替わったら true。</summary>
        public bool SelectPrevious()
        {
            if (!TryStep(-1))
                return false;
            OnWeaponSwitched?.Invoke(true);
            return true;
        }

        /// <summary>
        /// 入手ずみ武器の並びの中で1つ進む/戻る。0本・1本のときは変化しない
        /// (未入手の武器は選べない = 押しても変化なし)。
        /// </summary>
        private bool TryStep(int direction)
        {
            if (_owned.Count <= 1)
                return false;

            int position = _owned.IndexOf(_currentWeaponIndex);
            if (position < 0)
                position = 0;

            int next = (position + direction + _owned.Count) % _owned.Count;
            if (_owned[next] == _currentWeaponIndex)
                return false;

            ApplySelection(_owned[next]);
            return true;
        }

        /// <summary>選択を切り替えてモデルの表示と Animator を合わせる。index が負なら全部しまう。</summary>
        private void ApplySelection(int index)
        {
            _currentWeaponIndex = index;

            // 先に全部しまってから選択中だけ出す。同じオブジェクトが複数の枠に入っていても、選択中が消えないように
            foreach (var slot in _slots)
            {
                if (slot.weapon != null)
                    slot.weapon.SetActive(false);
            }
            GameObject selected = CurrentWeaponObject;
            if (selected != null)
                selected.SetActive(true);

            if (_animator != null && index >= 0)
            {
                // 武器ごとの移動・攻撃モーションは Override Controller のクリップ差し替えで切り替える。
                // ステート構成は全武器で共通なので、武器固有のステートへは各武器が送る専用 Trigger でしか入らない。
                _animator.runtimeAnimatorController = ResolveAnimatorController(index);

                // Rebindで全パラメータ・遷移・トリガーをリセットし、
                // 前の武器のアニメーション状態が新しい武器に漏れるのを防ぐ
                _animator.Rebind();
            }
        }

        private RuntimeAnimatorController ResolveAnimatorController(int index)
        {
            RuntimeAnimatorController controller =
                index < _slots.Length ? _slots[index].animatorController : null;
            // 空欄の枠は Animator に最初から設定されている Controller を使う
            return controller != null ? controller : _defaultAnimatorController;
        }

#if UNITY_EDITOR
        /// <summary>並び順の契約(WeaponKeys)とステータス補正の Key が食い違っていたら、編集時に警告する。</summary>
        private void OnValidate()
        {
            if (_slots.Length != WeaponKeys.Count)
                Debug.LogWarning(
                    $"{name}: 武器の一覧は {WeaponKeys.Count} 枠(並び順 {string.Join(" / ", WeaponKeys)})にしてください",
                    this
                );

            int count = Mathf.Min(_slots.Length, WeaponKeys.Count);
            for (int i = 0; i < count; i++)
            {
                WeaponData stats = _slots[i].stats;
                if (stats != null && stats.id != WeaponIds[i])
                    Debug.LogWarning(
                        $"{name}: 武器の一覧 [{i}] は「{WeaponKeys[i]}」の枠ですが、武器IDが「{stats.id}」です（期待値: {WeaponIds[i]}）",
                        this
                    );
            }
        }
#endif
    }
}
