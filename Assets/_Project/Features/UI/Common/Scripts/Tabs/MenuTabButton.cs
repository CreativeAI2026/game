using System;
using DG.Tweening;
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

        [Header("Underline Animation")]
        [SerializeField, Min(0f)]
        private float _underlineShowDuration = 0.26f;

        [SerializeField, Min(0f)]
        private float _underlineHideDuration = 0.12f;

        private RectTransform _selectedUnderlineTransform;
        private Vector3 _selectedUnderlineScale = Vector3.one;
        private bool _selectionInitialized;

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

            KillUnderlineTween();
            ApplyUnderlineImmediately(IsSelected);
        }

        public void SetSelected(bool selected)
        {
            ResolveReferences();

            if (_selectionInitialized && IsSelected == selected)
                return;

            IsSelected = selected;

            KillUnderlineTween();

            if (!_selectionInitialized)
            {
                _selectionInitialized = true;
                if (!selected)
                {
                    ApplyUnderlineImmediately(false);
                    return;
                }
            }

            if (selected)
                ShowUnderline();
            else
                HideUnderline();
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

            if (_selectedUnderlineTransform == null && _selectedUnderline != null)
            {
                _selectedUnderlineTransform = _selectedUnderline.transform as RectTransform;
                if (_selectedUnderlineTransform != null)
                    _selectedUnderlineScale = _selectedUnderlineTransform.localScale;
            }

            if (_label == null)
                _label = GetComponentInChildren<TMP_Text>(true);
        }

        private void ShowUnderline()
        {
            if (_selectedUnderlineTransform == null)
                return;

            _selectedUnderlineTransform.localScale = new Vector3(
                0f,
                _selectedUnderlineScale.y,
                _selectedUnderlineScale.z
            );
            _selectedUnderline.SetActive(true);

            if (_underlineShowDuration <= 0f)
            {
                _selectedUnderlineTransform.localScale = _selectedUnderlineScale;
                return;
            }

            _selectedUnderlineTransform
                .DOScaleX(_selectedUnderlineScale.x, _underlineShowDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        private void HideUnderline()
        {
            if (_selectedUnderlineTransform == null || !_selectedUnderline.activeSelf)
            {
                ApplyUnderlineImmediately(false);
                return;
            }

            if (_underlineHideDuration <= 0f)
            {
                ApplyUnderlineImmediately(false);
                return;
            }

            _selectedUnderlineTransform
                .DOScaleX(0f, _underlineHideDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (!IsSelected)
                        ApplyUnderlineImmediately(false);
                });
        }

        private void ApplyUnderlineImmediately(bool visible)
        {
            if (_selectedUnderlineTransform == null || _selectedUnderline == null)
                return;

            _selectedUnderlineTransform.localScale = visible
                ? _selectedUnderlineScale
                : new Vector3(0f, _selectedUnderlineScale.y, _selectedUnderlineScale.z);
            _selectedUnderline.SetActive(visible);
        }

        private void KillUnderlineTween()
        {
            _selectedUnderlineTransform?.DOKill();
        }
    }
}
