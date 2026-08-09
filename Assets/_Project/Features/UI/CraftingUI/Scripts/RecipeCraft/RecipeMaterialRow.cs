using CreativeAI.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class RecipeMaterialRow : MonoBehaviour
    {
        [SerializeField]
        private Color _sufficientColor = new Color32(67, 73, 91, 255);

        [SerializeField]
        private Color _insufficientColor = new Color32(180, 55, 55, 255);

        private Image _icon;
        private TMP_Text _text;

        private void Awake()
        {
            FindReferences();
        }

        public void Show(RecipeCraftMaterialRowData data)
        {
            FindReferences();
            var material = data.Item;
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
                _text.text = $"{material.itemName}  {data.AvailableCount} / {data.RequiredCount}";
                _text.color = data.IsSufficient ? _sufficientColor : _insufficientColor;
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
