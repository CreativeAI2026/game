using System;
using System.Collections.Generic;
using CreativeAI.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [SerializeField]
        private CanvasGroup _dialogCanvasGroup;

        [SerializeField]
        private CloseOnSelfClick _outsideClickCatcher;

        private RectTransform _dialogRect;
        private Action<int> _onConfirmed;
        private int _min = 1;
        private int _max = 1;
        private int _quantity = 1;
        private bool _acceptsInput;
        private bool _interactionEnabled = true;
        private bool _warnedMissingRequiredReferences;
        private int _openedFrame = -1;

        public bool IsOpen => _dialogRoot != null && _dialogRoot.activeInHierarchy;
        public event Action<int> QuantityChanged;
        public event Action Closed;

        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            _acceptsInput = enabled && IsOpen;

            if (_minButton != null)
                _minButton.interactable = enabled;
            if (_minusButton != null)
                _minusButton.interactable = enabled;
            if (_plusButton != null)
                _plusButton.interactable = enabled;
            if (_maxButton != null)
                _maxButton.interactable = enabled;
            if (_inputField != null)
                _inputField.interactable = enabled;

            RefreshQuantity(false);
        }

        private void Awake()
        {
            InitializeDerivedReferences();
            if (!HasRequiredReferences())
                return;

            Bind();
            BindOutsideClick();
        }

        private void OnEnable()
        {
            InitializeDerivedReferences();
            if (!HasRequiredReferences())
                return;

            _acceptsInput = IsOpen;
            BindOutsideClick();
        }

        private void OnDisable()
        {
            _acceptsInput = false;
            _outsideClickCatcher?.ClearClickAction(Hide);
        }

        private void OnDestroy()
        {
            _outsideClickCatcher?.ClearClickAction(Hide);
        }

        public bool Show(
            Sprite icon,
            string itemName,
            int min,
            int max,
            int initial,
            Action<int> onConfirmed
        )
        {
            if (!_interactionEnabled)
                return false;

            InitializeDerivedReferences();
            if (!HasRequiredReferences())
                return false;

            Bind();
            BindOutsideClick();

            _min = Mathf.Max(1, min);
            _max = Mathf.Max(_min, max);
            _onConfirmed = onConfirmed;
            _acceptsInput = true;
            _openedFrame = Time.frameCount;

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
            return true;
        }

        public void Hide()
        {
            bool wasOpen = IsOpen;
            _acceptsInput = false;
            InitializeDerivedReferences();
            if (!HasRequiredReferences())
                return;

            CraftQuantityDialogAnimation.PlayClose(
                _panelRoot,
                _dialogRoot,
                _dialogRect,
                _dialogCanvasGroup,
                _startScale,
                _animationDuration
            );
            if (wasOpen)
                Closed?.Invoke();
        }

        public void HideImmediate()
        {
            bool wasOpen = IsOpen;
            _acceptsInput = false;
            InitializeDerivedReferences();
            if (!HasRequiredReferences())
                return;

            CraftQuantityDialogAnimation.Kill(_dialogRect, _dialogCanvasGroup);
            CraftQuantityDialogUtility.HideImmediately(_panelRoot, _dialogRoot);
            if (wasOpen)
                Closed?.Invoke();
        }

        public void SetQuantity(int value)
        {
            if (!_interactionEnabled)
                return;

            SetQuantity(value, false);
        }

        public void Increment()
        {
            if (!_interactionEnabled)
                return;

            if (_quantity >= _max)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_quantity + 1, false);
        }

        public void Decrement()
        {
            if (!_interactionEnabled)
                return;

            if (_quantity <= _min)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_quantity - 1, false);
        }

        public void SetMin()
        {
            if (!_interactionEnabled)
                return;

            if (_quantity <= _min)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_min, true);
        }

        public void SetMax()
        {
            if (!_interactionEnabled)
                return;

            if (_quantity >= _max)
            {
                PlayLimitWarning();
                return;
            }

            SetQuantity(_max, true);
        }

        private void Update()
        {
            if (!_acceptsInput || !IsOpen || Time.frameCount <= _openedFrame)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                Hide();
            else if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
                Confirm();
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                Decrement();
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                Increment();
            else if (keyboard.upArrowKey.wasPressedThisFrame)
                SetMax();
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SetMin();
        }

        private void InitializeDerivedReferences()
        {
            _dialogRect = _dialogRoot != null ? _dialogRoot.transform as RectTransform : null;
            if (_inputField != null && _inputText != null)
                _inputField.textComponent = _inputText;
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Assign References")]
        private void AutoAssignReferences()
        {
            _panelRoot ??= FindPanelRootForEditor();
            _dialogRoot ??= FindDialogRootForEditor();

            var dialogTransform = _dialogRoot != null ? _dialogRoot.transform : transform;
            _itemImage ??=
                FindComponentInForEditor<Image>(dialogTransform, "Icon")
                ?? FindComponentInForEditor<Image>(dialogTransform, "ItemImage");
            _itemName ??=
                FindComponentInForEditor<TMP_Text>(dialogTransform, "Name")
                ?? FindComponentInForEditor<TMP_Text>(dialogTransform, "ItemName");
            _countLabel ??= FindComponentInForEditor<TMP_Text>(dialogTransform, "Counts");
            _countLabel ??= FindComponentInForEditor<TMP_Text>(dialogTransform, "CountLabel");
            _countLabel ??= FindComponentInForEditor<TMP_Text>(dialogTransform, "QuantityLabel");
            _inputField ??= FindComponentInForEditor<TMP_InputField>(dialogTransform, "InputField");
            _inputText ??= _inputField != null ? _inputField.textComponent : null;
            _minButton ??= FindButtonForEditor(dialogTransform, "MIN");
            _minusButton ??= FindButtonForEditor(dialogTransform, "-");
            _plusButton ??= FindButtonForEditor(dialogTransform, "+");
            _maxButton ??= FindButtonForEditor(dialogTransform, "MAX");
            _craftButton ??= FindButtonForEditor(dialogTransform, "CraftButton");
            _craftButtonText ??= _craftButton?.GetComponentInChildren<TMP_Text>(true);
            if (_dialogRoot != null)
                _dialogCanvasGroup ??= _dialogRoot.GetComponent<CanvasGroup>();
            if (_panelRoot != null)
                _outsideClickCatcher ??= _panelRoot.GetComponent<CloseOnSelfClick>();
        }

        private static T FindComponentInForEditor<T>(Transform root, string objectName)
            where T : Component
        {
            return root == null ? null : UIChildFinder.FindComponent<T>(root, objectName);
        }

        private static Button FindButtonForEditor(Transform root, string objectName)
        {
            return root == null ? null : UIChildFinder.FindButton(root, objectName);
        }

        private GameObject FindDialogRootForEditor()
        {
            if (gameObject.name == "CraftQuantityDialog")
                return gameObject;

            var dialog = UIChildFinder.Find(transform, "CraftQuantityDialog");
            return dialog != null ? dialog.gameObject : gameObject;
        }

        private GameObject FindPanelRootForEditor()
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name == "CQD-Panel")
                    return current.gameObject;
            }

            return null;
        }
