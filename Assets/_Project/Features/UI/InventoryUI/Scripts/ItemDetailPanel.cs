using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public class ItemDetailPanel : MonoBehaviour
    {
        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _category;

        [SerializeField]
        private TMP_Text _stats;

        [SerializeField]
        private TMP_Text _passiveTitle;

        [SerializeField]
        private TMP_Text _passiveDesc;

        [SerializeField]
        private float _iconSpinDuration = 1f;

        [SerializeField, Min(1f)]
        private float _charactersPerSecond = 24f;

        private ItemData _displayedItem;
        private bool _hasDisplayedContent;
        private string _displayedEmptyLabel;
        private FontStyles _defaultNameFontStyle;
        private bool _hasDefaultNameFontStyle;

        private void Awake()
        {
            ResolveReferences();
            Clear();
        }

        public void Clear()
        {
            ResolveReferences();
            DOTween.Kill(this);
            _icon?.rectTransform.DOKill();
            _displayedItem = null;
            _hasDisplayedContent = false;
            _displayedEmptyLabel = null;

            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.color = Color.clear;
                _icon.rectTransform.localRotation = Quaternion.identity;
            }

            ApplyNameUnderline(false);
            SetTextImmediately(_name, string.Empty);
            SetTextImmediately(_category, string.Empty);
            SetTextImmediately(_stats, string.Empty);
            SetTextImmediately(_passiveTitle, string.Empty);
            SetTextImmediately(_passiveDesc, string.Empty);
        }

        public void Show(ItemData item)
        {
            Show(item, "（未装備）");
        }

        public void Show(ItemData item, string emptyLabel)
        {
            Show(item, emptyLabel, false);
        }

        public void Show(ItemData item, string emptyLabel, bool forceTextRefresh)
        {
            ResolveReferences();
            bool hasItem = item != null;
            ApplyNameUnderline(hasItem);

            bool sameItem = hasItem && item == _displayedItem;
            bool sameEmptyState =
                !hasItem
                && _hasDisplayedContent
                && _displayedItem == null
                && _displayedEmptyLabel == emptyLabel;

            if (!forceTextRefresh && (sameItem || sameEmptyState))
            {
                if (sameItem)
                    PlayIconSpin();
                return;
            }

            DOTween.Kill(this);
            _icon?.rectTransform.DOKill();
            _displayedItem = item;
            _hasDisplayedContent = true;
            _displayedEmptyLabel = item == null ? emptyLabel : null;

            if (_icon != null)
            {
                _icon.sprite = hasItem ? item.icon : null;
                _icon.color = hasItem && item.icon != null ? Color.white : Color.clear;
                _icon.rectTransform.localRotation = Quaternion.identity;

                if (hasItem && item.icon != null)
                    PlayIconSpin();
            }

            TypeText(_name, hasItem ? item.itemName : emptyLabel);
            TypeText(_category, hasItem ? item.category.ToDisplayName() : string.Empty);
            TypeText(_stats, hasItem ? item.effect : string.Empty);
            TypeText(_passiveTitle, hasItem ? item.effect : string.Empty);
            TypeText(_passiveDesc, hasItem ? item.description : string.Empty);
        }

        private void TypeText(TMP_Text target, string text)
        {
            if (target == null)
                return;

            text ??= string.Empty;
            target.text = text;
            target.ForceMeshUpdate();

            int characterCount = target.textInfo.characterCount;
            if (characterCount <= 0)
            {
                target.maxVisibleCharacters = 0;
                return;
            }

            target.maxVisibleCharacters = 0;
            float duration = characterCount / Mathf.Max(1f, _charactersPerSecond);

            DOTween
                .To(
                    () => 0f,
                    value =>
                        target.maxVisibleCharacters = Mathf.Clamp(
                            Mathf.FloorToInt(value),
                            0,
                            characterCount
                        ),
                    characterCount,
                    duration
                )
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() => target.maxVisibleCharacters = characterCount);
        }

        private void PlayIconSpin()
        {
            if (_icon == null || _icon.sprite == null)
                return;

            var iconRect = _icon.rectTransform;
            iconRect.DOKill();
            iconRect.localRotation = Quaternion.identity;

            iconRect
                .DORotate(new Vector3(0f, 360f, 0f), _iconSpinDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuint)
                .SetUpdate(true);
        }

        private void ResolveReferences()
        {
            _icon ??= FindComponent<Image>("Icon");
            _name ??= FindComponent<TMP_Text>("Name");
            _category ??= FindComponent<TMP_Text>("Category");
            _stats ??= FindComponent<TMP_Text>("Stats");
            _passiveTitle ??= FindComponent<TMP_Text>("PassiveTitle");
            _passiveDesc ??= FindComponent<TMP_Text>("PassiveDesc");
            CaptureDefaultNameFontStyle();
        }

        private void CaptureDefaultNameFontStyle()
        {
            if (_hasDefaultNameFontStyle || _name == null)
                return;

            _defaultNameFontStyle = _name.fontStyle;
            _hasDefaultNameFontStyle = true;
        }

        private void ApplyNameUnderline(bool hasItem)
        {
            if (_name == null)
                return;

            CaptureDefaultNameFontStyle();
            _name.fontStyle = hasItem
                ? _defaultNameFontStyle
                : _defaultNameFontStyle & ~FontStyles.Underline;
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.name == objectName && child.TryGetComponent(out T component))
                    return component;

            return null;
        }

        private static void SetTextImmediately(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text;
                target.maxVisibleCharacters = int.MaxValue;
            }
        }
    }
}
