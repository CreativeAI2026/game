using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.UI
{
    public class CharacterUIController : MonoBehaviour
    {
        [Header("Tabs"), SerializeField]
        private TabGroup _tabGroup;

        private readonly List<ICharacterTabView> _tabViews = new();
        private bool _initialized;
        private bool _resetOnNextEnable;

        private void Awake()
        {
            CollectTabViews();
            foreach (var view in _tabViews)
                view?.EnsureInitialized();

            _initialized = true;
        }

        private void OnEnable()
        {
            if (_tabGroup != null)
            {
                _tabGroup.OnSelectionChanged -= OnSelectionChanged;
                _tabGroup.OnSelectionChanged += OnSelectionChanged;

                if (_tabGroup.CurrentIndex >= 0)
                    OnSelectionChanged(
                        _tabGroup.CurrentIndex,
                        _tabGroup.CurrentDefinition,
                        _tabGroup.CurrentView
                    );
            }

            if (_initialized && _resetOnNextEnable)
                StartCoroutine(ResetAfterOpen());
        }

        private void OnDisable()
        {
            if (_tabGroup != null)
                _tabGroup.OnSelectionChanged -= OnSelectionChanged;

            if (_initialized)
                _resetOnNextEnable = true;
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
                if (!BelongsToSelectedView(view, selectedView))
                    view.OnExit();
            }

            foreach (var view in _tabViews)
            {
                if (BelongsToSelectedView(view, selectedView))
                    view.OnEnter();
            }
        }

        private static bool BelongsToSelectedView(
            ICharacterTabView view,
            GameObject selectedView
        ) =>
            view is Component component
            && selectedView != null
            && (
                component.transform == selectedView.transform
                || component.transform.IsChildOf(selectedView.transform)
            );

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
