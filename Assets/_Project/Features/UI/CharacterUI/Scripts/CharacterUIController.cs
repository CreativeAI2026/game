using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI.CharacterUI
{
    public partial class CharacterUIController : MonoBehaviour
    {
        [Header("Tabs"), SerializeField]
        private TabGroup _tabGroup;

        private readonly List<ICharacterTabView> _tabViews = new();
        private bool _initialized;
        private bool _resetOnNextEnable;

        private void Start()
        {
            CollectTabViews();
            foreach (var view in _tabViews)
                view?.EnsureInitialized();

            if (_tabGroup != null)
                _tabGroup.OnSelectionChanged += OnSelectionChanged;

            _initialized = true;
        }

        private void OnEnable()
        {
            if (_initialized && _resetOnNextEnable)
                StartCoroutine(ResetAfterOpen());
        }

        private void OnDisable()
        {
            if (_initialized)
                _resetOnNextEnable = true;
        }

        private void OnDestroy()
        {
            if (_tabGroup != null)
                _tabGroup.OnSelectionChanged -= OnSelectionChanged;
        }

        private IEnumerator ResetAfterOpen()
        {
            yield return null;

            _resetOnNextEnable = false;
            _tabGroup?.ResetToFirstTab();

            foreach (var view in _tabViews)
                view?.ResetViewState();
        }

        private void OnSelectionChanged(
            int _index,
            TabDefinition _definition,
            GameObject selectedView
        )
        {
            foreach (var view in _tabViews)
            {
                if (view is not Component component)
                    continue;

                bool belongsToSelectedView =
                    selectedView != null
                    && (
                        component.transform == selectedView.transform
                        || component.transform.IsChildOf(selectedView.transform)
                    );
                if (!belongsToSelectedView)
                    view.OnExit();
                else
                    view.OnEnter();
            }
        }

        private void CollectTabViews()
        {
            _tabViews.Clear();
            if (_tabGroup == null)
                return;

            for (int i = 0; i < _tabGroup.EntryCount; i++)
            {
                var view = _tabGroup.GetView(i);
                if (view == null)
                    continue;

                foreach (var tabView in view.GetComponentsInChildren<ICharacterTabView>(true))
                {
                    if (tabView != null && !_tabViews.Contains(tabView))
                        _tabViews.Add(tabView);
                }
            }
        }
    }
}
