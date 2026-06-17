using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    public class TabHighlight : MonoBehaviour
    {
        private Text _label;

        [SerializeField]
        private float _activeScale = 1.2f;

        [SerializeField]
        private float _duration = 0.2f;

        private static readonly Color ActiveColor = new Color(0.55f, 0.75f, 1f, 1f);
        private static readonly Color InactiveColor = new Color(0.75f, 0.75f, 0.8f, 1f);

        private RectTransform _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _label = GetComponentInChildren<Text>();
        }

        public void SetActive(bool isActive)
        {
            if (_label != null)
                _label.color = isActive ? ActiveColor : InactiveColor;

            float targetScale = isActive ? _activeScale : 1f;
            _rect.DOScale(Vector3.one * targetScale, _duration).SetEase(Ease.OutQuad);
        }
    }
}
