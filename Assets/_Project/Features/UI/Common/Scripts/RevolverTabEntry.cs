using System;
using UnityEngine;

namespace CreativeAI.UI
{
    [Serializable]
    public sealed class RevolverTabEntry
    {
        [SerializeField]
        private TabDefinition _definition;

        [SerializeField]
        private GameObject _view;

        public TabDefinition Definition => _definition;
        public GameObject View => _view;

        public RevolverTabEntry(TabDefinition definition, GameObject view = null)
        {
            _definition = definition;
            _view = view;
        }
    }
}
