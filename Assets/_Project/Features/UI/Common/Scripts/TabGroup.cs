using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class TabGroup : MonoBehaviour
    {
        private static int _nextSelectionGroupId;

        [Serializable]
        public struct TabEntry
        {
            public TabDefinition definition;
            public GameObject view;
        }

        [SerializeField]
        private TabButton _tabButtonPrefab;

        [SerializeField]
        private List<TabEntry> _tabEntries;

        [SerializeField]
        private float _animDuration = 0.2f;

        [SerializeField]
        /*  */private bool _autoSelectFirstTabOnStart = true;

        [SerializeField]
        private bool _resetToFirstTabOnEnable = true;

        [SerializeField]
        private bool _dimInactiveTabs = true;

        [SerializeField]
        private bool _allowSelectedBounce = true;

        private List<TabButton> _buttons = new();
        private readonly List<int> _buttonToEntryIndices = new();
        private int _currentIndex = -1;
        private bool _initialized;
        public int CurrentIndex => _currentIndex;
        public int EntryCount => _tabEntries?.Count ?? 0;

        public event Action<int> OnTabSelected;
        public event Action<int, TabDefinition> OnTabDefinitionSelected;

        private void Start()
        {
            string selectionGroup = $"tab-group-{_nextSelectionGroupId++}";

            _buttons.Clear();
            _buttonToEntryIndices.Clear();

            if (_tabEntries == null)
                _tabEntries = new List<TabEntry>();

            if (_tabButtonPrefab == null)
            {
                _initialized = true;
                if (!_autoSelectFirstTabOnStart)
                    ApplyNoSelection();
                return;
            }

            for (int entryIndex = 0; entryIndex < _tabEntries.Count; entryIndex++)
            {
                var entry = _tabEntries[entryIndex];
                var btn = Instantiate(_tabButtonPrefab, transform, false);
                if (btn == null || btn.Button == null)
                    continue;

                btn.SetSelectionGroup(selectionGroup);
                btn.Bind(entry.definition);
                int captured = _buttons.Count;
                btn.Button.onClick.AddListener(() => SelectTab(captured));
                _buttons.Add(btn);
                _buttonToEntryIndices.Add(entryIndex);
            }

            _initialized = true;
            if (_autoSelectFirstTabOnStart)
                SelectTab(0);
            else
                ApplyNoSelection();
        }

        private void OnEnable()
        {
            if (!_initialized)
                return;

            if (_resetToFirstTabOnEnable)
            {
                ResetToFirstTab();
                return;
            }

            if (HasValidCurrentIndex())
                RestoreCurrentSelection();
            else if (_autoSelectFirstTabOnStart)
                SelectTab(0);
            else
                ApplyNoSelection();
        }

        public void SelectTab(int index)
        {
            if (index < 0 || index >= _buttons.Count || index >= _buttonToEntryIndices.Count)
                return;

            if (index == _currentIndex)
            {
                RestoreCurrentSelection();
                return;
            }

            _currentIndex = index;
            ApplySelection(index);

            OnTabSelected?.Invoke(index);
            OnTabDefinitionSelected?.Invoke(index, GetDefinitionForButtonIndex(index));
        }

        private void ApplySelection(int buttonIndex)
        {
            int entryIndex = _buttonToEntryIndices[buttonIndex];

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i]
                        .SetActive(
                            i == buttonIndex,
                            _animDuration,
                            _dimInactiveTabs,
                            _allowSelectedBounce
                        );
            }

            for (int i = 0; i < _tabEntries.Count; i++)
            {
                if (_tabEntries[i].view != null)
                    _tabEntries[i].view.SetActive(i == entryIndex);
            }
        }

        public void ResetToFirstTab()
        {
            if (!_initialized)
                return;

            if (_buttons.Count == 0)
            {
                ApplyNoSelection();
                return;
            }

            _currentIndex = -1;
            SelectTab(0);
        }

        public void RestoreCurrentSelection()
        {
            if (
                !_initialized
                || _currentIndex < 0
                || _currentIndex >= _buttons.Count
                || _currentIndex >= _buttonToEntryIndices.Count
            )
                return;

            ApplySelection(_currentIndex);
        }

        private bool HasValidCurrentIndex() =>
            _currentIndex >= 0
            && _currentIndex < _buttons.Count
            && _currentIndex < _buttonToEntryIndices.Count;

        private void ApplyNoSelection()
        {
            _currentIndex = -1;

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i]
                        .SetActive(false, _animDuration, _dimInactiveTabs, _allowSelectedBounce);
            }

            if (_tabEntries == null)
                return;

            for (int i = 0; i < _tabEntries.Count; i++)
            {
                if (_tabEntries[i].view != null)
                    _tabEntries[i].view.SetActive(false);
            }
        }

        public TabDefinition GetDefinitionForEntry(int entryIndex)
        {
            if (_tabEntries == null || entryIndex < 0 || entryIndex >= _tabEntries.Count)
                return null;

            return _tabEntries[entryIndex].definition;
        }

        public TabDefinition GetDefinitionForButtonIndex(int buttonIndex)
        {
            if (buttonIndex < 0 || _tabEntries == null)
                return null;

            if (buttonIndex < _buttonToEntryIndices.Count)
                return GetDefinitionForEntry(_buttonToEntryIndices[buttonIndex]);

            return GetDefinitionForEntry(buttonIndex);
        }

        public GameObject GetView(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= _tabEntries.Count)
                return null;
            return _tabEntries[entryIndex].view;
        }
    }
}
