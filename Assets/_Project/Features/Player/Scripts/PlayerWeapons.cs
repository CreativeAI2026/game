using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーに武器を持たせる(実行時の見た目と動き)。セッション常駐の <see cref="WeaponManager"/> で選ばれている1本のモデルを出し、
    /// Animator をその武器用に差し替える。切り替えの入力は WeaponManager への切り替え要求にする。
    /// 武器ごとのモデルと Animator はリグの Prefab に配線するので、ここで持つ。補正の値などの定義は <see cref="WeaponCatalog"/> 側。
    ///
    /// 武器切り替え時に武器ごとの AnimatorController へ差し替えてから Animator.Rebind() で状態を完全リセットし、
    /// 前の武器のアニメーショントリガーやステートが残留するのを防ぐ。
    /// </summary>
    public sealed class PlayerWeapons : MonoBehaviour
    {
        /// <summary>武器1本分の設定。以前は3本の配列に分かれていて、並びを揃え損ねると別の武器の値が使われていた。</summary>
        [Serializable]
        private struct WeaponSlot
        {
            [Tooltip("武器のルートオブジェクト(Player/Weapons の下)。モデル未配線なら空欄")]
            public GameObject weapon;

            [Tooltip(
                "選択時に Animator へ差し替える Controller(ベース Controller または Animator Override Controller)。"
                    + "空欄なら Animator に最初から設定されている Controller を使う"
            )]
            public RuntimeAnimatorController animatorController;
        }

        [Header("武器の一覧(並び順 = WeaponManager.WeaponKeys: 0 剣 / 1 弓 / 2 鎌 / 3 ハンマー)")]
        [SerializeField]
        private WeaponSlot[] _slots = Array.Empty<WeaponSlot>();

        [Header("プロトタイプ用")]
        [Tooltip(
            "ON にすると開始時に全武器を入手ずみにする。戦闘検証シーン専用の抜け道で、"
                + "仕様(初期0本・イベントで入手)は OFF 側。製品フローでは OFF のままにすること。"
        )]
        [SerializeField]
        private bool _startWithAllWeapons;

        private WeaponManager _weapons;
        private PlayerInputHandler _input;
        private Animator _animator;
        private RuntimeAnimatorController _defaultAnimatorController;
        private PlayerController _playerController;
        private int _shownIndex = WeaponManager.NoWeapon;

        /// <summary>
        /// プレイヤーの切替入力で武器が切り替わった時だけ通知する。
        /// <see cref="WeaponManager.SelectionChanged"/> は入手・セーブ復元でも発火するため、
        /// 操作としての切替回数を数えたい側（PanicDetector の武器の空回し）はこちらを使う。
        /// </summary>
        public event Action WeaponSwitchedByInput;

        /// <summary>
        /// 選択中の武器のルートオブジェクト。1本も持っていない・未登録なら null。
        /// PlayerAnimationEventRelay が AnimationEvent の届け先を探すのに使う。
        /// </summary>
        public GameObject CurrentWeaponObject =>
            _shownIndex >= 0 && _shownIndex < _slots.Length ? _slots[_shownIndex].weapon : null;

        /// <summary>モデルを配線している武器の枠の数。</summary>
        public int WeaponCount => _slots.Length;

        /// <summary>index(WeaponManager.WeaponKeys の並び)の武器のルートオブジェクト。範囲外・未配線なら null。</summary>
        public GameObject GetWeaponObject(int index) =>
            index >= 0 && index < _slots.Length ? _slots[index].weapon : null;

        private void Awake()
        {
            // 武器の常駐は、プレイヤーを出す前に GameSession.EnsureResidents が作っておく。
            _weapons = WeaponManager.Instance;
            Assert.IsNotNull(
                _weapons,
                $"{name}: WeaponManager がありません。プレイヤーより先に常駐を作ってください"
            );
            TryGetComponent(out _input);
            // EditMode テストでは PlayerWeapons 単体で生成されモデル層が無いため、無ければ Animator 操作をしない
            PlayerModel model = GetComponentInChildren<PlayerModel>(true);
            if (model != null)
            {
                _animator = model.Animator;
                _defaultAnimatorController = _animator.runtimeAnimatorController;
            }
            TryGetComponent(out _playerController);
        }

        private void OnEnable()
        {
            _weapons.SelectionChanged += OnSelectionChanged;
            // EditMode テストでは PlayerController 無しで生成される
            if (_playerController != null)
                _playerController.Conditions.Locked += OnPlayerLocked;
        }

        private void OnDisable()
        {
            _weapons.SelectionChanged -= OnSelectionChanged;
            if (_playerController != null)
                _playerController.Conditions.Locked -= OnPlayerLocked;
        }

        private void Start()
        {
            if (_startWithAllWeapons)
            {
                // モデルが配線されている武器だけを持たせる(未配線の枠を選ぶと武器が消えて見えるため)。
                int slots = Mathf.Min(_slots.Length, WeaponManager.WeaponKeys.Count);
                for (int i = 0; i < slots; i++)
                {
                    if (_slots[i].weapon != null)
                        _weapons.Give(i);
                }
            }
            // 作り直されたプレイヤーにも、今の選択(0本ならどれも出さない)を反映する。
            ApplySelection(_weapons.CurrentWeaponIndex);
        }

        private void Update()
        {
            if (!_playerController.Conditions.CanChangeWeapon)
            {
                // 切替できない間の入力は捨てる。残すと、ロックが外れた瞬間に勝手に切り替わる
                _input.weaponNext = false;
                _input.weaponPrev = false;
                return;
            }

            if (_input.weaponNext)
            {
                _input.weaponNext = false;
                if (_weapons.SelectNext())
                    WeaponSwitchedByInput?.Invoke();
            }

            if (_input.weaponPrev)
            {
                _input.weaponPrev = false;
                if (_weapons.SelectPrevious())
                    WeaponSwitchedByInput?.Invoke();
            }
        }

        private void OnSelectionChanged(bool _) => ApplySelection(_weapons.CurrentWeaponIndex);

        /// <summary>
        /// 武器の行動の制限(怯み・掴まれ・死亡など)が掛かった瞬間に、装備中の武器の行動を打ち切って中立状態へ戻す。
        /// 制限を掛けた側の Animator 操作より先に終わるよう、次のフレームを待たずにその場で戻す。
        /// しまっている武器は OnDisable で後始末済みなので対象外。
        /// </summary>
        private void OnPlayerLocked(PlayerLock added)
        {
            if ((added & PlayerLock.Attack) == 0)
                return;

            GameObject weapon = CurrentWeaponObject;
            if (weapon != null && weapon.TryGetComponent(out IResettableWeapon resettable))
                resettable.ForceReset();
        }

        /// <summary>選択中の武器のモデルを出し、Animator を合わせる。index が負なら全部しまう。</summary>
        private void ApplySelection(int index)
        {
            _shownIndex = index;

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
        /// <summary>並び順の契約(WeaponManager.WeaponKeys)と枠の数が食い違っていたら、編集時に警告する。</summary>
        private void OnValidate()
        {
            var keys = WeaponManager.WeaponKeys;
            if (_slots.Length != keys.Count)
                Debug.LogWarning(
                    $"{name}: 武器の一覧は {keys.Count} 枠(並び順 {string.Join(" / ", keys)})にしてください",
                    this
                );
        }
#endif
    }
}