#endif

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

        private void BindOutsideClick()
        {
            _outsideClickCatcher?.SetClickAction(Hide);
        }

        private void Confirm()
        {
            if (!_interactionEnabled)
                return;

            _onConfirmed?.Invoke(_quantity);
        }

        private void OnInputEndEdit(string value)
        {
            if (!_interactionEnabled)
                return;

            SetQuantity(int.TryParse(value, out int parsed) ? parsed : _min);
        }

        private void SetQuantity(int value, bool playBump)
        {
            int previousQuantity = _quantity;
            _quantity = Mathf.Clamp(value, _min, _max);
            RefreshQuantity(playBump);
            if (_quantity != previousQuantity)
                QuantityChanged?.Invoke(_quantity);
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
                _craftButton.interactable = _interactionEnabled && _max >= _min;

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
            var missingFields = new List<string>();
            AddMissingField(_panelRoot, nameof(_panelRoot), missingFields);
            AddMissingField(_dialogRoot, nameof(_dialogRoot), missingFields);
            AddMissingField(_dialogRect, nameof(_dialogRoot), missingFields);
            AddMissingField(_dialogCanvasGroup, nameof(_dialogCanvasGroup), missingFields);
            AddMissingField(_outsideClickCatcher, nameof(_outsideClickCatcher), missingFields);
            AddMissingField(_itemImage, nameof(_itemImage), missingFields);
            AddMissingField(_itemName, nameof(_itemName), missingFields);
            AddMissingField(_countLabel, nameof(_countLabel), missingFields);
            AddMissingField(_inputField, nameof(_inputField), missingFields);
            AddMissingField(_inputText, nameof(_inputText), missingFields);
            AddMissingField(_minButton, nameof(_minButton), missingFields);
            AddMissingField(_minusButton, nameof(_minusButton), missingFields);
            AddMissingField(_plusButton, nameof(_plusButton), missingFields);
            AddMissingField(_maxButton, nameof(_maxButton), missingFields);
            AddMissingField(_craftButton, nameof(_craftButton), missingFields);
            AddMissingField(_craftButtonText, nameof(_craftButtonText), missingFields);

            if (missingFields.Count == 0)
                return true;

            if (_warnedMissingRequiredReferences)
                return false;

            Debug.LogWarning(
                $"{nameof(CraftQuantityDialog)} on {name}: 必須参照が未設定です: {string.Join(", ", missingFields)}。Inspectorで設定してください。QuantityDialog処理を中止します。",
                this
            );
            _warnedMissingRequiredReferences = true;
            return false;
        }

        private static void AddMissingField(
            UnityEngine.Object reference,
            string fieldName,
            ICollection<string> missingFields
        )
        {
            if (reference != null || missingFields.Contains(fieldName))
                return;

            missingFields.Add(fieldName);
        }
    }
}
