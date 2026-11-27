using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セッション常駐の武器の持ち物。どの武器を入手したかと、選んでいる1本を持つ。
    /// 武器の定義(補正の値・アイコン)は <see cref="WeaponCatalog"/>、プレイヤーに持たせる見た目は <see cref="PlayerWeapons"/> が受け持つ。
    ///
    /// 仕様:
    /// - 主人公は最初1本も持たない。イベントの giveWeapon(<see cref="IWeaponGiver"/>)で入手する。
    /// - 切り替えられるのは<b>入手ずみの武器だけ</b>。未入手はスキップされる(押しても変化なし)。
    /// - 最初の1本を入手したら、それを選ぶ。
    /// </summary>
    public sealed class WeaponManager : MonoBehaviour, IWeaponGiver
    {
        /// <summary>武器が1本も選ばれていない状態の index。</summary>
        public const int NoWeapon = -1;

        /// <summary>
        /// events.json の weaponKey(documents/ScenarioCatalog.md「武器カタログ」)と武器の index の対応。
        /// 並び順がセーブ・events.json・<see cref="PlayerWeapons"/> の武器の一覧との契約。
        /// </summary>
        public static readonly IReadOnlyList<string> WeaponKeys = new[]
        {
            "sword",
            "bow",
            "scythe",
            "hammer",
        };

        /// <summary>
        /// 切り替えの順番(E = 次の武器 で進む向き)。Q はこの逆順。
        /// WeaponKeys の番号はセーブ・events.json との契約なので並べ替えず、切り替え順はここで別に持つ。
        /// 武器切替 HUD の輪の並びもこの順(選択中を上に置き、反時計回りに次の武器)。
        /// </summary>
        private static readonly string[] SwitchOrderKeys = { "sword", "scythe", "hammer", "bow" };

        /// <summary>セッション常駐のインスタンス。セッション開始前は null。</summary>
        public static WeaponManager Instance { get; private set; }

        /// <summary>選んでいる武器が変わったとき。true: 前へ(左回転) / false: 次へ(右回転)。入手・復元で選び直したときも false。</summary>
        public event Action<bool> SelectionChanged;

        /// <summary>入手ずみの武器が変わったとき。武器切替 UI が 0本→非表示 / 1本以上→表示 に使う。</summary>
        public event Action OwnedChanged;

        // 入手ずみ武器の index。切り替え順(SwitchOrderKeys = 剣→鎌→ハンマー→弓)に並べておく。
        private readonly List<int> _owned = new();

        /// <summary>選択中の武器 index。1本も持っていなければ <see cref="NoWeapon"/>。</summary>
        public int CurrentWeaponIndex { get; private set; } = NoWeapon;

        /// <summary>選んでいる武器の定義。1本も持っていない・定義が無ければ null。</summary>
        public WeaponData SelectedWeapon =>
            WeaponCatalog.Instance != null ? WeaponCatalog.Instance.Get(CurrentWeaponIndex) : null;

        /// <summary>選んでいる武器の補正。最終ステータスに乗るのは選択中の1本だけで、0本なら 0。</summary>
        public EquipmentBonus GetSelectedBonus()
        {
            var weapon = SelectedWeapon;
            return weapon != null ? weapon.Bonus : default;
        }

        /// <summary>入手ずみの本数。</summary>
        public int OwnedCount => _owned.Count;

        /// <summary>入手ずみ武器の index。切り替え順(E で進む順)に並んでいる。</summary>
        public IReadOnlyList<int> OwnedIndices => _owned;

        /// <summary>セッション常駐の武器の持ち物を1つだけ生成する(既存ならそれを返す)。</summary>
        public static WeaponManager EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(WeaponManager)).AddComponent<WeaponManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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

        /// <summary>
        /// IWeaponGiver。weaponKey(WeaponKeys のいずれか)の武器を1本入手する。既に所持なら何もしない。
        /// 最初の1本なら選択する。
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
            Give(index);
        }

        /// <summary>index の武器を1本入手する。範囲外・既に所持なら何もしない。最初の1本なら選択する。</summary>
        public void Give(int index)
        {
            if (index < 0 || index >= WeaponKeys.Count || _owned.Contains(index))
                return;

            _owned.Add(index);
            SortOwnedBySwitchOrder();

            bool isFirst = _owned.Count == 1;
            if (isFirst)
                CurrentWeaponIndex = index;

            OwnedChanged?.Invoke();
            if (isFirst)
                SelectionChanged?.Invoke(false);
        }

        /// <summary>持ったまま、どの武器も選んでいない状態にする(素手)。</summary>
        public void Unequip()
        {
            CurrentWeaponIndex = NoWeapon;
            SelectionChanged?.Invoke(false);
        }

        /// <summary>入手ずみ武器の並びで次の1本へ。切り替わったら true。</summary>
        public bool SelectNext() => TryStep(+1, false);

        /// <summary>入手ずみ武器の並びで前の1本へ。切り替わったら true。</summary>
        public bool SelectPrevious() => TryStep(-1, true);

        // --- セーブ(SaveService・LoadService から呼ばれる) ---

        /// <summary>保存用に入手ずみ武器のキーを返す(index ではなくキーで持つ = 並び替えに強い)。</summary>
        public IReadOnlyList<string> CaptureOwnedWeaponKeys()
        {
            var keys = new List<string>(_owned.Count);
            foreach (int i in _owned)
                keys.Add(WeaponKeys[i]);
            return keys;
        }

        /// <summary>
        /// 入手ずみ武器と選択中の武器をまとめて差し替える(ロード・はじめから)。選択が未所持・範囲外なら所持の先頭に寄せる。
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

            CurrentWeaponIndex = _owned.Contains(selectedWeaponIndex)
                ? selectedWeaponIndex
                : (_owned.Count > 0 ? _owned[0] : NoWeapon);

            OwnedChanged?.Invoke();
            SelectionChanged?.Invoke(false);
        }

        /// <summary>1本も持たない状態に戻す(はじめから)。</summary>
        public void Clear() => RestoreWeapons(Array.Empty<string>(), NoWeapon);

        /// <summary>
        /// 入手ずみ武器の並びの中で1つ進む/戻る。0本・1本のときは変化しない
        /// (未入手の武器は選べない = 押しても変化なし)。
        /// </summary>
        private bool TryStep(int direction, bool towardPrevious)
        {
            if (_owned.Count <= 1)
                return false;

            int position = _owned.IndexOf(CurrentWeaponIndex);
            if (position < 0)
                position = 0;

            int next = (position + direction + _owned.Count) % _owned.Count;
            if (_owned[next] == CurrentWeaponIndex)
                return false;

            CurrentWeaponIndex = _owned[next];
            SelectionChanged?.Invoke(towardPrevious);
            return true;
        }

        private void SortOwnedBySwitchOrder() =>
            _owned.Sort((a, b) => SwitchOrderOf(a).CompareTo(SwitchOrderOf(b)));

        private static int SwitchOrderOf(int weaponIndex)
        {
            int order = Array.IndexOf(SwitchOrderKeys, WeaponKeys[weaponIndex]);
            // 切り替え順に載っていない武器は末尾(番号順)に回す
            return order >= 0 ? order : SwitchOrderKeys.Length + weaponIndex;
        }
    }
}
