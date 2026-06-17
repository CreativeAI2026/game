using UnityEngine;

namespace CreativeAI.Gameplay
{
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

        /// <summary>現在装備している武器のインデックス（0=剣, 1=弓 など）</summary>
        public int CurrentWeaponIndex => _currentWeaponIndex;

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

            // 攻撃や防御中など、武器切り替えが禁止されている場合は入力を無視する
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
                // アニメーターの現在の状態や進行中のトランジション、残存しているトリガーをすべて破棄し、
                // デフォルト状態（通常はIdle/Locomotion）に強制的にスナップさせる（最も堅牢な手法）
                _animator.Rebind();

                // RebindによってリセットされたWeaponTypeパラメータを再設定する
                _animator.SetInteger("WeaponType", _currentWeaponIndex);
            }
        }
    }
}
