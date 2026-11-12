using UnityEngine;

namespace CreativeAI.UI
{
    [CreateAssetMenu(fileName = "TabDefinition", menuName = "CreativeAI/UI/Tab Definition")]
    public class TabDefinition : ScriptableObject
    {
        [SerializeField]
        private string _label;

        [SerializeField]
        private string _englishLabel;

        [SerializeField]
        private Sprite _icon;

        public string Label => _label;
        public string EnglishLabel => _englishLabel;
        public Sprite Icon => _icon;
    }
}
