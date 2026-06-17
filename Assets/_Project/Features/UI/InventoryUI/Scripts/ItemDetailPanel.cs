using CreativeAI.Gameplay;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class ItemDetailPanel : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Text _name;

        [SerializeField]
        private Text _category;

        [SerializeField]
        private Text _stats;

        [SerializeField]
        private Text _passiveTitle;

        [SerializeField]
        private Text _passiveDesc;

        [SerializeField]
        private float _typingDuration = 0.5f;

        public void Show(ItemData item)
        {
            bool hasItem = item != null;

            if (_icon != null)
            {
                _icon.sprite = hasItem ? item.icon : null;
                _icon.color = hasItem ? Color.white : Color.clear;
            }

            TypeText(_name, hasItem ? item.itemName : "（未装備）");
            TypeText(_category, hasItem ? item.category : "");
            TypeText(_stats, hasItem ? item.effect : "");
            TypeText(_passiveTitle, hasItem ? item.effect : "");
            TypeText(_passiveDesc, hasItem ? item.description : "");
        }

        private void TypeText(Text target, string text)
        {
            if (target == null)
                return;
            target.text = "";
            int totalChars = text.Length;
            DOTween
                .To(
                    () => 0f,
                    x => target.text = text.Substring(0, Mathf.RoundToInt(x)),
                    (float)totalChars,
                    _typingDuration
                )
                .SetEase(Ease.Linear);
        }
    }
}
