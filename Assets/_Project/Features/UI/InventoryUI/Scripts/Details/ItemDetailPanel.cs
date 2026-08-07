using CreativeAI.Gameplay;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public partial class ItemDetailPanel : MonoBehaviour
    {
        private const string DefaultEmptyLabel = "\uFF08\u672A\u88C5\u5099\uFF09";

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _category;

        [SerializeField]
        private TMP_Text _stats;

        [SerializeField, FormerlySerializedAs("_passiveDesc")]
        private TMP_Text _description;

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
            KillTweens();
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
            SetTextImmediately(_description, string.Empty);
        }

        public void Show(ItemData item)
        {
            Show(item, DefaultEmptyLabel);
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

            if (!forceTextRefresh && IsSameDisplay(item, emptyLabel))
            {
                if (hasItem)
                    PlayIconSpin();
                return;
            }

            KillTweens();
            _displayedItem = item;
            _hasDisplayedContent = true;
            _displayedEmptyLabel = item == null ? emptyLabel : null;

            RefreshIcon(item);
            RefreshTexts(item, emptyLabel);
        }

        private bool IsSameDisplay(ItemData item, string emptyLabel)
        {
            bool hasItem = item != null;
            bool sameItem = hasItem && item == _displayedItem;
            bool sameEmptyState =
                !hasItem
                && _hasDisplayedContent
                && _displayedItem == null
                && _displayedEmptyLabel == emptyLabel;

            return sameItem || sameEmptyState;
        }

        private void RefreshIcon(ItemData item)
        {
            if (_icon == null)
                return;

            bool hasIcon = item?.icon != null;
            _icon.sprite = hasIcon ? item.icon : null;
            _icon.color = hasIcon ? Color.white : Color.clear;

            if (hasIcon)
                PlayIconReveal();
            else
                _icon.rectTransform.localRotation = Quaternion.identity;
        }

        private void RefreshTexts(ItemData item, string emptyLabel)
        {
            bool hasItem = item != null;
            SetTextImmediately(_name, hasItem ? item.itemName : emptyLabel);
            SetTextImmediately(
                _category,
                hasItem ? $"[{item.category.ToDisplayName()}]" : string.Empty
            );
            TypeText(_stats, hasItem ? ItemStatTextFormatter.BuildStatsText(item) : string.Empty);
            TypeText(_description, hasItem ? RemoveLineBreaks(item.description) : string.Empty);
            RebuildLayout();
        }

        private static string RemoveLineBreaks(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\r", "").Replace("\n", "");

        private void RebuildLayout()
        {
            if (transform is not RectTransform rectTransform)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private void KillTweens()
        {
            DOTween.Kill(this);
            DOTween.Kill(_name);
            DOTween.Kill(_category);
            DOTween.Kill(_stats);
            DOTween.Kill(_description);
            _icon?.rectTransform.DOKill();
        }
    }
}
