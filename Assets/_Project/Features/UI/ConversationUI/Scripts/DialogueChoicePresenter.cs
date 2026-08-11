using System;
using System.Collections;
using System.Collections.Generic;
using CreativeAI.Core.EventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UIEventTrigger = UnityEngine.EventSystems.EventTrigger;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>選択肢の生成、配置、フォーカス、表示演出と後片付けを担当する。</summary>
    internal sealed class DialogueChoicePresenter
    {
        private readonly RectTransform _container;
        private readonly Button _template;
        private readonly float _containerWidth;
        private readonly float _buttonHeight;
        private readonly float _spacing;
        private readonly float _bottomMargin;
        private readonly float _staggerDelay;
        private readonly float _enterDuration;
        private readonly float _confirmDuration;
        private readonly List<GameObject> _spawned = new();
        private readonly List<float> _spawnedHeights = new();
        private readonly Dictionary<Button, Color> _baseColors = new();
        private int _selectedIndex = -1;

        private const float FocusScale = 1.055f;
        private static readonly Color FocusTint = new(0.82f, 0.92f, 1f, 1f);

        public DialogueChoicePresenter(
            RectTransform container,
            Button template,
            float containerWidth,
            float buttonHeight,
            float spacing,
            float bottomMargin,
            float staggerDelay,
            float enterDuration,
            float confirmDuration
        )
        {
            _container = container;
            _template = template;
            _containerWidth = containerWidth;
            _buttonHeight = buttonHeight;
            _spacing = spacing;
            _bottomMargin = bottomMargin;
            _staggerDelay = staggerDelay;
            _enterDuration = enterDuration;
            _confirmDuration = confirmDuration;
        }

        public bool Uses(RectTransform container, Button template) =>
            _container == container && _template == template;

        public int Spawn(
            IReadOnlyList<ChoiceOption> options,
            ISet<string> selectedValues,
            Action<string, string, Button> picked
        )
        {
            Clear();
            if (options == null || _template == null || _container == null)
                return 0;
            // The template must never become visible when its parent container is opened.
            _template.gameObject.SetActive(false);
            SetActive(true);

            foreach (var option in options)
            {
                if (option == null)
                    continue;

                var button = UnityEngine.Object.Instantiate(_template, _container);
                button.gameObject.SetActive(true);
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var group = button.GetComponent<CanvasGroup>();
                if (group == null)
                    group = button.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = option.Text;
                    label.textWrappingMode = TextWrappingModes.Normal;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = Mathf.Min(label.fontSize, 23f);
                    label.fontSizeMax = label.fontSize;
                }
                float buttonHeight = EstimateButtonHeight(option.Text);
                var layout = button.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.minHeight = buttonHeight;
                    layout.preferredHeight = buttonHeight;
                }

                string value = option.Value;
                string text = option.Text;
                button.onClick.AddListener(() => picked(value, text, button));
                RegisterFocusFeedback(button);
                if (selectedValues != null && selectedValues.Contains(value))
                    SetPreviouslySelected(button, label);
                _spawned.Add(button.gameObject);
                _spawnedHeights.Add(buttonHeight);
            }

            UpdateLayout(_spawned.Count);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_container);
            return _spawned.Count;
        }

        public void SetActive(bool active)
        {
            if (_container != null)
                _container.gameObject.SetActive(active);
        }

        public void Clear()
        {
            foreach (var choice in _spawned)
                if (choice != null)
                {
                    choice.SetActive(false);
                    UnityEngine.Object.Destroy(choice);
                }
            _spawned.Clear();
            _spawnedHeights.Clear();
            _baseColors.Clear();
            _selectedIndex = -1;
        }

        public void SelectFirst()
        {
            if (_spawned.Count == 0 || EventSystem.current == null)
                return;
            var button = _spawned[0].GetComponent<Button>();
            if (button != null && button.IsInteractable())
                Select(0);
        }

        public void HandleKeyboardInput()
        {
            if (_spawned.Count == 0)
                return;
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.upArrowKey.wasPressedThisFrame)
                SelectRelative(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame)
                SelectRelative(1);

            if (
                keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
            )
            {
                if (_selectedIndex < 0)
                    Select(0);
                var button = ButtonAt(_selectedIndex);
                if (button != null && button.IsInteractable())
                    button.onClick.Invoke();
            }
        }

        public IEnumerator AnimateIn()
        {
            foreach (var choice in _spawned)
            {
                if (choice == null)
                    continue;
                var group = choice.GetComponent<CanvasGroup>();
                var rect = choice.transform as RectTransform;
                Vector2 target = rect != null ? rect.anchoredPosition : Vector2.zero;
                Vector2 start = target + Vector2.up * 18f;
                float duration = Mathf.Max(0.01f, _enterDuration);
                for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    if (group != null)
                        group.alpha = t;
                    if (rect != null)
                        rect.anchoredPosition = Vector2.Lerp(start, target, t);
                    yield return null;
                }
                if (group != null)
                    group.alpha = 1f;
                if (rect != null)
                    rect.anchoredPosition = target;
                if (_staggerDelay > 0f)
                    yield return new WaitForSecondsRealtime(_staggerDelay);
            }
        }

        public IEnumerator AnimateSelection(Button selected)
        {
            foreach (var choice in _spawned)
            {
                var button = choice != null ? choice.GetComponent<Button>() : null;
                if (button != null)
                    button.interactable = false;
            }

            float duration = Mathf.Max(0.01f, _confirmDuration);
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = elapsed / duration;
                foreach (var choice in _spawned)
                {
                    if (choice == null)
                        continue;
                    var group = choice.GetComponent<CanvasGroup>();
                    var button = choice.GetComponent<Button>();
                    if (group != null)
                        group.alpha = button == selected ? 1f : Mathf.Lerp(1f, 0.25f, t);
                    if (button == selected)
                        choice.transform.localScale =
                            Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.04f);
                }
                yield return null;
            }
        }

        private void UpdateLayout(int count)
        {
            if (_container == null || count <= 0)
                return;
            float height = 0f;
            for (int i = 0; i < _spawnedHeights.Count; i++)
                height += _spawnedHeights[i];
            height += (count - 1) * _spacing;
            var layout = _container.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.spacing = _spacing;
            _container.anchorMin = _container.anchorMax = new Vector2(0.5f, 1f);
            _container.pivot = new Vector2(0.5f, 0f);
            float threeChoiceHeight = 3f * _buttonHeight + 2f * _spacing;
            float centeredBottom = _bottomMargin + (threeChoiceHeight - height) * 0.5f;
            _container.anchoredPosition = new Vector2(0f, Mathf.Max(20f, centeredBottom));
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _containerWidth);
            _container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private float EstimateButtonHeight(string text)
        {
            if (string.IsNullOrEmpty(text))
                return _buttonHeight;
            // Japanese text has few spaces, so character count is a more stable preview heuristic.
            int visualLines = Mathf.Clamp(Mathf.CeilToInt(text.Length / 19f), 1, 2);
            return _buttonHeight + (visualLines - 1) * 34f;
        }

        private void RegisterFocusFeedback(Button button)
        {
            var image = button.targetGraphic as Graphic;
            if (image != null)
                _baseColors[button] = image.color;

            var trigger = button.GetComponent<UIEventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<UIEventTrigger>();
            trigger.triggers ??= new List<UIEventTrigger.Entry>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => Select(ButtonIndex(button)));
            AddTrigger(trigger, EventTriggerType.Select, _ => SetFocused(button, true));
            AddTrigger(trigger, EventTriggerType.Deselect, _ => SetFocused(button, false));
        }

        private static void AddTrigger(
            UIEventTrigger trigger,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback
        )
        {
            var entry = new UIEventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void SelectRelative(int direction)
        {
            int start = _selectedIndex >= 0 ? _selectedIndex : 0;
            for (int offset = 1; offset <= _spawned.Count; offset++)
            {
                int index = (start + direction * offset + _spawned.Count) % _spawned.Count;
                var button = ButtonAt(index);
                if (button != null && button.IsInteractable())
                {
                    Select(index);
                    return;
                }
            }
        }

        private void Select(int index)
        {
            var button = ButtonAt(index);
            if (button == null || !button.IsInteractable())
                return;
            _selectedIndex = index;
            EventSystem.current?.SetSelectedGameObject(button.gameObject);
            SetFocused(button, true);
        }

        private void SetFocused(Button button, bool focused)
        {
            if (button == null)
                return;
            button.transform.localScale = Vector3.one * (focused ? FocusScale : 1f);
            if (
                button.targetGraphic is Graphic graphic
                && _baseColors.TryGetValue(button, out var color)
            )
                graphic.color = focused ? color * FocusTint : color;
        }

        private void SetPreviouslySelected(Button button, TMP_Text label)
        {
            if (button.targetGraphic is Graphic graphic)
            {
                Color selectedColor = new(
                    graphic.color.r * 0.5f,
                    graphic.color.g * 0.5f,
                    graphic.color.b * 0.5f,
                    graphic.color.a
                );
                graphic.color = selectedColor;
                _baseColors[button] = selectedColor;
            }
            if (label != null)
            {
                Color color = label.color;
                label.color = new Color(color.r * 0.72f, color.g * 0.72f, color.b * 0.72f, 0.78f);
            }
        }

        private int ButtonIndex(Button button)
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null && _spawned[i].GetComponent<Button>() == button)
                    return i;
            return -1;
        }

        private Button ButtonAt(int index) =>
            index >= 0 && index < _spawned.Count && _spawned[index] != null
                ? _spawned[index].GetComponent<Button>()
                : null;

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
    }
}
