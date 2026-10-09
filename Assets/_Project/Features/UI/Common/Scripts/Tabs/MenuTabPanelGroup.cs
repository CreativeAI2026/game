using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    [Serializable]
    public sealed class MenuTabPanelBinding
    {
        [SerializeField]
        private MenuTabButton _button;

        [SerializeField]
        private GameObject _panel;

        public MenuTabButton Button => _button;
        public GameObject Panel => _panel;

        public MenuTabPanelBinding(MenuTabButton button, GameObject panel)
        {
            _button = button;
            _panel = panel;
        }
    }

    /// <summary>
    /// 配置済みのメニュータブと表示パネルを一対一で管理する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuTabPanelGroup : MonoBehaviour
    {
        [SerializeField]
        private List<MenuTabPanelBinding> _tabs = new();

        [SerializeField, Min(0)]
        private int _initialIndex;

        [SerializeField]
        private bool _resetOnEnable = true;

        private bool _initialized;

        public int CurrentIndex { get; private set; } = -1;
        public MenuTabButton CurrentButton => GetBinding(CurrentIndex)?.Button;
        public GameObject CurrentPanel => GetBinding(CurrentIndex)?.Panel;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            if (_tabs.Count == 0)
                return;

            if (_resetOnEnable || !IsValidIndex(CurrentIndex))
                Select(Mathf.Clamp(_initialIndex, 0, _tabs.Count - 1));
            else
                ApplySelection();
        }

        private void OnDestroy()
        {
            foreach (MenuTabPanelBinding tab in _tabs)
            {
                if (tab?.Button != null)
                    tab.Button.Clicked -= OnButtonClicked;
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

            foreach (MenuTabPanelBinding tab in _tabs)
            {
                if (tab?.Button != null)
                    tab.Button.Clicked += OnButtonClicked;
            }

            _initialized = true;
        }

        private void OnButtonClicked(MenuTabButton button)
        {
            int index = _tabs.FindIndex(tab => tab?.Button == button);
            Select(index);
        }

        private void ApplySelection()
        {
            MenuTabPanelBinding selectedTab = GetBinding(CurrentIndex);
            foreach (MenuTabPanelBinding tab in _tabs)
            {
                if (tab == null)
                    continue;

                bool selected = tab == selectedTab;
                tab.Button?.SetSelected(selected);
                if (tab.Panel != null)
                    tab.Panel.SetActive(selected);
            }
        }

        private MenuTabPanelBinding GetBinding(int index) =>
            IsValidIndex(index) ? _tabs[index] : null;

        private bool IsValidIndex(int index) => index >= 0 && index < _tabs.Count;
    }
}
