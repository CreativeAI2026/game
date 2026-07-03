using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class RecipeMaterialRow : MonoBehaviour
    {
        private Image _icon;
        private TMP_Text _text;

        private void Awake()
        {
            FindReferences();
        }

        public void Show(ItemData material)
        {
            FindReferences();
            if (material == null)
            {
                if (_icon != null)
                {
                    _icon.sprite = null;
                    _icon.gameObject.SetActive(false);
                }
                if (_text != null)
                    _text.text = string.Empty;

                return;
            }

            if (_icon != null)
            {
                _icon.sprite = material.icon;
                _icon.color = Color.white;
                _icon.gameObject.SetActive(material.icon != null);
            }

            if (_text != null)
            {
                const int required = 1;
                int owned = InventoryManager.Instance?.GetItemCount(material) ?? 0;
                _text.text = $"{material.itemName}  {owned} / {required}";
                _text.color = owned >= required ? Color.white : new Color(0.9f, 0.25f, 0.25f);
            }
        }

        private void FindReferences()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (_icon == null && (child.name == "Icon" || child.name == "Image"))
                    child.TryGetComponent(out _icon);

                if (_text == null && child.name == "Text")
                    child.TryGetComponent(out _text);
            }
        }
    }
}
