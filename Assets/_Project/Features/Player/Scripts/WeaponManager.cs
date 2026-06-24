using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの武器切り替えを管理する。
    /// 武器切り替え時にAnimator.Rebind()で状態を完全リセットすることで、
    /// 前の武器のアニメーショントリガーやステートが残留するのを防ぐ。
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        [Header("武器リスト(0:剣, 1:弓 など)")]
        [Tooltip("子オブジェクトにある各武器のルートオブジェクトを登録します")]
        [SerializeField]
        private GameObject[] _weapons;

        private int _currentWeaponIndex = 0;
        private PlayerInputHandler _input;
        private Animator _animator;
        private PlayerController _playerController;

        //private WeaponHUDController _weaponHUDController;

        public int CurrentWeaponIndex => _currentWeaponIndex;

        private void Awake()
        {
            _input = GetComponent<PlayerInputHandler>();
            _animator = GetComponent<Animator>();
            _playerController = GetComponent<PlayerController>();
            //_weaponHUDController = GetComponent<WeaponHUDController>();
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
            }

            if (_input.weaponPrev)
            {
                _input.weaponPrev = false;
                int prevIndex = _currentWeaponIndex - 1;
                if (prevIndex < 0)
                    prevIndex = _weapons.Length - 1;
                EquipWeapon(prevIndex);
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
