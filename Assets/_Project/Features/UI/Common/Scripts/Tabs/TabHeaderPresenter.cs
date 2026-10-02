using TMPro;
using UnityEngine;

namespace CreativeAI.UI
{
    public sealed class TabHeaderPresenter : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _titleText;

        [SerializeField]
        private TMP_Text _englishTitleText;

        [SerializeField]
        private TabGroup _tabGroup;

        private void OnEnable()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected += OnTabDefinitionSelected;

            Show(_tabGroup != null ? _tabGroup.CurrentDefinition : null);
        }

        private void OnDisable()
        {
            if (_tabGroup != null)
                _tabGroup.OnTabDefinitionSelected -= OnTabDefinitionSelected;
        }

        private void OnTabDefinitionSelected(int _buttonIndex, TabDefinition definition) =>
            Show(definition);

        private void Show(TabDefinition definition)
        {
            if (_titleText != null)
                _titleText.text = definition != null ? definition.Label : string.Empty;

            if (_englishTitleText != null)
                _englishTitleText.text =
                    definition != null ? definition.EnglishLabel : string.Empty;
        }
    }
}
