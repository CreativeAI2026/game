using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    [Serializable]
    public sealed class CharacterWeaponSelection
    {
        [SerializeField]
        private MenuTabButton _button;

        [SerializeField]
        private WeaponData _data;

        public MenuTabButton Button => _button;
        public WeaponData Data => _data;

        public CharacterWeaponSelection(MenuTabButton button, WeaponData data)
        {
            _button = button;
            _data = data;
        }
    }

    /// <summary>
    /// キャラクター画面の武器ボタン、選択状態、武器詳細表示をまとめて管理する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterWeaponSelector : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _weaponName;

        [SerializeField]
        private TMP_Text _weaponDescription;

        [SerializeField]
        private TMP_Text _attackValue;

        [SerializeField]
        private TMP_Text _criticalRateValue;

        [SerializeField]
        private TMP_Text _criticalDamageValue;

        [SerializeField]
        private List<CharacterWeaponSelection> _weapons = new();

        [SerializeField, Min(0)]
        private int _initialIndex;

        [SerializeField]
        private bool _resetOnEnable = true;

        private bool _initialized;

        public int CurrentIndex { get; private set; } = -1;

        private void Awake()
        {
            ResolveReferences();
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            if (_weapons.Count == 0)
                return;

            if (_resetOnEnable || !IsValidIndex(CurrentIndex))
                Select(Mathf.Clamp(_initialIndex, 0, _weapons.Count - 1));
            else
                ApplySelection();
        }

        private void OnDestroy()
        {
            foreach (CharacterWeaponSelection weapon in _weapons)
            {
                if (weapon?.Button != null)
                    weapon.Button.Clicked -= OnButtonClicked;
            }
        }

        public void Select(int index)
        {
            if (!IsValidIndex(index))
                return;

            CurrentIndex = index;
            ApplySelection();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            foreach (CharacterWeaponSelection weapon in _weapons)
            {
                if (weapon?.Button != null)
                    weapon.Button.Clicked += OnButtonClicked;
            }

            _initialized = true;
        }

        private void OnButtonClicked(MenuTabButton button)
        {
            int index = _weapons.FindIndex(weapon => weapon?.Button == button);
            Select(index);
        }

        private void ApplySelection()
        {
            CharacterWeaponSelection selectedWeapon = GetWeapon(CurrentIndex);
            foreach (CharacterWeaponSelection weapon in _weapons)
            {
                if (weapon != null)
                    weapon.Button?.SetSelected(weapon == selectedWeapon);
            }

            if (selectedWeapon == null)
                return;

            WeaponData data = selectedWeapon.Data;
            if (data == null)
                return;

            if (_weaponName != null)
                _weaponName.text = data.itemName;
            if (_weaponDescription != null)
                _weaponDescription.text = data.description;
            SetModifier(_attackValue, data.attack);
            SetModifier(_criticalRateValue, data.criticalRate);
            SetModifier(_criticalDamageValue, data.criticalDamage);
        }

        private static void SetModifier(TMP_Text text, float value)
        {
            if (text == null)
                return;

            string sign = value >= 0f ? "+" : string.Empty;
            text.text = $"{sign}{value:0.##}%";
        }

        private void ResolveReferences()
        {
            _weaponName ??= transform.Find("ViewArea/Name")?.GetComponent<TMP_Text>();
            _weaponDescription ??= transform.Find("ViewArea/Description")?.GetComponent<TMP_Text>();
            _attackValue ??= FindStatusValue("ATK");
            _criticalRateValue ??= FindStatusValue("CRTR");
            _criticalDamageValue ??= FindStatusValue("CRTD");
        }

        private TMP_Text FindStatusValue(string rowName) =>
            transform
                .Find($"WeaponStatusArea/{rowName}/TextArea/ValueText")
                ?.GetComponent<TMP_Text>();

        private CharacterWeaponSelection GetWeapon(int index) =>
            IsValidIndex(index) ? _weapons[index] : null;

        private bool IsValidIndex(int index) => index >= 0 && index < _weapons.Count;
    }
}
