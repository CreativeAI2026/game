using UnityEngine;

namespace CreativeAI.UI
{
    public sealed class TabPanelSwitcher : MonoBehaviour
    {
        [SerializeField]
        private TabGroup _tabGroup;

        public TabGroup TabGroup => _tabGroup;

        private void OnEnable()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected += OnTabDefinitionSelected;

            ShowOnly(_tabGroup != null ? _tabGroup.CurrentView : null);
        }

        private void OnDisable()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected -= OnTabDefinitionSelected;
        }

        private void OnTabDefinitionSelected(int _buttonIndex, TabDefinition _definition) =>
            ShowOnly(_tabGroup.CurrentView);

        private void ShowOnly(GameObject selectedView)
        {
            if (_tabGroup == null)
                return;

            for (int entryIndex = 0; entryIndex < _tabGroup.EntryCount; entryIndex++)
            {
                GameObject view = _tabGroup.GetView(entryIndex);
                if (view != null)
                    view.SetActive(view == selectedView);
            }
        }
    }
}
