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
            public Sprite icon;
            public string label;
            public GameObject view;
            public bool enabled;
        }

        [SerializeField]
        private TabButton _tabButtonPrefab;

        [SerializeField]
        private List<TabEntry> _tabEntries;

        [SerializeField]
        private float _animDuration = 0.2f;

        private List<TabButton> _buttons = new();
        private readonly List<int> _buttonToEntryIndices = new();
        private int _currentIndex = -1;
        private bool _initialized;
        public int CurrentIndex => _currentIndex;
        public int EntryCount => _tabEntries?.Count ?? 0;

        public event Action<int> OnTabSelected;

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
                return;
            }

            for (int entryIndex = 0; entryIndex < _tabEntries.Count; entryIndex++)
            {
                var entry = _tabEntries[entryIndex];
                if (!entry.enabled)
                    continue;

                var btn = Instantiate(_tabButtonPrefab, transform, false);
                if (btn == null || btn.Button == null)
                    continue;

                btn.SetSelectionGroup(selectionGroup);
                btn.Setup(entry.icon, entry.label);
                int captured = _buttons.Count;
                btn.Button.onClick.AddListener(() => SelectTab(captured));
                _buttons.Add(btn);
                _buttonToEntryIndices.Add(entryIndex);
            }

            _initialized = true;
            SelectTab(0);
        }

        private void OnEnable()
        {
            if (_initialized)
                ResetToFirstTab();
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

            // Keep the existing public contract: subscribers receive the enabled button index.
            OnTabSelected?.Invoke(index);
        }

        private void ApplySelection(int buttonIndex)
        {
            int entryIndex = _buttonToEntryIndices[buttonIndex];

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].SetActive(i == buttonIndex, _animDuration);
            }

            for (int i = 0; i < _tabEntries.Count; i++)
            {
                if (_tabEntries[i].view != null)
                    _tabEntries[i].view.SetActive(i == entryIndex);
            }
        }

        public void ResetToFirstTab()
        {
            if (!_initialized || _buttons.Count == 0)
                return;

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

        public bool IsEnabled(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= _tabEntries.Count)
                return false;
            return _tabEntries[entryIndex].enabled;
        }

        public GameObject GetView(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= _tabEntries.Count)
                return null;
            return _tabEntries[entryIndex].view;
        }

        public int FindEntryIndexByView(GameObject view)
        {
            if (view == null)
                return -1;

            for (int i = 0; i < _tabEntries.Count; i++)
            {
                if (_tabEntries[i].view == view)
                    return i;
            }

            return -1;
        }

        public int AddTabEntry(Sprite icon, string label, GameObject view, bool enabled = true)
        {
            _tabEntries ??= new List<TabEntry>();
            _tabEntries.Add(
                new TabEntry
                {
                    icon = icon,
                    label = label,
                    view = view,
                    enabled = enabled,
                }
            );

            return _tabEntries.Count - 1;
        }
    }
}
