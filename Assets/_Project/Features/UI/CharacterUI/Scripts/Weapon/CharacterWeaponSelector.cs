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

        [Header("文字アニメーション")]
        [SerializeField]
        private bool _animateText = true;

        [SerializeField, Min(0f)]
        private float _textStartDelay = 0.05f;

        [SerializeField, Min(0.001f)]
        private float _characterInterval = 0.035f;

        [SerializeField, Min(0f)]
        private float _sectionDelay = 0.06f;

        private bool _initialized;
        private TmpTypewriterPlayer _typewriter;

        public int CurrentIndex { get; private set; } = -1;

        private void Awake()
        {
            _typewriter = new TmpTypewriterPlayer(this);
            ResolveReferences();
            Initialize();
        }

        private void OnEnable()
        {
            _typewriter ??= new TmpTypewriterPlayer(this);
            Initialize();
            if (_weapons.Count == 0)
                return;

            if (_resetOnEnable || !IsValidIndex(CurrentIndex))
                Select(Mathf.Clamp(_initialIndex, 0, _weapons.Count - 1));
            else
                ApplySelection();
        }

        private void OnDisable()
        {
            _typewriter?.CompleteAll();
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

            PlayText(_weaponName, data.itemName, 0);
            PlayText(_weaponDescription, data.description, 1);
            SetModifier(_attackValue, data.attack, 2);
            SetModifier(_criticalRateValue, data.criticalRate, 3);
            SetModifier(_criticalDamageValue, data.criticalDamage, 4);
        }

        private void SetModifier(TMP_Text text, float value, int sectionIndex)
        {
            string sign = value >= 0f ? "+" : string.Empty;
            PlayText(text, $"{sign}{value:0.##}%", sectionIndex);
        }

        private void PlayText(TMP_Text text, string value, int sectionIndex)
        {
            _typewriter ??= new TmpTypewriterPlayer(this);
            _typewriter.Play(
                text,
                value,
                _textStartDelay + _sectionDelay * sectionIndex,
                _characterInterval,
                _animateText
            );
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
