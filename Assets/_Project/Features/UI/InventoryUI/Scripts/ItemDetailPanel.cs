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

        [SerializeField]
        private float _iconSpinDuration = 1f;

        private void Awake()
        {
            Clear();
        }

        public void Clear()
        {
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.color = Color.clear;
            }

            if (_name != null)
                _name.text = "";
            if (_category != null)
                _category.text = "";
            if (_stats != null)
                _stats.text = "";
            if (_passiveTitle != null)
                _passiveTitle.text = "";
            if (_passiveDesc != null)
                _passiveDesc.text = "";
        }

        public void Show(ItemData item)
        {
            DOTween.Kill(this);
            bool hasItem = item != null;

            if (_icon != null)
            {
                _icon.sprite = hasItem ? item.icon : null;
                _icon.color = hasItem ? Color.white : Color.clear;

                // 回転リセットしてから1回転
                _icon.rectTransform.localRotation = Quaternion.identity;
                DOTween
                    .To(
                        () => 0f,
                        x => _icon.rectTransform.localRotation = Quaternion.Euler(0, x, 0),
                        360f,
                        _iconSpinDuration
                    )
                    .SetEase(Ease.OutQuint)
                    .SetTarget(this);
            }

            TypeText(_name, hasItem ? item.itemName : "（未装備）");
            TypeText(_category, hasItem ? item.category.ToDisplayName() : "");
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
                .SetEase(Ease.Linear)
                .SetTarget(this); // SetTarget追加
        }
    }
}
