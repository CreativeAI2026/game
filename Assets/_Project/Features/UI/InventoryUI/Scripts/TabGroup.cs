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
        private int _currentIndex = -1;
        private bool _initialized;
        public int CurrentIndex => _currentIndex;

        public event Action<int> OnTabSelected;

        private void Start()
        {
            string selectionGroup = $"tab-group-{_nextSelectionGroupId++}";

            foreach (var entry in _tabEntries)
            {
                if (!entry.enabled)
                    continue;

                var btn = Instantiate(_tabButtonPrefab, transform, false);
                btn.SetSelectionGroup(selectionGroup);
                btn.Setup(entry.icon, entry.label);
                int captured = _buttons.Count;
                btn.Button.onClick.AddListener(() => SelectTab(captured));
                _buttons.Add(btn);
            }

            _initialized = true;
            SelectTab(0);
        }

        private void OnEnable()
        {
            if (_initialized)
                RestoreCurrentSelection();
        }

        public void SelectTab(int index)
        {
            if (index == _currentIndex)
            {
                RestoreCurrentSelection();
                return;
            }
            _currentIndex = index;

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool isActive = i == index;
                _buttons[i].SetActive(isActive, _animDuration);

                if (_tabEntries[i].view != null)
                    _tabEntries[i].view.SetActive(isActive);
            }

            OnTabSelected?.Invoke(index);
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
            if (!_initialized || _currentIndex < 0)
                return;

            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].SetActive(i == _currentIndex, _animDuration);
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
    }
}
