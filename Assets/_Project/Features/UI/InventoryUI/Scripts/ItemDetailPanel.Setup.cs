using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    public partial class ItemDetailPanel
    {
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
            if (target == null)
                return;

            target.text = text;
            target.maxVisibleCharacters = int.MaxValue;
        }
    }
}
