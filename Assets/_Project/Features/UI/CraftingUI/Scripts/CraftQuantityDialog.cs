using System;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.UI.CraftingUI
{
    public class CraftQuantityDialog : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panelRoot;

        [SerializeField]
        private GameObject _dialogRoot;

        [SerializeField]
        private Image _itemImage;

        [SerializeField]
        private TMP_Text _itemName;

        [SerializeField]
        private TMP_Text _countLabel;

        [SerializeField]
        private TMP_InputField _inputField;

        [SerializeField]
        private TMP_Text _inputText;

        [SerializeField]
        private Button _minButton;

        [SerializeField]
        private Button _minusButton;

        [SerializeField]
        private Button _plusButton;

        [SerializeField]
        private Button _maxButton;

        [SerializeField]
        private Button _craftButton;

        [SerializeField]
        private TMP_Text _craftButtonText;

        [SerializeField]
        private float _animationDuration = 0.22f;

        [SerializeField]
        private float _startScale = 0.82f;

        private RectTransform _dialogRect;
        private CanvasGroup _dialogCanvasGroup;
        private CloseOnSelfClick _panelCloseOnSelfClick;
        private Action<int> _onConfirmed;
        private int _min = 1;
        private int _max = 1;
        private int _quantity = 1;
        private bool _warnedMissingRequiredReferences;
        private bool _warnedMissingCloseOnSelfClick;
        private bool _warnedMissingDialogCanvasGroup;

        public bool IsOpen => _dialogRoot != null && _dialogRoot.activeInHierarchy;

        private void Awake()
        {
            ResolveReferences();
            Bind();
        }

        public void Show(
            Sprite icon,
            string itemName,
            int min,
            int max,
            int initial,
            Action<int> onConfirmed
        )
        {
            ResolveReferences();
            Bind();

            _min = Mathf.Max(1, min);
            _max = Mathf.Max(_min, max);
            _onConfirmed = onConfirmed;

            if (!HasRequiredReferences())
                return;

            RefreshItem(icon, itemName);
            SetQuantity(initial);

            CraftQuantityDialogAnimation.PlayOpen(
                _panelRoot,
                _dialogRoot,
                _dialogRect,
                _dialogCanvasGroup,
                _startScale,
                _animationDuration
            );
        }

        public void Hide()
        {
            ResolveReferences();

            if (_dialogRoot == null)
                return;

            CraftQuantityDialogAnimation.PlayClose(
                _panelRoot,
                _dialogRoot,
                _dialogRect,
                _dialogCanvasGroup,
                _startScale,
                _animationDuration
            );
        }

        public void HideImmediate()
        {
            ResolveReferences();
            CraftQuantityDialogAnimation.Kill(_dialogRect, _dialogCanvasGroup);
            CraftQuantityDialogUtility.HideImmediately(_panelRoot, _dialogRoot);
        }

        public void SetQuantity(int value)
        {
            SetQuantity(value, false);
        }

        public void Increment()
        {
            if (_quantity >= _max)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_quantity + 1, false);
        }

        public void Decrement()
        {
            if (_quantity <= _min)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_quantity - 1, false);
        }

        public void SetMin()
        {
            if (_quantity <= _min)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_min, true);
        }

        public void SetMax()
        {
            if (_quantity >= _max)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_max, true);
        }

        private void ResolveReferences()
        {
            _panelRoot ??= gameObject;
            _dialogRoot ??= FindDialogRoot();

            var dialogTransform = _dialogRoot != null ? _dialogRoot.transform : transform;
            _itemImage ??=
                FindComponentIn<Image>(dialogTransform, "Icon")
                ?? FindComponentIn<Image>(dialogTransform, "ItemImage");
            _itemName ??=
                FindComponentIn<TMP_Text>(dialogTransform, "Name")
                ?? FindComponentIn<TMP_Text>(dialogTransform, "ItemName");
            _countLabel ??= FindComponentIn<TMP_Text>(dialogTransform, "Counts");
            _countLabel ??= FindComponentIn<TMP_Text>(dialogTransform, "CountLabel");
            _countLabel ??= FindComponentIn<TMP_Text>(dialogTransform, "QuantityLabel");
            _inputField ??= FindComponentIn<TMP_InputField>(dialogTransform, "InputField");
            if (_inputField != null)
            {
                if (_inputText != null)
                    _inputField.textComponent = _inputText;
                else
                    _inputText = _inputField.textComponent;
            }

            _minButton ??= FindButton(dialogTransform, "MIN");
            _minusButton ??= FindButton(dialogTransform, "-");
            _plusButton ??= FindButton(dialogTransform, "+");
            _maxButton ??= FindButton(dialogTransform, "MAX");
            _craftButton ??= FindButton(dialogTransform, "CraftButton");
            if (_craftButton != null)
                _craftButtonText ??= _craftButton.GetComponentInChildren<TMP_Text>(true);

            if (_dialogRoot != null)
            {
                _dialogRect ??= _dialogRoot.GetComponent<RectTransform>();
                _dialogCanvasGroup ??= _dialogRoot.GetComponent<CanvasGroup>();
                if (_dialogCanvasGroup == null)
                    WarnMissingDialogCanvasGroupOnce();
            }

            if (_panelRoot != null)
                _panelCloseOnSelfClick ??= _panelRoot.GetComponent<CloseOnSelfClick>();

            if (_panelCloseOnSelfClick != null)
                _panelCloseOnSelfClick.SetClickAction(Hide);
            else if (_panelRoot != null)
                WarnMissingCloseOnSelfClickOnce();
        }

        private void Bind()
        {
            CraftQuantityDialogUtility.BindButton(_minButton, SetMin);
            CraftQuantityDialogUtility.BindButton(_minusButton, Decrement);
            CraftQuantityDialogUtility.BindButton(_plusButton, Increment);
            CraftQuantityDialogUtility.BindButton(_maxButton, SetMax);
            CraftQuantityDialogUtility.BindButton(_craftButton, Confirm);

            UIButtonHoverScaleUtility.ApplyTo(_minButton);
            UIButtonHoverScaleUtility.ApplyTo(_minusButton);
            UIButtonHoverScaleUtility.ApplyTo(_plusButton);
            UIButtonHoverScaleUtility.ApplyTo(_maxButton);
            UIButtonHoverScaleUtility.ApplyTo(_craftButton);

            if (_inputField == null)
                return;

            _inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            _inputField.onEndEdit.RemoveListener(OnInputEndEdit);
            _inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        private void Confirm()
        {
            _onConfirmed?.Invoke(_quantity);
        }

        private void OnInputEndEdit(string value)
        {
            SetQuantity(int.TryParse(value, out int parsed) ? parsed : _min);
        }

        private void SetQuantity(int value, bool playBump)
        {
            _quantity = Mathf.Clamp(value, _min, _max);
            RefreshQuantity(playBump);
        }

        private void RefreshItem(Sprite icon, string itemName)
        {
            if (_itemImage != null)
            {
                _itemImage.sprite = icon;
                _itemImage.gameObject.SetActive(icon != null);
            }

            if (_itemName != null)
                _itemName.text = itemName ?? string.Empty;
        }

        private void RefreshQuantity(bool playBump)
        {
            if (_countLabel != null)
                _countLabel.text = $"\u4f5c\u6210\u6570\uff08\u6700\u5927 {_max}\uff09";

            if (_inputField != null)
                _inputField.SetTextWithoutNotify(_quantity.ToString());

            if (_craftButton != null)
                _craftButton.interactable = _max >= _min;

            if (playBump)
                PlayQuantityBump();
        }

        private void PlayLimitWarning()
        {
            CraftUIAnimationUtility.PlayTextLimitWarning(_countLabel);
            CraftUIAnimationUtility.PlayBump(_inputField?.transform as RectTransform);
        }

        private void PlayQuantityBump()
        {
            CraftUIAnimationUtility.PlayBump(_countLabel?.rectTransform);
            CraftUIAnimationUtility.PlayBump(_inputField?.transform as RectTransform);
        }

        private bool HasRequiredReferences()
        {
            bool hasRequiredReferences =
                _panelRoot != null
                && _dialogRoot != null
                && _dialogRect != null
                && _dialogCanvasGroup != null
                && _panelCloseOnSelfClick != null
                && _itemImage != null
                && _itemName != null
                && _countLabel != null
                && _inputField != null
                && _minButton != null
                && _minusButton != null
                && _plusButton != null
                && _maxButton != null
                && _craftButton != null;

            if (hasRequiredReferences || _warnedMissingRequiredReferences)
                return hasRequiredReferences;

            Debug.LogWarning(
                $"{nameof(CraftQuantityDialog)} on {name}: 必要なUI参照が不足しています。Inspector参照またはPrefab上の名前を確認してください。",
                this
            );
            _warnedMissingRequiredReferences = true;
            return false;
        }

        private void WarnMissingCloseOnSelfClickOnce()
        {
            if (_warnedMissingCloseOnSelfClick)
                return;

            Debug.LogWarning(
                $"{nameof(CraftQuantityDialog)} on {name}: {_panelRoot.name} に {nameof(CloseOnSelfClick)} が見つかりません。CQD-Panelに追加して外側クリックで閉じる対象を設定してください。",
                this
            );
            _warnedMissingCloseOnSelfClick = true;
        }

        private void WarnMissingDialogCanvasGroupOnce()
        {
            if (_warnedMissingDialogCanvasGroup)
                return;

            Debug.LogWarning(
                $"{nameof(CraftQuantityDialog)} on {name}: {_dialogRoot.name} に {nameof(CanvasGroup)} がありません。QuantityDialogの表示を中止します。Unity上で追加してください。",
                this
            );
            _warnedMissingDialogCanvasGroup = true;
        }

        private static T FindComponentIn<T>(Transform root, string objectName)
            where T : Component
        {
            return root == null ? null : UIChildFinder.FindComponent<T>(root, objectName);
        }

        private static Button FindButton(Transform root, string objectName)
        {
            return root == null ? null : UIChildFinder.FindButton(root, objectName);
        }

        private GameObject FindDialogRoot()
        {
            if (gameObject.name == "CraftQuantityDialog")
                return gameObject;

            var dialog = UIChildFinder.Find(transform, "CraftQuantityDialog");
            if (dialog != null)
                return dialog.gameObject;

            return gameObject;
        }
    }
}
