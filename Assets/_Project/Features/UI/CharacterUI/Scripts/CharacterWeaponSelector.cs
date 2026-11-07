using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private Image _weaponPreview;

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

        public event Action<WeaponData> SelectionChanged;
        public WeaponData SelectedWeapon => GetWeapon(CurrentIndex)?.Data;
        private WeaponManager _ownership;

        public void SetOwnedWeapons(WeaponManager manager)
        {
            _ownership = manager;
            foreach (var entry in _weapons)
                if (entry?.Button != null)
                    entry.Button.gameObject.SetActive(IsOwned(entry.Data));
            if (!IsOwned(SelectedWeapon))
                CurrentIndex = _weapons.FindIndex(entry => entry != null && IsOwned(entry.Data));
            if (CurrentIndex >= 0)
                ApplySelection();
            else
                SelectionChanged?.Invoke(null);
        }

        private bool IsOwned(WeaponData data) =>
            data != null && (_ownership == null || _ownership.IsOwned(data.id - 1001));

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

        private void Update()
        {
            if (_weapons.Count <= 1)
                return;

            if (!UiInputActionMap.TryReadDiscreteMove(out Vector2Int step))
                return;

            int direction = step.x != 0 ? step.x : -step.y;
            if (direction != 0)
                SelectRelative(direction > 0 ? 1 : -1);
        }

        private void SelectRelative(int step)
        {
            if (_weapons.Count == 0)
                return;

            int start = IsValidIndex(CurrentIndex) ? CurrentIndex : 0;
            for (int i = 1; i <= _weapons.Count; i++)
            {
                int candidate = (start + step * i + _weapons.Count) % _weapons.Count;
                if (IsOwned(_weapons[candidate]?.Data))
                {
                    Select(candidate);
                    return;
                }
            }
        }

        public void Select(int index)
        {
            if (!IsValidIndex(index) || !IsOwned(_weapons[index]?.Data))
                return;

            CurrentIndex = index;
            ApplySelection();
        }

        private void Initialize()
        {
            if (_initialized || _weapons.Count == 0)
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
                {
                    if (weapon.Button != null)
                        weapon.Button.SetSelected(weapon == selectedWeapon);
                    Transform iconTransform =
                        weapon.Button != null ? weapon.Button.transform.Find("WeaponIcon") : null;
                    var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                    if (icon != null)
                        icon.color =
                            weapon == selectedWeapon ? Color.white : new Color(.62f, .65f, .7f, 1);
                    var label =
                        weapon.Button != null
                            ? weapon.Button.GetComponentInChildren<TMP_Text>(true)
                            : null;
                    if (label != null)
                        label.color =
                            weapon == selectedWeapon
                                ? UiVisualTheme.Gold
                                : UiVisualTheme.Muted * new Color(.7f, .7f, .7f, 1);
                }
            }

            if (selectedWeapon == null)
                return;

            WeaponData data = selectedWeapon.Data;
            if (data == null)
                return;

            SelectionChanged?.Invoke(data);

            if (_weaponPreview != null)
            {
                _weaponPreview.sprite = data.icon;
                _weaponPreview.gameObject.SetActive(data.icon != null);
            }

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
            if (_weaponName == null)
                _weaponName = FindComponent<TMP_Text>(transform, "ViewArea/Name");
            if (_weaponDescription == null)
                _weaponDescription = FindComponent<TMP_Text>(transform, "ViewArea/Description");
            if (_attackValue == null)
                _attackValue = FindStatusValue("ATK");
            if (_criticalRateValue == null)
                _criticalRateValue = FindStatusValue("CRTR");
            if (_criticalDamageValue == null)
                _criticalDamageValue = FindStatusValue("CRTD");
        }

        private TMP_Text FindStatusValue(string rowName) =>
            FindComponent<TMP_Text>(transform, $"WeaponStatusArea/{rowName}/TextArea/ValueText");

        private static T FindComponent<T>(Transform root, string path)
            where T : Component
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private CharacterWeaponSelection GetWeapon(int index) =>
            IsValidIndex(index) ? _weapons[index] : null;

        private bool IsValidIndex(int index) => index >= 0 && index < _weapons.Count;
    }
}
