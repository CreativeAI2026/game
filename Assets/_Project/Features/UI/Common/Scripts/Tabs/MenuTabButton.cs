using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuTabButton : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private GameObject _selectedUnderline;

        [SerializeField]
        private TMP_Text _label;

        public Button Button
        {
            get
            {
                ResolveReferences();
                return _button;
            }
        }

        public bool IsSelected { get; private set; }

        public event Action<MenuTabButton> Clicked;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (_button != null)
            {
                _button.onClick.RemoveListener(NotifyClicked);
                _button.onClick.AddListener(NotifyClicked);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
                _button.onClick.RemoveListener(NotifyClicked);
        }

        public void SetSelected(bool selected)
        {
            ResolveReferences();
            IsSelected = selected;

            if (_selectedUnderline != null)
                _selectedUnderline.SetActive(selected);
        }

        private void NotifyClicked()
        {
            Clicked?.Invoke(this);
        }

        private void ResolveReferences()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_selectedUnderline == null)
            {
                Transform underline = transform.Find("Underline") ?? transform.Find("UnderBar");
                if (underline != null)
                    _selectedUnderline = underline.gameObject;
            }

            if (_label == null)
                _label = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
