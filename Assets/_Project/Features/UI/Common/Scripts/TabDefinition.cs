using UnityEngine;

namespace CreativeAI.UI
{
    [CreateAssetMenu(fileName = "TabDefinition", menuName = "CreativeAI/UI/Tab Definition")]
    public class TabDefinition : ScriptableObject
    {
        [SerializeField]
        private Sprite _icon;

        public Sprite Icon => _icon;
    }
}
