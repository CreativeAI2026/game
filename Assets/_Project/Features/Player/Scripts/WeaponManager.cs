using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの武器切り替えを管理する。
    /// 武器切り替え時にAnimator.Rebind()で状態を完全リセットすることで、
    /// 前の武器のアニメーショントリガーやステートが残留するのを防ぐ。
    /// </summary>
    public class WeaponManager : MonoBehaviour, IWeaponSaveState
    {
        [Header("武器リスト(0:剣, 1:弓 など)")]
        [Tooltip("子オブジェクトにある各武器のルートオブジェクトを登録します")]
        [SerializeField]
        private GameObject[] _weapons;

        [Header("武器ごとのステータス補正(_weapons と同じ index 順で登録)")]
        [Tooltip(
            "選択中の1本の補正のみが最終ステータスに乗る(Specification.md「アイテムカテゴリと付与ステータス」)"
        )]
        [SerializeField]
        private WeaponData[] _weaponStats;

        private int _currentWeaponIndex = 0;
        private PlayerInputHandler _input;
        private Animator _animator;
        private PlayerController _playerController;

        public event Action<bool> OnWeaponSwitched; // true: prev (left rotation), false: next (right rotation)

        public int CurrentWeaponIndex => _currentWeaponIndex;

        // --- セーブ復元(IWeaponSaveState): 選択武器を保存/復元する(spec §6) ---

        public int CaptureSelectedWeaponIndex() => _currentWeaponIndex;

        public void RestoreSelectedWeaponIndex(int index)
        {
            if (_weapons == null || index < 0 || index >= _weapons.Length)
                return;
            EquipWeapon(index);
            // PlayerStatus が購読して武器補正を再計算する(bool は HUD 回転向きの区別用。復元は次向き扱い)。
            OnWeaponSwitched?.Invoke(false);
        }

        /// <summary>
        /// 選択中の武器の補正を装備品と同じ <see cref="EquipmentBonus"/> 形式で返す。
        /// PlayerStatus が「装備補正 + 武器補正」として最終ステータスに合算する
        /// (装備品:InventoryManager と対称。選択の情報源はここ 1 箇所)。
        /// spec: 選択中の 1 本の補正のみが乗る。移動速度/攻撃速度は PlayerStatus の対象外なので含めない。
        /// </summary>
        public EquipmentBonus GetSelectedBonus()
        {
            var b = new EquipmentBonus();
            if (
                _weaponStats == null
                || _currentWeaponIndex < 0
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

            b.attack += w.attack;
            b.defense += w.defense;
            b.maxHp += w.maxHP;
            b.criticalChance += w.criticalRate;
            b.criticalDamage += w.criticalDamage;
            return b;
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _animator = GetComponent<Animator>();
            _playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            EquipWeapon(_currentWeaponIndex);
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
                int nextIndex = (_currentWeaponIndex + 1) % _weapons.Length;
                EquipWeapon(nextIndex);
                OnWeaponSwitched?.Invoke(false);
            }

            if (_input.weaponPrev)
            {
                _input.weaponPrev = false;
                int prevIndex = _currentWeaponIndex - 1;
                if (prevIndex < 0)
                    prevIndex = _weapons.Length - 1;
                EquipWeapon(prevIndex);
                OnWeaponSwitched?.Invoke(true);
            }
        }

        private void EquipWeapon(int index)
        {
            _currentWeaponIndex = index;

            for (int i = 0; i < _weapons.Length; i++)
            {
                if (_weapons[i] != null)
                {
                    _weapons[i].SetActive(i == index);
                }
            }

            if (_animator != null)
            {
                // Rebindで全パラメータ・遷移・トリガーをリセットし、
                // 前の武器のアニメーション状態が新しい武器に漏れるのを防ぐ
                _animator.Rebind();

                // RebindによってWeaponTypeもリセットされるため、再設定が必要
                _animator.SetInteger("WeaponType", _currentWeaponIndex);
            }
        }
    }
}
