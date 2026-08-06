using System;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの武器の<b>所持状態</b>と<b>選択中の1本</b>を管理する(documents/Specification.md §1.1, §5, §6)。
    ///
    /// 仕様:
    /// - 主人公は最初1本も持たない。イベントの giveWeapon(<see cref="IWeaponGiver"/>)で入手する。
    /// - 切り替えられるのは<b>入手ずみの武器だけ</b>。未入手はスキップされる(押しても変化なし)。
    /// - 最終ステータスに乗るのは<b>選択中の1本の補正だけ</b>。0本なら補正は 0。
    /// - 所持本数の変化は <see cref="OnOwnedCountChanged"/> で武器切替UIへ通知する(0本で非表示)。
    ///
    /// 武器切り替え時に Animator.Rebind() で状態を完全リセットし、
    /// 前の武器のアニメーショントリガーやステートが残留するのを防ぐ。
    /// </summary>
    public class WeaponManager : MonoBehaviour, IWeaponSaveState, IWeaponGiver
    {
        /// <summary>武器が1本も選ばれていない状態の index。</summary>
        public const int NoWeapon = -1;

        /// <summary>
        /// events.json の weaponKey(documents/ScenarioReference.md「武器カタログ」)と
        /// <see cref="_weapons"/> / <see cref="_weaponStats"/> の index の対応。並び順が契約。
        /// </summary>
        public static readonly IReadOnlyList<string> WeaponKeys = new[]
        {
            "sword",
            "bow",
            "scythe",
        };

        [Header("武器リスト(0:剣, 1:弓, 2:鎌 = WeaponKeys と同順)")]
        [Tooltip("子オブジェクトにある各武器のルートオブジェクトを登録します")]
        [SerializeField]
        private GameObject[] _weapons;

        [Header("武器ごとのステータス補正(_weapons と同じ index 順で登録)")]
        [Tooltip(
            "選択中の1本の補正のみが最終ステータスに乗る(Specification.md「アイテムカテゴリと付与ステータス」)"
        )]
        [SerializeField]
        private WeaponData[] _weaponStats;

        [Header("プロトタイプ用")]
        [Tooltip(
            "ON にすると開始時に全武器を入手ずみにする。戦闘検証シーン専用の抜け道で、"
                + "仕様(初期0本・イベントで入手)は OFF 側。製品フローでは OFF のままにすること。"
        )]
        [SerializeField]
        private bool _startWithAllWeapons;

        // 入手ずみ武器の index。昇順を保つ(切替順 = 剣→弓→鎌)。
        private readonly List<int> _owned = new();
        private int _currentWeaponIndex = NoWeapon;

        private PlayerInputHandler _input;
        private Animator _animator;
        private PlayerController _playerController;

        /// <summary>武器を切り替えたときに通知。true: prev(左回転) / false: next(右回転)。</summary>
        public event Action<bool> OnWeaponSwitched;

        /// <summary>
        /// 所持本数が変わったときに通知(引数は変化後の本数)。武器切替UIが 0本→非表示 / 1本以上→表示 に使う
        /// (Specification.md §5)。
        /// </summary>
        public event Action<int> OnOwnedCountChanged;

        /// <summary>選択中の武器 index。1本も持っていなければ <see cref="NoWeapon"/>。</summary>
        public int CurrentWeaponIndex => _currentWeaponIndex;

        /// <summary>入手ずみの本数。</summary>
        public int OwnedCount => _owned.Count;

        /// <summary>入手ずみ武器の index(昇順)。</summary>
        public IReadOnlyList<int> OwnedIndices => _owned;

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
        /// weaponKey(sword/bow/scythe)の武器を1本入手する。既に所持なら何もしない。
        /// 最初の1本なら自動で選択状態にし、補正を最終ステータスへ反映させる。
        /// </summary>
        public void GiveWeapon(string weaponKey)
        {
            int index = IndexOfKey(weaponKey);
            if (index < 0)
            {
                Debug.LogWarning(
                    $"[WeaponManager] 未知の weaponKey '{weaponKey}'(sword / bow / scythe のみ)。入手をスキップしました。"
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
            _owned.Sort();

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
        /// spec: 選択中の 1 本の補正のみが乗り、<b>0本のときは 0</b>。
        /// 移動速度/攻撃速度は PlayerStatus の対象外なので含めない。
        /// </summary>
        public EquipmentBonus GetSelectedBonus()
        {
            var b = new EquipmentBonus();
            if (
                _currentWeaponIndex < 0
                || _weaponStats == null
                || _currentWeaponIndex >= _weaponStats.Length
            )
            {
                return b;
            }

            var w = _weaponStats[_currentWeaponIndex];
            if (w == null)
            {
                return b;
            }

            b.attackPct += w.attack;
            b.defensePct += w.defense;
            b.maxHpPct += w.maxHP;
            b.criticalChance += w.criticalRate;
            b.criticalDamage += w.criticalDamage;
            return b;
        }

        // --- IWeaponSaveState(セーブ復元の境界。SaveService から呼ばれる) ---

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
            _owned.Sort();

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
            _animator = GetComponent<Animator>();
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
                int slots = _weapons != null ? Mathf.Min(_weapons.Length, WeaponKeys.Count) : 0;
                for (int i = 0; i < slots; i++)
                {
                    if (_weapons[i] != null)
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
            if (_input == null || _playerController == null)
            {
                return;
            }

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
        /// (未入手の武器は選べない = 押しても変化なし。Specification.md §5)。
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

            if (_weapons != null)
            {
                for (int i = 0; i < _weapons.Length; i++)
                {
                    if (_weapons[i] != null)
                    {
                        _weapons[i].SetActive(i == index);
                    }
                }
            }

            if (_animator != null && index >= 0)
            {
                // Rebindで全パラメータ・遷移・トリガーをリセットし、
                // 前の武器のアニメーション状態が新しい武器に漏れるのを防ぐ
                _animator.Rebind();

                // RebindによってWeaponTypeもリセットされるため、再設定が必要
                _animator.SetInteger("WeaponType", index);
            }
        }
    }
}
